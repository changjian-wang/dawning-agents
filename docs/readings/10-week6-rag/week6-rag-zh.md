# 第6周：RAG集成

> 第三阶段：工具系统与RAG集成
> 第6周学习材料：Agent的检索增强生成

---

## 第1-2天：向量数据库基础

### 1. 什么是RAG？

**检索增强生成（RAG）** 通过从外部知识库提供相关上下文来增强LLM的响应能力。

```
┌─────────────────────────────────────────────────────────────────┐
│                        RAG 流水线                                │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│    ┌──────────┐                                                  │
│    │   查询   │                                                  │
│    └────┬─────┘                                                  │
│         │                                                        │
│         ▼                                                        │
│    ┌──────────┐     ┌──────────┐     ┌──────────┐               │
│    │  向量化  │────►│   搜索   │────►│   检索   │               │
│    │  查询    │     │ 向量存储 │     │  文档    │               │
│    └──────────┘     └──────────┘     └────┬─────┘               │
│                                           │                      │
│                                           ▼                      │
│    ┌──────────┐     ┌──────────┐     ┌──────────┐               │
│    │   响应   │◄────│   LLM    │◄────│   增强   │               │
│    └──────────┘     └──────────┘     │  上下文  │               │
│                                      └──────────┘               │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. 理解嵌入向量

嵌入向量是捕获语义含义的稠密向量表示：

```csharp
namespace DawningAgents.Core.RAG;

/// <summary>
/// 嵌入向量生成接口
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// 为单个文本生成嵌入向量
    /// </summary>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 为多个文本批量生成嵌入向量
    /// </summary>
    Task<float[][]> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 嵌入向量的维度
    /// </summary>
    int Dimensions { get; }
    
    /// <summary>
    /// 使用的模型名称
    /// </summary>
    string ModelName { get; }
}
```

### 3. OpenAI嵌入向量实现

```csharp
namespace DawningAgents.Core.RAG;

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// OpenAI嵌入向量提供者
/// </summary>
public class OpenAIEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<OpenAIEmbeddingProvider> _logger;
    
    public int Dimensions => 1536; // text-embedding-ada-002
    public string ModelName { get; }

    public OpenAIEmbeddingProvider(
        string apiKey,
        ILogger<OpenAIEmbeddingProvider> logger,
        HttpClient? httpClient = null,
        string model = "text-embedding-ada-002")
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? new HttpClient();
        ModelName = model;
        
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var results = await EmbedBatchAsync([text], cancellationToken);
        return results[0];
    }

    public async Task<float[][]> EmbedBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        _logger.LogDebug("正在为 {Count} 个文本生成嵌入向量", textList.Count);

        var request = new
        {
            model = ModelName,
            input = textList
        };

        var response = await _httpClient.PostAsJsonAsync(
            "https://api.openai.com/v1/embeddings",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(
            cancellationToken: cancellationToken);

        if (result?.Data == null)
            throw new InvalidOperationException("嵌入响应无效");

        return result.Data
            .OrderBy(d => d.Index)
            .Select(d => d.Embedding)
            .ToArray();
    }

    private record EmbeddingResponse
    {
        public EmbeddingData[]? Data { get; init; }
    }

    private record EmbeddingData
    {
        public int Index { get; init; }
        public float[] Embedding { get; init; } = [];
    }
}
```

### 4. 向量相似度函数

```csharp
namespace DawningAgents.Core.RAG;

/// <summary>
/// 向量相似度计算
/// </summary>
public static class VectorSimilarity
{
    /// <summary>
    /// 计算两个向量的余弦相似度
    /// </summary>
    public static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("向量维度必须相同");

        float dotProduct = 0;
        float normA = 0;
        float normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
            return 0;

        return dotProduct / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    /// <summary>
    /// 计算两个向量的欧氏距离
    /// </summary>
    public static float EuclideanDistance(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("向量维度必须相同");

        float sum = 0;
        for (int i = 0; i < a.Length; i++)
        {
            float diff = a[i] - b[i];
            sum += diff * diff;
        }

        return MathF.Sqrt(sum);
    }

    /// <summary>
    /// 计算两个向量的点积
    /// </summary>
    public static float DotProduct(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("向量维度必须相同");

        float result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result += a[i] * b[i];
        }

        return result;
    }
}
```

---

## 第3-4天：RAG流水线实现

### 1. 文档模型

```csharp
namespace DawningAgents.Core.RAG;

/// <summary>
/// 知识库中的文档
/// </summary>
public record Document
{
    /// <summary>
    /// 唯一标识符
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// 文档内容
    /// </summary>
    public required string Content { get; init; }
    
