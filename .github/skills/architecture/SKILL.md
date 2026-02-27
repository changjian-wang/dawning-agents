---
description: "Dawning.Agents architecture reference: project structure, core interfaces, DI API, module boundaries. Trigger: 架构, 项目结构, project structure, interface, module, DI, namespace, where should I put"
---

# Architecture Skill

## 目标

提供项目结构、核心接口、模块边界和 DI 注册的权威参考。

## 触发条件

- **关键词**：架构, 项目结构, project structure, interface, module, DI, namespace, where should I put
- **文件模式**：`*.csproj`, `Directory.Build.props`, `Dawning.Agents.sln`
- **用户意图**：了解项目结构、确定代码放置位置、查询接口定义、DI 注册方式

## 编排

- **前置**：无
- **后续**：`code-update`（开始编码时）

## Skill 使用日志

使用本 skill 后，在 `/memories/repo/skill-usage.md` 追加一行：`- {日期} architecture — {触发原因}`

---

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

## 验收场景

- **输入**："新增一个 IXxxProvider 接口，应该放哪里？"
- **预期**：agent 读取此 skill，回答放在 `Abstractions/{Area}/` 下，实现放 provider 项目
- **上次验证**：2026-02-27
