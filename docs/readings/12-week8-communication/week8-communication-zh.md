# 第8周：Agent通信

> 第四阶段：多Agent协作
> 第8周学习材料：消息传递、共享状态与协作协议

---

## 第1-2天：消息传递模式

### 1. 通信架构

```text
┌─────────────────────────────────────────────────────────────────┐
│                     Agent通信模式                                │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────────────┐    ┌────────────────┐    ┌────────────────┐ │
│  │    直接通信    │    │     广播       │    │   发布/订阅    │ │
│  │   点对点通信   │    │   一对多通信   │    │  基于主题通信  │ │
│  └────────────────┘    └────────────────┘    └────────────────┘ │
│                                                                  │
│  ┌────────────────┐    ┌────────────────┐    ┌────────────────┐ │
│  │   请求/响应    │    │   事件驱动     │    │     黑板       │ │
│  │     同步       │    │     异步       │    │   共享状态     │ │
│  └────────────────┘    └────────────────┘    └────────────────┘ │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. 消息类型

```csharp
namespace Dawning.Agents.Core.MultiAgent.Communication;

/// <summary>
/// Agent通信的基础消息
/// </summary>
public abstract record AgentMessage
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required string SenderId { get; init; }
    public string? ReceiverId { get; init; }  // null表示广播
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// 任务请求消息
/// </summary>
public record TaskMessage : AgentMessage
{
    public required string Task { get; init; }
    public int Priority { get; init; } = 0;
    public TimeSpan? Timeout { get; init; }
    public string? CorrelationId { get; init; }
}

