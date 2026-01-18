# Week 7: Multi-Agent Architecture

> Phase 4: Multi-Agent Collaboration
> Week 7 Learning Material: Orchestration Patterns & Hierarchical Coordination

---

## Day 1-2: Multi-Agent Fundamentals

### 1. Why Multiple Agents?

Single agents have limitations:
- Complex tasks may require diverse expertise
- Long-running tasks benefit from parallelization
- Separation of concerns improves maintainability

```text
┌─────────────────────────────────────────────────────────────────┐
│                    Multi-Agent Benefits                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────────┐  ┌────────────┐  ┌────────────┐                 │
│  │ Specialized│  │  Parallel  │  │  Modular   │                 │
│  │  Expertise │  │ Processing │  │   Design   │                 │
│  └────────────┘  └────────────┘  └────────────┘                 │
│                                                                  │
│  • Research     • Multiple     • Easier                         │
│  • Coding         queries       testing                         │
│  • Review       • Independent  • Reusable                       │
│  • Testing        tasks         agents                          │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. Agent Team Interface

```csharp
namespace Dawning.Agents.Core.MultiAgent;

/// <summary>
/// Represents an agent that can participate in multi-agent workflows
/// </summary>
public interface ITeamAgent : IAgent
{
    /// <summary>
    /// Agent's role in the team
    /// </summary>
    string Role { get; }
    
    /// <summary>
    /// Capabilities this agent provides
    /// </summary>
    IReadOnlyList<string> Capabilities { get; }
    
    /// <summary>
    /// Whether this agent can handle the given task
    /// </summary>
    bool CanHandle(string task);
}

/// <summary>
/// Base class for team agents
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
        // Default: check if any capability keyword matches
        var taskLower = task.ToLowerInvariant();
        return Capabilities.Any(c => taskLower.Contains(c.ToLowerInvariant()));
    }
}
```

### 3. Task Definition

```csharp
namespace Dawning.Agents.Core.MultiAgent;

/// <summary>
/// Represents a task in a multi-agent workflow
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
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Result from a multi-agent workflow
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

## Day 3-4: Orchestration Patterns

### 1. Orchestrator Interface

```csharp
namespace Dawning.Agents.Core.MultiAgent;

/// <summary>
/// Interface for orchestrating multiple agents
/// </summary>
public interface IOrchestrator
{
    /// <summary>
    /// Execute a workflow with the given input
    /// </summary>
    Task<WorkflowResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Registered agents
    /// </summary>
    IReadOnlyList<ITeamAgent> Agents { get; }
    
    /// <summary>
    /// Register an agent
    /// </summary>
    void RegisterAgent(ITeamAgent agent);
}

/// <summary>
/// Base orchestrator with common functionality
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
        _logger.LogInformation("Registered agent {Name} with role {Role}", agent.Name, agent.Role);
    }

    public abstract Task<WorkflowResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default);
}
```

### 2. Sequential Orchestrator

```csharp
namespace Dawning.Agents.Core.MultiAgent;

using Microsoft.Extensions.Logging;

/// <summary>
/// Executes agents in sequence, passing output to next agent
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

        _logger.LogInformation("Starting sequential workflow {Id} with {Count} agents", 
            workflowId, _agents.Count);

        foreach (var agent in _agents)
        {
            var task = new AgentTask
            {
                Description = $"Execute {agent.Name}",
                AssignedAgentId = agent.Name,
                Status = TaskStatus.InProgress
            };

            try
            {
                _logger.LogDebug("Executing agent {Name}", agent.Name);

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
                _logger.LogError(ex, "Agent {Name} failed", agent.Name);
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
                    FinalOutput = $"Workflow failed at {agent.Name}: {ex.Message}",
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

### 3. Parallel Orchestrator

```csharp
namespace Dawning.Agents.Core.MultiAgent;

using Microsoft.Extensions.Logging;

