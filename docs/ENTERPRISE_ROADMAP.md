# 🚀 Dawning.Agents 企业级转型路线图

> **目标**: 将 dawning-agents 从学习项目转型为企业级 AI Agent 框架  
> **当前版本**: v0.1.0-preview.1  
> **目标版本**: v0.2.0 (企业就绪)  
> **最后更新**: 2026-02-11

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

## ✅ Phase 1: 基础修正 (已完成)

> **目标**: 解决所有 P0 阻塞问题，使框架可被企业评估  
> **完成日期**: 2026-02-11

### 1.1 Core 包拆分 (P0) ✅

**问题**: `Dawning.Agents.Core.csproj` 包含 32+ NuGet 包，安装 Core 拉入 ~35+ 无关传递依赖，违背"极简"设计。

**已完成改动**:

- 新建 `Dawning.Agents.OpenTelemetry` — 7 个 OTel 包 + `OpenTelemetryExtensions.cs`
- 新建 `Dawning.Agents.Serilog` — 10 个 Serilog 包 + 4 个源文件（LoggingServiceCollectionExtensions、AgentContextEnricher、SpanIdEnricher、LogLevelController）
- 移入 `Dawning.Agents.Redis` — RedisHealthCheck + HealthChecks 包 + `AddRedisHealthChecks` 扩展方法
- Core.csproj 从 ~32 个包降至 ~13 个包
- 所有 1929 个测试通过

### 1.2 Native Function Calling 支持 (P0) ✅

**已完成改动**:

- `ChatMessage` 扩展: `Name`、`ToolCalls`、`ToolCallId` 属性
- `ChatCompletionOptions` 扩展: `Tools`、`ToolChoice`、`ResponseFormat` 属性
- `ChatCompletionResponse` 扩展: `ToolCalls`、`FinishReason` 属性
- 新增 `ToolCall`、`ToolDefinition`、`ToolChoiceMode`、`ResponseFormat` 模型
- OllamaProvider 适配 Function Calling
- 新增 `FunctionCallingAgent` 实现
- 新增 23 个测试覆盖

### 1.3 异常保留 (P0) ✅

**已完成改动**:

- `AgentResponse.Exception` 属性保留原始异常
- `AgentBase`、`OrchestratorBase`、`ParallelOrchestrator` 全部保留异常信息

### 1.4 Provider 基类抽取 + 基础设施接入 (P1) ✅

**已完成改动**:

- 新建 `OpenAIProviderBase` 抽象基类（~135 行共享代码）
- 共享: `ChatAsync`、`ChatStreamAsync`、`BuildMessages`、`CreateAssistantWithToolCalls`、`BuildRequestOptions`
- `OpenAIProvider` 从 ~193 行精简至 ~40 行
- `AzureOpenAIProvider` 从 ~226 行精简至 ~90 行
- Azure 项目新增对 OpenAI 项目的 ProjectReference

### 1.5 DI 生命周期修复 (P1) ✅

**验证结果**: Agent 已注册为 Scoped（`TryAddScoped<IAgent, ReActAgent>`），Memory 已注册为 Scoped（`TryAddScoped<IConversationMemory>`），无 Captive Dependency 问题。无需修改。

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

| Phase | 验收条件 | 状态 |
|-------|---------|------|
| **Phase 1** | `dotnet pack Dawning.Agents.Core` 传递依赖 ≤ 15 个；`ChatMessage.ToolCalls` 可编译；`AgentResponse.Exception` 有值 | ✅ 已通过 |
| **Phase 2** | `ResponseFormat = json_object` 端到端通过；Options 启动校验覆盖 100% | ⏳ 待验证 |
| **Phase 3** | NetArchTest 全绿；`ParallelOrchestrator` 部分失败测试通过 | ⏳ 待验证 |

---

## 📚 相关文档

- [ARCHITECTURE.md](architecture/ARCHITECTURE.md) - 源码架构文档（含已知问题章节）
- [ENTERPRISE_READINESS_ASSESSMENT.md](ENTERPRISE_READINESS_ASSESSMENT.md) - 企业就绪评估
- [ENTERPRISE_GAP_PLAN.md](ENTERPRISE_GAP_PLAN.md) - 功能差距计划
- [README.md](../README.md) - 项目介绍

---

> 📌 **创建日期**: 2026-01-27  
> 📌 **最后更新**: 2026-02-11  
> 📌 **状态**: Phase 1 已完成 ✅ | Phase 2 进行中
