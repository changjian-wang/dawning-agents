# Dawning.Agents 开发指南

## 项目概述

Dawning.Agents 是一个 .NET 企业级 AI Agent 框架，设计灵感来自 OpenAI Agents SDK 的极简风格。

## 核心设计原则

### 1. 极简 API

- API 越少越好，合理默认值
- 一行完成核心功能注册
- 避免 Builder 模式的过度设计

```csharp
// ✅ 好
services.AddLLMProvider(configuration);

// ❌ 避免
services.AddLLMProvider(builder => builder.UseFactory(...).WithRetry(...));
```

### 2. 纯 DI 架构

- 所有服务通过依赖注入获取
- 不提供静态工厂或直接 new 实例的方式
- 保持架构一致性

```csharp
// ✅ 唯一方式
var provider = serviceProvider.GetRequiredService<ILLMProvider>();

// ❌ 禁止
var provider = new OllamaProvider("model");
```

### 3. 企业级基础设施

必须支持：
- `IHttpClientFactory` - HttpClient 生命周期管理
- `ILogger<T>` - 结构化日志
- `IOptions<T>` + `IConfiguration` - 配置绑定
- `CancellationToken` - 所有异步方法

### 4. 破坏性修改优先

- 开发阶段允许破坏性修改
- 不使用 `[Obsolete]` 过渡
- 直接删除旧 API，保持代码简洁

### 5. 接口与实现分离

```
Dawning.Agents.Abstractions/  → 接口、数据模型（零依赖）
├── LLM/                      → LLM 相关接口
│   ├── ILLMProvider.cs
│   ├── ChatMessage.cs
│   └── LLMOptions.cs
├── Agent/                    → Agent 相关接口
│   ├── IAgent.cs
│   ├── AgentContext.cs
│   ├── AgentStep.cs
│   ├── AgentResponse.cs
│   └── AgentOptions.cs
└── Prompts/                  → 提示词模板接口
    └── IPromptTemplate.cs

Dawning.Agents.Core/          → 核心实现、DI 扩展
├── LLM/
│   ├── OllamaProvider.cs
│   └── LLMServiceCollectionExtensions.cs
├── Agent/
│   ├── AgentBase.cs
│   ├── ReActAgent.cs
│   └── AgentServiceCollectionExtensions.cs
└── Prompts/
    ├── PromptTemplate.cs
    └── AgentPrompts.cs

Dawning.Agents.{Provider}/    → 具体提供者实现
```

### 6. 配置驱动

- 通过 appsettings.json 切换行为
- 支持环境变量覆盖
- 不硬编码配置值

## 代码格式（CSharpier）

项目使用 CSharpier 进行代码格式化，关键规则：

- **长参数列表**：每个参数独占一行

```csharp
// ✅ 好 - 多参数换行
public MyService(
    ILLMProvider llmProvider,
    IOptions<MyOptions> options,
    ILogger<MyService>? logger = null
)
{
}

// ❌ 避免 - 单行过长
public MyService(ILLMProvider llmProvider, IOptions<MyOptions> options, ILogger<MyService>? logger = null)
```

- **集合初始化**：元素换行，尾随逗号

```csharp
// ✅ 好
var messages = new List<ChatMessage>
{
    new("system", systemPrompt),
    new("user", userInput),
};

// ❌ 避免
var messages = new List<ChatMessage> { new("system", systemPrompt), new("user", userInput) };
```

- **方法链**：每个调用独占一行

```csharp
// ✅ 好
var result = items
    .Where(x => x.IsActive)
    .Select(x => x.Name)
    .ToList();
```

- **if 语句**：始终使用大括号

```csharp
// ✅ 好
if (condition)
{
    DoSomething();
}

// ❌ 避免
if (condition)
    DoSomething();
```

## 命名规范

| 类型 | 规范 | 示例 |
|------|------|------|
| 接口 | `I` 前缀 | `ILLMProvider`, `IAgent` |
| 配置类 | `Options` 后缀 | `LLMOptions`, `AgentOptions` |
| DI 扩展 | `Add` 前缀 | `AddLLMProvider`, `AddAgent` |
| 异步方法 | `Async` 后缀 | `ChatAsync`, `RunAsync` |
| 流式方法 | `StreamAsync` 后缀 | `ChatStreamAsync` |
| 提供者 | `Provider` 后缀 | `OllamaProvider` |

