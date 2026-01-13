# 第10周：人机协作

> 第五阶段：高级主题
> 第10周学习材料：确认模式、审批工作流与升级处理

---

## 第1-2天：人机协作基础

### 1. 为什么需要人机协作？

```
┌─────────────────────────────────────────────────────────────────┐
│                      人机协作场景                                │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────────────┐    ┌────────────────┐    ┌────────────────┐ │
│  │   高风险       │    │    模糊        │    │    敏感        │ │
│  │   操作         │    │    请求        │    │    操作        │ │
│  └────────────────┘    └────────────────┘    └────────────────┘ │
│                                                                  │
│  ┌────────────────┐    ┌────────────────┐    ┌────────────────┐ │
│  │   财务         │    │    策略        │    │    对外        │ │
│  │   决策         │    │    例外        │    │    沟通        │ │
│  └────────────────┘    └────────────────┘    └────────────────┘ │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. 确认请求模型

```csharp
namespace DawningAgents.Core.HumanLoop;

/// <summary>
/// 人工确认请求
/// </summary>
public record ConfirmationRequest
{
    /// <summary>
    /// 唯一请求标识符
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// 需要的确认类型
    /// </summary>
    public ConfirmationType Type { get; init; }
    
    /// <summary>
    /// 需要确认的操作
    /// </summary>
    public required string Action { get; init; }
    
    /// <summary>
    /// 详细描述
    /// </summary>
    public required string Description { get; init; }
    
    /// <summary>
    /// 操作的风险级别
    /// </summary>
    public RiskLevel RiskLevel { get; init; } = RiskLevel.Medium;
    
    /// <summary>
    /// 供人类选择的选项
    /// </summary>
    public IReadOnlyList<ConfirmationOption> Options { get; init; } = [];
    
    /// <summary>
    /// 用于决策的上下文数据
    /// </summary>
    public IDictionary<string, object> Context { get; init; } = new Dictionary<string, object>();
    
    /// <summary>
    /// 请求创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// 确认超时时间
    /// </summary>
    public TimeSpan? Timeout { get; init; }
    
    /// <summary>
    /// 超时时的默认操作
    /// </summary>
    public string? DefaultOnTimeout { get; init; }
}

public enum ConfirmationType
{
    Binary,          // 是/否
    MultiChoice,     // 多选项
    FreeformInput,   // 用户输入
    Review           // 审查和修改
}

public enum RiskLevel
{
    Low,       // 低风险
    Medium,    // 中等风险
    High,      // 高风险
    Critical   // 关键风险
}

public record ConfirmationOption
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public string? Description { get; init; }
    public bool IsDefault { get; init; }
    public bool IsDangerous { get; init; }
}

/// <summary>
/// 人工响应
/// </summary>
public record ConfirmationResponse
{
    public required string RequestId { get; init; }
    public required string SelectedOption { get; init; }
    public string? FreeformInput { get; init; }
    public string? ModifiedContent { get; init; }
    public DateTime RespondedAt { get; init; } = DateTime.UtcNow;
    public string? RespondedBy { get; init; }
    public string? Reason { get; init; }
}
```

### 3. 人机交互处理器接口

```csharp
namespace DawningAgents.Core.HumanLoop;

