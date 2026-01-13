# Agent Framework Comparison: LangChain vs Semantic Kernel vs AutoGen

> A comprehensive comparison of three major AI agent frameworks
> Day 5-7 Learning Material: Open Source Project Overview

---

## Overview

This document compares three popular frameworks for building AI agents:

| Framework | Organization | Primary Language | Focus |
|-----------|--------------|------------------|-------|
| **LangChain** | LangChain AI | Python, JS/TS | Agent applications & LLM integration |
| **Semantic Kernel** | Microsoft | C#, Python, Java | Enterprise AI orchestration |
| **AutoGen** | Microsoft | Python | Multi-agent conversations |

---

## 1. LangChain

### Introduction

LangChain is an open-source framework with a pre-built agent architecture and integrations for any model or tool — so you can build agents that adapt as fast as the ecosystem evolves.

LangChain is the easiest way to start building agents and applications powered by LLMs. With under 10 lines of code, you can connect to OpenAI, Anthropic, Google, and more.

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        LangChain Ecosystem                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│   ┌─────────────┐   ┌─────────────┐   ┌─────────────────────┐   │
│   │  LangChain  │   │  LangGraph  │   │     LangSmith       │   │
│   │   (Agent)   │   │  (Workflow) │   │    (Observability)  │   │
│   └─────────────┘   └─────────────┘   └─────────────────────┘   │
│         │                 │                     │               │
│         └────────────┬────┘                     │               │
│                      ▼                          ▼               │
│              ┌──────────────────────────────────────┐           │
│              │        Integrations (100+)           │           │
│              │  (Models, Tools, Retrievers, etc.)   │           │
│              └──────────────────────────────────────┘           │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Core Benefits

| Benefit | Description |
|---------|-------------|
| **Standard Model Interface** | Different providers have unique APIs. LangChain standardizes how you interact with models so you can seamlessly swap providers and avoid lock-in. |
| **Easy to Use, Highly Flexible** | Build a simple agent in under 10 lines of code. But also flexible enough for complex context engineering. |
| **Built on LangGraph** | Agents are built on LangGraph, providing durable execution, human-in-the-loop support, persistence, and more. |
| **Debug with LangSmith** | Gain deep visibility into complex agent behavior with visualization tools that trace execution paths. |

### Quick Start Example

```python
# pip install -qU langchain "langchain[anthropic]"
from langchain.agents import create_agent

def get_weather(city: str) -> str:
    """Get weather for a given city."""
    return f"It's always sunny in {city}!"

agent = create_agent(
    model="claude-sonnet-4-5-20250929",
    tools=[get_weather],
    system_prompt="You are a helpful assistant",
)

# Run the agent
agent.invoke(
    {"messages": [{"role": "user", "content": "what is the weather in sf"}]}
)
```

### Key Components

| Component | Purpose |
|-----------|---------|
| **Models** | Chat models, LLMs, embeddings |
| **Prompts** | Prompt templates, few-shot examples |
| **Chains** | Composable sequences of calls |
| **Agents** | Autonomous decision-makers with tools |
| **Tools** | Functions the agent can call |
| **Memory** | Conversation history management |
| **Retrievers** | Document retrieval for RAG |

### LangGraph for Advanced Workflows

When you have more advanced needs that require:
- Combination of deterministic and agentic workflows
- Heavy customization
- Carefully controlled latency

LangGraph provides low-level agent orchestration with:
- State machines
- Cycles and branches
- Persistence
- Human-in-the-loop

### Pros and Cons

| Pros | Cons |
|------|------|
| ✅ Largest ecosystem & integrations | ❌ Fast-changing API (breaking changes) |
| ✅ Excellent documentation | ❌ Can be over-abstracted for simple use cases |
| ✅ Active community | ❌ Python-centric (JS/TS catching up) |
| ✅ Quick to prototype | ❌ Performance overhead for production |

---

## 2. Semantic Kernel

### Introduction

Semantic Kernel is a lightweight, open-source development kit that lets you easily build AI agents and integrate the latest AI models into your C#, Python, or Java codebase. It serves as an efficient middleware that enables rapid delivery of enterprise-grade solutions.

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      Semantic Kernel                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │                       Kernel                              │  │
│   │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐ │  │
│   │  │  Plugins │  │ Planners │  │  Memory  │  │Connectors│ │  │
│   │  └──────────┘  └──────────┘  └──────────┘  └──────────┘ │  │
│   └──────────────────────────────────────────────────────────┘  │
│                              │                                   │
│                              ▼                                   │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │                   AI Services                             │  │
│   │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐ │  │
│   │  │  OpenAI  │  │  Azure   │  │  Google  │  │  Local   │ │  │
│   │  └──────────┘  └──────────┘  └──────────┘  └──────────┘ │  │
│   └──────────────────────────────────────────────────────────┘  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Enterprise Ready Features