    /// <summary>
    /// 文档元数据
    /// </summary>
    public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
    
    /// <summary>
    /// 文档来源（文件路径、URL等）
    /// </summary>
    public string? Source { get; init; }
}

/// <summary>
/// 带有嵌入向量的文档块
/// </summary>
public record DocumentChunk
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required string DocumentId { get; init; }
    public required string Content { get; init; }
    public float[]? Embedding { get; init; }
    public int ChunkIndex { get; init; }
    public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// 带有相似度分数的搜索结果
/// </summary>
public record SearchResult
{
    public required DocumentChunk Chunk { get; init; }
    public required float Score { get; init; }
}
```

### 2. 文档分块器

```csharp
namespace DawningAgents.Core.RAG;

/// <summary>
/// 文档分块接口
/// </summary>
public interface IDocumentChunker
{
    /// <summary>
    /// 将文档分割成块
    /// </summary>
    IEnumerable<DocumentChunk> Chunk(Document document);
}

/// <summary>
/// 按字符数分割文档，带重叠
/// </summary>
public class CharacterChunker : IDocumentChunker
{
    private readonly int _chunkSize;
    private readonly int _chunkOverlap;
    private readonly string[] _separators;

    public CharacterChunker(
        int chunkSize = 1000,
        int chunkOverlap = 200,
        string[]? separators = null)
    {
        _chunkSize = chunkSize;
        _chunkOverlap = chunkOverlap;
        _separators = separators ?? ["\n\n", "\n", "。", ". ", " "];
    }

    public IEnumerable<DocumentChunk> Chunk(Document document)
    {
        var text = document.Content;
        var chunks = new List<DocumentChunk>();
        var currentPosition = 0;
        var chunkIndex = 0;

        while (currentPosition < text.Length)
        {
            var endPosition = Math.Min(currentPosition + _chunkSize, text.Length);
            
            // 尝试找到一个好的断点
            if (endPosition < text.Length)
            {
                var breakPoint = FindBreakPoint(text, currentPosition, endPosition);
                if (breakPoint > currentPosition)
                {
                    endPosition = breakPoint;
                }
            }

            var chunkText = text[currentPosition..endPosition].Trim();
            
            if (!string.IsNullOrEmpty(chunkText))
            {
                chunks.Add(new DocumentChunk
                {
                    DocumentId = document.Id,
                    Content = chunkText,
                    ChunkIndex = chunkIndex++,
                    Metadata = new Dictionary<string, object>(document.Metadata)
                    {
                        ["source"] = document.Source ?? "",
                        ["start_char"] = currentPosition,
                        ["end_char"] = endPosition
                    }
                });
            }

            // 带重叠地移动位置
            currentPosition = endPosition - _chunkOverlap;
            if (currentPosition <= 0 || currentPosition >= text.Length - _chunkOverlap)
            {
                currentPosition = endPosition;
            }
        }

        return chunks;
    }

    private int FindBreakPoint(string text, int start, int end)
    {
        // 从后向前查找分隔符
        foreach (var separator in _separators)
        {
            var pos = text.LastIndexOf(separator, end - 1, end - start);
            if (pos > start)
            {
                return pos + separator.Length;
            }
        }
        return end;
    }
}

/// <summary>
/// 按句子分割文档
/// </summary>
public class SentenceChunker : IDocumentChunker
{
    private readonly int _maxChunkSize;
    private readonly int _minChunkSize;

    public SentenceChunker(int maxChunkSize = 1000, int minChunkSize = 100)
    {
        _maxChunkSize = maxChunkSize;
        _minChunkSize = minChunkSize;
    }

    public IEnumerable<DocumentChunk> Chunk(Document document)
    {
        var sentences = SplitIntoSentences(document.Content);
        var chunks = new List<DocumentChunk>();
        var currentChunk = new StringBuilder();
        var chunkIndex = 0;

        foreach (var sentence in sentences)
        {
            if (currentChunk.Length + sentence.Length > _maxChunkSize && currentChunk.Length >= _minChunkSize)
            {
                chunks.Add(CreateChunk(document, currentChunk.ToString(), chunkIndex++));
                currentChunk.Clear();
            }
            
            currentChunk.Append(sentence);
        }

        if (currentChunk.Length > 0)
        {
            chunks.Add(CreateChunk(document, currentChunk.ToString(), chunkIndex));
        }

        return chunks;
    }