/// <summary>
/// 人机交互接口
/// </summary>
public interface IHumanInteractionHandler
{
    /// <summary>
    /// 请求人工确认
    /// </summary>
    Task<ConfirmationResponse> RequestConfirmationAsync(
        ConfirmationRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 请求人工输入/反馈
    /// </summary>
    Task<string> RequestInputAsync(
        string prompt,
        string? defaultValue = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 通知人类（无需响应）
    /// </summary>
    Task NotifyAsync(
        string message,
        NotificationLevel level = NotificationLevel.Info,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 升级到人工处理
    /// </summary>
    Task<EscalationResult> EscalateAsync(
        EscalationRequest request,
        CancellationToken cancellationToken = default);
}

public enum NotificationLevel
{
    Info,     // 信息
    Warning,  // 警告
    Error,    // 错误
    Success   // 成功
}
```

---

## 第3-4天：确认模式

### 1. 控制台交互处理器

```csharp
namespace DawningAgents.Core.HumanLoop.Handlers;

using Microsoft.Extensions.Logging;

/// <summary>
/// 基于控制台的人机交互
/// </summary>
public class ConsoleInteractionHandler : IHumanInteractionHandler
{
    private readonly ILogger<ConsoleInteractionHandler> _logger;

    public ConsoleInteractionHandler(ILogger<ConsoleInteractionHandler> logger)
    {
        _logger = logger;
    }

    public async Task<ConfirmationResponse> RequestConfirmationAsync(
        ConfirmationRequest request,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine($"🔔 需要确认 ({request.RiskLevel})");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine($"操作：{request.Action}");
        Console.WriteLine($"描述：{request.Description}");
        Console.WriteLine();

        if (request.Context.Count > 0)
        {
            Console.WriteLine("上下文：");
            foreach (var (key, value) in request.Context)
            {
                Console.WriteLine($"  {key}：{value}");
            }
            Console.WriteLine();
        }

        string selectedOption;

        switch (request.Type)
        {
            case ConfirmationType.Binary:
                selectedOption = await GetBinaryConfirmation(cancellationToken);
                break;
                
            case ConfirmationType.MultiChoice:
                selectedOption = await GetMultiChoiceConfirmation(request.Options, cancellationToken);
                break;
                
            case ConfirmationType.FreeformInput:
                var input = await GetFreeformInput(cancellationToken);
                return new ConfirmationResponse
                {
                    RequestId = request.Id,
                    SelectedOption = "input",
                    FreeformInput = input
                };
                
            default:
                selectedOption = "unknown";
                break;
        }

        return new ConfirmationResponse
        {
            RequestId = request.Id,
            SelectedOption = selectedOption
        };
    }

    public Task<string> RequestInputAsync(
        string prompt,
        string? defaultValue = null,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.Write($"📝 {prompt}");
        if (defaultValue != null)
        {
            Console.Write($" [{defaultValue}]");
        }
        Console.Write("：");
        
        var input = Console.ReadLine();
        return Task.FromResult(string.IsNullOrWhiteSpace(input) ? (defaultValue ?? "") : input);
    }

    public Task NotifyAsync(
        string message,
        NotificationLevel level = NotificationLevel.Info,
        CancellationToken cancellationToken = default)
    {
        var icon = level switch
        {
            NotificationLevel.Info => "ℹ️",
            NotificationLevel.Warning => "⚠️",
            NotificationLevel.Error => "❌",
            NotificationLevel.Success => "✅",
            _ => "📢"
        };

        Console.WriteLine($"{icon} {message}");
        return Task.CompletedTask;
    }

    public async Task<EscalationResult> EscalateAsync(
        EscalationRequest request,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine($"🚨 需要升级处理 ({request.Severity})");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine($"原因：{request.Reason}");
        Console.WriteLine($"描述：{request.Description}");
        Console.WriteLine();

        Console.WriteLine("可用操作：");
        Console.WriteLine("  1. 解决 - 提供解决方案");
        Console.WriteLine("  2. 跳过 - 跳过此操作");
        Console.WriteLine("  3. 中止 - 中止整个操作");
        Console.WriteLine();

        Console.Write("选择操作 (1/2/3)：");
        var choice = Console.ReadLine()?.Trim();

        return choice switch
        {
            "1" => new EscalationResult
            {
                RequestId = request.Id,
                Action = EscalationAction.Resolved,
                Resolution = await RequestInputAsync("输入解决方案", cancellationToken: cancellationToken)
            },
            "2" => new EscalationResult
            {
                RequestId = request.Id,
                Action = EscalationAction.Skipped
            },
            _ => new EscalationResult
            {
                RequestId = request.Id,
                Action = EscalationAction.Aborted
            }
        };
    }

    private Task<string> GetBinaryConfirmation(CancellationToken cancellationToken)
    {
        Console.Write("继续？(y/n)：");
        var input = Console.ReadLine()?.Trim().ToLower();
        return Task.FromResult(input == "y" || input == "yes" ? "yes" : "no");
    }

    private Task<string> GetMultiChoiceConfirmation(
        IReadOnlyList<ConfirmationOption> options,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("选项：");
        for (int i = 0; i < options.Count; i++)
        {
            var opt = options[i];
            var marker = opt.IsDefault ? "*" : " ";
            var danger = opt.IsDangerous ? " ⚠️" : "";
            Console.WriteLine($"  {marker}{i + 1}. {opt.Label}{danger}");
            if (!string.IsNullOrEmpty(opt.Description))
            {
                Console.WriteLine($"      {opt.Description}");
            }
        }
        Console.WriteLine();

        Console.Write("选择选项：");
        var input = Console.ReadLine()?.Trim();
        
        if (int.TryParse(input, out var index) && index > 0 && index <= options.Count)
        {
            return Task.FromResult(options[index - 1].Id);
        }

        // 返回默认选项
        var defaultOpt = options.FirstOrDefault(o => o.IsDefault);
        return Task.FromResult(defaultOpt?.Id ?? options[0].Id);
    }

    private Task<string> GetFreeformInput(CancellationToken cancellationToken)
    {
        Console.Write("输入您的内容：");
        return Task.FromResult(Console.ReadLine() ?? "");
    }
}
```

### 2. 异步回调处理器

```csharp
namespace DawningAgents.Core.HumanLoop.Handlers;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

/// <summary>
/// 用于Web/API交互的异步处理器
/// </summary>
public class AsyncCallbackHandler : IHumanInteractionHandler
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ConfirmationResponse>> _pendingConfirmations = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<EscalationResult>> _pendingEscalations = new();
    private readonly ILogger<AsyncCallbackHandler> _logger;

    public event EventHandler<ConfirmationRequest>? ConfirmationRequested;
    public event EventHandler<EscalationRequest>? EscalationRequested;
    public event EventHandler<(string Message, NotificationLevel Level)>? NotificationSent;

    public AsyncCallbackHandler(ILogger<AsyncCallbackHandler> logger)
    {
        _logger = logger;
    }

    public async Task<ConfirmationResponse> RequestConfirmationAsync(
        ConfirmationRequest request,
        CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<ConfirmationResponse>();
        _pendingConfirmations[request.Id] = tcs;

        // 触发事件供UI处理
        ConfirmationRequested?.Invoke(this, request);

        try
        {
            // 等待响应，支持超时
            if (request.Timeout.HasValue)
            {
                using var cts = new CancellationTokenSource(request.Timeout.Value);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
                
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(-1, linked.Token));
                if (completedTask != tcs.Task)
                {
                    // 超时 - 返回默认值
                    return new ConfirmationResponse
                    {
                        RequestId = request.Id,
                        SelectedOption = request.DefaultOnTimeout ?? "timeout"
                    };
                }
            }

            return await tcs.Task;
        }
        finally
        {
            _pendingConfirmations.TryRemove(request.Id, out _);
        }
    }

    /// <summary>
    /// 完成挂起的确认（由UI/API调用）
    /// </summary>
    public void CompleteConfirmation(ConfirmationResponse response)
    {
        if (_pendingConfirmations.TryGetValue(response.RequestId, out var tcs))
        {
            tcs.TrySetResult(response);
        }
    }

    public Task<string> RequestInputAsync(
        string prompt,
        string? defaultValue = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ConfirmationRequest
        {
            Type = ConfirmationType.FreeformInput,
            Action = "input",
            Description = prompt,
            Context = new Dictionary<string, object>
            {
                ["defaultValue"] = defaultValue ?? ""
            }
        };

        var tcs = new TaskCompletionSource<string>();
        _pendingConfirmations[request.Id] = new TaskCompletionSource<ConfirmationResponse>();
        
        // 完成时转换
        _pendingConfirmations[request.Id].Task.ContinueWith(t =>
        {
            tcs.TrySetResult(t.Result.FreeformInput ?? defaultValue ?? "");
        }, cancellationToken);

        ConfirmationRequested?.Invoke(this, request);
        
        return tcs.Task;
    }

    public Task NotifyAsync(
        string message,
        NotificationLevel level = NotificationLevel.Info,
        CancellationToken cancellationToken = default)
    {
        NotificationSent?.Invoke(this, (message, level));
        return Task.CompletedTask;
    }

    public async Task<EscalationResult> EscalateAsync(
        EscalationRequest request,
        CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<EscalationResult>();
        _pendingEscalations[request.Id] = tcs;

        EscalationRequested?.Invoke(this, request);

        try
        {
            return await tcs.Task;
        }
        finally
        {
            _pendingEscalations.TryRemove(request.Id, out _);
        }
    }

    /// <summary>
    /// 完成挂起的升级（由UI/API调用）
    /// </summary>
    public void CompleteEscalation(EscalationResult result)
    {
        if (_pendingEscalations.TryGetValue(result.RequestId, out var tcs))
        {
            tcs.TrySetResult(result);
        }
    }
}
```

---

## 第5-7天：审批工作流与升级处理

### 1. 升级模型

```csharp
namespace DawningAgents.Core.HumanLoop;

/// <summary>
/// 升级到人工处理的请求
/// </summary>
public record EscalationRequest
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required string Reason { get; init; }
    public required string Description { get; init; }
    public EscalationSeverity Severity { get; init; } = EscalationSeverity.Medium;
    public string? AgentName { get; init; }
    public string? TaskId { get; init; }
    public IDictionary<string, object> Context { get; init; } = new Dictionary<string, object>();
    public IReadOnlyList<string> AttemptedSolutions { get; init; } = [];
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

public enum EscalationSeverity
{
    Low,       // 低
    Medium,    // 中等
    High,      // 高
    Critical   // 关键
}

/// <summary>
/// 升级结果
/// </summary>
public record EscalationResult
{
    public required string RequestId { get; init; }
    public EscalationAction Action { get; init; }
    public string? Resolution { get; init; }
    public string? Instructions { get; init; }
    public string? ResolvedBy { get; init; }
    public DateTime ResolvedAt { get; init; } = DateTime.UtcNow;
}

public enum EscalationAction
{
    Resolved,    // 已解决
    Skipped,     // 已跳过
    Aborted,     // 已中止
    Delegated,   // 已委派
    Retried      // 重试
}
```

### 2. 审批工作流管理器

```csharp
namespace DawningAgents.Core.HumanLoop;

using Microsoft.Extensions.Logging;

/// <summary>
/// 管理审批工作流
/// </summary>
public class ApprovalWorkflow
{
    private readonly IHumanInteractionHandler _handler;
    private readonly ILogger<ApprovalWorkflow> _logger;
    private readonly ApprovalConfig _config;