## 代码模板

### 新增服务接口

```csharp
namespace Dawning.Agents.Abstractions;

/// <summary>
/// 服务描述
/// </summary>
public interface IMyService
{
    Task<Result> DoSomethingAsync(
        Input input,
        CancellationToken cancellationToken = default);
}
```

### 新增服务实现

```csharp
namespace Dawning.Agents.Core;

public class MyService : IMyService
{
    private readonly ILogger<MyService> _logger;

    public MyService(ILogger<MyService>? logger = null)
    {
        _logger = logger ?? NullLogger<MyService>.Instance;
    }

    public async Task<Result> DoSomethingAsync(
        Input input,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("开始处理...");
        // 实现
    }
}
```

### 新增 DI 扩展

```csharp
namespace Dawning.Agents.Core;

public static class MyServiceExtensions
{
    public static IServiceCollection AddMyService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MyOptions>(
            configuration.GetSection(MyOptions.SectionName));
        
        services.TryAddSingleton<IMyService, MyService>();
        
        return services;
    }
}
```

### 新增配置类

```csharp
namespace Dawning.Agents.Abstractions;

/// <summary>
/// 服务配置选项
/// </summary>
/// <remarks>
/// appsettings.json 示例:
/// <code>
/// { "My": { "Option1": "value" } }
/// </code>
/// </remarks>
public class MyOptions
{
    public const string SectionName = "My";
    
    public string Option1 { get; set; } = "default";
    
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Option1))
            throw new InvalidOperationException("Option1 is required");
    }
}
```

## 新功能检查清单

新增功能前确认：

- [ ] 是否通过 DI 注入？
- [ ] 是否支持 `ILogger`？
- [ ] 是否支持 `CancellationToken`？
- [ ] 是否有 XML 文档注释？
- [ ] API 是否足够简洁？
- [ ] 配置是否支持 `IConfiguration`？
- [ ] 是否有单元测试？

## 技术栈

- .NET 10.0
- 本地 LLM: Ollama
- 远程 LLM: OpenAI, Azure OpenAI
- 测试: xUnit, FluentAssertions, Moq

---

## 已完成功能

### Tools/Skills 系统 ✅ (Week 5 已完成)

Tools 是 Agent 的"手"和"眼"，允许 Agent 与外部系统交互。

**核心接口（已实现）：**

```csharp
public interface ITool
{
    string Name { get; }
    string Description { get; }
    string ParametersSchema { get; }
    bool RequiresConfirmation { get; }  // 是否需要确认
    ToolRiskLevel RiskLevel { get; }    // Low/Medium/High
    string? Category { get; }           // 工具分类
    Task<ToolResult> ExecuteAsync(string input, CancellationToken ct = default);
}

public interface IToolRegistry
{
    void Register(ITool tool);
    void RegisterToolsFromType<T>() where T : class;
    ITool? GetTool(string name);
    IReadOnlyList<ITool> GetAllTools();
    IReadOnlyList<ITool> GetToolsByCategory(string category);
}
```

**使用特性标记（已实现）：**

```csharp
[FunctionTool(
    "删除文件",
    RequiresConfirmation = true,
    RiskLevel = ToolRiskLevel.High,
    Category = "FileSystem"
)]
public string DeleteFile(string path) => File.Delete(path);
```

**内置工具（64 个方法）：**
- `DateTimeTool` (4) - 日期时间
- `MathTool` (8) - 数学计算
- `JsonTool` (4) - JSON 处理
- `UtilityTool` (5) - 实用工具
- `FileSystemTool` (13) - 文件操作
- `HttpTool` (6) - HTTP 请求
- `ProcessTool` (6) - 进程管理
- `GitTool` (18) - Git 操作

**DI 注册方式（已实现）：**

```csharp
services.AddAllBuiltInTools();     // 所有内置工具
services.AddBuiltInTools();        // 只有安全工具
services.AddFileSystemTools();     // 按类别注册
services.AddToolsFromAssembly(typeof(Program).Assembly);
```

### Agent 核心循环 ✅ (Week 3 已完成)

