using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Memory;

/// <summary>
/// 向量记忆 - 使用向量检索增强上下文相关性
/// </summary>
/// <remarks>
/// <para>将所有消息嵌入为向量并存储在向量数据库中</para>
/// <para>获取上下文时，基于最近对话检索相关历史消息</para>
/// <para>实现 Retrieve 策略，解决长程任务中的上下文丢失问题</para>
/// <para>线程安全</para>
/// </remarks>
public class VectorMemory : IConversationMemory
{
    private readonly List<ConversationMessage> _recentMessages = [];
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ITokenCounter _tokenCounter;
    private readonly int _recentWindowSize;
    private readonly int _retrieveTopK;
    private readonly float _minRelevanceScore;
    private readonly ILogger<VectorMemory> _logger;
    private readonly Lock _lock = new();
    private readonly string _sessionId;

    /// <summary>
    /// 获取当前消息数量（最近窗口 + 向量存储）
    /// </summary>
    public int MessageCount
    {
        get
        {
            lock (_lock)
            {
                return _recentMessages.Count + _vectorStore.Count;
            }
        }
    }

    /// <summary>
    /// 获取最近消息数量
    /// </summary>
    public int RecentMessageCount
    {
        get
        {
            lock (_lock)
            {
                return _recentMessages.Count;
            }
        }
    }

    /// <summary>
    /// 获取向量存储中的消息数量
    /// </summary>
    public int VectorStoreCount => _vectorStore.Count;

    /// <summary>
    /// 会话 ID
    /// </summary>
    public string SessionId => _sessionId;

    /// <summary>
    /// 初始化向量记忆
    /// </summary>
    /// <param name="vectorStore">向量存储</param>
    /// <param name="embeddingProvider">嵌入提供者</param>
    /// <param name="tokenCounter">Token 计数器</param>
    /// <param name="recentWindowSize">保留的最近消息数（默认 6）</param>
    /// <param name="retrieveTopK">检索的相关消息数（默认 5）</param>
    /// <param name="minRelevanceScore">最小相关性分数（默认 0.5）</param>
    /// <param name="sessionId">会话 ID（可选，用于隔离不同会话）</param>
    /// <param name="logger">日志记录器（可选）</param>
    public VectorMemory(
        IVectorStore vectorStore,
        IEmbeddingProvider embeddingProvider,
        ITokenCounter tokenCounter,
        int recentWindowSize = 6,
        int retrieveTopK = 5,
        float minRelevanceScore = 0.5f,
        string? sessionId = null,
        ILogger<VectorMemory>? logger = null
    )
    {
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _embeddingProvider =
            embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
        _recentWindowSize =
            recentWindowSize > 0
                ? recentWindowSize
                : throw new ArgumentException("窗口大小必须为正数", nameof(recentWindowSize));
        _retrieveTopK =
            retrieveTopK > 0
                ? retrieveTopK
                : throw new ArgumentException("检索数量必须为正数", nameof(retrieveTopK));
        _minRelevanceScore =
            minRelevanceScore >= 0 && minRelevanceScore <= 1
                ? minRelevanceScore
                : throw new ArgumentException(
                    "相关性分数必须在 0-1 之间",
                    nameof(minRelevanceScore)
                );
        _sessionId = sessionId ?? Guid.NewGuid().ToString();
        _logger = logger ?? NullLogger<VectorMemory>.Instance;

        _logger.LogDebug(
            "VectorMemory 初始化，会话: {SessionId}，窗口: {WindowSize}，检索数: {TopK}",
            _sessionId,
            _recentWindowSize,
            _retrieveTopK
        );
    }

    /// <summary>
    /// 添加消息，超过窗口大小时将旧消息存入向量数据库
    /// </summary>
    public async Task AddMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        ConversationMessage messageWithTokens;
        ConversationMessage? messageToArchive = null;