    public ApprovalWorkflow(
        IHumanInteractionHandler handler,
        ApprovalConfig config,
        ILogger<ApprovalWorkflow> logger)
    {
        _handler = handler;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// 检查操作是否需要审批并获取审批
    /// </summary>
    public async Task<ApprovalResult> RequestApprovalAsync(
        string action,
        string description,
        IDictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        var riskLevel = AssessRiskLevel(action, context);
        
        // 根据风险级别检查是否需要审批
        if (!RequiresApproval(riskLevel))
        {
            _logger.LogDebug("操作 {Action} 自动批准（风险：{Risk}）", action, riskLevel);
            return ApprovalResult.AutoApproved(action);
        }

        _logger.LogInformation("请求审批 {Action}（风险：{Risk}）", action, riskLevel);

        var request = new ConfirmationRequest
        {
            Type = ConfirmationType.Binary,
            Action = action,
            Description = description,
            RiskLevel = riskLevel,
            Context = context ?? new Dictionary<string, object>(),
            Options = new[]
            {
                new ConfirmationOption { Id = "approve", Label = "批准", IsDefault = true },
                new ConfirmationOption { Id = "reject", Label = "拒绝", IsDangerous = true },
                new ConfirmationOption { Id = "modify", Label = "修改" }
            },
            Timeout = _config.ApprovalTimeout,
            DefaultOnTimeout = _config.DefaultOnTimeout
        };

        var response = await _handler.RequestConfirmationAsync(request, cancellationToken);

        return response.SelectedOption switch
        {
            "approve" => ApprovalResult.Approved(action, response.RespondedBy),
            "reject" => ApprovalResult.Rejected(action, response.Reason, response.RespondedBy),
            "modify" => ApprovalResult.Modified(action, response.ModifiedContent, response.RespondedBy),
            "timeout" => _config.DefaultOnTimeout == "approve" 
                ? ApprovalResult.AutoApproved(action) 
                : ApprovalResult.TimedOut(action),
            _ => ApprovalResult.Rejected(action, "未知响应")
        };
    }

    /// <summary>
    /// 请求多人审批
    /// </summary>
    public async Task<ApprovalResult> RequestMultiApprovalAsync(
        string action,
        string description,
        int requiredApprovals,
        IDictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        var approvals = new List<string>();
        var rejections = new List<(string Approver, string Reason)>();

        for (int i = 0; i < requiredApprovals; i++)
        {
            var result = await RequestApprovalAsync(
                $"{action}（审批 {i + 1}/{requiredApprovals}）",
                description,
                context,
                cancellationToken);

            if (result.IsApproved)
            {
                approvals.Add(result.ApprovedBy ?? $"审批人-{i + 1}");
            }
            else
            {
                rejections.Add((result.ApprovedBy ?? $"审批人-{i + 1}", result.RejectionReason ?? "未知"));
            }
        }

        if (approvals.Count >= requiredApprovals)
        {
            return ApprovalResult.Approved(action, string.Join(", ", approvals));
        }

        return ApprovalResult.Rejected(action, 
            $"审批数量不足：{approvals.Count}/{requiredApprovals}。" +
            $"拒绝：{string.Join("；", rejections.Select(r => $"{r.Approver}：{r.Reason}"))}");
    }

    private RiskLevel AssessRiskLevel(string action, IDictionary<string, object>? context)
    {
        // 检查高风险关键词
        var highRiskKeywords = new[] { "delete", "remove", "destroy", "execute", "transfer", "payment", "删除", "移除", "执行", "转账", "支付" };
        var criticalKeywords = new[] { "production", "financial", "customer data", "credentials", "生产", "财务", "客户数据", "凭证" };

        var lowerAction = action.ToLower();

        if (criticalKeywords.Any(k => lowerAction.Contains(k)))
            return RiskLevel.Critical;

        if (highRiskKeywords.Any(k => lowerAction.Contains(k)))
            return RiskLevel.High;

        // 检查上下文中的风险指标
        if (context != null)
        {
            if (context.TryGetValue("amount", out var amount) && amount is decimal d && d > 10000)
                return RiskLevel.High;
                
            if (context.TryGetValue("environment", out var env) && env?.ToString() == "production")
                return RiskLevel.Critical;
        }

        return RiskLevel.Medium;
    }

    private bool RequiresApproval(RiskLevel level)
    {
        return level switch
        {
            RiskLevel.Low => _config.RequireApprovalForLowRisk,
            RiskLevel.Medium => _config.RequireApprovalForMediumRisk,
            RiskLevel.High => true,
            RiskLevel.Critical => true,
            _ => true
        };
    }
}

public record ApprovalConfig
{
    public bool RequireApprovalForLowRisk { get; init; } = false;
    public bool RequireApprovalForMediumRisk { get; init; } = true;
    public TimeSpan ApprovalTimeout { get; init; } = TimeSpan.FromMinutes(30);
    public string DefaultOnTimeout { get; init; } = "reject";
}

public record ApprovalResult
{
    public required string Action { get; init; }
    public bool IsApproved { get; init; }
    public bool IsAutoApproved { get; init; }
    public bool IsTimedOut { get; init; }
    public string? ApprovedBy { get; init; }
    public string? RejectionReason { get; init; }
    public string? ModifiedAction { get; init; }

