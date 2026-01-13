# Week 8: Agent Communication

> Phase 4: Multi-Agent Collaboration
> Week 8 Learning Material: Message Passing, Shared State & Collaboration Protocols

---

## Day 1-2: Message Passing Patterns

### 1. Communication Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                   Agent Communication Patterns                   │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────────────┐    ┌────────────────┐    ┌────────────────┐ │
│  │  Direct        │    │   Broadcast    │    │   Pub/Sub      │ │
│  │  Point-to-Point│    │   One-to-All   │    │   Topic-based  │ │
│  └────────────────┘    └────────────────┘    └────────────────┘ │
│                                                                  │
│  ┌────────────────┐    ┌────────────────┐    ┌────────────────┐ │
│  │  Request/Reply │    │   Event-Driven │    │   Blackboard   │ │
│  │  Synchronous   │    │   Asynchronous │    │   Shared State │ │
│  └────────────────┘    └────────────────┘    └────────────────┘ │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. Message Types

```csharp
namespace DawningAgents.Core.MultiAgent.Communication;

/// <summary>
/// Base message for agent communication
/// </summary>
public abstract record AgentMessage
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required string SenderId { get; init; }
    public string? ReceiverId { get; init; }  // null for broadcast
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Task request message
/// </summary>
public record TaskMessage : AgentMessage
{
    public required string Task { get; init; }
    public int Priority { get; init; } = 0;
    public TimeSpan? Timeout { get; init; }
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Task response message
/// </summary>
public record ResponseMessage : AgentMessage
{
    public required string CorrelationId { get; init; }
    public required string Result { get; init; }
    public bool IsSuccess { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Status update message
/// </summary>
public record StatusMessage : AgentMessage
{
    public required AgentStatus Status { get; init; }
    public string? CurrentTask { get; init; }
    public double? Progress { get; init; }
}

/// <summary>
/// Event notification message
/// </summary>
public record EventMessage : AgentMessage
{
    public required string EventType { get; init; }
    public required object Payload { get; init; }
}

public enum AgentStatus
{
    Idle,
    Busy,
    Error,
    Offline
}
```

### 3. Message Bus Interface

```csharp
namespace DawningAgents.Core.MultiAgent.Communication;

/// <summary>
/// Central message bus for agent communication
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Send a message to a specific agent
    /// </summary>
    Task SendAsync(AgentMessage message, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Broadcast a message to all agents
    /// </summary>
    Task BroadcastAsync(AgentMessage message, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Subscribe to messages for a specific agent
    /// </summary>
    IDisposable Subscribe(string agentId, Action<AgentMessage> handler);
    
    /// <summary>
    /// Subscribe to a specific topic
    /// </summary>
    IDisposable Subscribe(string agentId, string topic, Action<EventMessage> handler);
    
    /// <summary>
    /// Publish an event to a topic
    /// </summary>
    Task PublishAsync(string topic, EventMessage message, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Request/Reply pattern
    /// </summary>
    Task<ResponseMessage> RequestAsync(
        TaskMessage request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
```

### 4. In-Memory Message Bus

