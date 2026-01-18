# Week 6: RAG Integration

> Phase 3: Tool System & RAG Integration
> Week 6 Learning Material: Retrieval-Augmented Generation for Agents

---

## Day 1-2: Vector Database Fundamentals

### 1. What is RAG?

**Retrieval-Augmented Generation (RAG)** enhances LLM responses by providing relevant context from external knowledge bases.

```text
┌─────────────────────────────────────────────────────────────────┐
│                        RAG Pipeline                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│    ┌──────────┐                                                  │
│    │  Query   │                                                  │
│    └────┬─────┘                                                  │
│         │                                                        │
│         ▼                                                        │
│    ┌──────────┐     ┌──────────┐     ┌──────────┐               │
│    │ Embed    │────►│ Search   │────►│ Retrieve │               │
│    │ Query    │     │ Vector   │     │ Docs     │               │
│    └──────────┘     │ Store    │     └────┬─────┘               │
│                     └──────────┘          │                      │
│                                           ▼                      │
│    ┌──────────┐     ┌──────────┐     ┌──────────┐               │
│    │ Response │◄────│   LLM    │◄────│ Augment  │               │
│    └──────────┘     └──────────┘     │ Context  │               │
│                                      └──────────┘               │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. Understanding Embeddings

Embeddings are dense vector representations that capture semantic meaning:

```csharp
namespace Dawning.Agents.Core.RAG;

/// <summary>
/// Interface for generating embeddings
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Generate embedding for a single text
    /// </summary>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate embeddings for multiple texts
    /// </summary>
    Task<float[][]> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// The dimension of the embedding vectors
    /// </summary>
    int Dimensions { get; }
    
    /// <summary>
    /// Model name being used
    /// </summary>
    string ModelName { get; }
}
```

### 3. OpenAI Embeddings Implementation

```csharp
namespace Dawning.Agents.Core.RAG;

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// OpenAI embeddings provider
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
        _logger.LogDebug("Generating embeddings for {Count} texts", textList.Count);

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
            throw new InvalidOperationException("Invalid embedding response");

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

### 4. Vector Similarity Functions

```csharp
namespace Dawning.Agents.Core.RAG;

/// <summary>
/// Vector similarity calculations
/// </summary>
public static class VectorSimilarity
{
    /// <summary>
    /// Calculate cosine similarity between two vectors
    /// </summary>
    public static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have the same dimension");

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
    /// Calculate Euclidean distance between two vectors
    /// </summary>
    public static float EuclideanDistance(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have the same dimension");

        float sum = 0;
        for (int i = 0; i < a.Length; i++)
        {
            float diff = a[i] - b[i];
            sum += diff * diff;
        }

        return MathF.Sqrt(sum);
    }

    /// <summary>
    /// Calculate dot product of two vectors
    /// </summary>
    public static float DotProduct(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have the same dimension");

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

## Day 3-4: RAG Pipeline Implementation

### 1. Document Model

```csharp
namespace Dawning.Agents.Core.RAG;

/// <summary>
/// Represents a document in the knowledge base
/// </summary>
public record Document
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Document content
    /// </summary>
    public required string Content { get; init; }
    
    /// <summary>
    /// Document metadata
    /// </summary>
    public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
    
    /// <summary>
    /// Source of the document (file path, URL, etc.)
    /// </summary>
    public string? Source { get; init; }
}

/// <summary>
/// A chunk of a document with its embedding
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
/// Search result with similarity score
/// </summary>
public record SearchResult
{
    public required DocumentChunk Chunk { get; init; }
    public required float Score { get; init; }
}
```

### 2. Document Chunker

```csharp
namespace Dawning.Agents.Core.RAG;

/// <summary>
/// Interface for splitting documents into chunks
/// </summary>
public interface IDocumentChunker
{
    /// <summary>
    /// Split a document into chunks
    /// </summary>
    IEnumerable<DocumentChunk> Chunk(Document document);
}

