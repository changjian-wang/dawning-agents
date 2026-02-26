---
description: "Dawning.Agents architecture reference: project structure, core interfaces, DI API, module boundaries. Trigger: 架构, 项目结构, project structure, interface, module, DI, namespace, where should I put"
---

# Architecture Skill

> **Skill 使用日志**：使用本 skill 后，在 `/memories/session/skill-log.md` 追加一行：`- {时间} architecture — {触发原因}`

## 项目概述

Dawning.Agents 是一个 .NET 企业级 AI Agent 框架，设计灵感来自 OpenAI Agents SDK 的极简风格。

- **目标用户**：需要在 .NET 生态中构建 LLM 驱动 Agent 的企业开发者
- **核心价值**：极简 API + 纯依赖注入 + 企业级基础设施（可观测性、弹性、安全）
- **当前阶段**：pre-release（0.1.0-preview），快速迭代，API 不稳定

## When to Use

- "What is the project structure?"
- "Which interface should I use?"
- "Where should this class live?"
- "How do I register this in DI?"

## Project Layout

```text
Dawning.Agents.sln
├── src/
│   ├── Dawning.Agents.Abstractions
│   ├── Dawning.Agents.Core
│   ├── Dawning.Agents.OpenAI
│   ├── Dawning.Agents.Azure
│   ├── Dawning.Agents.MCP
│   ├── Dawning.Agents.OpenTelemetry
│   ├── Dawning.Agents.Serilog
│   ├── Dawning.Agents.Redis
│   ├── Dawning.Agents.Chroma
│   ├── Dawning.Agents.Pinecone
│   ├── Dawning.Agents.Qdrant
│   └── Dawning.Agents.Weaviate
├── tests/
│   └── Dawning.Agents.Tests
└── samples/
    ├── Dawning.Agents.Api
    ├── Dawning.Agents.Samples.Common
    ├── Dawning.Agents.Samples.Enterprise
    ├── Dawning.Agents.Samples.GettingStarted
    ├── Dawning.Agents.Samples.Memory
    └── Dawning.Agents.Samples.RAG
```

## Namespace Rule

Use folder-based namespaces.

- `Dawning.Agents.Abstractions.{Area}`
- `Dawning.Agents.Core.{Area}`

## Core Interfaces (Current)

### `IAgent`

```csharp
public interface IAgent
{
    string Name { get; }
    string Instructions { get; }
    Task<AgentResponse> RunAsync(string input, CancellationToken cancellationToken = default);
    Task<AgentResponse> RunAsync(AgentContext context, CancellationToken cancellationToken = default);
}
```

### `ILLMProvider`

```csharp
public interface ILLMProvider
{
    string Name { get; }
    Task<ChatCompletionResponse> ChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default);
    IAsyncEnumerable<StreamingChatEvent> ChatStreamEventsAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

### `IToolReader` / `IToolRegistrar` / `IToolRegistry`

```csharp
public interface IToolReader
{
    ITool? GetTool(string name);
    IReadOnlyList<ITool> GetAllTools();
    bool HasTool(string name);
    int Count { get; }
    IReadOnlyList<ITool> GetToolsByCategory(string category);
    IReadOnlyList<string> GetCategories();
}

public interface IToolRegistrar
{
    void Register(ITool tool);
}

public interface IToolRegistry : IToolReader, IToolRegistrar { }
```

### `ICostTracker`

```csharp
public interface ICostTracker
{
    decimal TotalCost { get; }
    decimal? Budget { get; }
    void Add(decimal cost);
    void Reset();
}
```

## Tools DI API (Current)

```csharp
services.AddToolRegistry();
services.AddCoreTools();
services.AddTool(tool);
services.AddToolsFrom<T>();
services.AddToolsFromAssembly(assembly);
services.AddToolApprovalHandler();
```

## Built-in Core Tools

`read_file`, `write_file`, `edit_file`, `search`, `bash`, `create_tool`
