# 🎯 Dawning.Agents 企业级差距修复计划

> **创建日期**: 2026-02-04  
> **目标**: 将框架从 72% 提升至 90%+ 企业级就绪度  
> **预计工期**: 8-10 周

---

## 📊 差距总览

| 优先级 | 任务 | 影响 | 工期 | 状态 |
|--------|------|------|------|------|
| **P5** | MCP 协议支持 | 🔴 关键 - AI 工具互操作标准 | 2 周 | ✅ |
| **P6** | Agent 评估框架 | 🟡 重要 - 量化 Agent 效果 | 1.5 周 | ✅ |
| **P7** | 图形化工作流 DSL | 🟡 重要 - 可视化编排 | 1.5 周 | ✅ |
| **P8** | 多模态支持 | 🟢 增强 - Vision/Audio | 1.5 周 | ✅ |
| **P9** | MS Agent Framework 集成 | 🟢 增强 - 微软生态互操作 | 1 周 | ⏳ |
| **P10** | 文档与案例完善 | 🟡 重要 - 降低采用门槛 | 1 周 | ⏳ |

---

## 📅 Phase F: Week 28-29 - MCP 协议支持

### 目标
实现 Anthropic Model Context Protocol，使 Agent 能与 Claude Desktop、Cursor 等工具互操作。

### 任务分解

#### F1: MCP Server 实现 (Week 28)

```
src/Dawning.Agents.MCP/
├── Dawning.Agents.MCP.csproj
├── Server/
│   ├── MCPServer.cs              # MCP Server 主类
│   ├── MCPServerOptions.cs       # 服务器配置
│   ├── MCPToolHandler.cs         # 工具调用处理
│   ├── MCPResourceHandler.cs     # 资源提供处理
│   └── MCPPromptHandler.cs       # 提示词模板处理
├── Protocol/
│   ├── MCPMessage.cs             # 消息模型
│   ├── MCPCapabilities.cs        # 能力声明
│   ├── MCPToolDefinition.cs      # 工具定义
│   └── MCPResource.cs            # 资源定义
└── Transport/
    ├── StdioTransport.cs         # stdio 传输
    └── HttpTransport.cs          # HTTP/SSE 传输
```

**核心功能**:
- [ ] stdio 传输协议
- [ ] 工具注册与调用
- [ ] 资源暴露 (文件/数据)
- [ ] 提示词模板支持
- [ ] 与 Claude Desktop 集成测试

#### F2: MCP Client 实现 (Week 29)

```
src/Dawning.Agents.MCP/
└── Client/
    ├── MCPClient.cs              # MCP Client 主类
    ├── MCPClientOptions.cs       # 客户端配置
    ├── MCPToolProxy.cs           # 远程工具代理
    └── MCPResourceFetcher.cs     # 资源获取器
```

**核心功能**:
- [ ] 连接远程 MCP Server
- [ ] 发现并调用远程工具
- [ ] 获取远程资源
- [ ] 错误处理与重试

### 验收标准
- [ ] 能作为 MCP Server 被 Claude Desktop 调用
- [ ] 能作为 MCP Client 调用其他 MCP Server
- [ ] 30+ 单元测试通过
- [ ] 集成测试文档

---

## 📅 Phase G: Week 30 - Agent 评估框架

### 目标
建立 Agent 效果量化评估体系，支持 A/B 测试和持续改进。

### 任务分解

```
src/Dawning.Agents.Abstractions/Evaluation/
├── IAgentEvaluator.cs            # 评估器接口
├── EvaluationResult.cs           # 评估结果
├── EvaluationMetrics.cs          # 评估指标
└── EvaluationOptions.cs          # 评估配置

src/Dawning.Agents.Core/Evaluation/
├── TaskSuccessEvaluator.cs       # 任务成功率评估
├── LatencyEvaluator.cs           # 延迟评估
├── CostEvaluator.cs              # 成本评估
├── QualityEvaluator.cs           # 输出质量评估 (LLM-as-Judge)
├── CompositeEvaluator.cs         # 复合评估器
├── EvaluationRunner.cs           # 评估执行器
├── EvaluationReport.cs           # 评估报告生成
└── ABTestRunner.cs               # A/B 测试运行器
```