| Feature | Description |
|---------|-------------|
| **Security** | Telemetry support, hooks and filters for responsible AI |
| **Multi-Language** | Version 1.0+ support across C#, Python, and Java |
| **Stable API** | Committed to non-breaking changes |
| **Future Proof** | Easily swap models without rewriting codebase |
| **Extensible** | OpenAPI specifications (like Microsoft 365 Copilot) |

### Key Concepts

#### Plugins
Your existing code can be exposed to AI models:

```csharp
public class WeatherPlugin
{
    [KernelFunction, Description("Get weather for a city")]
    public string GetWeather(string city)
    {
        return $"It's sunny in {city}!";
    }
}
```

#### Kernel
The central orchestrator:

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

#### Automatic Function Calling
```csharp
var settings = new OpenAIPromptExecutionSettings
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

var result = await kernel.InvokePromptAsync(
    "What's the weather in Seattle?",
    new KernelArguments(settings)
);
```

### Automating Business Processes

Semantic Kernel combines prompts with existing APIs to perform actions:

1. When a request is made, the model calls a function
2. Semantic Kernel translates the model's request to a function call
3. Results are passed back to the model

### Pros and Cons

| Pros | Cons |
|------|------|
| ✅ First-class C# support | ❌ Smaller community than LangChain |
| ✅ Enterprise-grade features | ❌ Fewer integrations |
| ✅ Microsoft backing & Azure integration | ❌ Learning curve for non-.NET developers |
| ✅ Stable API with versioning | ❌ Less documentation/examples |

---

## 3. AutoGen

### Introduction

AutoGen is an open-source programming framework for building AI agents and facilitating cooperation among multiple agents to solve tasks. AutoGen aims to provide an easy-to-use and flexible framework for accelerating development and research on agentic AI, like PyTorch for Deep Learning.

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      AutoGen Framework                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │              Multi-Agent Conversation                     │  │
│   │                                                           │  │
│   │   ┌─────────┐    ┌─────────┐    ┌─────────────────────┐  │  │
│   │   │Assistant│◄──►│  User   │◄──►│  Custom Agents      │  │  │
│   │   │  Agent  │    │  Proxy  │    │  (Group Chat, etc.) │  │  │
│   │   └─────────┘    └─────────┘    └─────────────────────┘  │  │
│   │         │              │               │                  │  │
│   │         └──────────────┼───────────────┘                  │  │
│   │                        ▼                                  │  │
│   │   ┌────────────────────────────────────────────────────┐ │  │
│   │   │              Conversation Patterns                  │ │  │
│   │   │  • Two-agent chat    • Group chat                  │ │  │
│   │   │  • Sequential        • Hierarchical                │ │  │
│   │   └────────────────────────────────────────────────────┘ │  │
│   └──────────────────────────────────────────────────────────┘  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Main Features

| Feature | Description |
|---------|-------------|
| **Multi-Agent Conversations** | Build next-gen LLM applications with minimal effort |
| **Diverse Conversation Patterns** | Customizable for complex workflows |
| **Conversation Autonomy** | Configure number of agents and topology |
| **Working Systems** | Collection of examples from various domains |

### Quick Start

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

# Start the chat
user_proxy.initiate_chat(
    assistant,
    message="Tell me a joke about NVDA and TESLA stock prices.",
)
```

### Multi-Agent Conversation Framework

AutoGen offers customizable and conversable agents which integrate:
- LLMs
- Tools
- Humans

By automating chat among multiple capable agents, one can easily make them collectively perform tasks autonomously or with human feedback.

### Conversation Patterns

```
Two-Agent Chat:           Group Chat:
┌─────────┐              ┌─────────┐
│  Agent  │              │  Agent  │
│    A    │◄────────────►│    A    │
└─────────┘              └────┬────┘
     ▲                        │
     │                   ┌────▼────┐
     ▼                   │ Manager │
┌─────────┐              └────┬────┘
│  Agent  │              ┌────▼────┐
│    B    │◄────────────►│  Agent  │
└─────────┘              │    B    │
                         └─────────┘

Sequential:              Hierarchical:
A → B → C → D            ┌─────────┐
                         │ Manager │
                         └────┬────┘
                    ┌─────────┼─────────┐
                    ▼         ▼         ▼
               ┌───────┐ ┌───────┐ ┌───────┐
               │ Agent │ │ Agent │ │ Agent │
               │   A   │ │   B   │ │   C   │
               └───────┘ └───────┘ └───────┘
