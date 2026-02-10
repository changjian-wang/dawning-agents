# 🚀 Dawning.Agents 企业级转型路线图

> **目标**: 将 dawning-agents 从学习项目转型为企业级 AI Agent 框架  
> **当前版本**: v0.1.0-preview.1  
> **目标版本**: v0.2.0 (企业就绪)  
> **最后更新**: 2026-02-10

---

## 📊 架构审查结论 (2026-02-10)

经过全面代码审查，对标 Semantic Kernel / AutoGen / OpenAI Agents SDK，发现以下需重点解决的问题：

### 严重度分级

| 优先级 | 数量 | 说明 |
|--------|------|------|
| **P0 (必须修复)** | 3 | 阻碍企业采用 |
| **P1 (重要优化)** | 5 | 影响生产体验 |
| **P2 (建议改进)** | 6 | 提升代码质量 |

---

## 🔴 Phase 1: 基础修正 (2-3 周)

> **目标**: 解决所有 P0 阻塞问题，使框架可被企业评估

### 1.1 Core 包拆分 (P0)

**问题**: `Dawning.Agents.Core.csproj` 包含 32+ NuGet 包 + 反向引用 OpenAI/Azure Provider，安装 Core 拉入 ~35+ 无关传递依赖，违背"极简"设计。

**改动计划**:

```
Dawning.Agents.Core.csproj（瘦身后）
├── 保留: FluentValidation, Polly, M.E.Http, M.E.Logging.Abstractions
├── 保留: M.E.Configuration.*, M.E.Options.*, System.Numerics.Tensors
└── 移除以下依赖:

移除 → Dawning.Agents.Observability（新建）
├── OpenTelemetry (7 个包)
└── ObservabilityServiceCollectionExtensions.cs

移除 → Dawning.Agents.Logging.Serilog（新建）
├── Serilog (10 个包 + Elastic sink)
└── SerilogServiceCollectionExtensions.cs

移除 → Dawning.Agents.Redis (已有，移入)
├── StackExchange.Redis
└── AspNetCore.HealthChecks.Redis

移除 ProjectReference:
├── Dawning.Agents.OpenAI  ← Core 不应依赖 Provider
└── Dawning.Agents.Azure   ← Core 不应依赖 Provider
```

**影响**: 纯 Core 消费者的传递依赖从 ~35 个降至 ~12 个。

### 1.2 Native Function Calling 支持 (P0)

**问题**: `ChatMessage` 只有 `Role + Content`，`ChatCompletionOptions` 只有 3 个字段，无法使用现代 LLM 的原生 Function Calling。

**改动计划**:

```csharp
// ChatMessage 扩展
public record ChatMessage
{
    public string Role { get; init; }
    public string? Content { get; init; }
    public string? Name { get; init; }                    // 新增
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }  // 新增
    public string? ToolCallId { get; init; }              // 新增
}

public record ToolCall(string Id, string Name, string Arguments);

// ChatCompletionOptions 扩展
public record ChatCompletionOptions
{
    public float Temperature { get; init; } = 0.7f;
    public int MaxTokens { get; init; } = 1000;
    public string? SystemPrompt { get; init; }
    public IReadOnlyList<ToolDefinition>? Tools { get; init; }     // 新增
    public ToolChoiceMode? ToolChoice { get; init; }               // 新增
    public ResponseFormat? ResponseFormat { get; init; }           // 新增
}

// ChatCompletionResponse 扩展
public record ChatCompletionResponse
{
    // ...existing
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }  // 新增
    public string? FinishReason { get; init; }                // 新增
}

// 流式事件结构化
public record StreamingChatEvent
{
    public string? ContentDelta { get; init; }
    public ToolCall? ToolCallDelta { get; init; }
    public string? FinishReason { get; init; }
    public TokenUsage? Usage { get; init; }
}
```

**影响范围**: `Abstractions/LLM/`、所有 Provider、`ReActAgent`（可选用 Function Calling 模式）。

### 1.3 异常保留 (P0)

**问题**: `AgentBase.RunAsync` 的 `catch (Exception ex)` 丢失异常类型和堆栈，调用方无法重试或诊断。

**改动计划**:

```csharp
// AgentResponse 增加
public Exception? Exception { get; init; }

// AgentBase.RunAsync catch 改为
catch (Exception ex)
{
    return AgentResponse.Failed(ex.Message, context.Steps, stopwatch.Elapsed, ex);
}
```

**影响范围**: `AgentResponse`、`AgentBase`、`OrchestratorBase`、`ParallelOrchestrator`。

### 1.4 Provider 基类抽取 + 基础设施接入 (P1)

**问题**: OpenAI/Azure Provider 无 ILogger、无 IOptions、无重试、`Content[0].Text` 不安全取值、代码重复。

**改动计划**:

