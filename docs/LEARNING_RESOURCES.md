# 📚 Dawning.Agents 学习资源索引

> 本文档整合所有学习资料，便于快速查阅

**相关文档**:
- [完整学习计划](LEARNING_PLAN_FULL.md) - 12 周详细任务清单
- [企业转型路线图](ENTERPRISE_ROADMAP.md) - 下一步规划

---

## 🎓 学习路线

### Phase 1: 基础理论 (Week 1-2)

| 主题 | 资料 | 时长 |
|------|------|------|
| Agent 核心概念 | [00-agent-core-concepts](readings/00-agent-core-concepts/) | 2h |
| 构建有效 Agent | [01-building-effective-agents](readings/01-building-effective-agents/) | 3h |
| OpenAI Function Calling | [02-openai-function-calling](readings/02-openai-function-calling/) | 2h |
| ReAct 论文 | [03-react-paper](readings/03-react-paper/) | 4h |
| Chain of Thought | [04-chain-of-thought](readings/04-chain-of-thought/) | 2h |

### Phase 2: 框架学习 (Week 2)

| 主题 | 资料 | 重点 |
|------|------|------|
| LangChain 分析 | [02-langchain-analysis](readings/02-langchain-analysis/) | Agent 模式 |
| MS Agent Framework | [03-ms-agent-framework-analysis](readings/03-ms-agent-framework-analysis/) | Handoff 设计 |
| OpenAI Agents SDK | [04-openai-agents-sdk-analysis](readings/04-openai-agents-sdk-analysis/) | 四个原语 |
| 框架对比 | [05-framework-comparison](readings/05-framework-comparison/) | 选型参考 |

### Phase 3: 实践开发 (Week 3-12)

| 周 | 主题 | 资料 |
|---|------|------|
| 2 | 环境搭建 | [06-week2-setup-guide](readings/06-week2-setup-guide/) |
| 3 | Agent 循环 | [07-week3-agent-loop](readings/07-week3-agent-loop/) |
| 4 | Memory 系统 | [08-week4-memory](readings/08-week4-memory/) |
| 5 | Tools 系统 | [09-week5-tools](readings/09-week5-tools/) |
| 6 | RAG 集成 | [10-week6-rag](readings/10-week6-rag/) |
| 7 | 多 Agent | [11-week7-multi-agent](readings/11-week7-multi-agent/) |
| 8 | 通信机制 | [12-week8-communication](readings/12-week8-communication/) |
| 9 | 安全护栏 | [13-week9-safety](readings/13-week9-safety/) |
| 10 | 人机协作 | [14-week10-human-loop](readings/14-week10-human-loop/) |
| 11 | 可观测性 | [15-week11-observability](readings/15-week11-observability/) |
| 12 | 部署扩展 | [16-week12-deployment](readings/16-week12-deployment/) |

---

## 📖 必读论文

### 核心论文