/// <summary>
/// 任务响应消息
/// </summary>
public record ResponseMessage : AgentMessage
{
    public required string CorrelationId { get; init; }
    public required string Result { get; init; }
    public bool IsSuccess { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// 状态更新消息
/// </summary>
public record StatusMessage : AgentMessage
{
    public required AgentStatus Status { get; init; }
    public string? CurrentTask { get; init; }
    public double? Progress { get; init; }
}

/// <summary>
/// 事件通知消息
/// </summary>
public record EventMessage : AgentMessage
{
    public required string EventType { get; init; }
    public required object Payload { get; init; }
}

public enum AgentStatus
{
    Idle,    // 空闲
    Busy,    // 忙碌
    Error,   // 错误
    Offline  // 离线
}
```

### 3. 消息总线接口

```csharp
namespace Dawning.Agents.Core.MultiAgent.Communication;

/// <summary>
/// Agent通信的中央消息总线
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// 向特定Agent发送消息
    /// </summary>
    Task SendAsync(AgentMessage message, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 向所有Agent广播消息
    /// </summary>
    Task BroadcastAsync(AgentMessage message, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 订阅特定Agent的消息
    /// </summary>
    IDisposable Subscribe(string agentId, Action<AgentMessage> handler);
    
    /// <summary>
    /// 订阅特定主题
    /// </summary>
    IDisposable Subscribe(string agentId, string topic, Action<EventMessage> handler);
    
    /// <summary>
    /// 向主题发布事件
    /// </summary>
    Task PublishAsync(string topic, EventMessage message, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 请求/响应模式
    /// </summary>
    Task<ResponseMessage> RequestAsync(
        TaskMessage request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
```

### 4. 内存消息总线

```csharp
namespace Dawning.Agents.Core.MultiAgent.Communication;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

/// <summary>
/// 内存消息总线实现
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
            throw new ArgumentException("直接消息需要ReceiverId");

        _logger.LogDebug("从 {Sender} 向 {Receiver} 发送消息 {Id}",
            message.SenderId, message.ReceiverId, message.Id);

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
                    _logger.LogError(ex, "处理消息 {Id} 时出错", message.Id);
                }
            }
        }

        // 处理响应关联
        if (message is ResponseMessage response &&
            _pendingRequests.TryRemove(response.CorrelationId, out var tcs))
        {
            tcs.SetResult(response);
        }

        return Task.CompletedTask;
    }

    public Task BroadcastAsync(AgentMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("从 {Sender} 广播消息 {Id}", message.SenderId, message.Id);

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
                    _logger.LogError(ex, "处理广播消息 {Id} 时出错", message.Id);
                }
            }
        }

        return Task.CompletedTask;
    }

    public IDisposable Subscribe(string agentId, Action<AgentMessage> handler)
    {
        var handlers = _subscribers.GetOrAdd(agentId, _ => []);
        handlers.Add(handler);
        
        _logger.LogDebug("Agent {AgentId} 订阅了消息", agentId);
        
        return new Subscription(() =>
        {
            handlers.Remove(handler);
            _logger.LogDebug("Agent {AgentId} 取消订阅", agentId);
        });
    }

    public IDisposable Subscribe(string agentId, string topic, Action<EventMessage> handler)
    {
        var subscribers = _topicSubscribers.GetOrAdd(topic, _ => []);
        var subscription = (agentId, handler);
        subscribers.Add(subscription);
        
        _logger.LogDebug("Agent {AgentId} 订阅了主题 {Topic}", agentId, topic);
        
        return new Subscription(() =>
        {
            subscribers.Remove(subscription);
            _logger.LogDebug("Agent {AgentId} 取消订阅主题 {Topic}", agentId, topic);
        });
    }

    public Task PublishAsync(string topic, EventMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("向主题 {Topic} 发布事件 {EventType}", topic, message.EventType);

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
                    _logger.LogError(ex, "Agent {AgentId} 处理事件时出错", agentId);
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

            throw new TimeoutException($"请求 {correlationId} 在 {timeout} 后超时");
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

## 第3-4天：共享状态管理

### 1. 共享状态接口

```csharp
namespace Dawning.Agents.Core.MultiAgent.State;

/// <summary>
/// 多Agent协作的共享状态存储
/// </summary>
public interface ISharedState
{
    /// <summary>
    /// 从共享状态获取值
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 在共享状态中设置值
    /// </summary>
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 从共享状态删除值
    /// </summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 检查键是否存在
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取匹配模式的所有键
    /// </summary>
    Task<IReadOnlyList<string>> GetKeysAsync(string pattern = "*", CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 原子比较并交换操作
    /// </summary>
    Task<bool> CompareAndSwapAsync<T>(
        string key,
        T expectedValue,
        T newValue,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 监听键的变化
    /// </summary>
    IDisposable Watch<T>(string key, Action<T?> onChange);
}
```

### 2. 内存共享状态

```csharp
namespace Dawning.Agents.Core.MultiAgent.State;

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// 内存共享状态实现
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
        
        // 键不存在
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
                    // 忽略监听器错误
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

### 3. 黑板系统

```csharp
namespace Dawning.Agents.Core.MultiAgent.State;

using Microsoft.Extensions.Logging;

/// <summary>
/// 用于协作问题解决的黑板模式
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
    /// 向黑板发布问题
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
        
        // 通知Agent
        await _messageBus.PublishAsync("blackboard", new EventMessage
        {
            SenderId = "blackboard",
            EventType = "problem.posted",
            Payload = problem
        }, cancellationToken);

        _logger.LogInformation("问题 {Id} 已发布到黑板", problemId);
    }

    /// <summary>
    /// 发布解决方案尝试
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

        // 通知Agent
        await _messageBus.PublishAsync("blackboard", new EventMessage
        {
            SenderId = agentId,
            EventType = "solution.posted",
            Payload = attempt
        }, cancellationToken);

        _logger.LogInformation("Agent {AgentId} 为问题 {ProblemId} 发布了解决方案",
            agentId, problemId);
    }

    /// <summary>
    /// 获取问题的所有解决方案
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
    /// 接受解决方案并关闭问题
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

        _logger.LogInformation("问题 {Id} 已被 {AgentId} 解决", problemId, solutionAgentId);
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
    Open,       // 开放
    InProgress, // 进行中
    Solved,     // 已解决
    Abandoned   // 已放弃
}
```

---

## 第5-7天：协作协议

### 1. 可通信Agent

```csharp
namespace Dawning.Agents.Core.MultiAgent.Communication;

