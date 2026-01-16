# Changelog

本文档记录 dawning-agents 项目的所有重要变更，便于在不同会话中快速恢复上下文。

---

## [2026-01-16] Phase 0: 框架分析文档全面更新

### 背景
微软在 2025年11月宣布将 **Semantic Kernel** 和 **AutoGen** 整合为统一的 **Microsoft Agent Framework**。同时 **OpenAI Agents SDK**（Swarm 的生产版本）成为主流框架。因此需要更新所有框架分析文档。

### 删除的文档
- `docs/readings/03-semantic-kernel-analysis/` - Semantic Kernel 分析（已过时）
- `docs/readings/04-autogen-analysis/` - AutoGen 分析（已过时）

### 新增的文档
| 文件 | 描述 |
|------|------|
| `docs/readings/03-ms-agent-framework-analysis/ms-agent-framework-analysis-zh.md` | MS Agent Framework 架构分析（中文） |
| `docs/readings/03-ms-agent-framework-analysis/ms-agent-framework-analysis-en.md` | MS Agent Framework 架构分析（英文） |
| `docs/readings/04-openai-agents-sdk-analysis/openai-agents-sdk-analysis-zh.md` | OpenAI Agents SDK 架构分析（中文） |
| `docs/readings/04-openai-agents-sdk-analysis/openai-agents-sdk-analysis-en.md` | OpenAI Agents SDK 架构分析（英文） |

### 更新的文档

#### `LEARNING_PLAN.md`
- **Week 1 Day 5-7**: Semantic Kernel/AutoGen → MS Agent Framework/OpenAI Agents SDK
- **Week 5**: SK Plugins → OpenAI Agents SDK `@function_tool` + MS Agent Framework `ai_function`
- **Week 7**: AutoGen 源码 → MS Agent Framework HandoffBuilder + OpenAI Agents SDK Handoff
- **资源列表**: 更新必读源码（新增 LangGraph、MS Agent Framework、OpenAI Agents SDK）

#### `docs/readings/05-framework-comparison/`
- **三框架对比**: LangChain/LangGraph, MS Agent Framework, OpenAI Agents SDK
- **新增双编排模式**: 
  - `IWorkflow` - Workflow 编排（LLM 动态决策交接）
  - `IStateGraph` - 状态机编排（开发者预定义流程）
- **更新设计原则**: 从"四个核心原语 + 工作流"改为"四个核心原语 + 双编排模式"
- **新增接口**: `IStateGraph<TState>`, `StateGraphBuilder<TState>`

#### `docs/readings/06-week2-setup-guide/`
- **Python 包更新**:
  - 移除: `autogen-agentchat`
  - 新增: `openai-agents`, `langgraph`, `agent-framework`
- **.NET 包更新**:
  - 移除: `Microsoft.SemanticKernel`
  - 新增: `Microsoft.Agents.AI --prerelease`

### 安装的 VS Code 扩展
- `shd101wyy.markdown-preview-enhanced` - 增强的 Markdown 预览（支持 Mermaid）

---

## [2026-01-XX] Phase 0: 初始框架分析（历史记录）

### 创建的文档
- `docs/readings/00-agent-core-concepts/` - Agent 核心概念
- `docs/readings/01-building-effective-agents/` - 构建有效 Agent
- `docs/readings/02-langchain-analysis/` - LangChain 分析
- `docs/readings/02-openai-function-calling/` - OpenAI Function Calling
- `docs/readings/03-react-paper/` - ReAct 论文分析
- `docs/readings/04-chain-of-thought/` - 思维链分析
- `docs/readings/05-framework-comparison/` - 框架对比（初版，比较 LangChain/SK/AutoGen）
- `docs/readings/06-week2-setup-guide/` 至 `16-week12-deployment/` - 12周学习计划

---

## dawning-agents 设计决策摘要