    private static List<string> SplitIntoSentences(string text)
    {
        var sentences = new List<string>();
        // 支持中英文标点
        var pattern = @"(?<=[.!?。！？])\s*";
        var parts = System.Text.RegularExpressions.Regex.Split(text, pattern);
        
        foreach (var part in parts)
        {
            if (!string.IsNullOrWhiteSpace(part))
            {
                sentences.Add(part.Trim() + " ");
            }
        }

        return sentences;
    }

    private static DocumentChunk CreateChunk(Document document, string content, int index)
    {
        return new DocumentChunk
        {
            DocumentId = document.Id,
            Content = content.Trim(),
            ChunkIndex = index,
            Metadata = new Dictionary<string, object>(document.Metadata)
            {
                ["source"] = document.Source ?? ""
            }
        };
    }
}
```

### 3. 向量存储接口

```csharp
namespace DawningAgents.Core.RAG;

/// <summary>
/// 向量存储和检索接口
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// 添加文档到存储
    /// </summary>
    Task AddAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 搜索相似文档
    /// </summary>
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        float[] queryEmbedding,
        int topK = 5,
        float minScore = 0.0f,
        IDictionary<string, object>? filter = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 按ID删除文档
    /// </summary>
    Task DeleteAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取文档数量
    /// </summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 清空所有文档
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
```

### 4. 内存向量存储

```csharp
namespace DawningAgents.Core.RAG;

using System.Collections.Concurrent;

/// <summary>
/// 简单的内存向量存储，用于开发测试
/// </summary>
public class InMemoryVectorStore : IVectorStore
{
    private readonly ConcurrentDictionary<string, DocumentChunk> _chunks = new();

    public Task AddAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        foreach (var chunk in chunks)
        {
            if (chunk.Embedding == null)
                throw new ArgumentException($"文档块 {chunk.Id} 没有嵌入向量");
                
            _chunks.TryAdd(chunk.Id, chunk);
        }
        
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SearchResult>> SearchAsync(
        float[] queryEmbedding,
        int topK = 5,
        float minScore = 0.0f,
        IDictionary<string, object>? filter = null,
        CancellationToken cancellationToken = default)
    {
        var results = _chunks.Values
            .Where(c => c.Embedding != null)
            .Where(c => MatchesFilter(c, filter))
            .Select(c => new SearchResult
            {
                Chunk = c,
                Score = VectorSimilarity.CosineSimilarity(queryEmbedding, c.Embedding!)
            })
            .Where(r => r.Score >= minScore)
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();

        return Task.FromResult<IReadOnlyList<SearchResult>>(results);
    }

    public Task DeleteAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        foreach (var id in ids)
        {
            _chunks.TryRemove(id, out _);
        }
        
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_chunks.Count);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _chunks.Clear();
        return Task.CompletedTask;
    }

    private static bool MatchesFilter(DocumentChunk chunk, IDictionary<string, object>? filter)
    {
        if (filter == null || filter.Count == 0)
            return true;

        foreach (var (key, value) in filter)
        {
            if (!chunk.Metadata.TryGetValue(key, out var metaValue))
                return false;
                
            if (!Equals(metaValue, value))
                return false;
        }

        return true;
    }
}
```

---

## 第5-7天：RAG与Agent集成

### 1. 检索器接口

```csharp
namespace DawningAgents.Core.RAG;

/// <summary>
/// 检索相关文档的接口
/// </summary>
public interface IRetriever
{
    /// <summary>
    /// 检索与查询相关的文档
    /// </summary>
    Task<IReadOnlyList<SearchResult>> RetrieveAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 基于向量的检索器
/// </summary>
public class VectorRetriever : IRetriever
{
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly float _minScore;

    public VectorRetriever(
        IVectorStore vectorStore,
        IEmbeddingProvider embeddingProvider,
        float minScore = 0.7f)
    {
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        _minScore = minScore;
    }

    public async Task<IReadOnlyList<SearchResult>> RetrieveAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        // 生成查询的嵌入向量
        var queryEmbedding = await _embeddingProvider.EmbedAsync(query, cancellationToken);
        
        // 搜索向量存储
        return await _vectorStore.SearchAsync(
            queryEmbedding,
            topK,
            _minScore,
            cancellationToken: cancellationToken);
    }
}
```

### 2. 知识库

```csharp
namespace DawningAgents.Core.RAG;

using Microsoft.Extensions.Logging;

/// <summary>
/// 用于RAG的高级知识库
/// </summary>
public class KnowledgeBase
{
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IDocumentChunker _chunker;
    private readonly ILogger<KnowledgeBase> _logger;

    public KnowledgeBase(
        IVectorStore vectorStore,
        IEmbeddingProvider embeddingProvider,
        IDocumentChunker chunker,
        ILogger<KnowledgeBase> logger)
    {
        _vectorStore = vectorStore;
        _embeddingProvider = embeddingProvider;
        _chunker = chunker;
        _logger = logger;
    }