using Microsoft.Extensions.Logging;

/// <summary>
/// 具有内置通信能力的Agent
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
    /// 初始化通信订阅
    /// </summary>
    public virtual void Initialize()
    {
        // 订阅直接消息
        _subscriptions.Add(MessageBus.Subscribe(Name, OnMessageReceived));
        
        // 订阅黑板事件
        _subscriptions.Add(MessageBus.Subscribe(Name, "blackboard", OnBlackboardEvent));
        
        Logger.LogInformation("Agent {Name} 已初始化通信", Name);
    }

    /// <summary>
    /// 处理传入消息
    /// </summary>
    protected virtual void OnMessageReceived(AgentMessage message)
    {
        Logger.LogDebug("Agent {Name} 收到来自 {Sender} 的消息 {Id}",
            Name, message.SenderId, message.Id);

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
    /// 处理黑板事件
    /// </summary>
    protected virtual void OnBlackboardEvent(EventMessage message)
    {
        Logger.LogDebug("Agent {Name} 收到黑板事件 {Type}",
            Name, message.EventType);
    }

    /// <summary>
    /// 处理传入的任务请求
    /// </summary>
    protected virtual async Task HandleTaskAsync(TaskMessage task)
    {
        try
        {
            // 通知忙碌状态
            await MessageBus.BroadcastAsync(new StatusMessage
            {
                SenderId = Name,
                Status = AgentStatus.Busy,
                CurrentTask = task.Task
            });

            // 执行任务
            var response = await ExecuteAsync(new AgentContext
            {
                Input = task.Task,
                MaxIterations = 10
            });

            // 发送响应
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
    /// 处理其他Agent的状态更新
    /// </summary>
    protected virtual void HandleStatus(StatusMessage status)
    {
        // 重写以处理状态更新
    }

    /// <summary>
    /// 向另一个Agent请求帮助
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
            throw new InvalidOperationException($"请求失败：{response.Error}");
        }

        return response.Result;
    }

    /// <summary>
    /// 将结果存储到共享状态
    /// </summary>
    protected Task ShareResultAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        return SharedState.SetAsync($"{Name}:{key}", value, cancellationToken);
    }

    /// <summary>
    /// 从另一个Agent获取共享结果
    /// </summary>
    protected Task<T?> GetSharedResultAsync<T>(string agentId, string key, CancellationToken cancellationToken = default)
    {
        return SharedState.GetAsync<T>($"{agentId}:{key}", cancellationToken);
    }

    /// <summary>
    /// 清理订阅
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

### 2. 协作协调器