    public static ApprovalResult AutoApproved(string action) => new()
    {
        Action = action,
        IsApproved = true,
        IsAutoApproved = true
    };

    public static ApprovalResult Approved(string action, string? approvedBy = null) => new()
    {
        Action = action,
        IsApproved = true,
        ApprovedBy = approvedBy
    };

    public static ApprovalResult Rejected(string action, string? reason = null, string? rejectedBy = null) => new()
    {
        Action = action,
        IsApproved = false,
        RejectionReason = reason,
        ApprovedBy = rejectedBy
    };

    public static ApprovalResult Modified(string action, string? modifiedAction, string? modifiedBy = null) => new()
    {
        Action = action,
        IsApproved = true,
        ModifiedAction = modifiedAction,
        ApprovedBy = modifiedBy
    };

    public static ApprovalResult TimedOut(string action) => new()
    {
        Action = action,
        IsApproved = false,
        IsTimedOut = true,
        RejectionReason = "审批请求超时"
    };
}
```

### 3. 人机协作Agent

```csharp
namespace DawningAgents.Core.HumanLoop;

using Microsoft.Extensions.Logging;

/// <summary>
/// 在决策点引入人工参与的Agent
/// </summary>
public class HumanInLoopAgent : IAgent
{
    private readonly IAgent _innerAgent;
    private readonly IHumanInteractionHandler _handler;
    private readonly ApprovalWorkflow _workflow;
    private readonly HumanLoopConfig _config;
    private readonly ILogger<HumanInLoopAgent> _logger;