```csharp
namespace DawningAgents.Core.MultiAgent.Communication;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

/// <summary>
/// In-memory message bus implementation
/// </summary>
public class InMemoryMessageBus : IMessageBus
{
    private readonly ConcurrentDictionary<string, List<Action<AgentMessage>>> _subscribers = new();
    private readonly ConcurrentDictionary<string, List<(string AgentId, Action<EventMessage> Handler)>> _topicSubscribers = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ResponseMessage>> _pendingRequests = new();
    private readonly ILogger<InMemoryMessageBus> _logger;

    public InMemoryMessageBus(ILogger<InMemoryMessageBus> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(AgentMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(message.ReceiverId))
            throw new ArgumentException("ReceiverId is required for direct messages");

        _logger.LogDebug("Sending message {Id} from {Sender} to {Receiver}",
            message.Id, message.SenderId, message.ReceiverId);

        if (_subscribers.TryGetValue(message.ReceiverId, out var handlers))
        {
            foreach (var handler in handlers)
            {
                try
                {
                    handler(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling message {Id}", message.Id);
                }
            }
        }

        // Handle response correlation
        if (message is ResponseMessage response &&
            _pendingRequests.TryRemove(response.CorrelationId, out var tcs))
        {
            tcs.SetResult(response);
        }

        return Task.CompletedTask;
    }

    public Task BroadcastAsync(AgentMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Broadcasting message {Id} from {Sender}", message.Id, message.SenderId);

        foreach (var handlers in _subscribers.Values)
        {
            foreach (var handler in handlers)
            {
                try
                {
                    handler(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling broadcast message {Id}", message.Id);
                }
            }
        }

        return Task.CompletedTask;
    }

    public IDisposable Subscribe(string agentId, Action<AgentMessage> handler)
    {
        var handlers = _subscribers.GetOrAdd(agentId, _ => []);
        handlers.Add(handler);
        
        _logger.LogDebug("Agent {AgentId} subscribed to messages", agentId);
        
        return new Subscription(() =>
        {
            handlers.Remove(handler);
            _logger.LogDebug("Agent {AgentId} unsubscribed", agentId);
        });
    }

    public IDisposable Subscribe(string agentId, string topic, Action<EventMessage> handler)
    {
        var subscribers = _topicSubscribers.GetOrAdd(topic, _ => []);
        var subscription = (agentId, handler);
        subscribers.Add(subscription);
        
        _logger.LogDebug("Agent {AgentId} subscribed to topic {Topic}", agentId, topic);
        
        return new Subscription(() =>
        {
            subscribers.Remove(subscription);
            _logger.LogDebug("Agent {AgentId} unsubscribed from topic {Topic}", agentId, topic);
        });
    }

    public Task PublishAsync(string topic, EventMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Publishing event {EventType} to topic {Topic}", message.EventType, topic);

        if (_topicSubscribers.TryGetValue(topic, out var subscribers))
        {
            foreach (var (agentId, handler) in subscribers)
            {
                try
                {
                    handler(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling event in agent {AgentId}", agentId);
                }
            }
        }

        return Task.CompletedTask;
    }

    public async Task<ResponseMessage> RequestAsync(
        TaskMessage request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var correlationId = request.CorrelationId ?? request.Id;
        var tcs = new TaskCompletionSource<ResponseMessage>();
        
        _pendingRequests.TryAdd(correlationId, tcs);

        try
        {
            await SendAsync(request with { CorrelationId = correlationId }, cancellationToken);
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            var completedTask = await Task.WhenAny(
                tcs.Task,
                Task.Delay(Timeout.Infinite, cts.Token));

            if (completedTask == tcs.Task)
            {
                return await tcs.Task;
            }

            throw new TimeoutException($"Request {correlationId} timed out after {timeout}");
        }
        finally
        {
            _pendingRequests.TryRemove(correlationId, out _);
        }
    }

    private class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;
        
        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;
        public void Dispose() => _unsubscribe();
    }
}
```

---

## Day 3-4: Shared State Management

### 1. Shared State Interface

```csharp
namespace DawningAgents.Core.MultiAgent.State;

/// <summary>
/// Shared state store for multi-agent collaboration
/// </summary>
public interface ISharedState
{
    /// <summary>
    /// Get a value from shared state
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Set a value in shared state
    /// </summary>
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a value from shared state
    /// </summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a key exists
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all keys matching a pattern
    /// </summary>
    Task<IReadOnlyList<string>> GetKeysAsync(string pattern = "*", CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Atomic compare-and-swap operation
    /// </summary>
    Task<bool> CompareAndSwapAsync<T>(
        string key,
        T expectedValue,
        T newValue,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Watch for changes to a key
    /// </summary>
    IDisposable Watch<T>(string key, Action<T?> onChange);
}
```

### 2. In-Memory Shared State

