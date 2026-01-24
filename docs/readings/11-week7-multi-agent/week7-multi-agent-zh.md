# 第7周：多Agent架构

> 第四阶段：多Agent协作
> 第7周学习材料：编排模式与层级协调

---

## 第1-2天：多Agent基础

### 1. 为什么需要多个Agent？

单个Agent有其局限性：

- 复杂任务可能需要多种专业知识
- 长时间运行的任务可以从并行化中受益
- 关注点分离提高可维护性

```text
┌─────────────────────────────────────────────────────────────────┐
│                    多Agent的优势                                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────────┐  ┌────────────┐  ┌────────────┐                 │
│  │   专业化   │  │   并行化   │  │   模块化   │                 │
│  │   专长     │  │   处理     │  │   设计     │                 │
│  └────────────┘  └────────────┘  └────────────┘                 │
│                                                                  │
│  • 研究       • 多个查询     • 更容易                           │
│  • 编码       • 独立任务       测试                             │
│  • 审查       • 并发执行     • 可复用                           │
│  • 测试                        Agent                            │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. Agent团队接口

```csharp
namespace Dawning.Agents.Core.MultiAgent;

/// <summary>
/// 可参与多Agent工作流的Agent
/// </summary>
public interface ITeamAgent : IAgent
{
    /// <summary>
    /// Agent在团队中的角色
    /// </summary>
    string Role { get; }
    
    /// <summary>
    /// 此Agent提供的能力
    /// </summary>
    IReadOnlyList<string> Capabilities { get; }
    
    /// <summary>
    /// 此Agent是否能处理给定任务
    /// </summary>
    bool CanHandle(string task);
}

/// <summary>
/// 团队Agent基类
/// </summary>
public abstract class TeamAgentBase : AgentBase, ITeamAgent
{
    public abstract string Role { get; }
    public virtual IReadOnlyList<string> Capabilities { get; } = [];

    protected TeamAgentBase(
        ILLMProvider llm,
        ILogger logger,
        string name) : base(llm, logger, name)
    {
    }

    public virtual bool CanHandle(string task)
    {
        // 默认：检查是否有任何能力关键词匹配
        var taskLower = task.ToLowerInvariant();
        return Capabilities.Any(c => taskLower.Contains(c.ToLowerInvariant()));
    }
}
```

### 3. 任务定义

```csharp
namespace Dawning.Agents.Core.MultiAgent;

/// <summary>
/// 多Agent工作流中的任务
/// </summary>
public record AgentTask
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required string Description { get; init; }
    public string? AssignedAgentId { get; init; }
    public TaskStatus Status { get; init; } = TaskStatus.Pending;
    public string? Result { get; init; }
    public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
    public IList<string> Dependencies { get; init; } = [];
    public int Priority { get; init; } = 0;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; init; }
}

public enum TaskStatus
{
    Pending,     // 待处理
    InProgress,  // 进行中
    Completed,   // 已完成
    Failed,      // 失败
    Cancelled    // 已取消
}

/// <summary>
/// 多Agent工作流的结果
/// </summary>
public record WorkflowResult
{
    public required string WorkflowId { get; init; }
    public required IReadOnlyList<AgentTask> Tasks { get; init; }
    public bool IsSuccess { get; init; }
    public string? FinalOutput { get; init; }
    public TimeSpan Duration { get; init; }
    public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}
```

---

## 第3-4天：编排模式

### 1. 编排器接口

```csharp
namespace Dawning.Agents.Core.MultiAgent;

/// <summary>
/// 编排多个Agent的接口
/// </summary>
public interface IOrchestrator
{
    /// <summary>
    /// 用给定输入执行工作流
    /// </summary>
    Task<WorkflowResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 已注册的Agent
    /// </summary>
    IReadOnlyList<ITeamAgent> Agents { get; }
    
    /// <summary>
    /// 注册Agent
    /// </summary>
    void RegisterAgent(ITeamAgent agent);
}