        lock (_lock)
        {
            // 计算 token 数量
            if (message.TokenCount == null)
            {
                var tokenCount = _tokenCounter.CountTokens(message.Content);
                messageWithTokens = message with { TokenCount = tokenCount };
            }
            else
            {
                messageWithTokens = message;
            }

            _recentMessages.Add(messageWithTokens);

            // 检查是否需要归档旧消息
            if (_recentMessages.Count > _recentWindowSize)
            {
                messageToArchive = _recentMessages[0];
                _recentMessages.RemoveAt(0);
            }
        }

        // 在锁外进行向量存储操作
        if (messageToArchive != null)
        {
            await ArchiveMessageAsync(messageToArchive, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 将消息归档到向量存储
    /// </summary>
    private async Task ArchiveMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken
    )
    {
        try
        {
            // 生成嵌入向量
            var embedding = await _embeddingProvider
                .EmbedAsync(message.Content, cancellationToken)
                .ConfigureAwait(false);

            // 创建文档块
            var chunk = new DocumentChunk
            {
                Id = message.Id,
                Content = message.Content,
                Embedding = embedding,
                DocumentId = _sessionId,
                ChunkIndex = 0,
                Metadata = new Dictionary<string, string>
                {
                    ["role"] = message.Role,
                    ["timestamp"] = message.Timestamp.ToString("O"),
                    ["sessionId"] = _sessionId,
                },
            };

            await _vectorStore.AddAsync(chunk, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug(
                "消息已归档到向量存储: {MessageId}, 角色: {Role}",
                message.Id,
                message.Role
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "归档消息失败，消息将保留在最近消息中: {MessageId}", message.Id);

            // 归档失败时将消息放回最近消息列表末尾，防止数据丢失
            lock (_lock)
            {
                _recentMessages.Add(message);
            }
        }
    }

    /// <summary>
    /// 获取所有最近消息
    /// </summary>
    public Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(
        CancellationToken cancellationToken = default
    )
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<ConversationMessage>>(_recentMessages.ToList());
        }
    }

    /// <summary>
    /// 获取上下文：相关历史 + 最近消息
    /// </summary>
    /// <remarks>
    /// 1. 基于最近消息构建查询
    /// 2. 从向量存储检索相关历史
    /// 3. 合并：相关历史（按时间排序）+ 最近消息
    /// </remarks>
    public async Task<IReadOnlyList<ChatMessage>> GetContextAsync(
        int? maxTokens = null,
        CancellationToken cancellationToken = default
    )
    {
        IReadOnlyList<ConversationMessage> recentMessages;
        lock (_lock)
        {
            recentMessages = _recentMessages.ToList();
        }

        // 如果没有历史，直接返回最近消息
        if (_vectorStore.Count == 0 || recentMessages.Count == 0)
        {
            return TrimAndConvertMessages(recentMessages, maxTokens);
        }

        // 基于最近消息构建查询
        var query = BuildQueryFromRecentMessages(recentMessages);

        // 检索相关历史
        var relevantHistory = await RetrieveRelevantHistoryAsync(query, cancellationToken)
            .ConfigureAwait(false);

        // 合并上下文
        var context = MergeContext(relevantHistory, recentMessages, maxTokens);

        _logger.LogDebug(
            "构建上下文: 检索 {RetrievedCount} 条相关历史, 最近 {RecentCount} 条消息",
            relevantHistory.Count,
            recentMessages.Count
        );

        return context;
    }

    /// <summary>
    /// 从最近消息构建查询文本
    /// </summary>
    private static string BuildQueryFromRecentMessages(
        IReadOnlyList<ConversationMessage> recentMessages
    )
    {
        // 使用最近的用户消息作为查询
        var userMessages = recentMessages.Where(m => m.Role == "user").TakeLast(3);
        return string.Join(" ", userMessages.Select(m => m.Content));
    }