### 核心指标

| 指标 | 说明 | 计算方式 |
|------|------|----------|
| 任务成功率 | 完成预期任务的比例 | 成功数 / 总数 |
| 平均延迟 | 响应时间 | P50/P95/P99 |
| Token 消耗 | LLM 调用成本 | Input + Output tokens |
| 工具调用准确率 | 选择正确工具的比例 | 正确调用 / 总调用 |
| 输出质量 | LLM-as-Judge 评分 | 1-5 分 |

### 验收标准
- [ ] 支持批量评估测试集
- [ ] 生成 JSON/HTML 评估报告
- [ ] 支持 A/B 测试对比
- [ ] 20+ 单元测试通过

---

## 📅 Phase G: Week 31 - 图形化工作流 DSL

### 目标
提供声明式工作流定义，支持条件分支、循环和可视化。

### 任务分解

```
src/Dawning.Agents.Abstractions/Workflow/
├── IWorkflow.cs                  # 工作流接口
├── IWorkflowNode.cs              # 节点接口
├── WorkflowDefinition.cs         # 工作流定义
├── NodeTypes.cs                  # 节点类型枚举
└── WorkflowOptions.cs            # 工作流配置

src/Dawning.Agents.Core/Workflow/
├── WorkflowBuilder.cs            # 流式构建器
├── WorkflowEngine.cs             # 执行引擎
├── Nodes/
│   ├── AgentNode.cs              # Agent 节点
│   ├── ToolNode.cs               # 工具节点
│   ├── ConditionNode.cs          # 条件节点
│   ├── LoopNode.cs               # 循环节点
│   ├── ParallelNode.cs           # 并行节点
│   └── SubWorkflowNode.cs        # 子工作流节点
├── WorkflowSerializer.cs         # JSON/YAML 序列化
└── WorkflowVisualizer.cs         # Mermaid 图生成
```

### DSL 示例

```csharp
var workflow = new WorkflowBuilder("ResearchWorkflow")
    .StartWith<ResearcherAgent>("research")
    .Then<WriterAgent>("draft")
    .Condition(ctx => ctx.GetResult<int>("quality") < 7)
        .Then<EditorAgent>("review")
        .Loop(maxIterations: 3)
    .EndCondition()
    .Then<PublisherAgent>("publish")
    .Build();

// 或 YAML 定义
var workflow = WorkflowDefinition.FromYaml(@"
name: ResearchWorkflow
nodes:
  - id: research
    type: agent
    agent: ResearcherAgent
  - id: draft
    type: agent
    agent: WriterAgent
    dependsOn: [research]
  - id: review
    type: condition
    condition: quality < 7
    then: [edit]
    maxLoop: 3
");
```

### 验收标准
- [ ] 支持 Agent/Tool/Condition/Loop/Parallel 节点
- [ ] 支持 JSON/YAML 定义
- [ ] 生成 Mermaid 可视化图
- [ ] 25+ 单元测试通过

---

## 📅 Phase H: Week 32 - 多模态支持

### 目标
支持图像输入 (Vision) 和音频输入 (Whisper)。

### 任务分解

```
src/Dawning.Agents.Abstractions/Multimodal/
├── IImageProvider.cs             # 图像处理接口
├── IAudioProvider.cs             # 音频处理接口
├── MediaContent.cs               # 媒体内容模型
└── MultimodalOptions.cs          # 多模态配置

src/Dawning.Agents.Core/Multimodal/
├── ImageProcessor.cs             # 图像预处理
├── AudioTranscriber.cs           # 音频转文字
└── MultimodalChatMessage.cs      # 多模态消息

src/Dawning.Agents.OpenAI/
├── OpenAIVisionProvider.cs       # GPT-4V 支持
└── OpenAIWhisperProvider.cs      # Whisper 支持

src/Dawning.Agents.Azure/
├── AzureVisionProvider.cs        # Azure Vision
└── AzureSpeechProvider.cs        # Azure Speech
```