/// <summary>
/// Splits documents by character count with overlap
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
        _separators = separators ?? ["\n\n", "\n", ". ", " "];
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
            
            // Try to find a good break point
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

            // Move position with overlap
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
        // Look for separators from end to start
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
/// Splits documents by sentences
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
        var pattern = @"(?<=[.!?])\s+";
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

### 3. Vector Store Interface

```csharp
namespace Dawning.Agents.Core.RAG;

/// <summary>
/// Interface for vector storage and retrieval
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Add documents to the store
    /// </summary>
    Task AddAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search for similar documents
    /// </summary>
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        float[] queryEmbedding,
        int topK = 5,
        float minScore = 0.0f,
        IDictionary<string, object>? filter = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete documents by ID
    /// </summary>
    Task DeleteAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get document count
    /// </summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clear all documents
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
```

### 4. In-Memory Vector Store

```csharp
namespace Dawning.Agents.Core.RAG;

using System.Collections.Concurrent;

/// <summary>
/// Simple in-memory vector store for development
/// </summary>
public class InMemoryVectorStore : IVectorStore
{
    private readonly ConcurrentDictionary<string, DocumentChunk> _chunks = new();

    public Task AddAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        foreach (var chunk in chunks)
        {
            if (chunk.Embedding == null)
                throw new ArgumentException($"Chunk {chunk.Id} has no embedding");
                
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

## Day 5-7: RAG & Agent Integration

### 1. Retriever Interface

```csharp
namespace Dawning.Agents.Core.RAG;

/// <summary>
/// Interface for retrieving relevant documents
/// </summary>
public interface IRetriever
{
    /// <summary>
    /// Retrieve relevant documents for a query
    /// </summary>
    Task<IReadOnlyList<SearchResult>> RetrieveAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Vector-based retriever
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
        // Generate query embedding
        var queryEmbedding = await _embeddingProvider.EmbedAsync(query, cancellationToken);
        
        // Search vector store
        return await _vectorStore.SearchAsync(
            queryEmbedding,
            topK,
            _minScore,
            cancellationToken: cancellationToken);
    }
}
```

### 2. Knowledge Base

```csharp
namespace Dawning.Agents.Core.RAG;

using Microsoft.Extensions.Logging;

/// <summary>
/// High-level knowledge base for RAG
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
    /// Add a document to the knowledge base
    /// </summary>
    public async Task AddDocumentAsync(
        Document document,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adding document {Id} to knowledge base", document.Id);
        
        // Chunk the document
        var chunks = _chunker.Chunk(document).ToList();
        _logger.LogDebug("Document split into {Count} chunks", chunks.Count);
        
        // Generate embeddings
        var contents = chunks.Select(c => c.Content).ToList();
        var embeddings = await _embeddingProvider.EmbedBatchAsync(contents, cancellationToken);
        
        // Add embeddings to chunks
        var chunksWithEmbeddings = chunks.Zip(embeddings, (chunk, embedding) => 
            chunk with { Embedding = embedding }).ToList();
        
        // Store in vector store
        await _vectorStore.AddAsync(chunksWithEmbeddings, cancellationToken);
        
        _logger.LogInformation("Document {Id} added with {Count} chunks", document.Id, chunks.Count);
    }

    /// <summary>
    /// Add multiple documents
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
    /// Search the knowledge base
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
    /// Get context string for LLM augmentation
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
        sb.AppendLine("Relevant information:");
        sb.AppendLine();
        
        foreach (var result in results)
        {
            sb.AppendLine($"[Source: {result.Chunk.Metadata.GetValueOrDefault("source", "unknown")}]");
            sb.AppendLine(result.Chunk.Content);
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
```

### 3. RAG Tool for Agents

```csharp
namespace Dawning.Agents.Core.Tools.BuiltIn;

using Dawning.Agents.Core.RAG;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tool that allows agents to search a knowledge base
/// </summary>
public class RAGTool : ToolBase
{
    private readonly KnowledgeBase _knowledgeBase;

    public override string Name => "knowledge_search";
    public override string Description => "Search the knowledge base for relevant information. Use this when you need specific facts or documentation.";
    
    public override string ParametersSchema => """
        {
            "type": "object",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "The search query to find relevant information"
                },
                "numResults": {
                    "type": "integer",
                    "description": "Number of results to return (default: 3)"
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
            // Treat input as raw query
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
            return ToolResult.Success("No relevant information found in the knowledge base.");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Found {results.Count} relevant results:");
        sb.AppendLine();

        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            sb.AppendLine($"--- Result {i + 1} (Score: {result.Score:F2}) ---");
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

### 4. RAG-Enhanced Agent

```csharp
namespace Dawning.Agents.Core.Agents;

using Dawning.Agents.Core.LLM;
using Dawning.Agents.Core.RAG;
using Microsoft.Extensions.Logging;

/// <summary>
/// Agent with built-in RAG capabilities
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
        // If auto-retrieve is enabled, augment context with knowledge
        if (_autoRetrieve)
        {
            var relevantContext = await _knowledgeBase.GetContextAsync(
                context.Input,
                topK: 3,
                cancellationToken);

            if (!string.IsNullOrEmpty(relevantContext))
            {
                // Create augmented context
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
            
            You have access to the following knowledge base information that may be relevant:
            
            {context}
            
            Use this information when relevant to answer the user's question.
            Always cite the source when using information from the knowledge base.
            """;
    }
}
```

---

## Complete RAG Pipeline Example

```csharp
// Setup
var embeddingProvider = new OpenAIEmbeddingProvider(apiKey, logger);
var vectorStore = new InMemoryVectorStore();
var chunker = new CharacterChunker(chunkSize: 500, chunkOverlap: 100);
var knowledgeBase = new KnowledgeBase(vectorStore, embeddingProvider, chunker, logger);

