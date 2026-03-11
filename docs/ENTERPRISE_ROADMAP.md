# 🚀 Dawning.Agents 企业级路线图

> **目标**: 将 dawning-agents 打造为企业级 AI Agent 框架
> **当前版本**: v0.1.0-preview.1
> **目标版本**: v0.2.0 (企业就绪)
> **最后更新**: 2026-03-11

---

## 📊 当前状态概览

| 维度 | 评分 | 说明 |
|------|------|------|
| **核心功能** | ⭐⭐⭐⭐⭐ 95% | Agent/Memory/Tools/RAG/MCP/Multimodal 完整 |
| **架构健康度** | ⭐⭐⭐⭐ 85% | Core 拆分完成，DI 正确，38 轮深度审计 |
| **生产就绪度** | ⭐⭐⭐⭐ 85% | Function Calling、异常保留、弹性策略、护栏 |
| **企业级特性** | ⭐⭐⭐⭐ 85% | OpenTelemetry、Serilog、配置热重载、安全护栏 |
| **代码质量** | ⭐⭐⭐⭐ 88% | 2225 测试、Meziantou Analyzer、~190 审计修复 |
| **文档与 DX** | ⭐⭐⭐ 70% | API 文档完整，教程和案例待补充 |

**综合评分: 85%** — 已达企业级就绪基线

### 关键指标

| 指标 | 初始 (2026-01) | 当前 (2026-03) |
|------|---------------|----------------|
| 测试数量 | 1,906 | **2,225** |
| Core 传递依赖 | ~35 | **≤15** |
| src 项目数 | 6 | **12** |
| 深度审计轮次 | 0 | **38** |
| 审计修复 | 0 | **~190** |

---

## ✅ Phase 1: 基础修正 — 已完成 (2026-02-11)

> 解决所有 P0 阻塞问题，使框架可被企业采用

| 编号 | 任务 | 影响 | 状态 |
|------|------|------|------|
| P0-1 | Core 包拆分 (移除 OpenAI/Azure/Serilog/OTel) | 🔴 架构违规 | ✅ |
| P0-2 | Native Function Calling 支持 | 🔴 核心缺失 | ✅ |
| P0-3 | 异常保留 (AgentResponse.Exception) | 🔴 生产阻塞 | ✅ |
| P1-1 | Provider 基类抽取 + ILogger/IOptions | 🟡 质量问题 | ✅ |
| P1-2 | DI 生命周期修复 | 🟡 运行时 Bug | ✅ (无需修改) |

**成果**: Core 包从 ~32 个依赖降至 ~13 个，新建 OpenTelemetry/Serilog 独立包。

---

## ✅ Phase 2: 功能补齐 — 大部分完成

| 编号 | 任务 | 状态 | 说明 |
|------|------|------|------|
| 2.1 | Structured Output (ResponseFormat) | ✅ | json_object / json_schema 支持 |
| 2.2 | 流式事件结构化 (StreamingChatEvent) | ❌ 未开始 | v0.2.0 规划 |
| 2.3 | Prompt Injection Guardrail | ✅ | PromptInjectionGuardrail + 测试 |
| 2.4 | Options 启动校验 (IValidateOptions) | ✅ | AddOptionsWithValidateOnStart |
| 2.5 | Roslyn 分析器 | ✅ | Meziantou.Analyzer + TreatWarningsAsErrors |

---

## 🟡 Phase 3: 企业加固 — 部分完成

| 编号 | 任务 | 状态 | 说明 |
|------|------|------|------|
| 3.1 | 架构测试 (NetArchTest) | ✅ | ArchitectureTests.cs 验证模块边界 |
| 3.2 | Agent 状态持久化 | ❌ 未开始 | v0.2.0 规划 |
| 3.3 | Tools 重设计 | ✅ | 6 核心工具 + 动态工具创建 |
| 3.4 | DTO 不可变化 | 🟡 部分 | 审计中逐步改进 |
| 3.5 | ParallelOrchestrator 部分失败 | ✅ | ContinueOnError + 逐 Task try/catch |
| 3.6 | ReActAgent 清理 | ✅ | 移除虚假回退工具 |

---

## ✅ 已完成的功能模块

