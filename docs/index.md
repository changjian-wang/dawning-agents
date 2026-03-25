# Dawning.Agents Documentation

Welcome to the **Dawning.Agents** documentation - a minimalist .NET AI Agent framework inspired by OpenAI Agents SDK.

## Quick Links

- [Getting Started](guides/getting-started.md)
- [Architecture](architecture.md)
- [API Reference](api/index.md)
- [GitHub Repository](https://github.com/changjian-wang/dawning-agents)

## What is Dawning.Agents?

Dawning.Agents is an enterprise-grade AI Agent framework for .NET that provides:

- **Multiple LLM Providers** - Ollama, OpenAI, Azure OpenAI
- **Memory Systems** - Buffer, Window, Summary memory
- **Tools/Skills** - Built-in tools, custom tool support
- **Multi-Agent** - Agent orchestration, handoffs, communication
- **RAG** - Vector stores (Qdrant, Pinecone), embeddings
- **Enterprise Features** - Guardrails, observability, resilience

## Installation

```bash
# Core package
dotnet add package Dawning.Agents.Core

# Optional providers
dotnet add package Dawning.Agents.OpenAI
dotnet add package Dawning.Agents.Azure
dotnet add package Dawning.Agents.Qdrant
dotnet add package Dawning.Agents.Pinecone
```

## Quick Start

```csharp
// Configure services
services.AddLLMProvider(configuration);
services.AddAgent<ReActAgent>();
services.AddAllBuiltInTools();

// Run agent
var agent = serviceProvider.GetRequiredService<IAgent>();
var response = await agent.RunAsync("What's the weather in Beijing?");
Console.WriteLine(response.Output);
```

## Guides

| Guide | Description |
|-------|-------------|
| [Getting Started](guides/getting-started.md) | Installation and first steps |
| [LLM Providers](guides/llm-providers.md) | Configure Ollama, OpenAI, Azure |
| [Tools & Skills](guides/tools.md) | Built-in and custom tools |
| [Memory](guides/memory.md) | Conversation memory systems |
| [RAG](guides/rag.md) | Vector stores and retrieval |
| [Multi-Agent](guides/multi-agent.md) | Agent orchestration |
| [Performance](guides/performance.md) | Performance tuning |
| [Production](guides/production.md) | Production & security best practices |
| [Examples](guides/examples.md) | Code review agent, customer service bot |

## Reference

- [Architecture](architecture.md)
- [API Reference](api/index.md)

## License

MIT License - see [LICENSE](https://github.com/changjian-wang/dawning-agents/blob/main/LICENSE)