// Add documents
var documents = new[]
{
    new Document
    {
        Content = "The Eiffel Tower is a wrought-iron lattice tower on the Champ de Mars in Paris...",
        Source = "wikipedia/eiffel_tower",
        Metadata = { ["category"] = "landmarks" }
    },
    new Document
    {
        Content = "The Great Wall of China is a series of fortifications made of stone, brick...",
        Source = "wikipedia/great_wall",
        Metadata = { ["category"] = "landmarks" }
    }
};

await knowledgeBase.AddDocumentsAsync(documents);

// Create RAG-enhanced agent
var llm = new OpenAIProvider(apiKey, logger);
var ragAgent = new RAGAgent(llm, knowledgeBase, logger);

// Execute with RAG
var response = await ragAgent.ExecuteAsync(new AgentContext
{
    Input = "What is the Eiffel Tower made of?",
    MaxIterations = 5
});

Console.WriteLine(response.Output);
```

---

## Summary

### Week 6 Deliverables

```
src/Dawning.Agents.Core/
├── RAG/
│   ├── IEmbeddingProvider.cs       # Embedding interface
│   ├── OpenAIEmbeddingProvider.cs  # OpenAI implementation
│   ├── VectorSimilarity.cs         # Similarity functions
│   ├── Document.cs                 # Document models
│   ├── IDocumentChunker.cs         # Chunking interface
│   ├── CharacterChunker.cs         # Character-based chunker
│   ├── SentenceChunker.cs          # Sentence-based chunker
│   ├── IVectorStore.cs             # Vector store interface
│   ├── InMemoryVectorStore.cs      # In-memory implementation
│   ├── IRetriever.cs               # Retriever interface
│   ├── VectorRetriever.cs          # Vector-based retriever
│   └── KnowledgeBase.cs            # High-level KB
├── Tools/BuiltIn/
│   └── RAGTool.cs                  # RAG tool for agents
└── Agents/
    └── RAGAgent.cs                 # RAG-enhanced agent
```

### Key Concepts

| Concept | Description |
|---------|-------------|
| **Embeddings** | Dense vector representations |
| **Chunking** | Splitting documents appropriately |
| **Vector Store** | Storage & similarity search |
| **Retrieval** | Finding relevant documents |
| **Context Augmentation** | Injecting knowledge into prompts |

### Next: Phase 4

Phase 4 (Week 7-8) will cover multi-agent collaboration:
- Sequential & parallel orchestration
- Hierarchical coordination
- Agent communication patterns