    public string Name => $"HumanLoop({_innerAgent.Name})";

    public HumanInLoopAgent(
        IAgent innerAgent,
        IHumanInteractionHandler handler,
        HumanLoopConfig config,
        ILogger<HumanInLoopAgent> logger)
    {
        _innerAgent = innerAgent;
        _handler = handler;
        _config = config;
        _logger = logger;
        _workflow = new ApprovalWorkflow(
            handler,
            new ApprovalConfig
            {
                RequireApprovalForMediumRisk = config.RequireApprovalForMediumRisk,
                ApprovalTimeout = config.DefaultTimeout
            },
            logger);
    }

    public async Task<AgentResponse> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // 执行前确认（如果配置）
            if (_config.ConfirmBeforeExecution)
            {
                var approval = await _workflow.RequestApprovalAsync(
                    "执行Agent任务",
                    $"Agent '{_innerAgent.Name}' 将处理：{context.Input}",
                    cancellationToken: cancellationToken);

                if (!approval.IsApproved)
                {
                    return new AgentResponse
                    {
                        Output = $"任务未批准：{approval.RejectionReason}",
                        IsSuccess = false,
                        Duration = DateTime.UtcNow - startTime
                    };
                }
            }

            // 带升级处理的执行
            var response = await ExecuteWithEscalationAsync(context, cancellationToken);

