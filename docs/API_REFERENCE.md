# 📦 Dawning.Agents API 参考

> 核心接口和类的快速参考

---

## 🤖 Agent 模块

### IAgent

Agent 的核心接口。

```csharp
public interface IAgent
{
    string Name { get; }
    string Description { get; }
    IReadOnlyList<ITool> Tools { get; }
    Task<AgentResponse> RunAsync(string input, CancellationToken ct = default);
}
```

### AgentResponse

Agent 执行结果。

```csharp
public record AgentResponse
{
    public string FinalAnswer { get; init; }
    public IReadOnlyList<AgentStep> Steps { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
}
```

### AgentStep

单步执行记录。

```csharp
public record AgentStep
{
    public string Thought { get; init; }
    public string? Action { get; init; }
    public string? ActionInput { get; init; }
    public string? Observation { get; init; }
}
```

### DI 注册

```csharp
// 注册 ReAct Agent
services.AddReActAgent(options =>
{
    options.Name = "MyAgent";
    options.Instructions = "你是一个智能助手";
    options.MaxSteps = 5;
    options.MaxTokens = 1024;
});
```

---

## 🔌 LLM Provider 模块

### ILLMProvider

LLM 提供者接口。

```csharp
public interface ILLMProvider
{
    Task<ChatResponse> ChatAsync(
        IReadOnlyList<ChatMessage> messages,
        LLMOptions? options = null,
        CancellationToken ct = default);
    
    IAsyncEnumerable<string> ChatStreamAsync(
        IReadOnlyList<ChatMessage> messages,
        LLMOptions? options = null,
        CancellationToken ct = default);
}
```

### ChatMessage

对话消息。

```csharp
public record ChatMessage(string Role, string Content);
```

### 可用实现

| 类 | Provider |
|---|----------|
| `OllamaProvider` | Ollama 本地 LLM |
| `OpenAIProvider` | OpenAI API |
| `AzureOpenAIProvider` | Azure OpenAI |

### DI 注册

```csharp
// 根据配置自动选择 Provider
services.AddLLMProvider(configuration);
```

### 配置示例

```json
{
  "LLM": {
    "ProviderType": "Ollama",
    "Model": "qwen2.5:0.5b",
    "Endpoint": "http://localhost:11434"
  }
}
```

---

## 🛠️ Tools 模块

### ITool

工具接口。

```csharp
public interface ITool
{
    string Name { get; }
    string Description { get; }
    string ParametersSchema { get; }
    bool RequiresConfirmation { get; }
    ToolRiskLevel RiskLevel { get; }
    string? Category { get; }
    Task<ToolResult> ExecuteAsync(string input, CancellationToken ct = default);
}
```

### FunctionToolAttribute

工具标记特性。

```csharp
[FunctionTool(
    "工具描述",
    RequiresConfirmation = false,
    RiskLevel = ToolRiskLevel.Low,
    Category = "CategoryName"
)]
public string MyTool(string param1, int param2) { ... }
```

### ToolRiskLevel

风险等级枚举。

```csharp
public enum ToolRiskLevel
{
    Low,      // 安全操作
    Medium,   // 需要注意
    High      // 需要确认
}
```

### IToolRegistry

工具注册表。

```csharp
public interface IToolRegistry
{
    void Register(ITool tool);
    void RegisterToolsFromType<T>() where T : class;
    ITool? GetTool(string name);
    IReadOnlyList<ITool> GetAllTools();
    IReadOnlyList<ITool> GetToolsByCategory(string category);
}
```

### 内置工具

| 类 | 方法数 | 类别 |
|---|--------|------|
| `DateTimeTool` | 4 | DateTime |
| `MathTool` | 8 | Math |
| `JsonTool` | 4 | Json |
| `UtilityTool` | 5 | Utility |
| `FileSystemTool` | 13 | FileSystem |
| `HttpTool` | 6 | Http |
| `ProcessTool` | 6 | Process |
| `GitTool` | 18 | Git |
| `PackageManagerTool` | 19 | Package |

### DI 注册

```csharp
services.AddAllBuiltInTools();     // 所有工具
services.AddBuiltInTools();        // 安全工具
services.AddFileSystemTools();     // 按类别
services.AddToolsFromAssembly(assembly);  // 自定义
```

---

## 🧠 Memory 模块

### IConversationMemory

对话记忆接口。