| 模块 | 完成度 | 关键能力 |
|------|--------|---------|
| **Agent 核心** | 95% | ReActAgent + FunctionCallingAgent + AgentContext/Response/Step |
| **LLM Provider** | 90% | Ollama/OpenAI/Azure + ResponseFormat + 共享基类 |
| **Tools** | 95% | 6 核心工具 + MethodTool + ToolScanner + ToolApproval |
| **Memory** | 95% | Buffer/Window/Summary/Adaptive/Vector + SemanticCache |
| **RAG** | 90% | Embedding + VectorStore×5 + DocumentChunker + KnowledgeBase |
| **多 Agent** | 90% | Sequential/Parallel Orchestrator + Handoff |
| **安全护栏** | 95% | PII 检测 + Prompt Injection + RateLimiter + SafeAgent |
| **人机协作** | 90% | ApprovalWorkflow + HumanInLoopAgent + AsyncCallback |
| **MCP** | 90% | Server + Client + StdioTransport + MCPToolProxy |
| **可观测性** | 85% | OpenTelemetry + Serilog + MetricsCollector + HealthCheck |
| **弹性** | 90% | Polly V8 + ResilientLLMProvider + 熔断/重试/超时 |
| **评估** | 85% | Evaluator + ABTestRunner + EvaluationReport |
| **工作流** | 85% | WorkflowBuilder + WorkflowEngine + 7 种节点 + Mermaid |
| **多模态** | 80% | Vision (GPT-4V) + Whisper + TTS |
| **配置** | 90% | IOptionsMonitor + HotReloadableLLMProvider + ConfigChangeNotifier |
| **向量存储** | 90% | Qdrant + Pinecone + Redis + Chroma + Weaviate + InMemory |

---

## 🔲 v0.2.0 路线图 — 2026 Q2

### 功能开发

| 优先级 | 任务 | 工期 | 说明 |
|--------|------|------|------|
| P1 | 流式事件结构化 (StreamingChatEvent) | 1 周 | ContentDelta / ToolCallDelta / Usage |
| P1 | Agent 状态持久化 (IAgentCheckpoint) | 1 周 | 跨进程重启恢复 |
| P2 | A2A 协议支持 | 2-3 周 | Agent 间标准化通信 |
| P2 | Prompt 模板与版本管理 | 1-2 周 | 提示词生命周期管理 |
| P2 | 持久化 Memory (SQLite/PostgreSQL) | 1 周 | 替代 InMemory 的生产存储 |
| P3 | MS Agent Framework 工具适配器 | 1 周 | 微软生态互操作 |
| P3 | OTEL gen_ai.* 语义约定 | 0.5 周 | 符合 OpenTelemetry GenAI Spec |

### 文档与社区

| 任务 | 状态 | 说明 |
|------|------|------|
| CONTRIBUTING.md | ✅ | Fork & PR 流程 |
| CODE_OF_CONDUCT.md | ✅ | Contributor Covenant v2.1 |
| SECURITY.md | ✅ | 漏洞报告流程 |
| API Sample 重建 | ❌ | ASP.NET Core Minimal API 示例 |
| 生产案例教程 | ❌ | 聊天机器人 / RAG 应用 / 多 Agent |
| NuGet 发布 | ❌ | 公开包发布 |

---

## 🔲 v0.3.0 路线图 — 2026 Q3

- 🔲 Multi-Agent Swarm 模式
- 🔲 Agent 可视化调试器
- 🔲 Workflow 可视化调试
- 🔲 云端 Agent 托管
- 🔲 企业 SSO 集成

---

## 📊 与主流框架对比

| 特性 | Dawning.Agents | MS Agent Framework | LangChain | OpenAI Agents SDK |
|------|----------------|-----------------|-----------|-------------------|
| **语言** | C# (.NET 10) | C#/Python | Python/JS | Python |
| **Function Calling** | ✅ | ✅ | ✅ | ✅ |
| **Structured Output** | ✅ | ✅ | ✅ | ✅ |
| **Tools** | ✅ (6 核心 + 动态) | ✅ | ✅ | ✅ |
| **Memory** | ✅ (5 策略) | ✅ | ✅ | ✅ |
| **RAG** | ✅ (完整) | ✅ | ✅ (完整) | ⚠️ |
| **多 Agent** | ✅ | ✅ | ✅ | ✅ |
| **安全护栏** | ✅ | ✅ | ⚠️ | ✅ |
| **MCP 支持** | ✅ | ⚠️ | ✅ | ✅ |
| **多模态** | ✅ | ✅ | ✅ | ✅ |
| **工作流 DSL** | ✅ | ⚠️ | ✅ | ⚠️ |
| **A/B 测试** | ✅ | ⚠️ | ✅ | ⚠️ |
| **可观测性** | ✅ (OTel) | ✅ | ✅ | ✅ |

**Dawning.Agents 独特优势**: 纯 DI 架构、Dawning Gateway 生态整合、.NET 最佳实践、6 核心工具极简设计。

---

## 📚 相关文档

- [ARCHITECTURE.md](architecture/ARCHITECTURE.md) — 源码架构文档
- [OPENSOURCE_PLAN.md](OPENSOURCE_PLAN.md) — 开源准备计划
- [API_REFERENCE.md](API_REFERENCE.md) — API 参考文档
- [QUICKSTART.md](QUICKSTART.md) — 快速入门
- [README.md](../README.md) — 项目介绍

---

> 📌 **创建日期**: 2026-01-27
> 📌 **最后更新**: 2026-03-11
> 📌 **整合自**: ENTERPRISE_ROADMAP + ENTERPRISE_GAP_PLAN + ENTERPRISE_READINESS_ASSESSMENT