            // 返回前审查（如果配置）
            if (_config.ReviewBeforeReturn && response.IsSuccess)
            {
                response = await ReviewResponseAsync(response, cancellationToken);
            }

            return response;
        }
        catch (AgentEscalationException ex)
        {
            _logger.LogWarning("Agent升级：{Reason}", ex.Reason);
            
            var escalation = await _handler.EscalateAsync(new EscalationRequest
            {
                Reason = ex.Reason,
                Description = ex.Description,
                Severity = EscalationSeverity.High,
                AgentName = _innerAgent.Name,
                Context = ex.Context,
                AttemptedSolutions = ex.AttemptedSolutions
            }, cancellationToken);

            return HandleEscalationResult(escalation, startTime);
        }
    }

    private async Task<AgentResponse> ExecuteWithEscalationAsync(
        AgentContext context,
        CancellationToken cancellationToken)
    {
        var maxRetries = 3;
        Exception? lastException = null;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return await _innerAgent.ExecuteAsync(context, cancellationToken);
            }
            catch (Exception ex) when (attempt < maxRetries - 1)
            {
                lastException = ex;
                _logger.LogWarning(ex, "第 {Attempt} 次尝试失败，请求指导", attempt + 1);

                var input = await _handler.RequestInputAsync(
                    $"Agent遇到错误：{ex.Message}\n请提供指导或输入'abort'停止：",
                    cancellationToken: cancellationToken);

                if (input.Equals("abort", StringComparison.OrdinalIgnoreCase))
                {
                    throw new OperationCanceledException("用户中止");
                }

                // 带指导重试
                context = context with
                {
                    Input = $"{context.Input}\n\n额外指导：{input}"
                };
            }
        }

        throw lastException ?? new Exception("未知错误");
    }

    private async Task<AgentResponse> ReviewResponseAsync(
        AgentResponse response,
        CancellationToken cancellationToken)
    {
        var review = await _handler.RequestConfirmationAsync(new ConfirmationRequest
        {
            Type = ConfirmationType.Review,
            Action = "审查响应",
            Description = $"Agent响应：\n\n{response.Output}",
            RiskLevel = RiskLevel.Low,
            Options = new[]
            {
                new ConfirmationOption { Id = "approve", Label = "批准", IsDefault = true },
                new ConfirmationOption { Id = "edit", Label = "编辑响应" },
                new ConfirmationOption { Id = "reject", Label = "拒绝" }
            }
        }, cancellationToken);

        return review.SelectedOption switch
        {
            "approve" => response,
            "edit" => response with { Output = review.ModifiedContent ?? response.Output },
            "reject" => response with { IsSuccess = false, Output = "响应被审查者拒绝" },
            _ => response
        };
    }

    private AgentResponse HandleEscalationResult(EscalationResult result, DateTime startTime)
    {
        return result.Action switch
        {
            EscalationAction.Resolved => new AgentResponse
            {
                Output = result.Resolution ?? "已由人工解决",
                IsSuccess = true,
                Duration = DateTime.UtcNow - startTime,
                Metadata = new Dictionary<string, object>
                {
                    ["resolved_by"] = result.ResolvedBy ?? "human",
                    ["escalation_id"] = result.RequestId
                }
            },
            EscalationAction.Skipped => new AgentResponse
            {
                Output = "步骤被人工跳过",
                IsSuccess = true,
                Duration = DateTime.UtcNow - startTime
            },
            _ => new AgentResponse
            {
                Output = "操作被人工中止",
                IsSuccess = false,
                Duration = DateTime.UtcNow - startTime
            }
        };
    }
}