    /// <summary>
    /// 添加文档到知识库
    /// </summary>
    public async Task AddDocumentAsync(
        Document document,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("正在添加文档 {Id} 到知识库", document.Id);
        
        // 对文档进行分块
        var chunks = _chunker.Chunk(document).ToList();
        _logger.LogDebug("文档分割成 {Count} 个块", chunks.Count);
        
        // 生成嵌入向量
        var contents = chunks.Select(c => c.Content).ToList();
        var embeddings = await _embeddingProvider.EmbedBatchAsync(contents, cancellationToken);
        
        // 将嵌入向量添加到块中
        var chunksWithEmbeddings = chunks.Zip(embeddings, (chunk, embedding) => 
            chunk with { Embedding = embedding }).ToList();
        
        // 存储到向量存储
        await _vectorStore.AddAsync(chunksWithEmbeddings, cancellationToken);
        
        _logger.LogInformation("文档 {Id} 添加完成，共 {Count} 个块", document.Id, chunks.Count);
    }

    /// <summary>
    /// 批量添加多个文档
    /// </summary>
    public async Task AddDocumentsAsync(
        IEnumerable<Document> documents,
        CancellationToken cancellationToken = default)
    {
        foreach (var document in documents)
        {
            await AddDocumentAsync(document, cancellationToken);
        }
    }