```csharp
namespace DawningAgents.Core.MultiAgent.State;

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// In-memory shared state implementation
/// </summary>
public class InMemorySharedState : ISharedState
{
    private readonly ConcurrentDictionary<string, string> _store = new();
    private readonly ConcurrentDictionary<string, List<Action<object?>>> _watchers = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var json))
        {
            return Task.FromResult(JsonSerializer.Deserialize<T>(json, _jsonOptions));
        }
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value, _jsonOptions);
        _store.AddOrUpdate(key, json, (_, _) => json);
        
        NotifyWatchers(key, value);
        
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(key, out _);
        NotifyWatchers<object>(key, null);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.ContainsKey(key));
    }

    public Task<IReadOnlyList<string>> GetKeysAsync(string pattern = "*", CancellationToken cancellationToken = default)
    {
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        var regex = new Regex(regexPattern, RegexOptions.Compiled);
        
        var keys = _store.Keys.Where(k => regex.IsMatch(k)).ToList();
        return Task.FromResult<IReadOnlyList<string>>(keys);
    }

    public Task<bool> CompareAndSwapAsync<T>(
        string key,
        T expectedValue,
        T newValue,
        CancellationToken cancellationToken = default)
    {
        var expectedJson = JsonSerializer.Serialize(expectedValue, _jsonOptions);
        var newJson = JsonSerializer.Serialize(newValue, _jsonOptions);
        
        if (_store.TryGetValue(key, out var currentJson))
        {
            if (currentJson == expectedJson)
            {
                _store.TryUpdate(key, newJson, currentJson);
                NotifyWatchers(key, newValue);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
        
        // Key doesn't exist
        if (expectedValue == null)
        {
            _store.TryAdd(key, newJson);
            NotifyWatchers(key, newValue);
            return Task.FromResult(true);
        }
        
        return Task.FromResult(false);
    }

    public IDisposable Watch<T>(string key, Action<T?> onChange)
    {
        var watchers = _watchers.GetOrAdd(key, _ => []);
        Action<object?> handler = obj => onChange(obj is T typed ? typed : default);
        watchers.Add(handler);
        
        return new Watcher(() => watchers.Remove(handler));
    }

    private void NotifyWatchers<T>(string key, T? value)
    {
        if (_watchers.TryGetValue(key, out var watchers))
        {
            foreach (var watcher in watchers.ToList())
            {
                try
                {
                    watcher(value);
                }
                catch
                {
                    // Ignore watcher errors
                }
            }
        }
    }

    private class Watcher : IDisposable
    {
        private readonly Action _unwatch;
        public Watcher(Action unwatch) => _unwatch = unwatch;
        public void Dispose() => _unwatch();
    }
}
```

### 3. Blackboard System