public record HumanLoopConfig
{
    public bool ConfirmBeforeExecution { get; init; } = false;
    public bool ReviewBeforeReturn { get; init; } = false;
    public bool RequireApprovalForMediumRisk { get; init; } = true;
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromMinutes(30);
}

/// <summary>
/// 升级到人工的异常
/// </summary>
public class AgentEscalationException : Exception
{
    public string Reason { get; }
    public string Description { get; }
    public IDictionary<string, object> Context { get; }
    public IReadOnlyList<string> AttemptedSolutions { get; }

    public AgentEscalationException(
        string reason,
        string description,
        IDictionary<string, object>? context = null,
        IReadOnlyList<string>? attemptedSolutions = null)
        : base(reason)
    {
        Reason = reason;
        Description = description;
        Context = context ?? new Dictionary<string, object>();
        AttemptedSolutions = attemptedSolutions ?? [];
    }
}
```

---

## 完整示例

```csharp
// 创建处理器
var handler = new ConsoleInteractionHandler(
    loggerFactory.CreateLogger<ConsoleInteractionHandler>());

// 创建审批工作流
var workflow = new ApprovalWorkflow(
    handler,
    new ApprovalConfig
    {
        RequireApprovalForLowRisk = false,
        RequireApprovalForMediumRisk = true,
        ApprovalTimeout = TimeSpan.FromMinutes(5)
    },
    loggerFactory.CreateLogger<ApprovalWorkflow>());

// 创建人机协作Agent
var innerAgent = new ReActAgent(llm, loggerFactory.CreateLogger<ReActAgent>());
var hilAgent = new HumanInLoopAgent(
    innerAgent,
    handler,
    new HumanLoopConfig
    {
        ConfirmBeforeExecution = true,
        ReviewBeforeReturn = false,
        RequireApprovalForMediumRisk = true
    },
    loggerFactory.CreateLogger<HumanInLoopAgent>());

// 带人工监督执行
var response = await hilAgent.ExecuteAsync(new AgentContext
{
    Input = "从数据库删除所有过期的用户账户"
});

Console.WriteLine(response.Output);

// 直接使用审批工作流
var approval = await workflow.RequestApprovalAsync(
    "转账",
    "向账户 XYZ-123 转账 50,000 元",
    new Dictionary<string, object>
    {
        ["amount"] = 50000m,
        ["destination"] = "XYZ-123"
    });

if (approval.IsApproved)
{
    Console.WriteLine($"转账已由 {approval.ApprovedBy} 批准");
}
else
{
    Console.WriteLine($"转账被拒绝：{approval.RejectionReason}");
}
```

---

## 总结

### 第10周交付物

```
src/DawningAgents.Core/
└── HumanLoop/
    ├── ConfirmationRequest.cs      # 请求模型
    ├── ConfirmationResponse.cs     # 响应模型
    ├── EscalationRequest.cs        # 升级模型
    ├── EscalationResult.cs         # 升级结果
    ├── IHumanInteractionHandler.cs # 处理器接口
    ├── ApprovalWorkflow.cs         # 审批逻辑
    ├── HumanInLoopAgent.cs         # Agent包装器
    └── Handlers/
        ├── ConsoleInteractionHandler.cs  # 控制台UI
        └── AsyncCallbackHandler.cs       # 异步/API
```

### 人机协作模式

| 模式 | 用例 |
|------|------|
| **确认** | 二元是/否决策 |
| **多选** | 从选项中选择 |
| **审查** | 审查和修改输出 |
| **审批工作流** | 基于风险的审批 |
| **升级** | 处理错误/边缘情况 |
| **多人审批** | 关键操作 |

### 第五阶段完成！

完成第9-10周后，您已学习：
- 安全与护栏（输入验证、内容审核、速率限制）
- 人机协作模式（确认、审批、升级）

下一步：第六阶段 - 生产就绪（第11-12周）
