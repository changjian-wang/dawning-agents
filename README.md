# 🌅 Dawning.Agents

> 企业级 .NET AI Agent 框架，设计灵感来自 OpenAI Agents SDK 的极简风格

[![Build Status](https://github.com/changjian-wang/dawning-agents/actions/workflows/build.yml/badge.svg)](https://github.com/changjian-wang/dawning-agents/actions)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

## ✨ 特性

- 🎯 **极简 API** - 一行代码完成核心功能注册
- 🔌 **纯 DI 架构** - 完全基于依赖注入，与 ASP.NET Core 无缝集成
- 🛠️ **丰富的工具** - 64 个内置工具，支持自定义扩展
- 🤖 **多 Agent 协作** - 支持顺序/并行编排、Handoff 任务转交
- 🔒 **安全护栏** - 内容过滤、敏感数据检测、速率限制
- 👥 **人机协作** - 审批工作流、升级处理
- 📊 **可观测性** - 结构化日志、指标收集、分布式追踪
- 🚀 **生产就绪** - 熔断器、负载均衡、自动扩展
- 🔄 **MCP 协议** - 与 Claude Desktop、Cursor 等工具互操作
- 📝 **工作流 DSL** - 声明式工作流定义，支持可视化
- 🎨 **多模态** - Vision 图像理解 + Whisper 音频转录 + TTS 语音合成
- 📈 **A/B 测试** - Agent 效果评估框架

## 📦 安装

```bash
dotnet add package Dawning.Agents.Core
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

// 注册内置工具
builder.Services.AddBuiltInTools();

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
src/
├── Dawning.Agents.Abstractions/   # 接口和数据模型（零依赖）
│   ├── Agent/                     # IAgent, AgentContext, AgentResponse
│   ├── LLM/                       # ILLMProvider, ChatMessage
│   ├── Tools/                     # ITool, IToolRegistry
│   ├── Memory/                    # IConversationMemory
│   ├── Orchestration/             # IOrchestrator
│   ├── Handoff/                   # Handoff 模型
│   ├── Communication/             # IMessageBus, ISharedState
│   ├── Safety/                    # IGuardrail
│   ├── HumanLoop/                 # IHumanInteractionHandler
│   ├── Observability/             # 遥测配置和模型
│   └── Scaling/                   # 扩展组件接口
│
├── Dawning.Agents.Core/           # 核心实现
│   ├── Agent/                     # ReActAgent, AgentBase
│   ├── LLM/                       # OllamaProvider
│   ├── Tools/                     # 64 个内置工具
│   ├── Memory/                    # BufferMemory, WindowMemory, SummaryMemory
│   ├── Orchestration/             # Sequential/Parallel Orchestrator
│   ├── Handoff/                   # HandoffHandler
│   ├── Communication/             # InMemoryMessageBus, InMemorySharedState
│   ├── Safety/                    # GuardrailPipeline, SafeAgent
│   ├── HumanLoop/                 # ApprovalWorkflow, ConsoleInteractionHandler
│   ├── Observability/             # AgentTelemetry, MetricsCollector
│   └── Scaling/                   # CircuitBreaker, LoadBalancer, AutoScaler
│
├── Dawning.Agents.OpenAI/         # OpenAI Provider
└── Dawning.Agents.Azure/          # Azure OpenAI Provider
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

### 内置工具 (64 个方法)

| 类别 | 工具类 | 方法数 |
|------|--------|--------|
| DateTime | DateTimeTool | 4 |
| Math | MathTool | 8 |
| Json | JsonTool | 4 |
| Utility | UtilityTool | 5 |
| FileSystem | FileSystemTool | 13 |
| Http | HttpTool | 6 |
| Process | ProcessTool | 6 |
| Git | GitTool | 18 |

```csharp
// 注册所有内置工具
builder.Services.AddAllBuiltInTools();

// 或按类别注册
builder.Services.AddFileSystemTools();
builder.Services.AddGitTools();
```

### Memory 系统

```csharp
// 滑动窗口记忆（保留最近 N 条）
builder.Services.AddWindowMemory(windowSize: 10);

// 摘要记忆（自动摘要旧消息）
builder.Services.AddSummaryMemory();
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

## 🎮 运行 Demo

```bash
cd samples/Dawning.Agents.Demo
dotnet run
```

### Demo 选项

| 选项 | 说明 |
|------|------|
| `--chat` | 简单聊天 |
| `--agent` | ReAct Agent |
| `--stream` | 流式输出 |
| `-i` | 交互式对话 |
| `-m` | Memory 系统 |
| `-o` | 多 Agent 编排 |
| `-hf` | Handoff 协作 |
| `-hl` | 人机协作 |
| `-ob` | 可观测性 |
| `-sc` | 扩展部署 |

## 📖 文档

### 入门指南
- [快速入门](docs/QUICKSTART.md) - 5 分钟运行第一个 Agent
- [API 参考](docs/API_REFERENCE.md) - 核心接口和类

### 学习资料
- [学习资源索引](docs/LEARNING_RESOURCES.md) - 整合的学习材料
- [完整学习计划](docs/LEARNING_PLAN_FULL.md) - 12 周详细任务清单
- [阅读材料](docs/readings/) - 16 个主题的详细资料

### 开发参考
- [变更日志](CHANGELOG.md) - 版本更新记录
- [企业路线图](docs/ENTERPRISE_ROADMAP.md) - 企业级转型规划
- [开发指南](.github/copilot-instructions.md) - 代码规范

## 🧪 测试

```bash
dotnet test
```

**测试统计**:
- 测试数量: 1,828 个
- 行覆盖率: 72.9%
- 分支覆盖率: 62.6%

## 🤝 贡献

欢迎贡献！请查看 [贡献指南](.github/copilot-instructions.md) 了解代码规范。

## 📄 许可证

[MIT License](LICENSE)

---

<p align="center">
  <sub>Built with ❤️ using .NET 10.0</sub>
</p>