```csharp
namespace DawningAgents.Core.MultiAgent.State;

using Microsoft.Extensions.Logging;

/// <summary>
/// Blackboard pattern for collaborative problem solving
/// </summary>
public class Blackboard
{
    private readonly ISharedState _state;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<Blackboard> _logger;

    public Blackboard(
        ISharedState state,
        IMessageBus messageBus,
        ILogger<Blackboard> logger)
    {
        _state = state;
        _messageBus = messageBus;
        _logger = logger;
    }

    /// <summary>
    /// Post a problem to the blackboard
    /// </summary>
    public async Task PostProblemAsync(
        string problemId,
        string description,
        CancellationToken cancellationToken = default)
    {
        var problem = new BlackboardProblem
        {
            Id = problemId,
            Description = description,
            Status = ProblemStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        await _state.SetAsync($"problem:{problemId}", problem, cancellationToken);
        
        // Notify agents
        await _messageBus.PublishAsync("blackboard", new EventMessage
        {
            SenderId = "blackboard",
            EventType = "problem.posted",
            Payload = problem
        }, cancellationToken);

        _logger.LogInformation("Problem {Id} posted to blackboard", problemId);
    }

    /// <summary>
    /// Post a solution attempt
    /// </summary>
    public async Task PostSolutionAsync(
        string problemId,
        string agentId,
        string solution,
        double confidence,
        CancellationToken cancellationToken = default)
    {
        var attempt = new SolutionAttempt
        {
            ProblemId = problemId,
            AgentId = agentId,
            Solution = solution,
            Confidence = confidence,
            Timestamp = DateTime.UtcNow
        };

        var key = $"solution:{problemId}:{agentId}";
        await _state.SetAsync(key, attempt, cancellationToken);

        // Notify agents
        await _messageBus.PublishAsync("blackboard", new EventMessage
        {
            SenderId = agentId,
            EventType = "solution.posted",
            Payload = attempt
        }, cancellationToken);

        _logger.LogInformation("Agent {AgentId} posted solution for problem {ProblemId}",
            agentId, problemId);
    }

    /// <summary>
    /// Get all solutions for a problem
    /// </summary>
    public async Task<IReadOnlyList<SolutionAttempt>> GetSolutionsAsync(
        string problemId,
        CancellationToken cancellationToken = default)
    {
        var keys = await _state.GetKeysAsync($"solution:{problemId}:*", cancellationToken);
        var solutions = new List<SolutionAttempt>();

        foreach (var key in keys)
        {
            var solution = await _state.GetAsync<SolutionAttempt>(key, cancellationToken);
            if (solution != null)
            {
                solutions.Add(solution);
            }
        }

        return solutions.OrderByDescending(s => s.Confidence).ToList();
    }

    /// <summary>
    /// Accept a solution and close the problem
    /// </summary>
    public async Task AcceptSolutionAsync(
        string problemId,
        string solutionAgentId,
        CancellationToken cancellationToken = default)
    {
        var problem = await _state.GetAsync<BlackboardProblem>($"problem:{problemId}", cancellationToken);
        if (problem == null) return;

        problem = problem with
        {
            Status = ProblemStatus.Solved,
            SolvedBy = solutionAgentId,
            SolvedAt = DateTime.UtcNow
        };

        await _state.SetAsync($"problem:{problemId}", problem, cancellationToken);

        await _messageBus.PublishAsync("blackboard", new EventMessage
        {
            SenderId = "blackboard",
            EventType = "problem.solved",
            Payload = problem
        }, cancellationToken);

        _logger.LogInformation("Problem {Id} solved by {AgentId}", problemId, solutionAgentId);
    }
}

public record BlackboardProblem
{
    public required string Id { get; init; }
    public required string Description { get; init; }
    public ProblemStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? SolvedBy { get; init; }
    public DateTime? SolvedAt { get; init; }
}

public record SolutionAttempt
{
    public required string ProblemId { get; init; }
    public required string AgentId { get; init; }
    public required string Solution { get; init; }
    public double Confidence { get; init; }
    public DateTime Timestamp { get; init; }
}

public enum ProblemStatus
{
    Open,
    InProgress,
    Solved,
    Abandoned
}
```

---

## Day 5-7: Collaboration Protocols

### 1. Communicating Agent