```csharp
public interface IConversationMemory
{
    Task AddMessageAsync(ConversationMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ChatMessage>> GetContextAsync(int? maxTokens = null, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
    Task<int> GetTokenCountAsync(CancellationToken ct = default);
    int MessageCount { get; }
}
```

### 可用实现

| 类 | 策略 |
|---|------|
| `BufferMemory` | 存储所有消息 |
| `WindowMemory` | 滑动窗口 |
| `SummaryMemory` | 自动摘要 |

### DI 注册

```csharp
services.AddMemory(configuration);        // 配置驱动
services.AddBufferMemory();               // 缓冲
services.AddWindowMemory(windowSize: 10); // 滑动窗口
services.AddSummaryMemory();              // 摘要
```

---

## 📚 RAG 模块

### IEmbeddingProvider

嵌入向量提供者。

```csharp
public interface IEmbeddingProvider
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
    int Dimensions { get; }
}
```

### IVectorStore

向量存储。

```csharp
public interface IVectorStore
{
    Task AddAsync(DocumentChunk chunk, CancellationToken ct = default);
    Task<IReadOnlyList<SearchResult>> SearchAsync(float[] query, int topK = 5, CancellationToken ct = default);
}
```

### IRetriever

检索器。

```csharp
public interface IRetriever
{
    Task<IReadOnlyList<SearchResult>> RetrieveAsync(string query, int topK = 5, CancellationToken ct = default);
}
```

### DI 注册

```csharp
services.AddRAG(configuration);
services.AddEmbedding();
services.AddVectorStore();
services.AddRetriever();
```

---

## 🤝 Orchestration 模块

### IOrchestrator

编排器接口。

```csharp
public interface IOrchestrator
{
    string Name { get; }
    Task<OrchestratorResult> ExecuteAsync(string input, CancellationToken ct = default);
}
```

### 可用实现

| 类 | 模式 |
|---|------|
| `SequentialOrchestrator` | 顺序执行 |
| `ParallelOrchestrator` | 并行执行 |
| `HierarchicalOrchestrator` | 层级协作 |
| `VotingOrchestrator` | 投票决策 |

### 使用示例

```csharp
var orchestrator = new SequentialOrchestrator("Pipeline")
    .AddAgent(agent1)
    .AddAgent(agent2)
    .AddAgent(agent3);

var result = await orchestrator.ExecuteAsync("输入");
```

---

## 🔒 Safety 模块

### IGuardrail

护栏接口。

```csharp
public interface IGuardrail
{
    Task<GuardrailResult> ValidateAsync(string input, CancellationToken ct = default);
}
```

### GuardrailResult

验证结果。

```csharp
public record GuardrailResult
{
    public bool IsValid { get; init; }
    public string? Reason { get; init; }
    public IReadOnlyList<string> Violations { get; init; }
}
```

### DI 注册

```csharp
services.AddSafetyGuardrails(options =>
{
    options.EnableContentFilter = true;
    options.EnableSensitiveDataFilter = true;
    options.MaxInputLength = 10000;
});
```

---

## 👥 HumanLoop 模块

### IHumanInteractionHandler

人机交互接口。

```csharp
public interface IHumanInteractionHandler
{
    Task<ConfirmationResponse> RequestConfirmationAsync(
        ConfirmationRequest request,
        CancellationToken ct = default);
}
```

### ApprovalWorkflow

审批工作流。

```csharp
var workflow = new ApprovalWorkflow(handler, config);
var result = await workflow.RequestApprovalAsync(
    action: "delete",
    description: "删除生产数据"
);
```

---

## 📊 Observability 模块

### IMetricsCollector

指标收集器。

```csharp
public interface IMetricsCollector
{
    void IncrementCounter(string name, long value = 1, IDictionary<string, string>? tags = null);
    void RecordHistogram(string name, double value, IDictionary<string, string>? tags = null);
    void SetGauge(string name, double value, IDictionary<string, string>? tags = null);
    MetricsSnapshot GetSnapshot();
}
```

### IHealthCheck

健康检查。

```csharp
public interface IHealthCheck
{
    string Name { get; }
    Task<HealthCheckResult> CheckAsync(CancellationToken ct = default);
}
```

### DI 注册

```csharp
services.AddAgentTelemetry(config =>
{
    config.EnableLogging = true;
    config.EnableMetrics = true;
    config.EnableTracing = true;
});
```

---

## ⚡ Scaling 模块