/// <summary>
/// Executes agents in parallel and aggregates results
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

        _logger.LogInformation("Starting parallel workflow {Id} with {Count} agents", 
            workflowId, _agents.Count);

        var semaphore = new SemaphoreSlim(_maxConcurrency);
        var agentTasks = _agents.Select(agent => ExecuteAgentAsync(
            agent, input, semaphore, cancellationToken));

        var results = await Task.WhenAll(agentTasks);
        var tasks = results.ToList();

        // Aggregate results
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
            _logger.LogDebug("Executing agent {Name} in parallel", agent.Name);

            var response = await agent.ExecuteAsync(new AgentContext
            {
                Input = input,
                MaxIterations = 10
            }, cancellationToken);

            return new AgentTask
            {
                Description = $"Execute {agent.Name}",
                AssignedAgentId = agent.Name,
                Status = response.IsSuccess ? TaskStatus.Completed : TaskStatus.Failed,
                Result = response.Output,
                CompletedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent {Name} failed", agent.Name);
            return new AgentTask
            {
                Description = $"Execute {agent.Name}",
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
/// Aggregates results from multiple agents
/// </summary>
public interface IResultAggregator
{
    Task<string> AggregateAsync(
        IReadOnlyList<string> results,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Simple concatenation aggregator
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
/// LLM-based result aggregator
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
            You are tasked with synthesizing multiple agent responses into a single coherent answer.
            
            Agent Responses:
            {string.Join("\n\n---\n\n", results.Select((r, i) => $"Response {i + 1}:\n{r}"))}
            
            Please provide a comprehensive synthesis that:
            1. Combines the key insights from all responses
            2. Resolves any contradictions
            3. Presents the information in a clear, organized manner
            """;

        var response = await _llm.GenerateAsync(prompt, cancellationToken: cancellationToken);
        return response.Content;
    }
}
```

---

## Day 5-7: Hierarchical Coordination

### 1. Supervisor Agent

```csharp
namespace Dawning.Agents.Core.MultiAgent;

using Microsoft.Extensions.Logging;

/// <summary>
/// Supervisor that delegates tasks to worker agents
/// </summary>
public class SupervisorAgent : TeamAgentBase
{
    private readonly IReadOnlyList<ITeamAgent> _workers;
    private readonly ILLMProvider _llm;

    public override string Role => "Supervisor";
    public override IReadOnlyList<string> Capabilities { get; } = ["delegate", "coordinate", "supervise"];

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

        // Step 1: Analyze task and create execution plan
        var plan = await CreatePlanAsync(context.Input, cancellationToken);
        steps.Add(new AgentStep
        {
            StepNumber = 1,
            Action = "Plan",
            Input = context.Input,
            Output = plan.ToString()
        });

        // Step 2: Execute plan
        var results = new List<(string Agent, string Result)>();
        
        foreach (var task in plan.Tasks)
        {
            var worker = SelectWorker(task);
            if (worker == null)
            {
                _logger.LogWarning("No suitable worker found for task: {Task}", task);
                continue;
            }

            _logger.LogInformation("Delegating task to {Worker}: {Task}", worker.Name, task);

            var response = await worker.ExecuteAsync(new AgentContext
            {
                Input = task,
                MaxIterations = context.MaxIterations
            }, cancellationToken);

            results.Add((worker.Name, response.Output));
            
            steps.Add(new AgentStep
            {
                StepNumber = steps.Count + 1,
                Action = $"Delegate to {worker.Name}",
                Input = task,
                Output = response.Output
            });
        }

        // Step 3: Synthesize results
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
            You are a supervisor coordinating a team of specialized agents.
            
            Available workers:
            {workerDescriptions}
            
            Task: {input}
            
            Create an execution plan by breaking down the task into subtasks.
            For each subtask, identify which worker should handle it.
            
            Respond in JSON format:
            {{
                "tasks": [
                    {{"task": "subtask description", "worker": "worker name"}},
                    ...
                ]
            }}
            """;

        var response = await _llm.GenerateAsync(prompt, cancellationToken: cancellationToken);
        return ParsePlan(response.Content);
    }

    private ITeamAgent? SelectWorker(string task)
    {
        // Find best matching worker
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
            Original task: {originalTask}
            
            Worker results:
            {resultsText}
            
            Please synthesize these results into a comprehensive final answer.
            """;

        var response = await _llm.GenerateAsync(prompt, cancellationToken: cancellationToken);
        return response.Content;
    }

    private ExecutionPlan ParsePlan(string content)
    {
        try
        {
            // Extract JSON from content
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
            _logger.LogWarning(ex, "Failed to parse execution plan");
        }

        return new ExecutionPlan { Tasks = [content] };
    }

    private record ExecutionPlan
    {
        public List<string> Tasks { get; init; } = [];
    }
}
```

### 2. Specialized Worker Agents

```csharp
namespace Dawning.Agents.Core.MultiAgent.Workers;

using Microsoft.Extensions.Logging;

/// <summary>
/// Research specialist agent
/// </summary>
public class ResearchAgent : TeamAgentBase
{
    public override string Role => "Researcher";
    public override IReadOnlyList<string> Capabilities { get; } = 
        ["research", "search", "find", "lookup", "information"];

    public ResearchAgent(
        ILLMProvider llm,
        ILogger<ResearchAgent> logger) : base(llm, logger, "Researcher")
    {
    }

    protected override string GetDefaultSystemPrompt() => """
        You are a research specialist. Your role is to:
        1. Find relevant information on the given topic
        2. Synthesize findings from multiple sources
        3. Present clear, factual summaries
        
        Be thorough but concise. Always cite your reasoning.
        """;
}

/// <summary>
/// Code specialist agent
/// </summary>
public class CodeAgent : TeamAgentBase
{
    public override string Role => "Coder";
    public override IReadOnlyList<string> Capabilities { get; } = 
        ["code", "program", "implement", "develop", "fix", "debug"];

    public CodeAgent(
        ILLMProvider llm,
        ILogger<CodeAgent> logger) : base(llm, logger, "Coder")
    {
    }

    protected override string GetDefaultSystemPrompt() => """
        You are a coding specialist. Your role is to:
        1. Write clean, efficient code
        2. Debug and fix issues
        3. Explain code clearly
        
        Follow best practices and include comments.
        """;
}

/// <summary>
/// Code review specialist agent
/// </summary>
public class ReviewAgent : TeamAgentBase
{
    public override string Role => "Reviewer";
    public override IReadOnlyList<string> Capabilities { get; } = 
        ["review", "analyze", "critique", "evaluate", "check"];

    public ReviewAgent(
        ILLMProvider llm,
        ILogger<ReviewAgent> logger) : base(llm, logger, "Reviewer")
    {
    }

    protected override string GetDefaultSystemPrompt() => """
        You are a code review specialist. Your role is to:
        1. Review code for bugs and issues
        2. Suggest improvements
        3. Check for security vulnerabilities
        4. Ensure code follows best practices
        
        Be constructive and specific in your feedback.
        """;
}

/// <summary>
/// Writing specialist agent
/// </summary>
public class WriterAgent : TeamAgentBase
{
    public override string Role => "Writer";
    public override IReadOnlyList<string> Capabilities { get; } = 
        ["write", "document", "explain", "describe", "summarize"];

    public WriterAgent(
        ILLMProvider llm,
        ILogger<WriterAgent> logger) : base(llm, logger, "Writer")
    {
    }

    protected override string GetDefaultSystemPrompt() => """
        You are a technical writing specialist. Your role is to:
        1. Write clear documentation
        2. Create user-friendly explanations
        3. Summarize complex topics
        
        Use clear language and organize content logically.
        """;
}
```

### 3. Dynamic Router

```csharp
namespace Dawning.Agents.Core.MultiAgent;

using Microsoft.Extensions.Logging;

/// <summary>
/// Routes tasks to appropriate agents dynamically
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

        // Select best agent for the task
        var selectedAgent = await SelectAgentAsync(input, cancellationToken);
        
        if (selectedAgent == null)
        {
            return new WorkflowResult
            {
                WorkflowId = workflowId,
                Tasks = tasks,
                IsSuccess = false,
                FinalOutput = "No suitable agent found for this task",
                Duration = DateTime.UtcNow - startTime
            };
        }

        _logger.LogInformation("Routing task to {Agent}", selectedAgent.Name);

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
            Given the following task, select the most appropriate agent to handle it.
            
            Task: {input}
            
            Available agents:
            {agentDescriptions}
            
            Respond with just the number of the best agent (e.g., "1" or "2").
            """;

        var response = await _llm.GenerateAsync(prompt, cancellationToken: cancellationToken);
        
        if (int.TryParse(response.Content.Trim(), out var index) && 
            index >= 1 && index <= _agents.Count)
        {
            return _agents[index - 1];
        }

        // Fallback: find by capability matching
        return _agents.FirstOrDefault(a => a.CanHandle(input)) ?? _agents[0];
    }
}
```

---

## Complete Example

```csharp
// Create specialized agents
var llm = new OpenAIProvider(apiKey, logger);

var researcher = new ResearchAgent(llm, loggerFactory.CreateLogger<ResearchAgent>());
var coder = new CodeAgent(llm, loggerFactory.CreateLogger<CodeAgent>());
var reviewer = new ReviewAgent(llm, loggerFactory.CreateLogger<ReviewAgent>());
var writer = new WriterAgent(llm, loggerFactory.CreateLogger<WriterAgent>());

// Option 1: Sequential workflow (e.g., research → code → review)
var sequential = new SequentialOrchestrator(logger);
sequential.RegisterAgent(researcher);
sequential.RegisterAgent(coder);
sequential.RegisterAgent(reviewer);

var result = await sequential.ExecuteAsync(
    "Create a function to validate email addresses");

// Option 2: Parallel workflow
var parallel = new ParallelOrchestrator(
    new LLMAggregator(llm),
    loggerFactory.CreateLogger<ParallelOrchestrator>());
parallel.RegisterAgent(researcher);
parallel.RegisterAgent(coder);

var parallelResult = await parallel.ExecuteAsync(
    "Explain REST API best practices");

// Option 3: Supervisor pattern
var supervisor = new SupervisorAgent(
    llm,
    [researcher, coder, reviewer, writer],
    loggerFactory.CreateLogger<SupervisorAgent>());

var supervisedResult = await supervisor.ExecuteAsync(new AgentContext
{
    Input = "Build a user registration system with validation",
    MaxIterations = 20
});
```

---

## Summary

### Week 7 Deliverables

```
src/Dawning.Agents.Core/
└── MultiAgent/
    ├── ITeamAgent.cs              # Team agent interface
    ├── TeamAgentBase.cs           # Base implementation
    ├── AgentTask.cs               # Task model
    ├── WorkflowResult.cs          # Result model
    ├── IOrchestrator.cs           # Orchestrator interface
    ├── SequentialOrchestrator.cs  # Sequential execution
    ├── ParallelOrchestrator.cs    # Parallel execution
    ├── IResultAggregator.cs       # Aggregation interface
    ├── ConcatAggregator.cs        # Simple aggregator
    ├── LLMAggregator.cs           # LLM aggregator
    ├── SupervisorAgent.cs         # Hierarchical supervisor
    ├── DynamicRouter.cs           # Dynamic routing
    └── Workers/
        ├── ResearchAgent.cs       # Research specialist
        ├── CodeAgent.cs           # Coding specialist
        ├── ReviewAgent.cs         # Review specialist
        └── WriterAgent.cs         # Writing specialist
```

### Orchestration Patterns

| Pattern | Use Case | Execution |
|---------|----------|-----------|
| **Sequential** | Pipeline workflows | One after another |
| **Parallel** | Independent tasks | Concurrent execution |
| **Supervisor** | Complex delegation | Hierarchical control |
| **Router** | Dynamic selection | Best-fit routing |

### Next: Week 8

Week 8 will cover agent communication:
- Message passing patterns
- Shared state management
- Collaboration protocols
