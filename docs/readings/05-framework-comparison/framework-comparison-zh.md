# Agent 框架对比：LangChain vs Semantic Kernel vs AutoGen

> 三大主流 AI Agent 框架的全面对比
> Day 5-7 学习资料：开源项目概览

---

## 概述

本文档对比三个流行的 AI Agent 构建框架：

| 框架 | 组织 | 主要语言 | 专注领域 |
|------|------|----------|----------|
| **LangChain** | LangChain AI | Python, JS/TS | Agent 应用 & LLM 集成 |
| **Semantic Kernel** | 微软 | C#, Python, Java | 企业级 AI 编排 |
| **AutoGen** | 微软 | Python | 多 Agent 对话 |

---

## 1. LangChain

### 简介

LangChain 是一个开源框架，具有预构建的 Agent 架构和任何模型或工具的集成——让你可以构建随生态系统发展而快速适应的 Agent。

LangChain 是开始构建由 LLM 驱动的 Agent 和应用的最简单方式。使用不到 10 行代码，你就可以连接到 OpenAI、Anthropic、Google 等服务。

### 架构

```
┌─────────────────────────────────────────────────────────────────┐
│                        LangChain 生态系统                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│   ┌─────────────┐   ┌─────────────┐   ┌─────────────────────┐   │
│   │  LangChain  │   │  LangGraph  │   │     LangSmith       │   │
│   │   (Agent)   │   │   (工作流)  │   │     (可观察性)       │   │
│   └─────────────┘   └─────────────┘   └─────────────────────┘   │
│         │                 │                     │               │
│         └────────────┬────┘                     │               │
│                      ▼                          ▼               │
│              ┌──────────────────────────────────────┐           │
│              │         集成 (100+)                  │           │
│              │  (模型, 工具, 检索器等)              │           │
│              └──────────────────────────────────────┘           │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 核心优势

| 优势 | 描述 |
|------|------|
| **标准模型接口** | 不同提供商有独特的 API。LangChain 标准化了你与模型的交互方式，让你可以无缝切换提供商，避免锁定。 |
| **易用且高度灵活** | 用不到 10 行代码构建一个简单的 Agent。但也足够灵活，可以进行复杂的上下文工程。 |
| **基于 LangGraph 构建** | Agent 基于 LangGraph 构建，提供持久执行、人机协作支持、持久化等功能。 |
| **使用 LangSmith 调试** | 通过可视化工具深入了解复杂的 Agent 行为，追踪执行路径。 |

### 快速开始示例

```python
# pip install -qU langchain "langchain[anthropic]"
from langchain.agents import create_agent

def get_weather(city: str) -> str:
    """获取城市的天气。"""
    return f"在 {city} 总是阳光明媚！"

agent = create_agent(
    model="claude-sonnet-4-5-20250929",
    tools=[get_weather],
    system_prompt="你是一个有帮助的助手",
)

# 运行 Agent
agent.invoke(
    {"messages": [{"role": "user", "content": "旧金山的天气怎么样"}]}
)
```

### 核心组件

| 组件 | 用途 |
|------|------|
| **模型 (Models)** | 聊天模型、LLM、嵌入 |
| **提示 (Prompts)** | 提示模板、少样本示例 |
| **链 (Chains)** | 可组合的调用序列 |
| **Agent** | 具有工具的自主决策者 |
| **工具 (Tools)** | Agent 可以调用的函数 |
| **记忆 (Memory)** | 对话历史管理 |
| **检索器 (Retrievers)** | RAG 的文档检索 |

### LangGraph 用于高级工作流

当你有更高级的需求时：
- 确定性和 Agent 工作流的组合
- 重度定制
- 精确控制的延迟

LangGraph 提供低级 Agent 编排：
- 状态机
- 循环和分支
- 持久化
- 人机协作

### 优缺点

| 优点 | 缺点 |
|------|------|
| ✅ 最大的生态系统和集成 | ❌ API 变化快（破坏性变更） |
| ✅ 优秀的文档 | ❌ 对简单用例可能过度抽象 |
| ✅ 活跃的社区 | ❌ 以 Python 为中心（JS/TS 正在追赶） |
| ✅ 快速原型开发 | ❌ 生产环境性能开销 |

---

## 2. Semantic Kernel

### 简介

Semantic Kernel 是一个轻量级、开源的开发工具包，让你可以轻松构建 AI Agent 并将最新的 AI 模型集成到你的 C#、Python 或 Java 代码库中。它作为一个高效的中间件，使企业级解决方案的快速交付成为可能。

### 架构

```
┌─────────────────────────────────────────────────────────────────┐
│                      Semantic Kernel                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │                       内核 (Kernel)                       │  │
│   │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐ │  │
│   │  │  插件    │  │  规划器  │  │   记忆   │  │  连接器  │ │  │
│   │  │ Plugins  │  │ Planners │  │  Memory  │  │Connectors│ │  │
│   │  └──────────┘  └──────────┘  └──────────┘  └──────────┘ │  │
│   └──────────────────────────────────────────────────────────┘  │
│                              │                                   │
│                              ▼                                   │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │                      AI 服务                              │  │
│   │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐ │  │
│   │  │  OpenAI  │  │  Azure   │  │  Google  │  │   本地   │ │  │
│   │  └──────────┘  └──────────┘  └──────────┘  └──────────┘ │  │
│   └──────────────────────────────────────────────────────────┘  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 企业级特性