```csharp
namespace DawningAgents.Core.MultiAgent.Communication;

using Microsoft.Extensions.Logging;

/// <summary>
/// Agent with built-in communication capabilities
/// </summary>
public abstract class CommunicatingAgent : TeamAgentBase
{
    protected readonly IMessageBus MessageBus;
    protected readonly ISharedState SharedState;
    private readonly List<IDisposable> _subscriptions = [];

    protected CommunicatingAgent(
        ILLMProvider llm,
        IMessageBus messageBus,
        ISharedState sharedState,
        ILogger logger,
        string name) : base(llm, logger, name)
    {
        MessageBus = messageBus;
        SharedState = sharedState;
    }

    /// <summary>
    /// Initialize communication subscriptions
    /// </summary>
    public virtual void Initialize()
    {
        // Subscribe to direct messages
        _subscriptions.Add(MessageBus.Subscribe(Name, OnMessageReceived));
        
        // Subscribe to blackboard events
        _subscriptions.Add(MessageBus.Subscribe(Name, "blackboard", OnBlackboardEvent));
        
        Logger.LogInformation("Agent {Name} initialized communication", Name);
    }

    /// <summary>
    /// Handle incoming messages
    /// </summary>
    protected virtual void OnMessageReceived(AgentMessage message)
    {
        Logger.LogDebug("Agent {Name} received message {Id} from {Sender}",
            Name, message.Id, message.SenderId);

        switch (message)
        {
            case TaskMessage task:
                _ = HandleTaskAsync(task);
                break;
            case StatusMessage status:
                HandleStatus(status);
                break;
        }
    }

    /// <summary>
    /// Handle blackboard events
    /// </summary>
    protected virtual void OnBlackboardEvent(EventMessage message)
    {
        Logger.LogDebug("Agent {Name} received blackboard event {Type}",
            Name, message.EventType);
    }

    /// <summary>
    /// Handle incoming task requests
    /// </summary>
    protected virtual async Task HandleTaskAsync(TaskMessage task)
    {
        try
        {
            // Notify busy status
            await MessageBus.BroadcastAsync(new StatusMessage
            {
                SenderId = Name,
                Status = AgentStatus.Busy,
                CurrentTask = task.Task
            });

            // Execute the task
            var response = await ExecuteAsync(new AgentContext
            {
                Input = task.Task,
                MaxIterations = 10
            });

            // Send response
            await MessageBus.SendAsync(new ResponseMessage
            {
                SenderId = Name,
                ReceiverId = task.SenderId,
                CorrelationId = task.CorrelationId ?? task.Id,
                Result = response.Output,
                IsSuccess = response.IsSuccess
            });
        }
        catch (Exception ex)
        {
            await MessageBus.SendAsync(new ResponseMessage
            {
                SenderId = Name,
                ReceiverId = task.SenderId,
                CorrelationId = task.CorrelationId ?? task.Id,
                Result = ex.Message,
                IsSuccess = false,
                Error = ex.ToString()
            });
        }
        finally
        {
            await MessageBus.BroadcastAsync(new StatusMessage
            {
                SenderId = Name,
                Status = AgentStatus.Idle
            });
        }
    }

    /// <summary>
    /// Handle status updates from other agents
    /// </summary>
    protected virtual void HandleStatus(StatusMessage status)
    {
        // Override to handle status updates
    }

    /// <summary>
    /// Request help from another agent
    /// </summary>
    protected async Task<string> RequestHelpAsync(
        string targetAgentId,
        string task,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var response = await MessageBus.RequestAsync(new TaskMessage
        {
            SenderId = Name,
            ReceiverId = targetAgentId,
            Task = task
        }, timeout, cancellationToken);

        if (!response.IsSuccess)
        {
            throw new InvalidOperationException($"Request failed: {response.Error}");
        }

        return response.Result;
    }

    /// <summary>
    /// Store a result in shared state
    /// </summary>
    protected Task ShareResultAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        return SharedState.SetAsync($"{Name}:{key}", value, cancellationToken);
    }

    /// <summary>
    /// Get a shared result from another agent
    /// </summary>
    protected Task<T?> GetSharedResultAsync<T>(string agentId, string key, CancellationToken cancellationToken = default)
    {
        return SharedState.GetAsync<T>($"{agentId}:{key}", cancellationToken);
    }

    /// <summary>
    /// Cleanup subscriptions
    /// </summary>
    public virtual void Dispose()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
        _subscriptions.Clear();
    }
}
```

### 2. Collaboration Coordinator

