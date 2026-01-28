# RAG (Retrieval-Augmented Generation)

RAG enhances LLM responses by retrieving relevant documents from a knowledge base.

## Components

| Component | Description |
|-----------|-------------|
| Embedding Provider | Converts text to vectors |
| Vector Store | Stores and searches vectors |
| RAG Pipeline | Orchestrates retrieval and generation |

## Supported Vector Stores

| Store | Package | Description |
|-------|---------|-------------|
| In-Memory | `Dawning.Agents.Core` | For development/testing |
| Qdrant | `Dawning.Agents.Qdrant` | Open-source, self-hosted |
| Pinecone | `Dawning.Agents.Pinecone` | Managed cloud service |

## Quick Start

```csharp
// 1. Register embedding provider
services.AddEmbeddingProvider(configuration);

// 2. Register vector store
services.AddQdrantVectorStore(configuration);
// or
services.AddPineconeVectorStore(configuration);

// 3. Use RAG
var vectorStore = serviceProvider.GetRequiredService<IVectorStore>();

// Index documents
await vectorStore.UpsertAsync(new Document
{
    Id = "doc1",
    Content = "Dawning.Agents is a .NET AI framework",
    Metadata = new { source = "readme" }
});

// Search
var results = await vectorStore.SearchAsync("What is Dawning.Agents?", topK: 5);
```

## Embedding Providers

### Ollama Embeddings

```json
{
  "Embedding": {
    "ProviderType": "Ollama",
    "Model": "nomic-embed-text",
    "Endpoint": "http://localhost:11434"
  }
}
```

### OpenAI Embeddings

```json
{
  "Embedding": {
    "ProviderType": "OpenAI",
    "Model": "text-embedding-3-small",
    "ApiKey": "sk-..."
  }
}
```

### Azure OpenAI Embeddings

```json
{
  "Embedding": {
    "ProviderType": "AzureOpenAI",
    "Endpoint": "https://your-resource.openai.azure.com",
    "DeploymentName": "text-embedding-3-small"
  }
}
```

## Qdrant Vector Store

Self-hosted, open-source vector database.

### Installation

```bash
dotnet add package Dawning.Agents.Qdrant

# Run Qdrant
docker run -p 6333:6333 qdrant/qdrant
```

### Configuration

```json
{
  "Qdrant": {
    "Host": "localhost",
    "Port": 6333,
    "CollectionName": "documents",
    "VectorSize": 1536
  }
}
```

### Usage

```csharp
services.AddQdrantVectorStore(configuration);

var store = serviceProvider.GetRequiredService<IVectorStore>();

// Create collection
await store.CreateCollectionAsync("documents", vectorSize: 1536);

// Upsert documents
await store.UpsertAsync(documents);

// Search
var results = await store.SearchAsync(query, topK: 5);
```

## Pinecone Vector Store

Managed cloud vector database.

### Installation

```bash
dotnet add package Dawning.Agents.Pinecone
```

### Configuration

```json
{
  "Pinecone": {
    "ApiKey": "your-api-key",
    "Environment": "us-east-1",
    "IndexName": "documents"
  }
}
```

### Usage

```csharp
services.AddPineconeVectorStore(configuration);

var store = serviceProvider.GetRequiredService<IVectorStore>();

// Pinecone creates index automatically
await store.UpsertAsync(documents);

// Search with metadata filter
var results = await store.SearchAsync(
    query,
    topK: 5,
    filter: new { source = "readme" }
);
```

## RAG Pipeline

Complete RAG workflow:

```csharp
// 1. Retrieve relevant documents
var docs = await vectorStore.SearchAsync(userQuery, topK: 5);

// 2. Build context
var context = string.Join("\n", docs.Select(d => d.Content));

// 3. Generate response with context
var prompt = $"""
    Answer based on the following context:
    
    {context}
    
    Question: {userQuery}
    """;

var response = await llmProvider.ChatAsync(new[] { new ChatMessage("user", prompt) });
```