### 核心原语（来自 OpenAI Agents SDK）
```csharp
public interface IAgent { }      // Agent - LLM + 指令 + 工具
public interface ITool { }       // Tool - 可调用的功能
public interface IHandoff { }    // Handoff - Agent 间委托
public interface IGuardrail { }  // Guardrail - 输入/输出验证
```

### 双编排模式
```csharp
// Workflow 编排 - LLM 动态决策（来自 MS Agent Framework）
public interface IWorkflow<TContext> { }
public class HandoffBuilder<TContext> { }

// 状态机编排 - 开发者预定义流程（来自 LangGraph）
public interface IStateGraph<TState> { }
public class StateGraphBuilder<TState> { }
```

### 场景选择指南
| 场景 | 推荐模式 | 原因 |
|------|----------|------|
| 多 Agent 协作、客服分流 | Workflow (HandoffBuilder) | LLM 智能决策交接目标 |
| 审批流、数据管道、多轮迭代 | StateGraph | 需要确定性的流程控制 |
| 简单对话 | 直接用 Agent | 无需编排 |

### 关键设计来源
| 特性 | 来源 |
|------|------|
| 四个核心原语 | OpenAI Agents SDK |
| Guardrails | OpenAI Agents SDK |
| Tracing | OpenAI Agents SDK |
| HandoffBuilder | MS Agent Framework |
| 两层架构 | MS Agent Framework |
| StateGraph | LangGraph |
| `[Tool]` 属性 | .NET 最佳实践 |

---

## 当前文档结构

```text
docs/readings/
├── 00-agent-core-concepts/           # Agent 核心概念
├── 01-building-effective-agents/     # 构建有效 Agent
├── 02-langchain-analysis/            # LangChain 分析
├── 02-openai-function-calling/       # OpenAI Function Calling
├── 03-ms-agent-framework-analysis/   # MS Agent Framework 分析 ✨ 新
├── 03-react-paper/                   # ReAct 论文
├── 04-chain-of-thought/              # 思维链
├── 04-openai-agents-sdk-analysis/    # OpenAI Agents SDK 分析 ✨ 新
├── 05-framework-comparison/          # 框架对比 ✅ 已更新
├── 06-week2-setup-guide/             # Week 2 环境搭建 ✅ 已更新
├── 07-week3-agent-loop/              # Week 3 Agent 循环
├── 08-week4-memory/                  # Week 4 记忆系统
├── 09-week5-tools/                   # Week 5 工具系统
├── 10-week6-rag/                     # Week 6 RAG
├── 11-week7-multi-agent/             # Week 7 多 Agent
├── 12-week8-communication/           # Week 8 通信
├── 13-week9-safety/                  # Week 9 安全
├── 14-week10-human-loop/             # Week 10 人机协作
├── 15-week11-observability/          # Week 11 可观测性
└── 16-week12-deployment/             # Week 12 部署
```

---

## 下一步计划

### Phase 1: 核心原语实现（Week 1-2）
- [ ] 创建解决方案结构
- [ ] 实现 `IAgent` 和 `Agent`
- [ ] 实现 `ITool` 和 `FunctionTool`
- [ ] 实现 `[Tool]` 属性发现
- [ ] OpenAI 集成
- [ ] 基础 `Runner`

### Phase 2: Handoff 与 Guardrails（Week 3-4）
- [ ] 实现 `IHandoff`
- [ ] 实现 `HandoffBuilder`
- [ ] 实现 `IGuardrail`
- [ ] 输入/输出护栏

### Phase 3: 双编排模式（Week 5-6）
- [ ] 实现 `HandoffWorkflow`
- [ ] 实现 `StateGraph` 和 `StateGraphBuilder`
- [ ] 条件边和循环
- [ ] 人机协作

### Phase 4: 可观测性（Week 7-8）
- [ ] Tracing 系统
- [ ] OpenTelemetry 集成

### Phase 5: 完善（Week 9-10）
- [ ] 更多 LLM 提供商
- [ ] Session 管理
- [ ] 文档和示例