| 特性 | 描述 |
|------|------|
| **安全性** | 遥测支持、钩子和过滤器用于负责任的 AI |
| **多语言** | C#、Python 和 Java 的 1.0+ 版本支持 |
| **稳定 API** | 承诺无破坏性变更 |
| **面向未来** | 轻松切换模型而无需重写代码库 |
| **可扩展** | OpenAPI 规范（如 Microsoft 365 Copilot） |

### 核心概念

#### 插件 (Plugins)
你现有的代码可以暴露给 AI 模型：

```csharp
public class WeatherPlugin
{
    [KernelFunction, Description("获取城市的天气")]
    public string GetWeather(string city)
    {
        return $"在 {city} 阳光明媚！";
    }
}
```

#### 内核 (Kernel)
中央编排器：

```csharp
var builder = Kernel.CreateBuilder();
builder.AddAzureOpenAIChatCompletion(
    deploymentName: "gpt-4",
    endpoint: "https://your-resource.openai.azure.com/",
    apiKey: "your-api-key"
);
builder.Plugins.AddFromType<WeatherPlugin>();

var kernel = builder.Build();
```

#### 自动函数调用
```csharp
var settings = new OpenAIPromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

var result = await kernel.InvokePromptAsync(
    "西雅图的天气怎么样？",
    new KernelArguments(settings)
);
```

### 自动化业务流程

Semantic Kernel 将提示与现有 API 结合以执行操作：

1. 当请求发出时，模型调用一个函数
2. Semantic Kernel 将模型的请求转换为函数调用
3. 结果传回模型

### 优缺点

| 优点 | 缺点 |
|------|------|
| ✅ 一流的 C# 支持 | ❌ 社区比 LangChain 小 |
| ✅ 企业级特性 | ❌ 集成较少 |
| ✅ 微软支持 & Azure 集成 | ❌ 非 .NET 开发者的学习曲线 |
| ✅ 带版本控制的稳定 API | ❌ 文档/示例较少 |

---

## 3. AutoGen

### 简介

AutoGen 是一个开源编程框架，用于构建 AI Agent 并促进多个 Agent 之间的合作以解决任务。AutoGen 旨在提供一个易于使用且灵活的框架，加速 Agent AI 的开发和研究，就像深度学习的 PyTorch 一样。

### 架构

