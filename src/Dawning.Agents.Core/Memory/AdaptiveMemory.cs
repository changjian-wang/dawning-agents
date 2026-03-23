using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Memory;

/// <summary>
/// 自适应记忆 - 根据 token 使用量自动从 BufferMemory 降级到 SummaryMemory
/// </summary>
/// <remarks>
/// <para>初始使用 BufferMemory 存储完整消息</para>
/// <para>当 token 数量超过阈值时，自动切换到 SummaryMemory</para>
/// <para>切换时会将已有消息迁移到 SummaryMemory 并触发摘要</para>
/// <para>线程安全</para>
/// </remarks>
public class AdaptiveMemory : IConversationMemory, IDisposable
{
    private IConversationMemory _currentMemory;
    private readonly ILLMProvider _llm;
    private readonly ITokenCounter _tokenCounter;
    private readonly int _downgradeThreshold;
    private readonly int _maxRecentMessages;
    private readonly int _summaryThreshold;
    private readonly ILogger<AdaptiveMemory> _logger;
    private readonly SemaphoreSlim _downgradeLock = new(1, 1);
    private bool _hasDowngraded;
    private volatile bool _disposed;

    /// <summary>
    /// 获取当前存储的消息数量
    /// </summary>
    public int MessageCount => Volatile.Read(ref _currentMemory).MessageCount;

    /// <summary>
    /// 是否已降级到 SummaryMemory
    /// </summary>
    public bool HasDowngraded => Volatile.Read(ref _hasDowngraded);

    /// <summary>
    /// 初始化自适应记忆
    /// </summary>
    /// <param name="llm">LLM 提供者，用于生成摘要</param>
    /// <param name="tokenCounter">Token 计数器</param>
    /// <param name="downgradeThreshold">触发降级的 token 阈值（默认 4000）</param>
    /// <param name="maxRecentMessages">降级后保留的最近消息数（默认 6）</param>
    /// <param name="summaryThreshold">降级后触发摘要的消息数阈值（默认 10）</param>
    /// <param name="logger">日志记录器（可选）</param>
    public AdaptiveMemory(
        ILLMProvider llm,
        ITokenCounter tokenCounter,
        int downgradeThreshold = 4000,
        int maxRecentMessages = 6,
        int summaryThreshold = 10,
        ILogger<AdaptiveMemory>? logger = null
    )
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
        _downgradeThreshold =
            downgradeThreshold > 0
                ? downgradeThreshold
                : throw new ArgumentException("降级阈值必须为正数", nameof(downgradeThreshold));
        _maxRecentMessages =
            maxRecentMessages > 0
                ? maxRecentMessages
                : throw new ArgumentException("最大消息数必须为正数", nameof(maxRecentMessages));
        _summaryThreshold =
            summaryThreshold > maxRecentMessages
                ? summaryThreshold
                : throw new ArgumentException(
                    "摘要阈值必须大于最大保留消息数",
                    nameof(summaryThreshold)
                );
        _logger = logger ?? NullLogger<AdaptiveMemory>.Instance;

        // 初始使用 BufferMemory
        _currentMemory = new BufferMemory(tokenCounter);
        _hasDowngraded = false;

        _logger.LogDebug(
            "AdaptiveMemory 初始化，降级阈值: {Threshold} tokens",
            _downgradeThreshold
        );
    }

    /// <summary>
    /// 添加消息，超过阈值时自动降级到 SummaryMemory
    /// </summary>
    public async Task AddMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _downgradeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var currentMemory = _currentMemory;
            await currentMemory.AddMessageAsync(message, cancellationToken).ConfigureAwait(false);

            // 检查是否需要降级
            if (!Volatile.Read(ref _hasDowngraded))
            {
                var totalTokens = await currentMemory
                    .GetTokenCountAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (totalTokens >= _downgradeThreshold)
                {
                    await DowngradeToSummaryMemoryCoreAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _downgradeLock.Release();
        }
    }

    /// <summary>
    /// 降级到 SummaryMemory（调用方必须持有 _downgradeLock）
    /// </summary>
    private async Task DowngradeToSummaryMemoryCoreAsync(CancellationToken cancellationToken)
    {
        if (_hasDowngraded)
        {
            return;
        }

        var currentTokens = await _currentMemory
            .GetTokenCountAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "触发自动降级：从 BufferMemory 切换到 SummaryMemory（当前 token: {Tokens}）",
            currentTokens
        );

        // 获取现有消息
        var existingMessages = await _currentMemory
            .GetMessagesAsync(cancellationToken)
            .ConfigureAwait(false);

        // 创建 SummaryMemory（ILogger<AdaptiveMemory> 无法转换为 ILogger<SummaryMemory>，显式传 null 由 SummaryMemory 回退到 NullLogger）
        var summaryMemory = new SummaryMemory(
            _llm,
            _tokenCounter,
            _maxRecentMessages,
            _summaryThreshold
        );

        // 迁移消息到 SummaryMemory（这会自动触发摘要）
        foreach (var msg in existingMessages)
        {
            await summaryMemory.AddMessageAsync(msg, cancellationToken).ConfigureAwait(false);
        }

        // 切换到 SummaryMemory
        Volatile.Write(ref _currentMemory, summaryMemory);
        Volatile.Write(ref _hasDowngraded, true);

        _logger.LogInformation(
            "降级完成，新 token 数: {NewTokens}",
            await summaryMemory.GetTokenCountAsync(cancellationToken).ConfigureAwait(false)
        );
    }

    /// <summary>
    /// 获取所有消息
    /// </summary>
    public Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Volatile.Read(ref _currentMemory).GetMessagesAsync(cancellationToken);
    }

    /// <summary>
    /// 获取用于 LLM 调用的消息上下文
    /// </summary>
    public Task<IReadOnlyList<ChatMessage>> GetContextAsync(
        int? maxTokens = null,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Volatile.Read(ref _currentMemory).GetContextAsync(maxTokens, cancellationToken);
    }

    /// <summary>
    /// 清空所有消息，重置为 BufferMemory
    /// </summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _downgradeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var oldMemory = _currentMemory;
            await oldMemory.ClearAsync(cancellationToken).ConfigureAwait(false);

            // 重置为 BufferMemory
            Volatile.Write(ref _currentMemory, new BufferMemory(_tokenCounter));
            Volatile.Write(ref _hasDowngraded, false);

            // 不立即 Dispose 旧 memory，避免与并发读取路径竞态（让 GC 回收）
        }
        finally
        {
            _downgradeLock.Release();
        }

        _logger.LogDebug("AdaptiveMemory 已清空并重置为 BufferMemory");
    }

    /// <summary>
    /// 获取当前总 token 数
    /// </summary>
    public Task<int> GetTokenCountAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Volatile.Read(ref _currentMemory).GetTokenCountAsync(cancellationToken);
    }

    /// <summary>
    /// 释放信号量和内部记忆资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _downgradeLock.Dispose();
        (_currentMemory as IDisposable)?.Dispose();
    }
}
