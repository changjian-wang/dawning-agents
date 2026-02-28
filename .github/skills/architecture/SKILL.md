---
description: |
  Use when: Understanding project structure, finding where to place new code, checking module boundaries, reviewing namespace rules, or exploring core interfaces and DI registration API
  Don't use when: Writing or modifying code (use code-update), reviewing existing code quality (use code-review)
  Inputs: Question about project structure, namespace, or module placement
  Outputs: Project layout reference, namespace rules, interface definitions, DI API examples
  Success criteria: User knows exactly where to place new code and which interfaces to implement
---

# Architecture Skill

## Project Layout

```text
Dawning.Agents.sln
├── src/
│   ├── Dawning.Agents.Abstractions   — 接口、模型、Options、枚举（零依赖）
│   ├── Dawning.Agents.Core           — Agent 实现、Tools、Memory、Orchestration、Safety、Resilience
│   ├── Dawning.Agents.OpenAI         — OpenAI provider
│   ├── Dawning.Agents.Azure          — Azure OpenAI provider
│   ├── Dawning.Agents.MCP            — MCP 协议客户端/服务端
│   ├── Dawning.Agents.OpenTelemetry  — 可观测性
│   ├── Dawning.Agents.Serilog        — 结构化日志
│   ├── Dawning.Agents.Redis          — Redis 缓存、队列、锁
│   ├── Dawning.Agents.Chroma         — Chroma 向量数据库
│   ├── Dawning.Agents.Pinecone       — Pinecone 向量数据库
│   ├── Dawning.Agents.Qdrant         — Qdrant 向量数据库
│   └── Dawning.Agents.Weaviate       — Weaviate 向量数据库
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

Use folder-based namespaces:

- `Dawning.Agents.Abstractions.{Area}`
- `Dawning.Agents.Core.{Area}`

## Module Boundary Rules

- `Abstractions/` **禁止**引用 `Core/` 或任何实现项目
- `Core/` 只引用 `Abstractions/`
- Provider 项目（OpenAI、Azure 等）引用 `Abstractions/`，可选引用 `Core/`
- 所有 DI 扩展方法放在各自实现项目中（非 Abstractions）

## Core Interfaces

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

## DI Registration API

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