```csharp
namespace DawningAgents.Core.MultiAgent.Collaboration;

using Microsoft.Extensions.Logging;

/// <summary>
/// Coordinates collaboration between multiple agents
/// </summary>
public class CollaborationCoordinator
{
    private readonly IMessageBus _messageBus;
    private readonly ISharedState _sharedState;
    private readonly Blackboard _blackboard;
    private readonly ILogger<CollaborationCoordinator> _logger;
    private readonly List<CommunicatingAgent> _agents = [];

    public CollaborationCoordinator(
        IMessageBus messageBus,
        ISharedState sharedState,
        Blackboard blackboard,
        ILogger<CollaborationCoordinator> logger)
    {
        _messageBus = messageBus;
        _sharedState = sharedState;
        _blackboard = blackboard;
        _logger = logger;
    }

    /// <summary>
    /// Register an agent for collaboration
    /// </summary>
    public void Register(CommunicatingAgent agent)
    {
        agent.Initialize();
        _agents.Add(agent);
        _logger.LogInformation("Registered agent {Name} for collaboration", agent.Name);
    }

    /// <summary>
    /// Solve a problem collaboratively
    /// </summary>
    public async Task<CollaborationResult> SolveAsync(
        string problem,
        CollaborationStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        var problemId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("Starting collaborative problem solving: {Problem}", problem);

        // Post to blackboard
        await _blackboard.PostProblemAsync(problemId, problem, cancellationToken);

        string? result;
        switch (strategy)
        {
            case CollaborationStrategy.Consensus:
                result = await SolveWithConsensusAsync(problemId, problem, cancellationToken);
                break;
            case CollaborationStrategy.BestEffort:
                result = await SolveWithBestEffortAsync(problemId, problem, cancellationToken);
                break;
            case CollaborationStrategy.Voting:
                result = await SolveWithVotingAsync(problemId, problem, cancellationToken);
                break;
            default:
                throw new ArgumentException($"Unknown strategy: {strategy}");
        }

        return new CollaborationResult
        {
            ProblemId = problemId,
            Problem = problem,
            Result = result,
            Strategy = strategy,
            Duration = DateTime.UtcNow - startTime,
            ParticipantCount = _agents.Count
        };
    }

    private async Task<string> SolveWithConsensusAsync(
        string problemId,
        string problem,
        CancellationToken cancellationToken)
    {
        // All agents must contribute
        var tasks = _agents.Select(async agent =>
        {
            var response = await agent.ExecuteAsync(new AgentContext
            {
                Input = problem,
                MaxIterations = 10
            }, cancellationToken);

            await _blackboard.PostSolutionAsync(
                problemId,
                agent.Name,
                response.Output,
                response.IsSuccess ? 0.8 : 0.2,
                cancellationToken);

            return response.Output;
        });

        var solutions = await Task.WhenAll(tasks);
        
        // Find consensus (simplified - could use LLM to analyze)
        return string.Join("\n\n---\n\n", solutions);
    }

    private async Task<string> SolveWithBestEffortAsync(
        string problemId,
        string problem,
        CancellationToken cancellationToken)
    {
        // First agent with high confidence wins
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var tasks = _agents.Select(async agent =>
        {
            var response = await agent.ExecuteAsync(new AgentContext
            {
                Input = problem,
                MaxIterations = 5
            }, cts.Token);

            await _blackboard.PostSolutionAsync(
                problemId,
                agent.Name,
                response.Output,
                response.IsSuccess ? 0.9 : 0.3,
                cancellationToken);

            return (Agent: agent, Response: response);
        });

        var firstSuccess = await Task.WhenAny(tasks);
        cts.Cancel();

        var (agent, response) = await firstSuccess;
        await _blackboard.AcceptSolutionAsync(problemId, agent.Name, cancellationToken);

        return response.Output;
    }

    private async Task<string> SolveWithVotingAsync(
        string problemId,
        string problem,
        CancellationToken cancellationToken)
    {
        // All agents vote on the best solution
        var tasks = _agents.Select(async agent =>
        {
            var response = await agent.ExecuteAsync(new AgentContext
            {
                Input = problem,
                MaxIterations = 10
            }, cancellationToken);

            return (Agent: agent, Response: response);
        });

        var results = await Task.WhenAll(tasks);

        // Post all solutions
        foreach (var (agent, response) in results)
        {
            await _blackboard.PostSolutionAsync(
                problemId,
                agent.Name,
                response.Output,
                response.IsSuccess ? 0.7 : 0.3,
                cancellationToken);
        }

        // Get best solution
        var solutions = await _blackboard.GetSolutionsAsync(problemId, cancellationToken);
        var best = solutions.FirstOrDefault();

        if (best != null)
        {
            await _blackboard.AcceptSolutionAsync(problemId, best.AgentId, cancellationToken);
            return best.Solution;
        }

        return "No solution found";
    }
}

public record CollaborationResult
{
    public required string ProblemId { get; init; }
    public required string Problem { get; init; }
    public required string? Result { get; init; }
    public CollaborationStrategy Strategy { get; init; }
    public TimeSpan Duration { get; init; }
    public int ParticipantCount { get; init; }
}

public enum CollaborationStrategy
{
    Consensus,   // All agents must agree
    BestEffort,  // First confident answer wins
    Voting       // Agents vote on best solution
}
```