### 核心功能

```csharp
// Vision 使用
var response = await agent.RunAsync(new MultimodalInput
{
    Text = "描述这张图片",
    Images = [ImageContent.FromFile("photo.jpg")]
});

// Audio 使用
var transcription = await whisper.TranscribeAsync("audio.mp3");
var response = await agent.RunAsync(transcription);
```

### 验收标准
- [ ] 支持 GPT-4V 图像理解
- [ ] 支持 Whisper 音频转文字
- [ ] 支持 Base64 和 URL 图像输入
- [ ] 20+ 单元测试通过

---

## 📅 Phase H: Week 33 - MS Agent Framework 集成

### 目标
与微软 Agent Framework 互操作，支持工具/插件共享。

### 任务分解

```
src/Dawning.Agents.MSAgentFramework/
├── Dawning.Agents.MSAgentFramework.csproj
├── Adapters/
│   ├── MSToolAdapter.cs          # 工具适配器
│   ├── MSPluginAdapter.cs        # 插件适配器
│   └── MSAgentAdapter.cs         # Agent 适配器
├── Interop/
│   ├── DawningToolAsPlugin.cs    # 将 Dawning Tool 包装为 MS Plugin
│   └── MSPluginAsTool.cs         # 将 MS Plugin 包装为 Dawning Tool
└── MSAgentFrameworkExtensions.cs # DI 扩展
```

### 核心功能

```csharp
// 使用 MS Agent Framework 的插件
services.AddMSAgentFrameworkPlugins(plugins => {
    plugins.AddPlugin<WebSearchPlugin>();
    plugins.AddPlugin<EmailPlugin>();
});

// 将 Dawning 工具暴露给 MS Agent Framework
var msAgent = new AgentBuilder()
    .WithPlugin(new DawningToolPlugin(dawningToolRegistry))
    .Build();
```

### 验收标准
- [ ] 能使用 MS Agent Framework 插件
- [ ] 能将 Dawning 工具暴露为 MS 插件
- [ ] 15+ 单元测试通过

---

## 📅 Phase I: Week 34 - 文档与案例完善

### 目标
完善文档，添加生产案例，提升开发者体验。

### 任务分解

```
docs/
├── guides/
│   ├── production-best-practices.md   # 生产最佳实践
│   ├── security-hardening.md          # 安全加固指南
│   ├── performance-tuning.md          # 性能调优
│   └── migration-guide.md             # 迁移指南
├── tutorials/
│   ├── building-chatbot.md            # 构建聊天机器人
│   ├── building-rag-app.md            # 构建 RAG 应用
│   ├── multi-agent-system.md          # 多 Agent 系统
│   └── mcp-integration.md             # MCP 集成教程
├── case-studies/
│   ├── customer-service-bot.md        # 客服机器人案例
│   ├── code-review-agent.md           # 代码审查 Agent
│   └── research-assistant.md          # 研究助手案例
└── api/                               # API 参考（自动生成）
```

### 验收标准
- [ ] 4+ 详细教程
- [ ] 3+ 生产案例
- [ ] API 文档 100% 覆盖
- [ ] 中英文双语

---

## 📈 预期成果

| 指标 | 当前 | 目标 |
|------|------|------|
| 企业就绪度 | 72% | 90%+ |
| 测试数量 | 1,630 | 2,000+ |
| 代码覆盖率 | 72.9% | 80%+ |
| 文档完整度 | 60% | 90%+ |

---

## 🚀 开始执行

**当前阶段**: Phase F - MCP 协议支持  
**当前任务**: F1 - MCP Server 实现

```bash
# 创建 MCP 包
dotnet new classlib -n Dawning.Agents.MCP -o src/Dawning.Agents.MCP
dotnet sln add src/Dawning.Agents.MCP
```

---

*计划创建于 2026-02-04*