### ICircuitBreaker

熔断器。

```csharp
public interface ICircuitBreaker
{
    CircuitState State { get; }
    Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct = default);
}
```

### ILoadBalancer

负载均衡。

```csharp
public interface ILoadBalancer
{
    void RegisterInstance(AgentInstance instance);
    AgentInstance? GetNextInstance();
}
```

### DI 注册

```csharp
services.AddScaling(options =>
{
    options.CircuitBreakerThreshold = 5;
    options.LoadBalancerStrategy = "RoundRobin";
});
```

---

## 📋 配置参考

### appsettings.json 完整示例

```json
{
  "LLM": {
    "ProviderType": "Ollama",
    "Model": "qwen2.5:0.5b",
    "Endpoint": "http://localhost:11434",
    "MaxTokens": 1024,
    "Temperature": 0.7
  },
  "Agent": {
    "MaxSteps": 5,
    "DefaultTimeout": 30
  },
  "Memory": {
    "Type": "Window",
    "WindowSize": 10
  },
  "Safety": {
    "EnableContentFilter": true,
    "EnableSensitiveDataFilter": true,
    "MaxInputLength": 10000
  },
  "Observability": {
    "EnableLogging": true,
    "EnableMetrics": true,
    "EnableTracing": true
  },
  "Scaling": {
    "CircuitBreakerThreshold": 5,
    "LoadBalancerStrategy": "RoundRobin"
  }
}
```

---

## 🔄 MCP 协议模块

> Model Context Protocol - 与 Claude Desktop、Cursor 等工具互操作

### IMCPServer

MCP 服务端接口。

```csharp
public interface IMCPServer
{
    string Name { get; }
    string Version { get; }
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}
```

### IMCPClient

MCP 客户端接口。

```csharp
public interface IMCPClient
{
    Task ConnectAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MCPToolDefinition>> ListToolsAsync(CancellationToken ct = default);
    Task<MCPToolCallResult> CallToolAsync(string name, object? args, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
}
```

### MCPToolProxy

将远程 MCP 工具包装为本地 ITool。

```csharp
// 连接远程 MCP Server 并获取工具代理
var client = sp.GetRequiredService<IMCPClient>();
await client.ConnectAsync();
var tools = await client.ListToolsAsync();

foreach (var tool in tools)
{
    var proxy = new MCPToolProxy(client, tool);
    toolRegistry.Register(proxy);
}
```

### DI 注册

```csharp
// 注册 MCP Server（暴露 Dawning 工具给外部）
services.AddMCPServer(options =>
{
    options.Name = "MyAgentServer";
    options.Version = "1.0.0";
});

// 注册 MCP Client（调用外部 MCP Server 的工具）
services.AddMCPClient(options =>
{
    options.Transport = MCPTransportType.Stdio;
    options.Command = "uvx";
    options.Arguments = ["mcp-server-filesystem"];
});
```

---

## 📊 工作流 DSL 模块

> 声明式工作流定义，支持条件分支、循环和可视化

### IWorkflow

工作流接口。

```csharp
public interface IWorkflow
{
    string Name { get; }
    IReadOnlyList<IWorkflowNode> Nodes { get; }
    Task<WorkflowResult> ExecuteAsync(WorkflowContext context, CancellationToken ct = default);
}
```

### WorkflowBuilder

流式工作流构建器。

```csharp
var workflow = new WorkflowBuilder("ResearchWorkflow")
    .StartWith<AgentNode>("research", agent: researchAgent)
    .Then<AgentNode>("draft", agent: writerAgent)
    .Condition(ctx => ctx.Get<int>("quality") < 7)
        .Then<AgentNode>("review", agent: editorAgent)
        .Loop(maxIterations: 3)
    .EndCondition()
    .Then<AgentNode>("publish", agent: publisherAgent)
    .Build();

var result = await workflow.ExecuteAsync(context);
```

### 节点类型

| 节点类型 | 说明 |
|----------|------|
| `AgentNode` | 执行 Agent |
| `ToolNode` | 执行单个工具 |
| `ConditionNode` | 条件分支 |
| `LoopNode` | 循环执行 |
| `ParallelNode` | 并行执行 |
| `DelayNode` | 延迟等待 |
| `HumanApprovalNode` | 人工审批 |

### YAML 定义

```yaml
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
```

### 可视化