/// <summary>
/// 带有通用功能的基础编排器
/// </summary>
public abstract class OrchestratorBase : IOrchestrator
{
    protected readonly List<ITeamAgent> _agents = [];
    protected readonly ILogger _logger;

    public IReadOnlyList<ITeamAgent> Agents => _agents.AsReadOnly();

    protected OrchestratorBase(ILogger logger)
    {
        _logger = logger;
    }

    public void RegisterAgent(ITeamAgent agent)
    {
        _agents.Add(agent);
        _logger.LogInformation("已注册Agent {Name}，角色为 {Role}", agent.Name, agent.Role);
    }

    public abstract Task<WorkflowResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default);
}
```

### 2. 顺序编排器

```csharp
namespace Dawning.Agents.Core.MultiAgent;

using Microsoft.Extensions.Logging;

/// <summary>
/// 按顺序执行Agent，将输出传递给下一个Agent
/// </summary>
public class SequentialOrchestrator : OrchestratorBase
{
    public SequentialOrchestrator(ILogger<SequentialOrchestrator> logger) : base(logger)
    {
    }

    public override async Task<WorkflowResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var workflowId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow;
        var tasks = new List<AgentTask>();
        var currentInput = input;

        _logger.LogInformation("启动顺序工作流 {Id}，共 {Count} 个Agent", 
            workflowId, _agents.Count);