```
┌─────────────────────────────────────────────────────────────────┐
│                      AutoGen 框架                                │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │                  多 Agent 对话                            │  │
│   │                                                           │  │
│   │   ┌─────────┐    ┌─────────┐    ┌─────────────────────┐  │  │
│   │   │ 助手    │◄──►│  用户   │◄──►│    自定义 Agent     │  │  │
│   │   │ Agent   │    │  代理   │    │  (群聊等)           │  │  │
│   │   └─────────┘    └─────────┘    └─────────────────────┘  │  │
│   │         │              │               │                  │  │
│   │         └──────────────┼───────────────┘                  │  │
│   │                        ▼                                  │  │
│   │   ┌────────────────────────────────────────────────────┐ │  │
│   │   │                  对话模式                           │ │  │
│   │   │  • 双 Agent 聊天    • 群聊                         │ │  │
│   │   │  • 顺序执行         • 层级结构                     │ │  │
│   │   └────────────────────────────────────────────────────┘ │  │
│   └──────────────────────────────────────────────────────────┘  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 主要特性

| 特性 | 描述 |
|------|------|
| **多 Agent 对话** | 以最少的努力构建下一代 LLM 应用 |
| **多样的对话模式** | 可定制的复杂工作流 |
| **对话自主性** | 配置 Agent 数量和拓扑结构 |
| **可用系统** | 来自各领域的示例集合 |

### 快速开始

```python
import os
from autogen import AssistantAgent, UserProxyAgent

llm_config = {
    "config_list": [
        {"model": "gpt-4", "api_key": os.environ.get("OPENAI_API_KEY")}
    ]
}

assistant = AssistantAgent("assistant", llm_config=llm_config)
user_proxy = UserProxyAgent("user_proxy", code_execution_config=False)

# 开始聊天
user_proxy.initiate_chat(
    assistant,
    message="给我讲一个关于 NVDA 和 TESLA 股价的笑话。",
)
```

### 多 Agent 对话框架

AutoGen 提供可定制的可对话 Agent，集成了：
- LLM
- 工具
- 人类

通过自动化多个有能力的 Agent 之间的聊天，可以轻松让它们集体自主执行任务或在人类反馈下执行任务。

### 对话模式

```
双 Agent 聊天:              群聊:
┌─────────┐               ┌─────────┐
│  Agent  │               │  Agent  │
│    A    │◄─────────────►│    A    │
└─────────┘               └────┬────┘
     ▲                         │
     │                    ┌────▼────┐
     ▼                    │  管理者  │
┌─────────┐               └────┬────┘
│  Agent  │               ┌────▼────┐
│    B    │◄─────────────►│  Agent  │
└─────────┘               │    B    │
                          └─────────┘

顺序执行:                  层级结构:
A → B → C → D              ┌─────────┐
                           │  管理者  │
                           └────┬────┘
                     ┌─────────┼─────────┐
                     ▼         ▼         ▼
                ┌───────┐ ┌───────┐ ┌───────┐
                │ Agent │ │ Agent │ │ Agent │
                │   A   │ │   B   │ │   C   │
                └───────┘ └───────┘ └───────┘
