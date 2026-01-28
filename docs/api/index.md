# API Reference

This section contains the complete API reference for Dawning.Agents.

## Packages

| Package | Description |
|---------|-------------|
| [Dawning.Agents.Abstractions](Dawning.Agents.Abstractions.yml) | Core interfaces and models |
| [Dawning.Agents.Core](Dawning.Agents.Core.yml) | Core implementations |
| [Dawning.Agents.OpenAI](Dawning.Agents.OpenAI.yml) | OpenAI provider |
| [Dawning.Agents.Azure](Dawning.Agents.Azure.yml) | Azure OpenAI provider |
| [Dawning.Agents.Redis](Dawning.Agents.Redis.yml) | Redis distributed components |
| [Dawning.Agents.Qdrant](Dawning.Agents.Qdrant.yml) | Qdrant vector store |
| [Dawning.Agents.Pinecone](Dawning.Agents.Pinecone.yml) | Pinecone vector store |

## Key Interfaces

### LLM

- `ILLMProvider` - Language model provider interface
- `IEmbeddingProvider` - Embedding generation interface

### Agent

- `IAgent` - Agent interface
- `IAgentContext` - Agent execution context

### Memory

- `IConversationMemory` - Conversation memory interface
- `ITokenCounter` - Token counting interface

### Tools

- `ITool` - Tool interface
- `IToolRegistry` - Tool registry interface
- `IToolSet` - Tool grouping interface

### RAG

- `IVectorStore` - Vector store interface
- `IDocument` - Document interface

## Namespaces

- `Dawning.Agents.Abstractions` - Core abstractions
- `Dawning.Agents.Abstractions.LLM` - LLM interfaces
- `Dawning.Agents.Abstractions.Agent` - Agent interfaces
- `Dawning.Agents.Abstractions.Memory` - Memory interfaces
- `Dawning.Agents.Abstractions.Tools` - Tool interfaces
- `Dawning.Agents.Abstractions.RAG` - RAG interfaces
- `Dawning.Agents.Core` - Core implementations
- `Dawning.Agents.Core.LLM` - LLM implementations
- `Dawning.Agents.Core.Agent` - Agent implementations
- `Dawning.Agents.Core.Memory` - Memory implementations
- `Dawning.Agents.Core.Tools` - Tool implementations
