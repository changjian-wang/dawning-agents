# LLM Providers

Dawning.Agents supports multiple LLM providers out of the box.

## Supported Providers

| Provider | Package | Description |
|----------|---------|-------------|
| Ollama | `Dawning.Agents.Core` | Local LLM inference |
| OpenAI | `Dawning.Agents.OpenAI` | OpenAI API (GPT-4, GPT-4o, etc.) |
| Azure OpenAI | `Dawning.Agents.Azure` | Azure-hosted OpenAI models |
| OpenAI-Compatible | `Dawning.Agents.OpenAI` | DeepSeek, Zhipu (GLM), Moonshot (Kimi), Baichuan, Qwen API, etc. |

## Ollama Provider

Best for local development and privacy-sensitive applications.

### Installation

```bash
# Ollama is included in Core
dotnet add package Dawning.Agents.Core
```

### Configuration

```json
{
  "LLM": {
    "ProviderType": "Ollama",
    "Model": "qwen2.5:7b",
    "Endpoint": "http://localhost:11434",
    "TimeoutSeconds": 300,
    "MaxRetries": 3
  }
}
```

### Recommended Models

| Model | Size | Speed | Use Case |
|-------|------|-------|----------|
| qwen2.5:0.5b | 400MB | Fast | Quick prototyping |
| qwen2.5:7b | 4.7GB | Medium | General use |
| qwen2.5:32b | 19GB | Slow | High quality |
| llama3.1:8b | 4.7GB | Medium | General use |

## OpenAI Provider

Best for production applications requiring high quality.

### Installation

```bash
dotnet add package Dawning.Agents.OpenAI
```

### Configuration

```json
{
  "LLM": {
    "ProviderType": "OpenAI",
    "Model": "gpt-4o-mini",
    "ApiKey": "sk-..."
  }
}
```

### Available Models

| Model | Context | Cost | Best For |
|-------|---------|------|----------|
| gpt-4o | 128K | $$$ | Complex reasoning |
| gpt-4o-mini | 128K | $ | Cost-effective |
| gpt-4-turbo | 128K | $$ | Balanced |

## Azure OpenAI Provider

Best for enterprise deployments with Azure compliance.

### Installation

```bash
dotnet add package Dawning.Agents.Azure
```

### Configuration

```json
{
  "LLM": {
    "ProviderType": "AzureOpenAI",
    "Endpoint": "https://your-resource.openai.azure.com",
    "DeploymentName": "gpt-4o",
    "ApiKey": "your-api-key"
  }
}
```

## OpenAI-Compatible Providers

Supports any LLM provider that exposes an OpenAI-compatible chat completions API, including Chinese providers like DeepSeek, Zhipu (GLM), Moonshot (Kimi), Baichuan, and Qwen API.

### Installation

```bash
dotnet add package Dawning.Agents.OpenAI
```

### Configuration Examples

**DeepSeek:**

```json
{
  "DeepSeek": {
    "ApiKey": "sk-xxx",
    "Model": "deepseek-chat",
    "Endpoint": "https://api.deepseek.com"
  }
}
```

**Zhipu (GLM):**

```json
{
  "Zhipu": {
    "ApiKey": "xxx.yyy",
    "Model": "glm-4",
    "Endpoint": "https://open.bigmodel.cn/api/paas/v4"
  }
}
```

**Moonshot (Kimi):**

```json
{
  "Moonshot": {
    "ApiKey": "sk-xxx",
    "Model": "moonshot-v1-8k",
    "Endpoint": "https://api.moonshot.cn/v1"
  }
}
```

### Registration

```csharp
// Direct registration
services.AddOpenAICompatibleProvider("sk-xxx", "deepseek-chat", "https://api.deepseek.com");

// With custom provider name for logging
services.AddOpenAICompatibleProvider(
    "sk-xxx", "glm-4", "https://open.bigmodel.cn/api/paas/v4", "Zhipu");

// From configuration section
services.AddOpenAICompatibleProvider(configuration.GetSection("DeepSeek"));

// Using options delegate
services.AddOpenAICompatibleProvider(options =>
{
    options.ApiKey = "sk-xxx";
    options.Model = "deepseek-chat";
    options.Endpoint = "https://api.deepseek.com";
    options.ProviderName = "DeepSeek";
});
```

### Supported Providers

| Provider | Endpoint | Models |
|----------|----------|--------|
| DeepSeek | `https://api.deepseek.com` | deepseek-chat, deepseek-reasoner |
| Zhipu (GLM) | `https://open.bigmodel.cn/api/paas/v4` | glm-4, glm-4-flash |
| Moonshot (Kimi) | `https://api.moonshot.cn/v1` | moonshot-v1-8k, moonshot-v1-32k, moonshot-v1-128k |
| Baichuan | `https://api.baichuan-ai.com/v1` | Baichuan4, Baichuan3-Turbo |
| Qwen (DashScope) | `https://dashscope.aliyuncs.com/compatible-mode/v1` | qwen-max, qwen-plus, qwen-turbo |
| Yi (零一万物) | `https://api.lingyiwanwu.com/v1` | yi-large, yi-medium |

## Provider Factory Pattern

Dawning.Agents uses a unified factory pattern for providers:

```csharp
// Register based on configuration
services.AddLLMProvider(configuration);

// Or register specific provider
services.AddOllamaProvider(configuration);
services.AddOpenAIProvider(configuration);
services.AddAzureOpenAIProvider(configuration);
services.AddOpenAICompatibleProvider(apiKey, model, endpoint);

// Use via DI
var provider = serviceProvider.GetRequiredService<ILLMProvider>();
var response = await provider.ChatAsync(messages);
```

## Streaming Support

All providers support streaming:

```csharp
await foreach (var chunk in provider.ChatStreamAsync(messages))
{
    Console.Write(chunk);
}
```

## Embeddings Support

For RAG applications, use embedding providers:

```csharp
services.AddEmbeddingProvider(configuration);

var embedder = serviceProvider.GetRequiredService<IEmbeddingProvider>();
var vector = await embedder.EmbedAsync("Hello world");
```
