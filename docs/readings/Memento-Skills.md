# Memento-Skills: Let Agents Design Agents

> **论文链接 / Paper**: <https://arxiv.org/abs/2603.18743>
> **代码仓库 / Code**: <https://github.com/Memento-Teams/Memento-Skills>
> **项目主页 / Homepage**: <https://skills.memento.run/>
> **许可证 / License**: MIT

## 论文信息 / Paper Info

| 字段 / Field | 内容 / Content |
|---|---|
| **标题 / Title** | Memento-Skills: Let Agents Design Agents |
| **作者 / Authors** | Huichi Zhou, Siyuan Guo, Anjie Liu, Zhongwei Yu, Ziqin Gong, Bowen Zhao, Zhixun Chen, Menglong Zhang, Yihang Chen, Jinsong Li, Runyu Yang, Qiangbin Liu, Xinlei Yu, Jianmin Zhou, Na Wang, Chunyang Sun, Jun Wang |
| **提交日期 / Submitted** | 2026-03-19 |
| **领域 / Subjects** | cs.AI, cs.CL, cs.LG |
| **类型 / Type** | Technical Report |

---

## English Summary

### Abstract

Memento-Skills is a **generalist, continually-learnable LLM agent system** that functions as an "agent-designing agent" — it autonomously constructs, adapts, and improves task-specific agents through experience. Built on a **memory-based reinforcement learning framework** with "stateful prompts", reusable skills (stored as structured markdown files) serve as persistent, evolving memory.

Starting from simple elementary skills (web search, terminal operations), the agent improves via the **Read-Write Reflective Learning** mechanism from Memento 2. In the **read phase**, a behaviour-trainable skill router selects the most relevant skill; in the **write phase**, the agent updates and expands its skill library. This enables **continual learning without updating LLM parameters**.

### Three Paradigms of LLM Adaptation

The paper identifies three paradigms of LLM adaptation:

1. **Pre-training** — updates model parameters θ, requires massive data and compute
2. **Fine-tuning** — updates model parameters θ, requires moderate data and compute
3. **Deployment-time learning (this work)** — keeps θ frozen, accumulates experience in an external skill memory M, enabling continual adaptation from live interactions at zero retraining cost

### Core Loop: Read → Execute → Reflect → Write

| Phase | Description |
|-------|-------------|
| **Read** | Retrieve candidate skills from the local library and remote catalogue instead of stuffing every skill into context |
| **Execute** | Run skills through tool calling and a local sandbox so the agent can act on files, scripts, webpages, and external systems |
| **Reflect** | When execution fails or quality drops, record state, update utility, and attribute the issue to concrete skills |
| **Write** | Optimise weak skills, rewrite broken ones, and create new skills when no existing capability is good enough |

The key difference from systems that simply accumulate more skills: Memento-Skills cares about whether a large skill library can still be **retrieved correctly**, **repaired correctly**, and **improved continuously**.

### Key Features

- **Fully self-developed agent framework** — not a thin wrapper; ships its own orchestration, skill routing, execution, reflection, storage, CLI, and GUI stack
- **Open-source LLM friendly** — especially friendly to Kimi/Moonshot, MiniMax, GLM/Zhipu, and other OpenAI-compatible endpoints
- **Skill self-evolution loop** — learns from failure, revises weak skills, grows library over time
- **Local-first deployment** — CLI, desktop GUI, Feishu bridge, local sandbox execution, persistent state

### Built-in Skills (9)

| Skill | Capability |
|-------|-----------|
| filesystem | File read, write, search, directory operations |
| web-search | Tavily-based web search and page fetching |
| image-analysis | Image understanding, OCR, caption-like tasks |
| pdf | PDF reading, form filling, merging, splitting, OCR |
| docx | Word document creation and editing |
| xlsx | Spreadsheet processing |
| pptx | PowerPoint creation and editing |
| skill-creator | New skill creation, optimisation, and evaluation |
| uv-pip-install | Python dependency installation via uv |

### Deployment Surfaces

| Surface | Command | Description |
|---------|---------|-------------|
| CLI | `memento agent` | Interactive or single-message mode |
| Desktop GUI | `memento-gui` | Session list, chat UI, slash commands |
| Feishu bridge | `memento feishu` | WebSocket-based IM bridge with per-user persistent sessions |
| Skill verification | `memento verify` | Download, static review, and execution validation |
| Local sandbox | `uv` | Isolated skill execution, dependency install |

### Experimental Results

Evaluated on two challenging benchmarks:

- **GAIA** (General AI Assistants) — real-world tasks requiring multi-step reasoning, web browsing, file handling, tool use
- **HLE** (Humanity's Last Exam) — extremely difficult questions spanning diverse academic disciplines

| Benchmark | Relative Improvement |
|-----------|---------------------|
| GAIA | **+26.2%** overall accuracy |
| HLE | **+116.2%** overall accuracy |

Performance improves over multiple learning rounds while the skill library grows from a small set of atomic skills into a richer set of learned skills. The goal is not merely to add more tools — it is to **learn better skills through task experience**.

### vs OpenClaw

- **OpenClaw** is about getting the assistant running in the real world
- **Memento-Skills** is about getting the agent learning from the real world

Key differences: Memento-Skills emphasises failure-triggered reflection loops, skill routing at scale, cloud skill catalogue with deduplication, and ability to create/recreate skills autonomously.

---

## 中文摘要

### 概述

Memento-Skills 是一个**通用型、持续学习的 LLM Agent 系统**，充当"设计 Agent 的 Agent"——它能自主构建、适应和改进特定任务的 Agent。该系统建立在基于**记忆的强化学习框架**之上，使用"有状态提示词"(stateful prompts)，将可复用的技能（以结构化 Markdown 文件存储）作为持久化、可演化的记忆。

从简单的基础技能（网页搜索、终端操作）出发，Agent 通过来自 Memento 2 的**读写反思学习机制** (Read-Write Reflective Learning) 不断进化。在**读取阶段**，行为可训练的技能路由器选择最相关的技能；在**写入阶段**，Agent 更新并扩展其技能库。这使得**无需更新 LLM 参数即可实现持续学习**。

### LLM 适应的三种范式

1. **预训练** — 更新模型参数 θ，需要海量数据和算力
2. **微调** — 更新模型参数 θ，需要中等规模数据和算力
3. **部署时学习（本工作）** — 保持 θ 冻结，在外部技能记忆 M 中积累经验，以零重训练成本实现从实时交互中持续适应

### 核心循环：读取 → 执行 → 反思 → 写入

| 阶段 | 描述 |
|------|------|
| **读取 (Read)** | 从本地库和远程目录中检索候选技能，而非将所有技能塞入上下文 |
| **执行 (Execute)** | 通过工具调用和本地沙箱运行技能，使 Agent 能操作文件、脚本、网页和外部系统 |
| **反思 (Reflect)** | 当执行失败或质量下降时，记录状态、更新效用分数，并将问题归因到具体技能 |
| **写入 (Write)** | 优化弱技能、重写损坏的技能，并在现有能力不足时创建新技能 |

与简单积累更多技能的系统的关键区别：Memento-Skills 关注的是一个大型技能库能否被**正确检索**、**正确修复**和**持续改进**。

### 核心特性

- **完全自主研发的 Agent 框架** — 不是对他人运行时的薄包装；自带编排、技能路由、执行、反思、存储、CLI 和 GUI 全栈
- **对开源 LLM 友好** — 特别适配 Kimi/Moonshot、MiniMax、GLM/智谱 等主流开源模型平台，以及其他 OpenAI 兼容端点
- **技能自演化循环** — 从失败中学习，修正弱技能，随时间增长技能库
- **本地优先部署** — CLI、桌面 GUI、飞书桥接、本地沙箱执行、持久化状态

### 内置技能（9 项）

| 技能 | 能力 |
|------|------|
| filesystem | 文件读写、搜索、目录操作 |
| web-search | 基于 Tavily 的网页搜索和页面抓取 |
| image-analysis | 图像理解、OCR、图像描述 |
| pdf | PDF 读取、表单填充、合并、拆分、OCR |
| docx | Word 文档创建和编辑 |
| xlsx | 电子表格处理 |
| pptx | PowerPoint 创建和编辑 |
| skill-creator | 新技能创建、优化和评估 |
| uv-pip-install | 通过 uv 安装 Python 依赖 |

### 实验结果

在两个高难度基准上进行了评估：

- **GAIA**（通用 AI 助手）— 需要多步推理、网页浏览、文件处理、工具使用的真实世界任务
- **HLE**（人类最终考试）— 涵盖多个学科领域的极高难度问题

| 基准 | 相对提升 |
|------|---------|
| GAIA | 总体准确率 **+26.2%** |
| HLE | 总体准确率 **+116.2%** |

性能随多轮学习持续提升，技能库从少量原子技能成长为更丰富的学习技能集。重点不仅仅是添加更多工具——而是**通过任务经验学习更好的技能**。

### 与 Dawning.Agents 的关联思考

Memento-Skills 的以下设计理念值得 Dawning.Agents 参考：

1. **技能即一等公民** — 将工具/技能视为可检索、可执行、可持久化、可演化的单元，而非扁平的函数堆
2. **读写反思循环** — 失败驱动的自我改进机制，Agent 能识别失败技能并修复/重写
3. **部署时学习** — 无需更新模型参数的持续学习能力
4. **技能路由** — 随着技能库增长，高效的技能检索和路由变得至关重要
5. **本地优先** — 多种部署表面（CLI、GUI、IM）使系统实用化

---

## Citation

```bibtex
@article{zhou2026mementoskills,
  title={Memento-Skills: Let Agents Design Agents},
  author={Zhou, Huichi and Guo, Siyuan and Liu, Anjie and Yu, Zhongwei and Gong, Ziqin and
          Zhao, Bowen and Chen, Zhixun and Zhang, Menglong and Chen, Yihang and Li,
          Jinsong and Yang, Runyu and Liu, Qiangbin and Yu, Xinlei and Zhou, Jianmin and Wang,
          Na and Sun, Chunyang and Wang, Jun},
  journal={arXiv preprint arXiv:2603.18743},
  year={2026},
  url={https://arxiv.org/abs/2603.18743}
}
```
