# Getting Started

This guide will help you get started with Dawning.Agents in minutes.

## Prerequisites

- .NET 10.0 SDK or later
- (Optional) Ollama for local LLM inference
- (Optional) OpenAI API key for cloud LLM

## Installation

### 1. Create a new project

```bash
dotnet new console -n MyAgent
cd MyAgent
```

### 2. Add Dawning.Agents packages

```bash
# Core package (includes Ollama provider)
dotnet add package Dawning.Agents.Core

# Optional: OpenAI provider
dotnet add package Dawning.Agents.OpenAI

# Optional: Azure OpenAI provider
dotnet add package Dawning.Agents.Azure
```

### 3. Configure appsettings.json

```json
{
  "LLM": {
    "ProviderType": "Ollama",
    "Model": "qwen2.5:7b",
    "Endpoint": "http://localhost:11434"
  }
}
```

### 4. Setup dependency injection

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Dawning.Agents.Core;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var services = new ServiceCollection();
services.AddLLMProvider(configuration);
services.AddAgent<ReActAgent>();
services.AddAllBuiltInTools();

var serviceProvider = services.BuildServiceProvider();
```

### 5. Run the agent

```csharp
var agent = serviceProvider.GetRequiredService<IAgent>();

var response = await agent.RunAsync("What is 25 * 4?");
Console.WriteLine(response.Output);
```

## Using Ollama (Local LLM)

1. Install Ollama from [ollama.ai](https://ollama.ai)
2. Pull a model:

```bash
ollama pull qwen2.5:7b
```

3. Configure:

```json
{
  "LLM": {
    "ProviderType": "Ollama",
    "Model": "qwen2.5:7b",
    "Endpoint": "http://localhost:11434"
  }
}
```

## Using OpenAI

1. Get API key from [platform.openai.com](https://platform.openai.com)
2. Configure:

```json
{
  "LLM": {
    "ProviderType": "OpenAI",
    "Model": "gpt-4o-mini",
    "ApiKey": "sk-..."
  }
}
```

Or use environment variable:

```bash
export OPENAI_API_KEY=sk-...
```

## Next Steps

- [LLM Providers](llm-providers.md) - Learn about all supported providers
- [Tools & Skills](tools.md) - Explore 64+ built-in tools
- [Memory](memory.md) - Add conversation memory
- [RAG](rag.md) - Build retrieval-augmented generation
