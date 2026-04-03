# 🌅 Dawning.Agents

> 企业级 .NET AI Agent 框架，设计灵感来自 OpenAI Agents SDK 的极简风格

> [!WARNING]
> **此仓库已弃用（Deprecated）**
>
> 自 **2026-04-03** 起，`dawning-agents` 不再作为 Agent Framework 的主开发仓库。
> - 停止接收新功能开发与架构演进。
> - 仅在必要时处理阻断级问题（如严重安全或关键稳定性问题）。
> - 新一代 Agent Framework 将在新的仓库中继续开发（仓库地址待公布）。
> - 详细说明见 [`DEPRECATION.md`](DEPRECATION.md)。
>
> 建议：新项目请勿继续基于本仓库启动；请迁移到新仓库。

[![Build Status](https://github.com/changjian-wang/dawning-agents/actions/workflows/build.yml/badge.svg)](https://github.com/changjian-wang/dawning-agents/actions)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Tests](https://img.shields.io/badge/Tests-2482%20passing-brightgreen)](https://github.com/changjian-wang/dawning-agents)
[![codecov](https://codecov.io/gh/changjian-wang/dawning-agents/branch/main/graph/badge.svg)](https://codecov.io/gh/changjian-wang/dawning-agents)

## 🎯 为什么选择 Dawning.Agents？

| 特性 | Dawning.Agents | LangChain (.NET) | Microsoft Agent Framework |
|------|----------------|------------------|---------------------------|
| **API 风格** | 极简 DI | Builder 链式 | 企业复杂 |
| **学习曲线** | ⭐ 低 | ⭐⭐ 中 | ⭐⭐⭐ 高 |
| **注册方式** | 一行代码 | 多步配置 | XML/属性配置 |
| **Memory 策略** | 5 种 + 自动降级 | 2 种 | 3 种 |
| **向量存储** | 5 种 | 3 种 | Azure 专属 |
| **MCP 协议** | ✅ 支持 | ❌ | ❌ |
| **本地模型** | ✅ Ollama | ✅ | ⚠️ 有限 |
| **生产就绪** | ✅ | ⚠️ | ✅ |

## 📋 环境要求

| 依赖 | 版本 | 说明 |
|------|------|------|
| **.NET** | 10.0+ | 必须 |
| **Ollama** | 0.1.0+ | 本地模型（可选） |
| **Docker** | 24.0+ | 向量数据库（可选） |

### 支持的 LLM 模型

| Provider | 模型 | 说明 |
|----------|------|------|
| **Ollama** | qwen2.5, llama3, mistral, phi3 | 本地部署，免费 |
| **OpenAI** | gpt-4o, gpt-4-turbo, gpt-3.5-turbo | 云端，按量付费 |
| **Azure OpenAI** | gpt-4, gpt-35-turbo | 企业级，合规 |

### 支持的 Embedding 模型

| Provider | 模型 | 维度 |
|----------|------|------|
| **Ollama** | nomic-embed-text, mxbai-embed-large | 768/1024 |
| **OpenAI** | text-embedding-3-small/large | 1536/3072 |
| **Azure OpenAI** | text-embedding-ada-002 | 1536 |

## ✨ 特性

### 核心能力
- 🎯 **极简 API** - 一行代码完成核心功能注册
- 🔌 **纯 DI 架构** - 完全基于依赖注入，与 ASP.NET Core 无缝集成
- 🛠️ **核心工具集** - 6 个核心工具 + 动态工具创建（create_tool）
- 🤖 **多 Agent 协作** - 顺序/并行编排、Handoff 任务转交

### 智能记忆 (五大上下文策略)
- 📦 **BufferMemory** - 完整存储，适合短对话
- 📐 **WindowMemory** - 滑动窗口，控制 token
- 📝 **SummaryMemory** - LLM 摘要压缩 (Reduce)
- 🔄 **AdaptiveMemory** - 自动降级 (Offload)
- 🔍 **VectorMemory** - 向量检索增强 (Retrieve)
- 💾 **SemanticCache** - 语义级智能缓存 (Cache)

### 多模态支持
- 👁️ **Vision** - GPT-4V/Azure Vision 图像理解
- 🎤 **Whisper** - 语音转文字
- 🔊 **TTS** - 文字转语音 (6 种声音)

### 向量存储 (5 种)
- 🟣 **Qdrant** - 高性能向量数据库
- 🌲 **Pinecone** - 云端托管向量数据库
- 🔴 **Redis** - 带向量搜索的缓存
- 🎨 **Chroma** - 轻量级本地向量数据库
- 🔷 **Weaviate** - GraphQL 向量数据库

### 企业级功能
- 🔒 **安全护栏** - 内容过滤、PII 检测、速率限制
- 👥 **人机协作** - 审批工作流、升级处理
- 📊 **可观测性** - Serilog 日志、OpenTelemetry 追踪
- 🚀 **弹性策略** - Polly V8 熔断器、负载均衡
- 🔄 **MCP 协议** - 与 Claude Desktop、Cursor 互操作
- 📝 **工作流 DSL** - 声明式工作流，Mermaid 可视化
- 📈 **评估框架** - A/B 测试、多指标评估
- ⚡ **模型路由** - 成本优化、延迟优化、故障转移

## 📦 安装

> [!WARNING]
> `Dawning.Agents.*` 系列包已进入弃用状态，不建议在新项目中安装。
> 如你正在评估或新建项目，请等待并迁移到新仓库中的后继包。

```bash
# 核心包
dotnet add package Dawning.Agents.Core

# OpenAI 支持
dotnet add package Dawning.Agents.OpenAI

# Azure OpenAI 支持
dotnet add package Dawning.Agents.Azure

# 向量存储（按需选择）
dotnet add package Dawning.Agents.Qdrant
dotnet add package Dawning.Agents.Pinecone
dotnet add package Dawning.Agents.Redis
dotnet add package Dawning.Agents.Chroma
dotnet add package Dawning.Agents.Weaviate

# 可观测性与日志
dotnet add package Dawning.Agents.OpenTelemetry
dotnet add package Dawning.Agents.Serilog

# MCP 协议
dotnet add package Dawning.Agents.MCP
```

## 🚀 快速开始

### 1. 配置 appsettings.json

```json
{
  "LLM": {
    "ProviderType": "Ollama",
    "Model": "qwen2.5:0.5b",
    "Endpoint": "http://localhost:11434"
  }
}
```

### 2. 注册服务

```csharp
var builder = Host.CreateApplicationBuilder(args);

// 注册 LLM Provider
builder.Services.AddLLMProvider(builder.Configuration);

// 注册核心工具
builder.Services.AddCoreTools();

// 注册 ReAct Agent
builder.Services.AddReActAgent(options =>
{
    options.Name = "MyAgent";
    options.Instructions = "你是一个智能助手";
});
```

### 3. 使用 Agent

```csharp
var agent = serviceProvider.GetRequiredService<IAgent>();
var response = await agent.RunAsync("今天北京天气怎么样？");
Console.WriteLine(response.FinalAnswer);
```

## 🏗️ 项目结构

```
dawning-agents/
├── src/
│   ├── Dawning.Agents.Abstractions/     # 📦 接口和数据模型（零依赖）
│   │   ├── Agent/                       # IAgent, AgentContext, AgentResponse, AgentOptions
│   │   ├── LLM/                         # ILLMProvider, ChatMessage, LLMOptions
│   │   ├── Tools/                       # ITool, IToolRegistry, IToolApprovalHandler
│   │   ├── Memory/                      # IConversationMemory, ITokenCounter, MemoryOptions
│   │   ├── RAG/                         # IVectorStore, IEmbeddingProvider, DocumentChunk
│   │   ├── Cache/                       # ISemanticCache, SemanticCacheOptions
│   │   ├── Workflow/                    # IWorkflow, IWorkflowNode, WorkflowContext
│   │   ├── Evaluation/                  # IAgentEvaluator, EvaluationResult, TestCase
│   │   ├── Multimodal/                  # IVisionProvider, IAudioProvider, ITranscriber
│   │   ├── Safety/                      # IGuardrail, IRateLimiter, IContentModerator
│   │   ├── Orchestration/               # IOrchestrator, OrchestratorOptions
│   │   ├── Handoff/                     # IHandoffTarget, HandoffRequest
│   │   ├── HumanLoop/                   # IApprovalHandler, ApprovalRequest
│   │   ├── Observability/               # AgentMetrics, AgentTrace
│   │   └── Scaling/                     # ILoadBalancer, ICircuitBreaker
│   │
│   ├── Dawning.Agents.Core/             # ⚙️ 核心实现
│   │   ├── Agent/                       # ReActAgent, AgentBase, AgentRunner
│   │   ├── LLM/
│   │   │   ├── OllamaProvider.cs        # Ollama 本地模型
│   │   │   └── HotReloadableLLMProvider.cs
│   │   ├── Tools/
│   │   │   ├── Core/                    # 6 个核心工具实现
│   │   │   │   ├── ReadFileTool.cs      # read_file
│   │   │   │   ├── WriteFileTool.cs     # write_file
│   │   │   │   ├── EditFileTool.cs      # edit_file
│   │   │   │   ├── SearchTool.cs        # search
│   │   │   │   ├── BashTool.cs          # bash
│   │   │   │   └── CreateToolTool.cs    # create_tool
│   │   │   ├── ToolRegistry.cs          # 工具注册中心
│   │   │   ├── ToolScanner.cs           # 动态扫描工具
│   │   │   └── MethodTool.cs            # 方法级工具包装
│   │   ├── Memory/
│   │   │   ├── BufferMemory.cs          # 完整存储
│   │   │   ├── WindowMemory.cs          # 滑动窗口
│   │   │   ├── SummaryMemory.cs         # LLM 摘要压缩
│   │   │   ├── AdaptiveMemory.cs        # 自动降级 (Buffer → Summary)
│   │   │   ├── VectorMemory.cs          # 向量检索增强
│   │   │   └── SimpleTokenCounter.cs    # Token 计数器
│   │   ├── RAG/
│   │   │   ├── InMemoryVectorStore.cs   # 内存向量存储
│   │   │   ├── VectorRetriever.cs       # 向量检索器
│   │   │   ├── DocumentChunker.cs       # 文档分块
│   │   │   └── KnowledgeBase.cs         # 知识库管理
│   │   ├── Cache/
│   │   │   └── SemanticCache.cs         # 语义级智能缓存
│   │   ├── Workflow/
│   │   │   ├── WorkflowBuilder.cs       # 流式工作流构建
│   │   │   ├── WorkflowEngine.cs        # 工作流执行引擎
│   │   │   └── WorkflowSerializer.cs    # YAML/JSON 序列化
│   │   ├── Evaluation/
│   │   │   ├── DefaultAgentEvaluator.cs # 默认评估器
│   │   │   └── ABTestRunner.cs          # A/B 测试运行器
│   │   ├── ModelManagement/             # 模型路由
│   │   │   ├── CostOptimizedRouter.cs   # 成本优化路由
│   │   │   ├── LatencyOptimizedRouter.cs# 延迟优化路由
│   │   │   └── LoadBalancedRouter.cs    # 负载均衡路由
│   │   ├── Multimodal/
│   │   │   ├── OpenAIVisionProvider.cs  # GPT-4V 图像理解
│   │   │   ├── OpenAIWhisperProvider.cs # Whisper 语音转文字
│   │   │   └── OpenAITTSProvider.cs     # TTS 文字转语音
│   │   ├── Safety/
│   │   │   ├── ContentFilterGuardrail.cs# 内容过滤
│   │   │   ├── SensitiveDataGuardrail.cs# PII 检测
│   │   │   ├── RateLimiter.cs           # 速率限制
│   │   │   └── SafeAgent.cs             # 安全包装器
│   │   ├── HumanLoop/
│   │   │   ├── ApprovalWorkflow.cs      # 审批工作流
│   │   │   └── HumanInLoopAgent.cs      # 人机协作 Agent
│   │   ├── Scaling/
│   │   │   ├── AgentLoadBalancer.cs     # Agent 负载均衡
│   │   │   ├── CircuitBreaker.cs        # 熔断器
│   │   │   ├── AgentWorkerPool.cs       # 工作池
│   │   │   └── AgentAutoScaler.cs       # 自动扩缩容
│   │   └── Orchestration/
│   │       ├── SequentialOrchestrator.cs# 顺序编排
│   │       └── ParallelOrchestrator.cs  # 并行编排
│   │
│   ├── Dawning.Agents.MCP/              # 🔌 MCP 协议实现
│   │   ├── Server/                      # MCP Server 实现
│   │   ├── Client/                      # MCP Client 实现
│   │   ├── Protocol/                    # 协议定义
│   │   └── Transport/                   # Stdio/SSE 传输
│   │
│   ├── Dawning.Agents.OpenAI/           # 🤖 OpenAI Provider
│   │   ├── OpenAIProvider.cs            # GPT-3.5/GPT-4
│   │   └── OpenAIEmbeddingProvider.cs   # text-embedding-ada-002
│   │
│   ├── Dawning.Agents.Azure/            # ☁️ Azure OpenAI Provider
│   ├── Dawning.Agents.OpenTelemetry/    # 📊 OpenTelemetry 可观测性
│   ├── Dawning.Agents.Serilog/          # 📝 Serilog 结构化日志
│   │
│   ├── Dawning.Agents.Qdrant/           # 🟣 Qdrant 向量存储
│   ├── Dawning.Agents.Pinecone/         # 🌲 Pinecone 向量存储
│   ├── Dawning.Agents.Redis/            # 🔴 Redis 向量存储 + 分布式 Memory
│   ├── Dawning.Agents.Chroma/           # 🎨 Chroma 向量存储
│   └── Dawning.Agents.Weaviate/         # 🔷 Weaviate 向量存储
│
├── tests/
│   └── Dawning.Agents.Tests/            # 🧪 1,906 个单元测试
│       ├── Abstractions/                # 接口契约测试
│       ├── Core/                        # 核心实现测试
│       ├── OpenAI/                      # OpenAI Provider 测试
│       ├── Azure/                       # Azure Provider 测试
│       ├── Qdrant/                      # Qdrant 测试
│       ├── Pinecone/                    # Pinecone 测试
│       ├── Redis/                       # Redis 测试
│       └── Integration/                 # 集成测试
│
├── samples/
│   ├── Dawning.Agents.Samples.GettingStarted/ # 🚀 入门示例
│   ├── Dawning.Agents.Samples.Memory/         # 🧠 Memory 示例
│   ├── Dawning.Agents.Samples.RAG/            # 🔍 RAG 示例
│   ├── Dawning.Agents.Samples.Enterprise/     # 🏢 企业级示例
│   └── Dawning.Agents.Api/                    # 🌐 Minimal API + SSE 示例
│
├── benchmarks/
│   └── Dawning.Agents.Benchmarks/       # ⚡ 性能基准测试
│
├── docs/                                # 📖 文档
│   ├── architecture.md                  # 架构设计
│   ├── guides/                          # 使用指南（11 篇）
│   └── api/                             # API 参考
│
└── deploy/                              # 🚀 部署配置
    ├── docker/                          # Docker 配置
    ├── k8s/                             # Kubernetes 配置
    └── observability/                   # 监控配置
```

### NuGet 包依赖关系

```
┌──────────────────────────────────────────┐
│       Dawning.Agents.Abstractions        │  零依赖，定义所有接口
└──────────────────────┬───────────────────┘
                       │
        ┌──────────────┼──────────────┬──────────────┐
        ▼              ▼              ▼              ▼
┌───────────┐  ┌───────────┐  ┌───────────┐  ┌───────────┐
│   Core    │  │  OpenAI   │  │   Azure   │  │    MCP    │
│  (核心)   │  │ (OpenAI)  │  │  (Azure)  │  │  (协议)   │
└─────┬─────┘  └───────────┘  └───────────┘  └───────────┘
    │
    ├──────────────┬──────────────┬──────────────┬──────────────┐
      ▼              ▼              ▼              ▼
┌───────────┐  ┌───────────┐  ┌───────────┐  ┌───────────┐
│  Qdrant   │  │ Pinecone  │  │   Redis   │  │  Chroma   │
│  (向量)   │  │  (向量)   │  │  (缓存)   │  │  (向量)   │
└───────────┘  └───────────┘  └───────────┘  └───────────┘
```

## 🛠️ 核心功能

### Agent 核心循环

```csharp
// ReAct 模式：Thought → Action → Observation → Final Answer
builder.Services.AddReActAgent(options =>
{
    options.MaxSteps = 5;
    options.MaxTokens = 1024;
});
```

### Memory 系统 (五大上下文策略)

```csharp
// 1. Buffer - 完整存储（短对话）
builder.Services.AddBufferMemory();

// 2. Window - 滑动窗口（控制 token）
builder.Services.AddWindowMemory(windowSize: 10);

// 3. Summary - LLM 摘要压缩（长对话）
builder.Services.AddSummaryMemory();

// 4. Adaptive - 自动降级（推荐生产环境）
builder.Services.AddAdaptiveMemory(
    downgradeThreshold: 4000,  // 超过 4K tokens 自动切换到 Summary
    maxRecentMessages: 6
);

// 5. Vector - 向量检索增强（超长程任务）
builder.Services.AddVectorMemory(
    recentWindowSize: 6,
    retrieveTopK: 5,
    minRelevanceScore: 0.5f
);
```

### 语义缓存

```csharp
// 减少重复 LLM 调用，降低成本
builder.Services.AddSemanticCache(
    similarityThreshold: 0.95f,  // 高阈值确保精确匹配
    maxEntries: 10000,
    expirationMinutes: 1440      // 24 小时过期
);

// 使用
var cached = await cache.GetAsync("What is AI?");
if (cached != null) return cached.Response;
```

### 向量存储

```csharp
// Qdrant（推荐生产环境）
builder.Services.AddQdrant(config);

// Pinecone（云端托管）
builder.Services.AddPinecone(config);

// Redis（带缓存）
builder.Services.AddRedisVectorStore(config);

// Chroma（本地开发）
builder.Services.AddChroma(config);
```

### 多模态

```csharp
// Vision - 图像理解
builder.Services.AddOpenAIVision(config);
var response = await vision.AnalyzeImageAsync(imageBytes, "描述这张图片");

// Whisper - 语音转文字
builder.Services.AddOpenAIWhisper(config);
var transcription = await whisper.TranscribeAsync(audioFile);

// TTS - 文字转语音
builder.Services.AddOpenAITTS(config);
var audio = await tts.SynthesizeAsync("Hello world", "nova");
```

### 工作流 DSL

```csharp
// 流式构建
var workflow = new WorkflowBuilder("ResearchWorkflow")
    .StartWith<ResearcherAgent>("research")
    .Then<WriterAgent>("draft")
    .Condition(ctx => ctx.GetResult<int>("quality") < 7)
        .Then<EditorAgent>("review")
        .Loop(maxIterations: 3)
    .EndCondition()
    .Build();

// 生成 Mermaid 可视化
var mermaid = workflow.ToMermaid();
```

### MCP 协议

```csharp
// 作为 MCP Server
builder.Services.AddMCPServer(options =>
{
    options.Name = "my-agent-server";
    options.Transport = MCPTransport.Stdio;
});

// 作为 MCP Client
builder.Services.AddMCPClient(options =>
{
    options.ServerCommand = "npx";
    options.ServerArgs = ["-y", "@anthropic-ai/mcp-server"];
});
```

### 模型路由

```csharp
// 成本优化路由
builder.Services.AddCostOptimizedRouter(config);

// 延迟优化路由
builder.Services.AddLatencyOptimizedRouter(config);

// A/B 测试路由
builder.Services.AddABTestRouter(config);
```

### Agent 评估

```csharp
var runner = new EvaluationRunner(evaluators);
var report = await runner.RunAsync(agent, testCases);

// A/B 测试
var abRunner = new ABTestRunner(agentA, agentB, evaluators);
var comparison = await abRunner.CompareAsync(testCases);
```

### 核心工具 (6 个)

| 工具名 | 用途 |
|--------|------|
| `read_file` | 读取文件内容 |
| `write_file` | 写入或覆盖文件 |
| `edit_file` | 局部编辑文件 |
| `search` | 代码/文本搜索 |
| `bash` | 执行终端命令 |
| `create_tool` | 动态创建新工具 |

```csharp
// 注册核心工具
builder.Services.AddCoreTools();
```

### 多 Agent 编排

```csharp
// 顺序编排：A → B → C
var orchestrator = new SequentialOrchestrator("Pipeline")
    .AddAgent(extractorAgent)
    .AddAgent(analyzerAgent)
    .AddAgent(summarizerAgent);

// 并行编排
var parallel = new ParallelOrchestrator("Experts")
    .AddAgent(techExpert)
    .AddAgent(legalExpert);
```

### 安全护栏

```csharp
// 内容过滤 + 敏感数据检测 + 长度限制
builder.Services.AddSafetyGuardrails(options =>
{
    options.EnableContentFilter = true;
    options.EnableSensitiveDataFilter = true;
    options.MaxInputLength = 10000;
});
```

### 人机协作

```csharp
// 审批工作流
var workflow = new ApprovalWorkflow(handler, config);
var result = await workflow.RequestApprovalAsync(
    action: "delete",
    description: "删除生产数据"
);
```

### 可观测性

```csharp
// 启用遥测
builder.Services.AddAgentTelemetry(config =>
{
    config.EnableLogging = true;
    config.EnableMetrics = true;
    config.EnableTracing = true;
});
```

### 生产部署

```csharp
// 熔断器保护
var circuitBreaker = new CircuitBreaker(failureThreshold: 5);
await circuitBreaker.ExecuteAsync(() => agent.RunAsync(input));

// 负载均衡
var loadBalancer = new AgentLoadBalancer();
loadBalancer.RegisterInstance(instance1);
var selected = loadBalancer.GetLeastLoadedInstance();
```

## 🎮 运行 API Sample

```bash
cd samples/Dawning.Agents.Api
dotnet run
```

### API 端点

| 端点 | 说明 |
|------|------|
| `POST /api/chat` | 同步聊天 |
| `POST /api/chat/stream` | SSE 流式聊天 |
| `POST /api/agent/run` | 执行 Agent |
| `GET /api/agent/health` | 健康检查 |

## 📖 文档

### 入门指南
- [快速入门](docs/guides/getting-started.md) - 5 分钟运行第一个 Agent
- [架构设计](docs/architecture.md) - 源码架构解读
- [API Sample](samples/Dawning.Agents.Api/) - Minimal API + SSE 示例

### 功能指南
- [LLM Providers](docs/guides/llm-providers.md) - Ollama、OpenAI、Azure OpenAI 配置
- [Tools 工具](docs/guides/tools.md) - 内置工具与自定义工具
- [Memory 记忆](docs/guides/memory.md) - 对话记忆系统
- [RAG 检索增强](docs/guides/rag.md) - 向量存储与检索
- [多 Agent 编排](docs/guides/multi-agent.md) - Agent 协作与 Handoff

### 生产部署
- [性能调优](docs/guides/performance.md) - Token 优化、并发控制、缓存策略
- [安全加固](docs/guides/security.md) - API 密钥安全、输入验证、输出过滤
- [生产最佳实践](docs/guides/production.md) - 部署检查清单与配置示例

### 开发参考
- [变更日志](CHANGELOG.md) - 版本更新记录
- [贡献指南](CONTRIBUTING.md) - 提交流程与代码规范

## 🧪 测试

```bash
dotnet test
```

**测试统计**:
- ✅ 测试数量: **2,225** 个（全部通过）
- 📊 深度审计: 38 轮，~190 修复

### 测试分布

| 模块 | 测试数量 |
|------|----------|
| Abstractions | 38 |
| Core | 682 |
| OpenAI | 158 |
| Azure | 164 |
| Qdrant | 88 |
| Pinecone | 86 |
| Redis | 88 |
| Integration | 602 |

## ❓ FAQ

<details>
<summary><b>Q: 如何选择 Memory 策略？</b></summary>

| 场景 | 推荐策略 | 原因 |
|------|----------|------|
| 短对话 (<10 轮) | BufferMemory | 简单高效 |
| 长对话 (10-50 轮) | WindowMemory | 控制 token |
| 超长对话 (>50 轮) | SummaryMemory | 压缩历史 |
| 生产环境 | **AdaptiveMemory** | 自动降级，最佳平衡 |
| 复杂任务 | VectorMemory | 检索相关上下文 |

</details>

<details>
<summary><b>Q: 本地开发推荐用哪个模型？</b></summary>

推荐 **Ollama + qwen2.5:0.5b**：
- 体积小 (~400MB)，启动快
- 支持中英文
- 免费无限制

```bash
ollama pull qwen2.5:0.5b
ollama serve
```

</details>

<details>
<summary><b>Q: 如何减少 LLM 调用成本？</b></summary>

1. **SemanticCache** - 缓存相似查询
2. **CostOptimizedRouter** - 简单问题用小模型
3. **SummaryMemory** - 压缩上下文
4. **ToolSelector** - 只传递相关工具

</details>

<details>
<summary><b>Q: 生产环境推荐配置？</b></summary>

```csharp
// 1. 使用 AdaptiveMemory
builder.Services.AddAdaptiveMemory(downgradeThreshold: 4000);

// 2. 启用 SemanticCache
builder.Services.AddSemanticCache(similarityThreshold: 0.95f);

// 3. 配置熔断器
builder.Services.AddScaling(config);

// 4. 启用遥测
builder.Services.AddAgentTelemetry(config);
```

</details>

<details>
<summary><b>Q: 支持哪些部署方式？</b></summary>

- ✅ Docker / Docker Compose
- ✅ Kubernetes (Helm Chart)
- ✅ Azure Container Apps
- ✅ AWS ECS / EKS
- ✅ 裸机部署

</details>

## 🗺️ Roadmap

### v0.1.0 (当前版本) - 2026 Q1 ✅

- ✅ 核心 Agent 循环 (ReAct)
- ✅ Function Calling Agent
- ✅ 5 种 Memory 策略
- ✅ Tools Redesign（6 核心工具 + 动态工具）
- ✅ 5 种向量存储
- ✅ MCP 协议支持
- ✅ 多模态 (Vision/Audio)
- ✅ 评估框架

### v0.2.0 - 2026 Q2

- 🔲 A2A 协议支持
- 🔲 Prompt 模板与版本管理
- 🔲 持久化 Memory (SQLite/PostgreSQL)
- 🔲 Workflow 可视化调试
- 🔲 Model Router 增强埋点

### v0.3.0 - 2026 Q3

- 🔲 Multi-Agent Swarm 模式
- 🔲 Agent 可视化调试器
- 🔲 云端 Agent 托管
- 🔲 企业 SSO 集成
- 🔲 合规审计日志

## ⚡ Benchmark

> 测试环境: Windows 11, AMD Ryzen 9 5900X, 32GB RAM, Ollama qwen2.5:0.5b

| 操作 | 平均耗时 | 内存占用 |
|------|----------|----------|
| Agent 启动 | 15ms | 50MB |
| 简单对话 | 120ms | +5MB |
| 工具调用 (1 步) | 180ms | +8MB |
| ReAct (3 步) | 450ms | +15MB |
| SemanticCache 命中 | 3ms | +0MB |
| VectorMemory 检索 | 25ms | +2MB |

## 🤝 贡献

欢迎贡献！请查看 [贡献指南](CONTRIBUTING.md) 了解流程与规范。

## 📄 许可证

[MIT License](LICENSE)

---

<p align="center">
  <sub>Built with ❤️ using .NET 10.0</sub>
</p>
