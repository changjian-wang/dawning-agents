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

> 📌 **提示**: 完整 API 文档请参考源码中的 XML 注释