```
新建 OpenAIProviderBase（共享 BuildMessages / BuildRequestOptions）
├── OpenAIProvider : OpenAIProviderBase
└── AzureOpenAIProvider : OpenAIProviderBase

每个 Provider:
├── 注入 ILogger<T>
├── 使用 IOptions<OpenAIProviderOptions>
├── Content 安全取值 (Content?.FirstOrDefault()?.Text)
└── 可选接入 Polly 重试策略
```

### 1.5 DI 生命周期修复 (P1)

**问题**: Agent 注册为 Singleton，Memory 注册为 Scoped，产生 Captive Dependency。

**修复**: Agent 改为 Scoped 注册，或 Memory 改为 Singleton。

---

## 🟡 Phase 2: 功能补齐 (3-4 周)

> **目标**: 补齐与 Semantic Kernel 的关键功能差距

### 2.1 Structured Output (ResponseFormat)

- `ChatCompletionOptions.ResponseFormat` 支持 `json_object` / `json_schema`
- Provider 适配 OpenAI `response_format` 参数
- Agent 层可自动解析 JSON 响应为强类型

### 2.2 流式事件结构化

- `ILLMProvider.ChatStreamAsync` 返回 `IAsyncEnumerable<StreamingChatEvent>`
- 事件类型: ContentDelta / ToolCallDelta / FinishReason / Usage
- 向下兼容: 保留 `IAsyncEnumerable<string>` 扩展方法

### 2.3 Prompt Injection Guardrail

- 新增 `PromptInjectionGuardrail`
- 检测常见注入模式 ("ignore previous instructions", "system prompt override" 等)
- Tool 输出消毒后再回注 LLM 上下文

### 2.4 `IValidateOptions<T>` 启动校验

- 所有 Options 类实现 `Validate()` (当前仅 29%)
- 使用 `services.AddOptionsWithValidateOnStart<T>()` 实现启动时 fail-fast
- 防止负值、空字符串等无效配置进入运行时

### 2.5 Roslyn 分析器接入

- 添加 `Meziantou.Analyzer` 或 `Microsoft.CodeAnalysis.NetAnalyzers`
- Abstractions 包移除 CS1591 NoWarn，强制 XML 文档
- 配置 `.editorconfig` 规则级别

---

## 🟢 Phase 3: 企业加固 (2-3 周)

> **目标**: 提升代码质量到企业级标准

### 3.1 架构测试 (NetArchTest)

- 验证 Abstractions 零外部依赖
- 验证 Core 不引用 Provider 包
- 验证命名规范 (I 前缀、Options 后缀、Async 后缀)

### 3.2 Agent 状态持久化

- `IAgentCheckpoint` 接口
- 序列化/恢复 `AgentContext` 中间状态
- 支持 SQLite / Redis 存储

### 3.3 IToolRegistry 拆分

- 拆为 `IToolRegistry` + `IToolSetRegistry` + `IVirtualToolManager`
- 增加 `UnregisterTool` / `UnregisterToolSet` 支持热卸载

### 3.4 DTO 不可变化

- `AgentContext.Steps` → `IReadOnlyList<AgentStep>`
- `DocumentChunk.Metadata` → `IReadOnlyDictionary<string, string>`
- `WorkflowContext` 内部集合全部改为 immutable

### 3.5 ParallelOrchestrator 部分失败修复

- `ContinueOnError=true` 时逐 Task try/catch
- 收集已完成结果，不因单个失败丢弃全部

### 3.6 ReActAgent 清理

- 移除无工具时的虚假 "Search/Calculate/Lookup" 回退
- 无工具时返回空列表 + "no tools available" 提示

---

## � 验收标准

每个 Phase 完成后需通过以下检查：

| Phase | 验收条件 |
|-------|---------|
| **Phase 1** | `dotnet pack Dawning.Agents.Core` 传递依赖 ≤ 15 个；`ChatMessage.ToolCalls` 可编译；`AgentResponse.Exception` 有值 |
| **Phase 2** | `ResponseFormat = json_object` 端到端通过；Options 启动校验覆盖 100% |
| **Phase 3** | NetArchTest 全绿；`ParallelOrchestrator` 部分失败测试通过 |

---

## 📚 相关文档

- [ARCHITECTURE.md](architecture/ARCHITECTURE.md) - 源码架构文档（含已知问题章节）
- [ENTERPRISE_READINESS_ASSESSMENT.md](ENTERPRISE_READINESS_ASSESSMENT.md) - 企业就绪评估
- [ENTERPRISE_GAP_PLAN.md](ENTERPRISE_GAP_PLAN.md) - 功能差距计划
- [README.md](../README.md) - 项目介绍

---

> 📌 **创建日期**: 2026-01-27  
> 📌 **最后更新**: 2026-02-10  
> 📌 **状态**: Phase 1 准备中