| 论文 | 链接 | 要点 |
|------|------|------|
| **ReAct** | [arXiv:2210.03629](https://arxiv.org/abs/2210.03629) | Reasoning + Acting 范式 |
| **Chain-of-Thought** | [arXiv:2201.11903](https://arxiv.org/abs/2201.11903) | 思维链推理 |
| **Tool Learning** | [arXiv:2304.08354](https://arxiv.org/abs/2304.08354) | LLM 工具使用 |
| **Multi-Agent** | [arXiv:2308.08155](https://arxiv.org/abs/2308.08155) | 多 Agent 协作 |

### 推荐阅读

| 论文 | 链接 | 相关模块 |
|------|------|----------|
| Self-Ask | arXiv:2210.03350 | Agent 循环 |
| Reflexion | arXiv:2303.11366 | 自我反思 |
| CAMEL | arXiv:2303.17760 | 角色扮演 |
| AutoGPT | GitHub | 自主 Agent |

---

## 🔗 外部资源

### 官方文档

| 项目 | 链接 | 参考价值 |
|------|------|----------|
| OpenAI Agents SDK | [GitHub](https://github.com/openai/openai-agents-python) | 核心设计 |
| MS Agent Framework | [GitHub](https://github.com/microsoft/agent-framework) | Handoff |
| LangChain | [Docs](https://docs.langchain.com) | Agent/Tools |
| LangGraph | [Docs](https://langchain-ai.github.io/langgraph/) | 状态机 |
| CrewAI | [GitHub](https://github.com/joaomdmoura/crewAI) | 任务分解 |
| MetaGPT | [GitHub](https://github.com/geekan/MetaGPT) | 角色设计 |

### 技术博客

| 来源 | 链接 | 主题 |
|------|------|------|
| Anthropic | [Building Effective Agents](https://www.anthropic.com/research/building-effective-agents) | Agent 设计 |
| OpenAI | [Function Calling](https://platform.openai.com/docs/guides/function-calling) | 工具调用 |
| LangChain | [Agent Docs](https://python.langchain.com/docs/modules/agents/) | 实现参考 |

### 视频教程

| 平台 | 内容 | 链接 |
|------|------|------|
| YouTube | LangChain 系列 | LangChain 官方频道 |
| YouTube | OpenAI Agents | OpenAI 官方频道 |
| Coursera | LLM 应用开发 | DeepLearning.AI |

---

## 📁 源码阅读指南

### OpenAI Agents SDK (Python)

```
openai-agents-python/
├── src/agents/
│   ├── agent.py          # Agent 核心定义
│   ├── tool.py           # @function_tool 装饰器
│   ├── handoffs.py       # Handoff 实现
│   └── guardrails.py     # Guardrails 系统
```

**重点关注**:
- `Agent` 类的 `run()` 方法
- `@function_tool` 装饰器实现
- `Handoff` 的过滤器机制

### MS Agent Framework

```
agent-framework/
├── dotnet/src/Microsoft.Agents.AI/
│   └── handoffs/         # Handoff 实现
├── python/packages/agent-framework/
│   └── handoffs/         # Python 版本
```

**重点关注**:
- `HandoffBuilder` 模式
- 状态机编排
- 工作流定义

### LangChain

```
langchain/
├── langchain/agents/
│   ├── agent.py          # Agent 基类
│   ├── mrkl/base.py      # MRKL Agent
│   └── react/agent.py    # ReAct Agent
├── langchain/tools/
│   └── base.py           # Tool 基类
└── langchain/memory/
    ├── buffer.py         # Buffer Memory
    └── summary.py        # Summary Memory
```

**重点关注**:
- Agent 执行循环
- Tool 定义方式
- Memory 抽象

---

## 🏷️ 按主题索引

### Agent 基础

- [什么是 Agent](readings/00-agent-core-concepts/)
- [ReAct 模式](readings/03-react-paper/)
- [思维链](readings/04-chain-of-thought/)
- [Agent 循环](readings/07-week3-agent-loop/)

### 工具系统

- [Function Calling](readings/02-openai-function-calling/)
- [工具设计](readings/09-week5-tools/)
- [工具安全](readings/09-week5-tools/)

### 记忆系统

- [Memory 架构](readings/08-week4-memory/)
- [Token 管理](readings/08-week4-memory/)

### RAG 系统

- [RAG 原理](readings/10-week6-rag/)
- [向量检索](readings/10-week6-rag/)
- [文档分块](readings/10-week6-rag/)

### 多 Agent

- [协作模式](readings/11-week7-multi-agent/)
- [通信机制](readings/12-week8-communication/)
- [Handoff](readings/11-week7-multi-agent/)

### 生产部署

- [安全护栏](readings/13-week9-safety/)
- [人机协作](readings/14-week10-human-loop/)
- [可观测性](readings/15-week11-observability/)
- [扩展部署](readings/16-week12-deployment/)

---

## 📊 学习进度追踪

### 理论学习

- [x] Agent 核心概念
- [x] ReAct 论文
- [x] Chain of Thought
- [x] 框架对比分析

### 实践开发

- [x] Week 1-2: 环境准备
- [x] Week 3: Agent 核心循环
- [x] Week 4: Memory 系统
- [x] Week 5: Tools 系统
- [x] Week 6: RAG 集成
- [x] Week 7-8: 多 Agent 协作
- [x] Week 9: 安全护栏
- [x] Week 10: 人机协作
- [x] Week 11: 可观测性
- [x] Week 12: 部署扩展

### 测试覆盖

- [x] 1,183 个单元测试
- [x] 72.9% 行覆盖率
- [x] Demo 示例项目

---

## 🔄 更新记录

| 日期 | 内容 |
|------|------|
| 2026-01-27 | 创建学习资源索引 |
| 2025-07-XX | 12 周学习计划完成 |
| 2025-01-XX | 开始学习计划 |

---

> 📌 **提示**: 建议按照学习路线顺序阅读，每个主题配合代码实践
