# Multi-Agent Systems

Dawning.Agents supports sophisticated multi-agent orchestration patterns.

## Orchestration Patterns

| Pattern | Description | Use Case |
|---------|-------------|----------|
| Sequential | Agents run in order | Pipeline processing |
| Parallel | Agents run simultaneously | Independent tasks |
| Hierarchical | Manager delegates to workers | Complex workflows |
| Handoff | Agent transfers to specialist | Domain expertise |

## Agent Teams

```csharp
// Create specialized agents
var researchAgent = new Agent("Researcher", "Research topics thoroughly");
var writerAgent = new Agent("Writer", "Write clear documentation");
var reviewerAgent = new Agent("Reviewer", "Review for quality");

// Create team
var team = new AgentTeam("Documentation Team")
    .AddAgent(researchAgent)
    .AddAgent(writerAgent)
    .AddAgent(reviewerAgent);

services.AddAgentTeam(team);
```

## Sequential Pipeline

```csharp
var pipeline = new SequentialPipeline()
    .AddStep(researchAgent)
    .AddStep(writerAgent)
    .AddStep(reviewerAgent);

var result = await pipeline.RunAsync("Write docs for feature X");
```

## Parallel Execution

```csharp
var parallel = new ParallelPipeline()
    .AddAgent(agent1)
    .AddAgent(agent2)
    .AddAgent(agent3);

// All agents process simultaneously
var results = await parallel.RunAsync("Analyze this data");
```

## Handoff Pattern

```csharp
// Triage agent delegates to specialists
var triageAgent = new Agent
{
    Name = "Triage",
    Instructions = "Analyze request and delegate to appropriate specialist",
    Handoffs = new[] { codeAgent, docsAgent, testAgent }
};

// Agent automatically hands off based on context
var response = await triageAgent.RunAsync("Write unit tests for UserService");
// → Delegates to testAgent
```

## Agent Communication

### Message Bus

```csharp
services.AddAgentMessageBus();

var bus = serviceProvider.GetRequiredService<IAgentMessageBus>();

// Subscribe
await bus.SubscribeAsync("research-complete", async (msg) =>
{
    await writerAgent.RunAsync(msg.Content);
});

// Publish
await bus.PublishAsync("research-complete", new AgentMessage
{
    From = "Researcher",
    Content = researchResults
});
```

### Shared State

```csharp
services.AddSharedState();

var state = serviceProvider.GetRequiredService<ISharedState>();

// Set state
await state.SetAsync("research_results", data);

// Get state (from any agent)
var data = await state.GetAsync<ResearchData>("research_results");
```

## Supervisor Pattern

```csharp
var supervisor = new SupervisorAgent
{
    Workers = new[] { worker1, worker2, worker3 },
    Strategy = SupervisionStrategy.RoundRobin
};

// Supervisor distributes work
await supervisor.RunAsync("Process these 100 items");
```

## Error Handling

```csharp
var pipeline = new SequentialPipeline()
    .AddStep(agent1)
    .AddStep(agent2)
    .OnError(async (ex, context) =>
    {
        // Log error
        logger.LogError(ex, "Pipeline failed at {Step}", context.CurrentStep);
        
        // Optionally retry or skip
        return ErrorAction.Retry;
    });
```

## Monitoring

```csharp
// Enable tracing
services.AddAgentTracing();

// Each agent call is traced
// - Agent name
// - Input/Output
// - Duration
// - Tool calls
// - Handoffs
```