    /// <summary>
    /// 搜索知识库
    /// </summary>
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        int topK = 5,
        float minScore = 0.7f,
        CancellationToken cancellationToken = default)
    {
        var queryEmbedding = await _embeddingProvider.EmbedAsync(query, cancellationToken);
        return await _vectorStore.SearchAsync(queryEmbedding, topK, minScore, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 获取用于LLM增强的上下文字符串
    /// </summary>
    public async Task<string> GetContextAsync(
        string query,
        int topK = 3,
        CancellationToken cancellationToken = default)
    {
        var results = await SearchAsync(query, topK, 0.5f, cancellationToken);
        
        if (results.Count == 0)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("相关信息：");
        sb.AppendLine();
        
        foreach (var result in results)
        {
            sb.AppendLine($"[来源: {result.Chunk.Metadata.GetValueOrDefault("source", "未知")}]");
            sb.AppendLine(result.Chunk.Content);
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
```

### 3. RAG工具

```csharp
namespace DawningAgents.Core.Tools.BuiltIn;

using DawningAgents.Core.RAG;
using Microsoft.Extensions.Logging;

/// <summary>
/// 允许Agent搜索知识库的工具
/// </summary>
public class RAGTool : ToolBase
{
    private readonly KnowledgeBase _knowledgeBase;

    public override string Name => "knowledge_search";
    public override string Description => "搜索知识库获取相关信息。当需要特定事实或文档时使用此工具。";
    
    public override string ParametersSchema => """
        {
            "type": "object",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "用于查找相关信息的搜索查询"
                },
                "numResults": {
                    "type": "integer",
                    "description": "返回结果数量（默认：3）"
                }
            },
            "required": ["query"]
        }
        """;

    public RAGTool(
        KnowledgeBase knowledgeBase,
        ILogger<RAGTool> logger) : base(logger)
    {
        _knowledgeBase = knowledgeBase ?? throw new ArgumentNullException(nameof(knowledgeBase));
    }

    protected override async Task<ToolResult> ExecuteCoreAsync(
        string input,
        CancellationToken cancellationToken)
    {
        var request = ParseInput<RAGRequest>(input);
        if (request == null || string.IsNullOrWhiteSpace(request.Query))
        {
            // 将输入作为原始查询处理
            request = new RAGRequest { Query = input.Trim('"') };
        }

        var numResults = request.NumResults ?? 3;
        var results = await _knowledgeBase.SearchAsync(
            request.Query,
            numResults,
            0.5f,
            cancellationToken);

        if (results.Count == 0)
        {
            return ToolResult.Success("在知识库中未找到相关信息。");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"找到 {results.Count} 个相关结果：");
        sb.AppendLine();

        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            sb.AppendLine($"--- 结果 {i + 1}（相似度：{result.Score:F2}）---");
            sb.AppendLine(result.Chunk.Content);
            sb.AppendLine();
        }

        return ToolResult.Success(sb.ToString(), new Dictionary<string, object>
        {
            ["resultCount"] = results.Count,
            ["query"] = request.Query
        });
    }

    private record RAGRequest
    {
        public string Query { get; init; } = "";
        public int? NumResults { get; init; }
    }
}
```

### 4. RAG增强Agent

```csharp
namespace DawningAgents.Core.Agents;

using DawningAgents.Core.LLM;
using DawningAgents.Core.RAG;
using Microsoft.Extensions.Logging;

/// <summary>
/// 内置RAG能力的Agent
/// </summary>
public class RAGAgent : ReActAgent
{
    private readonly KnowledgeBase _knowledgeBase;
    private readonly bool _autoRetrieve;

    public RAGAgent(
        ILLMProvider llm,
        KnowledgeBase knowledgeBase,
        ILogger<RAGAgent> logger,
        bool autoRetrieve = true,
        string? name = null) : base(llm, logger, name ?? "RAGAgent")
    {
        _knowledgeBase = knowledgeBase;
        _autoRetrieve = autoRetrieve;
    }

    public override async Task<AgentResponse> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        // 如果启用了自动检索，用知识增强上下文
        if (_autoRetrieve)
        {
            var relevantContext = await _knowledgeBase.GetContextAsync(
                context.Input,
                topK: 3,
                cancellationToken);

            if (!string.IsNullOrEmpty(relevantContext))
            {
                // 创建增强的上下文
                context = context with
                {
                    SystemPrompt = BuildAugmentedSystemPrompt(context.SystemPrompt, relevantContext)
                };
            }
        }

        return await base.ExecuteAsync(context, cancellationToken);
    }

    private string BuildAugmentedSystemPrompt(string? originalPrompt, string context)
    {
        var prompt = originalPrompt ?? GetDefaultSystemPrompt();
        
        return $"""
            {prompt}
            
            你可以访问以下可能相关的知识库信息：
            
            {context}
            
            在回答用户问题时，如果相关请使用这些信息。
            使用知识库信息时请始终注明来源。
            """;
    }
}
```

---

## 完整RAG流水线示例

```csharp
// 设置
var embeddingProvider = new OpenAIEmbeddingProvider(apiKey, logger);
var vectorStore = new InMemoryVectorStore();
var chunker = new CharacterChunker(chunkSize: 500, chunkOverlap: 100);
var knowledgeBase = new KnowledgeBase(vectorStore, embeddingProvider, chunker, logger);

// 添加文档
var documents = new[]
{
    new Document
    {
        Content = "埃菲尔铁塔是位于法国巴黎战神广场的格构式铁塔...",
        Source = "wikipedia/eiffel_tower",
        Metadata = { ["category"] = "地标" }
    },
    new Document
    {
        Content = "长城是中国古代的一系列防御工事，由石头、砖块建造...",
        Source = "wikipedia/great_wall",
        Metadata = { ["category"] = "地标" }
    }
};

await knowledgeBase.AddDocumentsAsync(documents);

// 创建RAG增强的Agent
var llm = new OpenAIProvider(apiKey, logger);
var ragAgent = new RAGAgent(llm, knowledgeBase, logger);

// 使用RAG执行
var response = await ragAgent.ExecuteAsync(new AgentContext
{
    Input = "埃菲尔铁塔是用什么建造的？",
    MaxIterations = 5
});

Console.WriteLine(response.Output);
```

---

## 总结

### 第6周交付物

```
src/DawningAgents.Core/
├── RAG/
│   ├── IEmbeddingProvider.cs       # 嵌入向量接口
│   ├── OpenAIEmbeddingProvider.cs  # OpenAI实现
│   ├── VectorSimilarity.cs         # 相似度函数
│   ├── Document.cs                 # 文档模型
│   ├── IDocumentChunker.cs         # 分块接口
│   ├── CharacterChunker.cs         # 字符分块器
│   ├── SentenceChunker.cs          # 句子分块器
│   ├── IVectorStore.cs             # 向量存储接口
│   ├── InMemoryVectorStore.cs      # 内存实现
│   ├── IRetriever.cs               # 检索器接口
│   ├── VectorRetriever.cs          # 向量检索器
│   └── KnowledgeBase.cs            # 高级知识库
├── Tools/BuiltIn/
│   └── RAGTool.cs                  # Agent的RAG工具
└── Agents/
    └── RAGAgent.cs                 # RAG增强Agent
```

### 核心概念

| 概念 | 描述 |
|------|------|
| **嵌入向量** | 稠密的向量表示 |
| **分块** | 适当地分割文档 |
| **向量存储** | 存储和相似性搜索 |
| **检索** | 查找相关文档 |
| **上下文增强** | 将知识注入提示词 |

### 下一步：第四阶段

第四阶段（第7-8周）将涵盖多Agent协作：
- 顺序和并行编排
- 层级协调
- Agent通信模式