```csharp
// 生成 Mermaid 图
var visualizer = new WorkflowVisualizer();
var mermaid = visualizer.ToMermaid(workflow);
Console.WriteLine(mermaid);
// graph TD
//   research[Research] --> draft[Draft]
//   draft --> review{Quality < 7?}
//   review -->|Yes| edit[Edit]
//   edit --> draft
//   review -->|No| publish[Publish]
```

---

## 📈 评估框架模块

> Agent 效果量化评估，支持 A/B 测试

### IAgentEvaluator

评估器接口。

```csharp
public interface IAgentEvaluator
{
    string Name { get; }
    Task<EvaluationMetric> EvaluateAsync(
        EvaluationTestCase testCase,
        AgentResponse response,
        CancellationToken ct = default);
}
```

### 评估测试用例

```csharp
var testCase = new EvaluationTestCase
{
    Input = "计算 15 + 27",
    ExpectedOutput = "42",
    ExpectedToolCalls = ["calculate"],
    Tags = ["math", "simple"],
};
```

### 内置评估器

| 评估器 | 说明 |
|--------|------|
| `KeywordMatchEvaluator` | 关键词匹配 |
| `ExactMatchEvaluator` | 精确匹配 |
| `ToolCallAccuracyEvaluator` | 工具调用准确率 |
| `LatencyEvaluator` | 响应延迟 |
| `LLMJudgeEvaluator` | LLM-as-Judge |

### A/B 测试

```csharp
var runner = new ABTestRunner(evaluator);

var resultA = await runner.RunAsync(agentA, testCases);
var resultB = await runner.RunAsync(agentB, testCases);

var comparison = runner.Compare(resultA, resultB);
Console.WriteLine($"Agent A 成功率: {comparison.AgentASuccessRate:P}");
Console.WriteLine($"Agent B 成功率: {comparison.AgentBSuccessRate:P}");
Console.WriteLine($"胜出: {comparison.Winner}");
```

### 报告生成

```csharp
var report = EvaluationReportGenerator.Generate(results);
await File.WriteAllTextAsync("report.md", report.ToMarkdown());
await File.WriteAllTextAsync("report.json", report.ToJson());
```

---

## 🖼️ 多模态模块

> Vision（图像理解）和 Audio（语音转文字/文字转语音）

### IVisionProvider

视觉处理接口。

```csharp
public interface IVisionProvider
{
    bool SupportsVision { get; }
    IReadOnlyList<string> SupportedImageFormats { get; }
    
    Task<VisionAnalysisResult> AnalyzeImageAsync(
        ImageContent image,
        string prompt,
        VisionOptions? options = null,
        CancellationToken ct = default);
    
    Task<VisionChatResponse> ChatWithVisionAsync(
        IReadOnlyList<MultimodalMessage> messages,
        VisionOptions? options = null,
        CancellationToken ct = default);
}
```

### 图像分析示例

```csharp
var provider = sp.GetRequiredService<IVisionProvider>();

// 从 URL 分析
var result = await provider.AnalyzeImageAsync(
    ImageContent.FromUrl("https://example.com/cat.jpg"),
    "描述这张图片中的内容"
);
Console.WriteLine(result.Description);

// 多轮视觉对话
var messages = new List<MultimodalMessage>
{
    MultimodalMessage.User(
        TextContent.Create("这是什么动物？"),
        await ImageContent.FromFileAsync("photo.jpg")
    ),
};
var response = await provider.ChatWithVisionAsync(messages);
```

### IAudioTranscriptionProvider

音频转录接口 (Speech-to-Text)。

```csharp
public interface IAudioTranscriptionProvider
{
    string Name { get; }
    IReadOnlyList<string> SupportedFormats { get; }
    long MaxFileSize { get; }
    
    Task<TranscriptionResult> TranscribeFileAsync(
        string filePath,
        TranscriptionOptions? options = null,
        CancellationToken ct = default);
}
```

### 音频转录示例

```csharp
var whisper = sp.GetRequiredService<IAudioTranscriptionProvider>();

var result = await whisper.TranscribeFileAsync("meeting.mp3", new TranscriptionOptions
{
    Language = "zh",
    ResponseFormat = TranscriptionFormat.VerboseJson,
    IncludeTimestamps = true,
});

Console.WriteLine(result.Text);
foreach (var segment in result.Segments ?? [])
{
    Console.WriteLine($"[{segment.Start:F1}s - {segment.End:F1}s] {segment.Text}");
}
```