```

### Agent 类型

| Agent 类型 | 描述 |
|------------|------|
| **AssistantAgent** | 由 LLM 驱动的 AI 助手 |
| **UserProxyAgent** | 代表人类用户，可以执行代码 |
| **GroupChatManager** | 编排多 Agent 群组对话 |
| **自定义 Agent** | 创建你自己的专用 Agent |

### 优缺点

| 优点 | 缺点 |
|------|------|
| ✅ 为多 Agent 场景而构建 | ❌ 仅支持 Python |
| ✅ 简单的对话抽象 | ❌ 不如 LangChain 成熟 |
| ✅ 代码执行支持 | ❌ 生态系统较小 |
| ✅ 学术研究支持 | ❌ 版本变化快 |

---

## 4. 对比总结

### 功能矩阵

| 功能 | LangChain | Semantic Kernel | AutoGen |
|------|-----------|-----------------|---------|
| **主要语言** | Python, JS | C#, Python, Java | Python |
| **Agent 焦点** | 单 & 多 | 单 & 插件 | 多 Agent |
| **工具集成** | 优秀 | 良好 | 良好 |
| **记忆/状态** | 内置 | 内置 | 基于对话 |
| **RAG 支持** | 优秀 | 良好 | 基础 |
| **企业就绪** | 中等 | 优秀 | 中等 |
| **学习曲线** | 中等 | 中高 | 中低 |
| **社区规模** | 最大 | 增长中 | 增长中 |

### 使用场景推荐

| 使用场景 | 推荐框架 |
|----------|----------|
| 快速原型开发 | LangChain |
| 企业级 C# 应用 | Semantic Kernel |
| 多 Agent 协作 | AutoGen |
| RAG 应用 | LangChain |
| Azure 生态集成 | Semantic Kernel |
| 研究和实验 | AutoGen |
| 生产级 Python 应用 | LangChain + LangGraph |

### 架构对比

```
┌──────────────────────────────────────────────────────────────────────┐
│                       框架架构对比                                    │
├──────────────────────────────────────────────────────────────────────┤
│                                                                       │
│  LangChain:                                                           │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐                        │
│  │   模型   │───►│    链    │───►│  Agent   │                        │
│  └──────────┘    └──────────┘    └──────────┘                        │
│       │               │               │                               │
│       └───────────────┴───────────────┘                               │
│                       │                                               │
│                  [工具/记忆/检索器]                                   │
│                                                                       │
│  Semantic Kernel:                                                     │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐                        │
│  │   内核   │───►│   插件   │───►│  规划器  │                        │
│  └──────────┘    └──────────┘    └──────────┘                        │
│       │               │               │                               │
│       └───────────────┴───────────────┘                               │
│                       │                                               │
│                  [AI 服务/连接器]                                     │
│                                                                       │
│  AutoGen:                                                             │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐                        │
│  │ Agent A  │◄──►│ Agent B  │◄──►│ Agent C  │                        │
│  └──────────┘    └──────────┘    └──────────┘                        │
│       │               │               │                               │
│       └───────────────┴───────────────┘                               │
│                       │                                               │
│                  [对话管理器]                                         │
│                                                                       │
└──────────────────────────────────────────────────────────────────────┘
```

---

## 5. dawning-agents 的选择

对于 `dawning-agents` 项目（以 C#/.NET 为重点），考虑：

### 推荐方法

1. **主要灵感来源**：Semantic Kernel
   - 原生 C# 支持
   - 企业级特性
   - 插件架构

2. **多 Agent 模式**：AutoGen 概念
   - 对话模式
   - Agent 协作方式

3. **集成思路**：LangChain
   - 工具/函数设计模式
   - 记忆抽象

### 实现策略

```csharp
// dawning-agents 架构灵感来自三个框架
namespace DawningAgents.Core
{
    // Semantic Kernel 风格 - Kernel 作为编排器
    public interface IAgentKernel
    {
        Task<AgentResponse> ExecuteAsync(AgentRequest request);
    }
    
    // LangChain 风格 - 工具作为一等公民
    public interface ITool
    {
        string Name { get; }
        string Description { get; }
        Task<ToolResult> ExecuteAsync(ToolInput input);
    }
    
    // AutoGen 风格 - 多 Agent 对话
    public interface IConversableAgent
    {
        Task SendAsync(IConversableAgent recipient, Message message);
        Task<Message> ReceiveAsync(IConversableAgent sender, Message message);
    }
}
```

---

## 6. 资源

### 官方文档

| 框架 | 链接 |
|------|------|
| **LangChain** | [文档](https://docs.langchain.com) • [GitHub](https://github.com/langchain-ai/langchain) • [LangGraph](https://docs.langchain.com/oss/python/langgraph/overview) |
| **Semantic Kernel** | [文档](https://learn.microsoft.com/semantic-kernel) • [GitHub](https://github.com/microsoft/semantic-kernel) |
| **AutoGen** | [文档](https://microsoft.github.io/autogen) • [GitHub](https://github.com/microsoft/autogen) |

### 学习资源

- LangChain 学院：https://academy.langchain.com/
- Semantic Kernel 示例：https://github.com/microsoft/semantic-kernel/tree/main/samples
- AutoGen Notebooks：https://microsoft.github.io/autogen/docs/notebooks

---

## 总结

| 框架 | 最适合 | 核心优势 |
|------|--------|----------|
| **LangChain** | 快速原型开发 & Python 应用 | 生态系统 & 集成 |
| **Semantic Kernel** | 企业级 C#/.NET 应用 | 稳定性 & Azure 集成 |
| **AutoGen** | 多 Agent 研究 & 实验 | 对话模式 |

三个框架都在积极开发中，根据你的具体需求都可以是优秀的选择。对于 `dawning-agents`，结合三者的概念并在 C# 中原生构建可以提供最佳的综合体验。