```csharp
namespace Dawning.Agents.Core.MultiAgent.Collaboration;

using Microsoft.Extensions.Logging;

/// <summary>
/// 协调多个Agent之间的协作
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
    /// 注册Agent参与协作
    /// </summary>
    public void Register(CommunicatingAgent agent)
    {
        agent.Initialize();
        _agents.Add(agent);
        _logger.LogInformation("已注册Agent {Name} 参与协作", agent.Name);
    }

    /// <summary>
    /// 协作解决问题
    /// </summary>
    public async Task<CollaborationResult> SolveAsync(
        string problem,
        CollaborationStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        var problemId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("开始协作问题解决：{Problem}", problem);

        // 发布到黑板
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
                throw new ArgumentException($"未知策略：{strategy}");
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
        // 所有Agent必须贡献
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
        
        // 找到共识（简化 - 可以使用LLM分析）
        return string.Join("\n\n---\n\n", solutions);
    }

    private async Task<string> SolveWithBestEffortAsync(
        string problemId,
        string problem,
        CancellationToken cancellationToken)
    {
        // 第一个高置信度的答案获胜
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
        // 所有Agent对最佳解决方案投票
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

        // 发布所有解决方案
        foreach (var (agent, response) in results)
        {
            await _blackboard.PostSolutionAsync(
                problemId,
                agent.Name,
                response.Output,
                response.IsSuccess ? 0.7 : 0.3,
                cancellationToken);
        }

        // 获取最佳解决方案
        var solutions = await _blackboard.GetSolutionsAsync(problemId, cancellationToken);
        var best = solutions.FirstOrDefault();

        if (best != null)
        {
            await _blackboard.AcceptSolutionAsync(problemId, best.AgentId, cancellationToken);
            return best.Solution;
        }

        return "未找到解决方案";
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
    Consensus,   // 所有Agent必须达成一致
    BestEffort,  // 第一个有信心的答案获胜
    Voting       // Agent对最佳解决方案投票
}
```

---

## 完整示例

```csharp
// 设置通信基础设施
var logger = loggerFactory.CreateLogger<InMemoryMessageBus>();
var messageBus = new InMemoryMessageBus(logger);
var sharedState = new InMemorySharedState();
var blackboard = new Blackboard(sharedState, messageBus, loggerFactory.CreateLogger<Blackboard>());

// 创建可通信Agent
var llm = new OpenAIProvider(apiKey, logger);

var researcher = new ResearchCommunicatingAgent(
    llm, messageBus, sharedState, loggerFactory.CreateLogger<ResearchCommunicatingAgent>());
var coder = new CodeCommunicatingAgent(
    llm, messageBus, sharedState, loggerFactory.CreateLogger<CodeCommunicatingAgent>());

// 创建协调器
var coordinator = new CollaborationCoordinator(
    messageBus, sharedState, blackboard, loggerFactory.CreateLogger<CollaborationCoordinator>());

coordinator.Register(researcher);
coordinator.Register(coder);

// 协作解决
var result = await coordinator.SolveAsync(
    "创建一个带重试逻辑的REST API客户端",
    CollaborationStrategy.Consensus);

Console.WriteLine($"结果：{result.Result}");
Console.WriteLine($"耗时：{result.Duration}");
Console.WriteLine($"参与者：{result.ParticipantCount}");
```

---

## 总结

### 第8周交付物

```
src/Dawning.Agents.Core/
└── MultiAgent/
    ├── Communication/
    │   ├── AgentMessage.cs           # 消息类型
    │   ├── TaskMessage.cs            # 任务请求
    │   ├── ResponseMessage.cs        # 任务响应
    │   ├── StatusMessage.cs          # 状态更新
    │   ├── EventMessage.cs           # 事件通知
    │   ├── IMessageBus.cs            # 消息总线接口
    │   ├── InMemoryMessageBus.cs     # 内存实现
    │   └── CommunicatingAgent.cs     # 可通信Agent
    ├── State/
    │   ├── ISharedState.cs           # 共享状态接口
    │   ├── InMemorySharedState.cs    # 内存实现
    │   └── Blackboard.cs             # 黑板系统
    └── Collaboration/
        ├── CollaborationCoordinator.cs  # 协调器
        ├── CollaborationResult.cs       # 结果模型
        └── CollaborationStrategy.cs     # 策略枚举
```

### 通信模式

| 模式 | 描述 | 用例 |
|------|------|------|
| **直接** | 点对点 | 特定请求 |
| **广播** | 一对多 | 状态更新 |
| **发布/订阅** | 基于主题 | 事件通知 |
| **请求/响应** | 同步 | 任务委派 |
| **黑板** | 共享工作空间 | 协作问题解决 |

### 第四阶段完成！

下一步：第五阶段（第9-10周）- 高级主题
- 安全与护栏
- 人机协作