### ITextToSpeechProvider

文字转语音接口 (Text-to-Speech)。

```csharp
public interface ITextToSpeechProvider
{
    string Name { get; }
    IReadOnlyList<VoiceInfo> AvailableVoices { get; }
    
    Task<SpeechResult> SynthesizeAsync(
        string text,
        SpeechOptions? options = null,
        CancellationToken ct = default);
    
    Task<SpeechResult> SynthesizeToFileAsync(
        string text,
        string outputPath,
        SpeechOptions? options = null,
        CancellationToken ct = default);
}
```

### 语音合成示例

```csharp
var tts = sp.GetRequiredService<ITextToSpeechProvider>();

// 合成并保存
var result = await tts.SynthesizeToFileAsync(
    "你好，欢迎使用 Dawning Agents 框架！",
    "output.mp3",
    new SpeechOptions
    {
        Voice = "nova",
        Speed = 1.0,
        OutputFormat = SpeechOutputFormat.Mp3,
    }
);

// 可用声音
foreach (var voice in tts.AvailableVoices)
{
    Console.WriteLine($"{voice.Id}: {voice.Name} ({voice.Gender})");
}
// alloy: Alloy (Neutral)
// echo: Echo (Male)
// nova: Nova (Female)
// shimmer: Shimmer (Female)
```

### DI 注册

```csharp
// Vision
services.AddOpenAIVision(configuration);

// Audio (Whisper + TTS)
services.AddOpenAIAudio(configuration);

// 或一次性注册所有多模态服务
services.AddOpenAIMultimodal(configuration);
```

---

## 🗄️ Vector Store 模块

> 真实向量数据库支持

### 可用实现

| 类 | 数据库 | 适用场景 |
|---|--------|----------|
| `InMemoryVectorStore` | 内存 | 开发/测试 |
| `QdrantVectorStore` | Qdrant | 生产 (自托管) |
| `PineconeVectorStore` | Pinecone | 生产 (云托管) |
| `RedisVectorStore` | Redis Stack | 生产 (已有 Redis) |
| `ChromaVectorStore` | Chroma | 轻量级 |
| `WeaviateVectorStore` | Weaviate | 企业级 |

### DI 注册

```csharp
// Qdrant
services.AddQdrantVectorStore(options =>
{
    options.Host = "localhost";
    options.Port = 6334;
    options.CollectionName = "documents";
});

// Pinecone
services.AddPineconeVectorStore(options =>
{
    options.ApiKey = "your-api-key";
    options.Environment = "us-east-1";
    options.IndexName = "documents";
});

// Redis
services.AddRedisVectorStore(options =>
{
    options.ConnectionString = "localhost:6379";
    options.IndexName = "documents";
});
```

---

## 🔀 模型路由模块

> 智能模型选择，成本/延迟优化

### IModelRouter

模型路由器接口。

```csharp
public interface IModelRouter
{
    string Name { get; }
    Task<ILLMProvider> SelectProviderAsync(ModelRoutingContext context, CancellationToken ct = default);
    IReadOnlyList<ILLMProvider> GetAvailableProviders();
    void ReportResult(ILLMProvider provider, ModelCallResult result);
}
```

### 路由策略

| 策略 | 说明 |
|------|------|
| `CostOptimized` | 选择最便宜的模型 |
| `LatencyOptimized` | 选择最快的模型 |
| `RoundRobin` | 轮询负载均衡 |
| `Random` | 随机选择 |

### 使用示例

```csharp
var router = sp.GetRequiredService<IModelRouter>();

var context = new ModelRoutingContext
{
    EstimatedInputTokens = 1000,
    EstimatedOutputTokens = 500,
    MaxLatencyMs = 2000,
    MaxCost = 0.01m,
};

var provider = await router.SelectProviderAsync(context);
var response = await provider.ChatAsync(messages);

// 报告结果用于学习
router.ReportResult(provider, ModelCallResult.Succeeded(
    latencyMs: 500,
    inputTokens: 1000,
    outputTokens: 450,
    cost: 0.005m
));
```

### DI 注册

```csharp
services.AddModelRouter(ModelRoutingStrategy.CostOptimized);
// 或
services.AddLatencyOptimizedRouter();
// 或
services.AddLoadBalancedRouter(ModelRoutingStrategy.RoundRobin);
```

---

> 📌 **提示**: 完整 API 文档请参考源码中的 XML 注释