---

## Complete Example

```csharp
// Setup communication infrastructure
var logger = loggerFactory.CreateLogger<InMemoryMessageBus>();
var messageBus = new InMemoryMessageBus(logger);
var sharedState = new InMemorySharedState();
var blackboard = new Blackboard(sharedState, messageBus, loggerFactory.CreateLogger<Blackboard>());

// Create communicating agents
var llm = new OpenAIProvider(apiKey, logger);

var researcher = new ResearchCommunicatingAgent(
    llm, messageBus, sharedState, loggerFactory.CreateLogger<ResearchCommunicatingAgent>());
var coder = new CodeCommunicatingAgent(
    llm, messageBus, sharedState, loggerFactory.CreateLogger<CodeCommunicatingAgent>());

// Create coordinator
var coordinator = new CollaborationCoordinator(
    messageBus, sharedState, blackboard, loggerFactory.CreateLogger<CollaborationCoordinator>());

coordinator.Register(researcher);
coordinator.Register(coder);

// Solve collaboratively
var result = await coordinator.SolveAsync(
    "Create a REST API client with retry logic",
    CollaborationStrategy.Consensus);

Console.WriteLine($"Result: {result.Result}");
Console.WriteLine($"Duration: {result.Duration}");
Console.WriteLine($"Participants: {result.ParticipantCount}");
```

---

## Summary

### Week 8 Deliverables

```
src/DawningAgents.Core/
└── MultiAgent/
    ├── Communication/
    │   ├── AgentMessage.cs           # Message types
    │   ├── TaskMessage.cs            # Task request
    │   ├── ResponseMessage.cs        # Task response
    │   ├── StatusMessage.cs          # Status updates
    │   ├── EventMessage.cs           # Event notifications
    │   ├── IMessageBus.cs            # Message bus interface
    │   ├── InMemoryMessageBus.cs     # In-memory implementation
    │   └── CommunicatingAgent.cs     # Agent with communication
    ├── State/
    │   ├── ISharedState.cs           # Shared state interface
    │   ├── InMemorySharedState.cs    # In-memory implementation
    │   └── Blackboard.cs             # Blackboard system
    └── Collaboration/
        ├── CollaborationCoordinator.cs  # Coordinator
        ├── CollaborationResult.cs       # Result model
        └── CollaborationStrategy.cs     # Strategy enum
```

### Communication Patterns

| Pattern | Description | Use Case |
|---------|-------------|----------|
| **Direct** | Point-to-point | Specific requests |
| **Broadcast** | One-to-all | Status updates |
| **Pub/Sub** | Topic-based | Event notifications |
| **Request/Reply** | Synchronous | Task delegation |
| **Blackboard** | Shared workspace | Collaborative problem solving |

### Phase 4 Complete!

Next: Phase 5 (Week 9-10) - Advanced Topics
- Safety & Guardrails
- Human-in-the-Loop