    /// <summary>
    /// 从向量存储检索相关历史
    /// </summary>
    private async Task<List<ConversationMessage>> RetrieveRelevantHistoryAsync(
        string query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        try
        {
            // 生成查询嵌入
            var queryEmbedding = await _embeddingProvider
                .EmbedAsync(query, cancellationToken)
                .ConfigureAwait(false);

            // 搜索相关文档
            var results = await _vectorStore
                .SearchAsync(queryEmbedding, _retrieveTopK, _minRelevanceScore, cancellationToken)
                .ConfigureAwait(false);

            // 转换为 ConversationMessage 并按时间排序
            var relevantMessages = results
                .Select(r => new ConversationMessage
                {
                    Id = r.Chunk.Id,
                    Role = r.Chunk.Metadata.GetValueOrDefault("role", "user"),
                    Content = r.Chunk.Content,
                    Timestamp = DateTime.TryParse(
                        r.Chunk.Metadata.GetValueOrDefault("timestamp", ""),
                        out var ts
                    )
                        ? ts
                        : DateTime.MinValue,
                    Metadata = new Dictionary<string, object> { ["relevanceScore"] = r.Score },
                })
                .OrderBy(m => m.Timestamp)
                .ToList();

            return relevantMessages;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "检索相关历史失败");
            return [];
        }
    }

    /// <summary>
    /// 合并相关历史和最近消息
    /// </summary>
    private IReadOnlyList<ChatMessage> MergeContext(
        List<ConversationMessage> relevantHistory,
        IReadOnlyList<ConversationMessage> recentMessages,
        int? maxTokens
    )
    {
        var allMessages = new List<ConversationMessage>();

        // 添加相关历史（如果有）
        if (relevantHistory.Count > 0)
        {
            // 添加一个系统消息标记历史上下文
            allMessages.Add(
                new ConversationMessage
                {
                    Role = "system",
                    Content = "[以下是与当前对话相关的历史上下文]",
                    TokenCount = _tokenCounter.CountTokens("[以下是与当前对话相关的历史上下文]"),
                }
            );

            allMessages.AddRange(relevantHistory);

            allMessages.Add(
                new ConversationMessage
                {
                    Role = "system",
                    Content = "[历史上下文结束，以下是当前对话]",
                    TokenCount = _tokenCounter.CountTokens("[历史上下文结束，以下是当前对话]"),
                }
            );
        }

        // 添加最近消息
        allMessages.AddRange(recentMessages);

        return TrimAndConvertMessages(allMessages, maxTokens);
    }

    /// <summary>
    /// 裁剪并转换消息
    /// </summary>
    private IReadOnlyList<ChatMessage> TrimAndConvertMessages(
        IReadOnlyList<ConversationMessage> messages,
        int? maxTokens
    )
    {
        if (!maxTokens.HasValue)
        {
            return messages.Select(m => new ChatMessage(m.Role, m.Content)).ToList();
        }

        var result = new List<ChatMessage>();
        var tokenCount = 0;

        // 从最近的消息开始，保留尽可能多的消息
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            var msgTokens =
                messages[i].TokenCount ?? _tokenCounter.CountTokens(messages[i].Content);
            if (tokenCount + msgTokens > maxTokens.Value)
            {
                break;
            }

            result.Insert(0, new ChatMessage(messages[i].Role, messages[i].Content));
            tokenCount += msgTokens;
        }

        return result;
    }

    /// <summary>
    /// 清空所有消息和向量存储
    /// </summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _recentMessages.Clear();
        }

        try
        {
            // 只清除当前会话的数据
            await _vectorStore
                .DeleteByDocumentIdAsync(_sessionId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清除向量存储失败");
        }

        _logger.LogDebug("VectorMemory 已清空，会话: {SessionId}", _sessionId);
    }

    /// <summary>
    /// 获取当前 token 数量（仅计算最近消息）
    /// </summary>
    public Task<int> GetTokenCountAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var total = _recentMessages.Sum(m => m.TokenCount ?? 0);
            return Task.FromResult(total);
        }
    }
}