```

### Agent Types

| Agent Type | Description |
|------------|-------------|
| **AssistantAgent** | AI assistant powered by LLM |
| **UserProxyAgent** | Represents human user, can execute code |
| **GroupChatManager** | Orchestrates multi-agent group conversations |
| **Custom Agents** | Create your own specialized agents |

### Pros and Cons

| Pros | Cons |
|------|------|
| ✅ Built for multi-agent scenarios | ❌ Python only |
| ✅ Simple conversation abstraction | ❌ Less mature than LangChain |
| ✅ Code execution support | ❌ Smaller ecosystem |
| ✅ Academic research backing | ❌ Rapid version changes |

---

## 4. Comparison Summary

### Feature Matrix

| Feature | LangChain | Semantic Kernel | AutoGen |
|---------|-----------|-----------------|---------|
| **Primary Language** | Python, JS | C#, Python, Java | Python |
| **Agent Focus** | Single & Multi | Single & Plugins | Multi-Agent |
| **Tool Integration** | Excellent | Good | Good |
| **Memory/State** | Built-in | Built-in | Conversation-based |
| **RAG Support** | Excellent | Good | Basic |
| **Enterprise Ready** | Moderate | Excellent | Moderate |
| **Learning Curve** | Medium | Medium-High | Low-Medium |
| **Community Size** | Largest | Growing | Growing |

### Use Case Recommendations

| Use Case | Recommended Framework |
|----------|----------------------|
| Quick prototyping with LLMs | LangChain |
| Enterprise C# applications | Semantic Kernel |
| Multi-agent collaboration | AutoGen |
| RAG applications | LangChain |
| Azure ecosystem integration | Semantic Kernel |
| Research & experimentation | AutoGen |
| Production Python apps | LangChain + LangGraph |

### Architecture Comparison

```
┌──────────────────────────────────────────────────────────────────────┐
│                     Framework Architecture Comparison                 │
├──────────────────────────────────────────────────────────────────────┤
│                                                                       │
│  LangChain:                                                           │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐                        │
│  │  Model   │───►│  Chain   │───►│  Agent   │                        │
│  └──────────┘    └──────────┘    └──────────┘                        │
│       │               │               │                               │
│       └───────────────┴───────────────┘                               │
│                       │                                               │
│                  [Tools/Memory/Retriever]                             │
│                                                                       │
│  Semantic Kernel:                                                     │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐                        │
│  │  Kernel  │───►│  Plugin  │───►│  Planner │                        │
│  └──────────┘    └──────────┘    └──────────┘                        │
│       │               │               │                               │
│       └───────────────┴───────────────┘                               │
│                       │                                               │
│                  [AI Services/Connectors]                             │
│                                                                       │
│  AutoGen:                                                             │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐                        │
│  │  Agent A │◄──►│  Agent B │◄──►│  Agent C │                        │
│  └──────────┘    └──────────┘    └──────────┘                        │
│       │               │               │                               │
│       └───────────────┴───────────────┘                               │
│                       │                                               │
│              [Conversation Manager]                                   │
│                                                                       │
└──────────────────────────────────────────────────────────────────────┘
```

---

## 5. Choosing for dawning-agents

For the `dawning-agents` project (C#/.NET focus), consider:

### Recommended Approach

1. **Primary Inspiration**: Semantic Kernel
   - Native C# support
   - Enterprise-ready features
   - Plugin architecture

2. **Multi-Agent Patterns**: AutoGen concepts
   - Conversation patterns
   - Agent collaboration modes

3. **Integration Ideas**: LangChain
   - Tool/function design patterns
   - Memory abstractions

### Implementation Strategy

```csharp
// dawning-agents architecture inspired by all three
namespace DawningAgents.Core
{
    // Semantic Kernel style - Kernel as orchestrator
    public interface IAgentKernel
    {
        Task<AgentResponse> ExecuteAsync(AgentRequest request);
    }
    
    // LangChain style - Tools as first-class citizens
    public interface ITool
    {
        string Name { get; }
        string Description { get; }
        Task<ToolResult> ExecuteAsync(ToolInput input);
    }
    
    // AutoGen style - Multi-agent conversations
    public interface IConversableAgent
    {
        Task SendAsync(IConversableAgent recipient, Message message);
        Task<Message> ReceiveAsync(IConversableAgent sender, Message message);
    }
}
```

---

## 6. Resources

### Official Documentation

| Framework | Links |
|-----------|-------|
| **LangChain** | [Docs](https://docs.langchain.com) • [GitHub](https://github.com/langchain-ai/langchain) • [LangGraph](https://docs.langchain.com/oss/python/langgraph/overview) |
| **Semantic Kernel** | [Docs](https://learn.microsoft.com/semantic-kernel) • [GitHub](https://github.com/microsoft/semantic-kernel) |
| **AutoGen** | [Docs](https://microsoft.github.io/autogen) • [GitHub](https://github.com/microsoft/autogen) |

### Learning Resources

- LangChain Academy: https://academy.langchain.com/
- Semantic Kernel Samples: https://github.com/microsoft/semantic-kernel/tree/main/samples
- AutoGen Notebooks: https://microsoft.github.io/autogen/docs/notebooks

---

## Summary

| Framework | Best For | Key Strength |
|-----------|----------|--------------|
| **LangChain** | Quick prototyping & Python apps | Ecosystem & integrations |
| **Semantic Kernel** | Enterprise C#/.NET apps | Stability & Azure integration |
| **AutoGen** | Multi-agent research & experiments | Conversation patterns |

All three frameworks are actively developed and can be excellent choices depending on your specific needs. For `dawning-agents`, combining concepts from all three while building natively in C# provides the best of all worlds.