```csharp
public interface IAgent
{
    string Name { get; }
    string Instructions { get; }
    IReadOnlyList<ITool> Tools { get; }
    Task<AgentResponse> RunAsync(string input, CancellationToken ct = default);
}

// 使用方式
var agent = sp.GetRequiredService<IAgent>();
var response = await agent.RunAsync("今天北京天气怎么样？");
```

---

### Tool Sets 与 Virtual Tools ✅ (Week 5.5 已完成)

参考 GitHub Copilot 的工具管理策略，实现了工具分组、虚拟工具、智能选择和审批流程。

**Tool Sets（工具集）：**

```csharp
public interface IToolSet
{
    string Name { get; }
    string Description { get; }
    string? Icon { get; }
    IReadOnlyList<ITool> Tools { get; }
    int Count { get; }
    ITool? GetTool(string toolName);
    bool Contains(string toolName);
}

// 使用方式
var mathTools = ToolSet.FromType<MathTool>("math", "数学计算工具集");
services.AddToolSet(mathTools);
```

**Virtual Tools（虚拟工具）：**

```csharp
public interface IVirtualTool : ITool
{
    IReadOnlyList<ITool> ExpandedTools { get; }
    bool IsExpanded { get; }
    IToolSet ToolSet { get; }
    void Expand();
    void Collapse();
}

// LLM 先看到虚拟工具摘要，需要时再展开
var gitVirtual = VirtualTool.FromType<GitTool>("git", "Git 版本控制工具集");
```

**Tool Selector（智能选择）：**

```csharp
public interface IToolSelector
{
    Task<IReadOnlyList<ITool>> SelectToolsAsync(
        string query,
        IReadOnlyList<ITool> availableTools,
        int maxTools = 20,
        CancellationToken ct = default);
}

// 使用方式
services.AddToolSelector();
var selector = sp.GetRequiredService<IToolSelector>();
var tools = await selector.SelectToolsAsync("计算文件大小", allTools, maxTools: 10);
```

**Tool Approval（审批流程）：**

```csharp
public enum ApprovalStrategy
{
    AlwaysApprove,   // 开发/测试环境
    AlwaysDeny,      // 安全敏感环境
    RiskBased,       // 基于风险等级（推荐）
    Interactive      // 交互式确认
}

public interface IToolApprovalHandler
{
    Task<bool> RequestApprovalAsync(ITool tool, string input, CancellationToken ct);
    Task<bool> RequestUrlApprovalAsync(ITool tool, string url, CancellationToken ct);
    Task<bool> RequestCommandApprovalAsync(ITool tool, string command, CancellationToken ct);
}

// 使用方式
services.AddToolApprovalHandler(ApprovalStrategy.RiskBased);
```

**DI 注册方式（已实现）：**

```csharp
// 工具选择器和审批处理器
services.AddToolSelector();
services.AddToolApprovalHandler(ApprovalStrategy.RiskBased);

// 工具集
services.AddToolSet(new ToolSet("math", "数学工具", mathTools));
services.AddToolSetFrom<MathTool>("math", "数学计算工具集");

// 虚拟工具
services.AddVirtualTool(new VirtualTool(toolSet));
services.AddVirtualToolFrom<GitTool>("git", "Git 版本控制工具集", "🔧");
```

---

## 未来功能规划

### Memory 系统 (Week 4)

```csharp
public interface IConversationMemory
{
    void AddMessage(ChatMessage message);
    IReadOnlyList<ChatMessage> GetMessages(int? limit = null);
    Task<string> SummarizeAsync(CancellationToken ct = default);
}

// 实现类型
// - BufferMemory: 滑动窗口
// - SummaryMemory: 对话摘要
// - TokenLimitMemory: Token 限制
```

### Handoff 多 Agent 协作 (Week 7-8)

```csharp
// OpenAI Agents SDK 风格的 Agent 切换
var triageAgent = new Agent
{
    Name = "Triage",
    Instructions = "分析用户请求并分配给专家",
    Handoffs = [researchAgent, writerAgent]
};
```

### Guardrails 安全护栏 (Week 9)

```csharp
// 输入/输出验证
var agent = new Agent
{
    InputGuardrails = [contentFilter],
    OutputGuardrails = [piiFilter]
};
```