        foreach (var agent in _agents)
        {
            var task = new AgentTask
            {
                Description = $"执行 {agent.Name}",
                AssignedAgentId = agent.Name,
                Status = TaskStatus.InProgress
            };

            try
            {
                _logger.LogDebug("执行Agent {Name}", agent.Name);

                var response = await agent.ExecuteAsync(new AgentContext
                {
                    Input = currentInput,
                    MaxIterations = 10
                }, cancellationToken);

                currentInput = response.Output;
                
                task = task with
                {
                    Status = response.IsSuccess ? TaskStatus.Completed : TaskStatus.Failed,
                    Result = response.Output,
                    CompletedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agent {Name} 失败", agent.Name);
                task = task with
                {
                    Status = TaskStatus.Failed,
                    Result = ex.Message,
                    CompletedAt = DateTime.UtcNow
                };
                
                tasks.Add(task);
                
                return new WorkflowResult
                {
                    WorkflowId = workflowId,
                    Tasks = tasks,
                    IsSuccess = false,
                    FinalOutput = $"工作流在 {agent.Name} 处失败：{ex.Message}",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            tasks.Add(task);
        }

        return new WorkflowResult
        {
            WorkflowId = workflowId,
            Tasks = tasks,
            IsSuccess = true,
            FinalOutput = currentInput,
            Duration = DateTime.UtcNow - startTime
        };
    }
}
```

### 3. 并行编排器

```csharp
namespace Dawning.Agents.Core.MultiAgent;

using Microsoft.Extensions.Logging;

/// <summary>
/// 并行执行Agent并聚合结果
/// </summary>
public class ParallelOrchestrator : OrchestratorBase
{
    private readonly IResultAggregator _aggregator;
    private readonly int _maxConcurrency;

    public ParallelOrchestrator(
        IResultAggregator aggregator,
        ILogger<ParallelOrchestrator> logger,
        int maxConcurrency = 5) : base(logger)
    {
        _aggregator = aggregator;
        _maxConcurrency = maxConcurrency;
    }

    public override async Task<WorkflowResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var workflowId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("启动并行工作流 {Id}，共 {Count} 个Agent", 
            workflowId, _agents.Count);

        var semaphore = new SemaphoreSlim(_maxConcurrency);
        var agentTasks = _agents.Select(agent => ExecuteAgentAsync(
            agent, input, semaphore, cancellationToken));

        var results = await Task.WhenAll(agentTasks);
        var tasks = results.ToList();

        // 聚合结果
        var successfulResults = tasks
            .Where(t => t.Status == TaskStatus.Completed)
            .Select(t => t.Result ?? "")
            .ToList();

        var finalOutput = await _aggregator.AggregateAsync(successfulResults, cancellationToken);

        return new WorkflowResult
        {
            WorkflowId = workflowId,
            Tasks = tasks,
            IsSuccess = tasks.All(t => t.Status == TaskStatus.Completed),
            FinalOutput = finalOutput,
            Duration = DateTime.UtcNow - startTime
        };
    }

    private async Task<AgentTask> ExecuteAgentAsync(
        ITeamAgent agent,
        string input,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        
        try
        {
            _logger.LogDebug("并行执行Agent {Name}", agent.Name);

            var response = await agent.ExecuteAsync(new AgentContext
            {
                Input = input,
                MaxIterations = 10
            }, cancellationToken);

            return new AgentTask
            {
                Description = $"执行 {agent.Name}",
                AssignedAgentId = agent.Name,
                Status = response.IsSuccess ? TaskStatus.Completed : TaskStatus.Failed,
                Result = response.Output,
                CompletedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent {Name} 失败", agent.Name);
            return new AgentTask
            {
                Description = $"执行 {agent.Name}",
                AssignedAgentId = agent.Name,
                Status = TaskStatus.Failed,
                Result = ex.Message,
                CompletedAt = DateTime.UtcNow
            };
        }
        finally
        {
            semaphore.Release();
        }
    }
}

/// <summary>
/// 聚合多个Agent的结果
/// </summary>
public interface IResultAggregator
{
    Task<string> AggregateAsync(
        IReadOnlyList<string> results,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 简单拼接聚合器
/// </summary>
public class ConcatAggregator : IResultAggregator
{
    public Task<string> AggregateAsync(
        IReadOnlyList<string> results,
        CancellationToken cancellationToken = default)
    {
        var combined = string.Join("\n\n---\n\n", results);
        return Task.FromResult(combined);
    }
}

/// <summary>
/// 基于LLM的结果聚合器
/// </summary>
public class LLMAggregator : IResultAggregator
{
    private readonly ILLMProvider _llm;

    public LLMAggregator(ILLMProvider llm)
    {
        _llm = llm;
    }

    public async Task<string> AggregateAsync(
        IReadOnlyList<string> results,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"""
            你的任务是将多个Agent的响应综合成一个连贯的答案。
            
            Agent响应：
            {string.Join("\n\n---\n\n", results.Select((r, i) => $"响应 {i + 1}:\n{r}"))}
            
            请提供一个全面的综合，需要：
            1. 结合所有响应的关键见解
            2. 解决任何矛盾
            3. 以清晰、有组织的方式呈现信息
            """;

        var response = await _llm.GenerateAsync(prompt, cancellationToken: cancellationToken);
        return response.Content;
    }
}
```

---

## 第5-7天：层级协调

### 1. 监督Agent

```csharp
namespace Dawning.Agents.Core.MultiAgent;

using Microsoft.Extensions.Logging;

/// <summary>
/// 将任务委派给工作Agent的监督者
/// </summary>
public class SupervisorAgent : TeamAgentBase
{
    private readonly IReadOnlyList<ITeamAgent> _workers;
    private readonly ILLMProvider _llm;

    public override string Role => "监督者";
    public override IReadOnlyList<string> Capabilities { get; } = ["委派", "协调", "监督"];

    public SupervisorAgent(
        ILLMProvider llm,
        IEnumerable<ITeamAgent> workers,
        ILogger<SupervisorAgent> logger,
        string name = "Supervisor") : base(llm, logger, name)
    {
        _llm = llm;
        _workers = workers.ToList();
    }

    public override async Task<AgentResponse> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        var steps = new List<AgentStep>();
        var startTime = DateTime.UtcNow;

        // 步骤1：分析任务并创建执行计划
        var plan = await CreatePlanAsync(context.Input, cancellationToken);
        steps.Add(new AgentStep
        {
            StepNumber = 1,
            Action = "规划",
            Input = context.Input,
            Output = plan.ToString()
        });

        // 步骤2：执行计划
        var results = new List<(string Agent, string Result)>();
        
        foreach (var task in plan.Tasks)
        {
            var worker = SelectWorker(task);
            if (worker == null)
            {
                _logger.LogWarning("未找到适合任务的工作者：{Task}", task);
                continue;
            }

            _logger.LogInformation("将任务委派给 {Worker}：{Task}", worker.Name, task);

            var response = await worker.ExecuteAsync(new AgentContext
            {
                Input = task,
                MaxIterations = context.MaxIterations
            }, cancellationToken);

            results.Add((worker.Name, response.Output));
            
            steps.Add(new AgentStep
            {
                StepNumber = steps.Count + 1,
                Action = $"委派给 {worker.Name}",
                Input = task,
                Output = response.Output
            });
        }

        // 步骤3：综合结果
        var finalOutput = await SynthesizeResultsAsync(
            context.Input, results, cancellationToken);

        return new AgentResponse
        {
            Output = finalOutput,
            IsSuccess = true,
            Steps = steps,
            TokensUsed = 0,
            Duration = DateTime.UtcNow - startTime
        };
    }

    private async Task<ExecutionPlan> CreatePlanAsync(
        string input,
        CancellationToken cancellationToken)
    {
        var workerDescriptions = string.Join("\n", 
            _workers.Select(w => $"- {w.Name} ({w.Role}): {string.Join(", ", w.Capabilities)}"));

        var prompt = $"""
            你是一个协调专业Agent团队的监督者。
            
            可用的工作者：
            {workerDescriptions}
            
            任务：{input}
            
            通过将任务分解为子任务来创建执行计划。
            对于每个子任务，确定应该由哪个工作者处理。
            
            以JSON格式响应：
            {{
                "tasks": [
                    {{"task": "子任务描述", "worker": "工作者名称"}},
                    ...
                ]
            }}
            """;

        var response = await _llm.GenerateAsync(prompt, cancellationToken: cancellationToken);
        return ParsePlan(response.Content);
    }

    private ITeamAgent? SelectWorker(string task)
    {
        // 找到最匹配的工作者
        return _workers.FirstOrDefault(w => w.CanHandle(task)) ?? _workers.FirstOrDefault();
    }

    private async Task<string> SynthesizeResultsAsync(
        string originalTask,
        List<(string Agent, string Result)> results,
        CancellationToken cancellationToken)
    {
        var resultsText = string.Join("\n\n", 
            results.Select(r => $"[{r.Agent}]\n{r.Result}"));

        var prompt = $"""
            原始任务：{originalTask}
            
            工作者结果：
            {resultsText}
            
            请将这些结果综合成一个全面的最终答案。
            """;

        var response = await _llm.GenerateAsync(prompt, cancellationToken: cancellationToken);
        return response.Content;
    }

    private ExecutionPlan ParsePlan(string content)
    {
        try
        {
            // 从内容中提取JSON
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}') + 1;
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content[jsonStart..jsonEnd];
                var plan = JsonSerializer.Deserialize<ExecutionPlan>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return plan ?? new ExecutionPlan();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析执行计划失败");
        }

        return new ExecutionPlan { Tasks = [content] };
    }

    private record ExecutionPlan
    {
        public List<string> Tasks { get; init; } = [];
    }
}
```

### 2. 专业工作Agent

```csharp
namespace Dawning.Agents.Core.MultiAgent.Workers;

using Microsoft.Extensions.Logging;

/// <summary>
/// 研究专家Agent
/// </summary>
public class ResearchAgent : TeamAgentBase
{
    public override string Role => "研究员";
    public override IReadOnlyList<string> Capabilities { get; } = 
        ["研究", "搜索", "查找", "查询", "信息"];

    public ResearchAgent(
        ILLMProvider llm,
        ILogger<ResearchAgent> logger) : base(llm, logger, "Researcher")
    {
    }

    protected override string GetDefaultSystemPrompt() => """
        你是一名研究专家。你的角色是：
        1. 查找给定主题的相关信息
        2. 综合多个来源的发现
        3. 呈现清晰、事实性的总结
        
        要全面但简洁。始终解释你的推理。
        """;
}

/// <summary>
/// 代码专家Agent
/// </summary>
public class CodeAgent : TeamAgentBase
{
    public override string Role => "编程员";
    public override IReadOnlyList<string> Capabilities { get; } = 
        ["代码", "编程", "实现", "开发", "修复", "调试"];

    public CodeAgent(
        ILLMProvider llm,
        ILogger<CodeAgent> logger) : base(llm, logger, "Coder")
    {
    }

    protected override string GetDefaultSystemPrompt() => """
        你是一名编程专家。你的角色是：
        1. 编写干净、高效的代码
        2. 调试和修复问题
        3. 清晰地解释代码
        
        遵循最佳实践并包含注释。
        """;
}

/// <summary>
/// 代码审查专家Agent
/// </summary>
public class ReviewAgent : TeamAgentBase
{
    public override string Role => "审查员";
    public override IReadOnlyList<string> Capabilities { get; } = 
        ["审查", "分析", "批评", "评估", "检查"];

    public ReviewAgent(
        ILLMProvider llm,
        ILogger<ReviewAgent> logger) : base(llm, logger, "Reviewer")
    {
    }

    protected override string GetDefaultSystemPrompt() => """
        你是一名代码审查专家。你的角色是：
        1. 审查代码中的错误和问题
        2. 建议改进
        3. 检查安全漏洞
        4. 确保代码遵循最佳实践
        
        反馈要有建设性且具体。
        """;
}

/// <summary>
/// 写作专家Agent
/// </summary>
public class WriterAgent : TeamAgentBase
{
    public override string Role => "写作员";
    public override IReadOnlyList<string> Capabilities { get; } = 
        ["写作", "文档", "解释", "描述", "总结"];

    public WriterAgent(
        ILLMProvider llm,
        ILogger<WriterAgent> logger) : base(llm, logger, "Writer")
    {
    }

    protected override string GetDefaultSystemPrompt() => """
        你是一名技术写作专家。你的角色是：
        1. 编写清晰的文档
        2. 创建用户友好的解释
        3. 总结复杂主题
        
        使用清晰的语言并有逻辑地组织内容。
        """;
}
```

### 3. 动态路由器

```csharp
namespace Dawning.Agents.Core.MultiAgent;

using Microsoft.Extensions.Logging;

/// <summary>
/// 动态将任务路由到适当的Agent
/// </summary>
public class DynamicRouter : OrchestratorBase
{
    private readonly ILLMProvider _llm;

    public DynamicRouter(
        ILLMProvider llm,
        ILogger<DynamicRouter> logger) : base(logger)
    {
        _llm = llm;
    }

    public override async Task<WorkflowResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var workflowId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow;
        var tasks = new List<AgentTask>();

        // 为任务选择最佳Agent
        var selectedAgent = await SelectAgentAsync(input, cancellationToken);
        
        if (selectedAgent == null)
        {
            return new WorkflowResult
            {
                WorkflowId = workflowId,
                Tasks = tasks,
                IsSuccess = false,
                FinalOutput = "未找到适合此任务的Agent",
                Duration = DateTime.UtcNow - startTime
            };
        }

        _logger.LogInformation("将任务路由到 {Agent}", selectedAgent.Name);

        var response = await selectedAgent.ExecuteAsync(new AgentContext
        {
            Input = input,
            MaxIterations = 10
        }, cancellationToken);

        tasks.Add(new AgentTask
        {
            Description = input,
            AssignedAgentId = selectedAgent.Name,
            Status = response.IsSuccess ? TaskStatus.Completed : TaskStatus.Failed,
            Result = response.Output,
            CompletedAt = DateTime.UtcNow
        });

        return new WorkflowResult
        {
            WorkflowId = workflowId,
            Tasks = tasks,
            IsSuccess = response.IsSuccess,
            FinalOutput = response.Output,
            Duration = DateTime.UtcNow - startTime
        };
    }

    private async Task<ITeamAgent?> SelectAgentAsync(
        string input,
        CancellationToken cancellationToken)
    {
        if (_agents.Count == 0) return null;
        if (_agents.Count == 1) return _agents[0];

        var agentDescriptions = string.Join("\n",
            _agents.Select((a, i) => $"{i + 1}. {a.Name} ({a.Role}): {string.Join(", ", a.Capabilities)}"));

        var prompt = $"""
            给定以下任务，选择最适合处理它的Agent。
            
            任务：{input}
            
            可用Agent：
            {agentDescriptions}
            
            只响应最佳Agent的编号（例如，"1"或"2"）。
            """;

        var response = await _llm.GenerateAsync(prompt, cancellationToken: cancellationToken);
        
        if (int.TryParse(response.Content.Trim(), out var index) && 
            index >= 1 && index <= _agents.Count)
        {
            return _agents[index - 1];
        }

        // 回退：通过能力匹配查找
        return _agents.FirstOrDefault(a => a.CanHandle(input)) ?? _agents[0];
    }
}
```

---

## 完整示例

```csharp
// 创建专业Agent
var llm = new OpenAIProvider(apiKey, logger);

var researcher = new ResearchAgent(llm, loggerFactory.CreateLogger<ResearchAgent>());
var coder = new CodeAgent(llm, loggerFactory.CreateLogger<CodeAgent>());
var reviewer = new ReviewAgent(llm, loggerFactory.CreateLogger<ReviewAgent>());
var writer = new WriterAgent(llm, loggerFactory.CreateLogger<WriterAgent>());

// 选项1：顺序工作流（例如，研究 → 编码 → 审查）
var sequential = new SequentialOrchestrator(logger);
sequential.RegisterAgent(researcher);
sequential.RegisterAgent(coder);
sequential.RegisterAgent(reviewer);

var result = await sequential.ExecuteAsync(
    "创建一个验证电子邮件地址的函数");

// 选项2：并行工作流
var parallel = new ParallelOrchestrator(
    new LLMAggregator(llm),
    loggerFactory.CreateLogger<ParallelOrchestrator>());
parallel.RegisterAgent(researcher);
parallel.RegisterAgent(coder);

var parallelResult = await parallel.ExecuteAsync(
    "解释REST API最佳实践");

// 选项3：监督者模式
var supervisor = new SupervisorAgent(
    llm,
    [researcher, coder, reviewer, writer],
    loggerFactory.CreateLogger<SupervisorAgent>());

var supervisedResult = await supervisor.ExecuteAsync(new AgentContext
{
    Input = "构建一个带验证的用户注册系统",
    MaxIterations = 20
});
```

---

## 总结

### 第7周交付物

```
src/Dawning.Agents.Core/
└── MultiAgent/
    ├── ITeamAgent.cs              # 团队Agent接口
    ├── TeamAgentBase.cs           # 基础实现
    ├── AgentTask.cs               # 任务模型
    ├── WorkflowResult.cs          # 结果模型
    ├── IOrchestrator.cs           # 编排器接口
    ├── SequentialOrchestrator.cs  # 顺序执行
    ├── ParallelOrchestrator.cs    # 并行执行
    ├── IResultAggregator.cs       # 聚合接口
    ├── ConcatAggregator.cs        # 简单聚合器
    ├── LLMAggregator.cs           # LLM聚合器
    ├── SupervisorAgent.cs         # 层级监督者
    ├── DynamicRouter.cs           # 动态路由
    └── Workers/
        ├── ResearchAgent.cs       # 研究专家
        ├── CodeAgent.cs           # 编程专家
        ├── ReviewAgent.cs         # 审查专家
        └── WriterAgent.cs         # 写作专家
```

### 编排模式

| 模式 | 用例 | 执行方式 |
|------|------|----------|
| **顺序** | 流水线工作流 | 依次执行 |
| **并行** | 独立任务 | 并发执行 |
| **监督者** | 复杂委派 | 层级控制 |
| **路由器** | 动态选择 | 最佳匹配路由 |

### 下一步：第8周

第8周将涵盖Agent通信：
- 消息传递模式
- 共享状态管理
- 协作协议
