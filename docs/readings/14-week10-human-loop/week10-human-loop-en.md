# Week 10: Human-in-the-Loop

> Phase 5: Advanced Topics
> Week 10 Learning Materials: Confirmation Patterns, Approval Workflows & Escalation

---

## Days 1-2: Human Oversight Fundamentals

### 1. Why Human-in-the-Loop?

```text
┌─────────────────────────────────────────────────────────────────┐
│                   Human-in-the-Loop Scenarios                    │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────────────┐    ┌────────────────┐    ┌────────────────┐ │
│  │   High-Risk    │    │   Ambiguous    │    │   Sensitive    │ │
│  │   Actions      │    │   Requests     │    │   Operations   │ │
│  └────────────────┘    └────────────────┘    └────────────────┘ │
│                                                                  │
│  ┌────────────────┐    ┌────────────────┐    ┌────────────────┐ │
│  │   Financial    │    │   Policy       │    │   External     │ │
│  │   Decisions    │    │   Exceptions   │    │   Communications│ │
│  └────────────────┘    └────────────────┘    └────────────────┘ │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. Confirmation Request Models

```csharp
namespace DawningAgents.Core.HumanLoop;

/// <summary>
/// Request for human confirmation
/// </summary>
public record ConfirmationRequest
{
    /// <summary>
    /// Unique request identifier
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Type of confirmation needed
    /// </summary>
    public ConfirmationType Type { get; init; }
    
    /// <summary>
    /// Action requiring confirmation
    /// </summary>
    public required string Action { get; init; }
    
    /// <summary>
    /// Detailed description
    /// </summary>
    public required string Description { get; init; }
    
    /// <summary>
    /// Risk level of the action
    /// </summary>
    public RiskLevel RiskLevel { get; init; } = RiskLevel.Medium;
    
    /// <summary>
    /// Options for the human to choose from
    /// </summary>
    public IReadOnlyList<ConfirmationOption> Options { get; init; } = [];
    
    /// <summary>
    /// Context data for decision making
    /// </summary>
    public IDictionary<string, object> Context { get; init; } = new Dictionary<string, object>();
    
    /// <summary>
    /// When the request was created
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Timeout for the confirmation
    /// </summary>
    public TimeSpan? Timeout { get; init; }
    
    /// <summary>
    /// Default action if timeout occurs
    /// </summary>
    public string? DefaultOnTimeout { get; init; }
}

public enum ConfirmationType
{
    Binary,          // Yes/No
    MultiChoice,     // Multiple options
    FreeformInput,   // User provides input
    Review           // Review and modify
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
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
/// Response from human
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

### 3. Human Interaction Handler Interface

```csharp
namespace DawningAgents.Core.HumanLoop;

/// <summary>
/// Interface for human interaction
/// </summary>
public interface IHumanInteractionHandler
{
    /// <summary>
    /// Request confirmation from a human
    /// </summary>
    Task<ConfirmationResponse> RequestConfirmationAsync(
        ConfirmationRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Request human input/feedback
    /// </summary>
    Task<string> RequestInputAsync(
        string prompt,
        string? defaultValue = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Notify human (no response needed)
    /// </summary>
    Task NotifyAsync(
        string message,
        NotificationLevel level = NotificationLevel.Info,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Escalate to human with full context
    /// </summary>
    Task<EscalationResult> EscalateAsync(
        EscalationRequest request,
        CancellationToken cancellationToken = default);
}

public enum NotificationLevel
{
    Info,
    Warning,
    Error,
    Success
}
```

---

## Days 3-4: Confirmation Patterns

### 1. Console-Based Handler

```csharp
namespace DawningAgents.Core.HumanLoop.Handlers;

using Microsoft.Extensions.Logging;

/// <summary>
/// Console-based human interaction
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
        Console.WriteLine($"🔔 CONFIRMATION REQUIRED ({request.RiskLevel})");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine($"Action: {request.Action}");
        Console.WriteLine($"Description: {request.Description}");
        Console.WriteLine();

        if (request.Context.Count > 0)
        {
            Console.WriteLine("Context:");
            foreach (var (key, value) in request.Context)
            {
                Console.WriteLine($"  {key}: {value}");
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
        Console.Write(": ");
        
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
        Console.WriteLine($"🚨 ESCALATION REQUIRED ({request.Severity})");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine($"Reason: {request.Reason}");
        Console.WriteLine($"Description: {request.Description}");
        Console.WriteLine();

        Console.WriteLine("Available actions:");
        Console.WriteLine("  1. Resolve - Provide resolution");
        Console.WriteLine("  2. Skip - Skip this action");
        Console.WriteLine("  3. Abort - Abort the entire operation");
        Console.WriteLine();

        Console.Write("Select action (1/2/3): ");
        var choice = Console.ReadLine()?.Trim();

        return choice switch
        {
            "1" => new EscalationResult
            {
                RequestId = request.Id,
                Action = EscalationAction.Resolved,
                Resolution = await RequestInputAsync("Enter resolution", cancellationToken: cancellationToken)
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
        Console.Write("Proceed? (y/n): ");
        var input = Console.ReadLine()?.Trim().ToLower();
        return Task.FromResult(input == "y" || input == "yes" ? "yes" : "no");
    }

    private Task<string> GetMultiChoiceConfirmation(
        IReadOnlyList<ConfirmationOption> options,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("Options:");
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

        Console.Write("Select option: ");
        var input = Console.ReadLine()?.Trim();
        
        if (int.TryParse(input, out var index) && index > 0 && index <= options.Count)
        {
            return Task.FromResult(options[index - 1].Id);
        }

        // Return default option
        var defaultOpt = options.FirstOrDefault(o => o.IsDefault);
        return Task.FromResult(defaultOpt?.Id ?? options[0].Id);
    }

    private Task<string> GetFreeformInput(CancellationToken cancellationToken)
    {
        Console.Write("Enter your input: ");
        return Task.FromResult(Console.ReadLine() ?? "");
    }
}
```

### 2. Async Callback Handler

```csharp
namespace DawningAgents.Core.HumanLoop.Handlers;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Async handler for web/API-based interactions
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

        // Raise event for UI to handle
        ConfirmationRequested?.Invoke(this, request);

        try
        {
            // Wait for response with optional timeout
            if (request.Timeout.HasValue)
            {
                using var cts = new CancellationTokenSource(request.Timeout.Value);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
                
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(-1, linked.Token));
                if (completedTask != tcs.Task)
                {
                    // Timeout - return default
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
    /// Complete a pending confirmation (called by UI/API)
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
        
        // Convert when completed
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
    /// Complete a pending escalation (called by UI/API)
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

## Days 5-7: Approval Workflows & Escalation

### 1. Escalation Models

```csharp
namespace DawningAgents.Core.HumanLoop;

/// <summary>
/// Request to escalate to human
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
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Result of escalation
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
    Resolved,    // Human provided resolution
    Skipped,     // Skip this step
    Aborted,     // Abort entire operation
    Delegated,   // Delegated to another agent/human
    Retried      // Retry with new instructions
}
```

### 2. Approval Workflow Manager

```csharp
namespace DawningAgents.Core.HumanLoop;

using Microsoft.Extensions.Logging;

/// <summary>
/// Manages approval workflows
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
    /// Check if action requires approval and get it
    /// </summary>
    public async Task<ApprovalResult> RequestApprovalAsync(
        string action,
        string description,
        IDictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        var riskLevel = AssessRiskLevel(action, context);
        
        // Check if approval is needed based on risk level
        if (!RequiresApproval(riskLevel))
        {
            _logger.LogDebug("Action {Action} auto-approved (risk: {Risk})", action, riskLevel);
            return ApprovalResult.AutoApproved(action);
        }

        _logger.LogInformation("Requesting approval for {Action} (risk: {Risk})", action, riskLevel);

        var request = new ConfirmationRequest
        {
            Type = ConfirmationType.Binary,
            Action = action,
            Description = description,
            RiskLevel = riskLevel,
            Context = context ?? new Dictionary<string, object>(),
            Options = new[]
            {
                new ConfirmationOption { Id = "approve", Label = "Approve", IsDefault = true },
                new ConfirmationOption { Id = "reject", Label = "Reject", IsDangerous = true },
                new ConfirmationOption { Id = "modify", Label = "Modify" }
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
            _ => ApprovalResult.Rejected(action, "Unknown response")
        };
    }

    /// <summary>
    /// Request approval from multiple approvers
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
                $"{action} (Approval {i + 1}/{requiredApprovals})",
                description,
                context,
                cancellationToken);

            if (result.IsApproved)
            {
                approvals.Add(result.ApprovedBy ?? $"Approver-{i + 1}");
            }
            else
            {
                rejections.Add((result.ApprovedBy ?? $"Approver-{i + 1}", result.RejectionReason ?? "Unknown"));
            }
        }

        if (approvals.Count >= requiredApprovals)
        {
            return ApprovalResult.Approved(action, string.Join(", ", approvals));
        }

        return ApprovalResult.Rejected(action, 
            $"Insufficient approvals: {approvals.Count}/{requiredApprovals}. " +
            $"Rejections: {string.Join("; ", rejections.Select(r => $"{r.Approver}: {r.Reason}"))}");
    }

    private RiskLevel AssessRiskLevel(string action, IDictionary<string, object>? context)
    {
        // Check high-risk keywords
        var highRiskKeywords = new[] { "delete", "remove", "destroy", "execute", "transfer", "payment" };
        var criticalKeywords = new[] { "production", "financial", "customer data", "credentials" };

        var lowerAction = action.ToLower();

        if (criticalKeywords.Any(k => lowerAction.Contains(k)))
            return RiskLevel.Critical;

        if (highRiskKeywords.Any(k => lowerAction.Contains(k)))
            return RiskLevel.High;

        // Check context for risk indicators
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
        RejectionReason = "Approval request timed out"
    };
}
```

### 3. Human-in-the-Loop Agent

```csharp
namespace DawningAgents.Core.HumanLoop;

using Microsoft.Extensions.Logging;

/// <summary>
/// Agent that involves humans at decision points
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
            // Pre-execution confirmation if configured
            if (_config.ConfirmBeforeExecution)
            {
                var approval = await _workflow.RequestApprovalAsync(
                    "Execute agent task",
                    $"Agent '{_innerAgent.Name}' will process: {context.Input}",
                    cancellationToken: cancellationToken);

                if (!approval.IsApproved)
                {
                    return new AgentResponse
                    {
                        Output = $"Task not approved: {approval.RejectionReason}",
                        IsSuccess = false,
                        Duration = DateTime.UtcNow - startTime
                    };
                }
            }

            // Execute with escalation handling
            var response = await ExecuteWithEscalationAsync(context, cancellationToken);

            // Post-execution review if configured
            if (_config.ReviewBeforeReturn && response.IsSuccess)
            {
                response = await ReviewResponseAsync(response, cancellationToken);
            }

            return response;
        }
        catch (AgentEscalationException ex)
        {
            _logger.LogWarning("Agent escalated: {Reason}", ex.Reason);
            
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
                _logger.LogWarning(ex, "Attempt {Attempt} failed, requesting guidance", attempt + 1);

                var input = await _handler.RequestInputAsync(
                    $"Agent encountered error: {ex.Message}\nProvide guidance or type 'abort' to stop:",
                    cancellationToken: cancellationToken);

                if (input.Equals("abort", StringComparison.OrdinalIgnoreCase))
                {
                    throw new OperationCanceledException("Aborted by user");
                }

                // Retry with guidance
                context = context with
                {
                    Input = $"{context.Input}\n\nAdditional guidance: {input}"
                };
            }
        }

        throw lastException ?? new Exception("Unknown error");
    }

    private async Task<AgentResponse> ReviewResponseAsync(
        AgentResponse response,
        CancellationToken cancellationToken)
    {
        var review = await _handler.RequestConfirmationAsync(new ConfirmationRequest
        {
            Type = ConfirmationType.Review,
            Action = "Review response",
            Description = $"Agent response:\n\n{response.Output}",
            RiskLevel = RiskLevel.Low,
            Options = new[]
            {
                new ConfirmationOption { Id = "approve", Label = "Approve", IsDefault = true },
                new ConfirmationOption { Id = "edit", Label = "Edit response" },
                new ConfirmationOption { Id = "reject", Label = "Reject" }
            }
        }, cancellationToken);

        return review.SelectedOption switch
        {
            "approve" => response,
            "edit" => response with { Output = review.ModifiedContent ?? response.Output },
            "reject" => response with { IsSuccess = false, Output = "Response rejected by reviewer" },
            _ => response
        };
    }

    private AgentResponse HandleEscalationResult(EscalationResult result, DateTime startTime)
    {
        return result.Action switch
        {
            EscalationAction.Resolved => new AgentResponse
            {
                Output = result.Resolution ?? "Resolved by human",
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
                Output = "Step skipped by human",
                IsSuccess = true,
                Duration = DateTime.UtcNow - startTime
            },
            _ => new AgentResponse
            {
                Output = "Operation aborted by human",
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
/// Exception to escalate to human
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

## Complete Example

```csharp
// Create handler
var handler = new ConsoleInteractionHandler(
    loggerFactory.CreateLogger<ConsoleInteractionHandler>());

// Create approval workflow
var workflow = new ApprovalWorkflow(
    handler,
    new ApprovalConfig
    {
        RequireApprovalForLowRisk = false,
        RequireApprovalForMediumRisk = true,
        ApprovalTimeout = TimeSpan.FromMinutes(5)
    },
    loggerFactory.CreateLogger<ApprovalWorkflow>());

// Create human-in-loop agent
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

// Execute with human oversight
var response = await hilAgent.ExecuteAsync(new AgentContext
{
    Input = "Delete all expired user accounts from the database"
});

Console.WriteLine(response.Output);

// Direct approval workflow usage
var approval = await workflow.RequestApprovalAsync(
    "Transfer funds",
    "Transfer $50,000 to account XYZ-123",
    new Dictionary<string, object>
    {
        ["amount"] = 50000m,
        ["destination"] = "XYZ-123"
    });

if (approval.IsApproved)
{
    Console.WriteLine($"Transfer approved by {approval.ApprovedBy}");
}
else
{
    Console.WriteLine($"Transfer rejected: {approval.RejectionReason}");
}
```

---

## Summary

### Week 10 Deliverables

```
src/DawningAgents.Core/
└── HumanLoop/
    ├── ConfirmationRequest.cs      # Request models
    ├── ConfirmationResponse.cs     # Response models
    ├── EscalationRequest.cs        # Escalation models
    ├── EscalationResult.cs         # Escalation result
    ├── IHumanInteractionHandler.cs # Handler interface
    ├── ApprovalWorkflow.cs         # Approval logic
    ├── HumanInLoopAgent.cs         # Agent wrapper
    └── Handlers/
        ├── ConsoleInteractionHandler.cs  # Console UI
        └── AsyncCallbackHandler.cs       # Async/API
```

### Human-in-the-Loop Patterns

| Pattern | Use Case |
|---------|----------|
| **Confirmation** | Binary yes/no decisions |
| **Multi-Choice** | Select from options |
| **Review** | Review and modify output |
| **Approval Workflow** | Risk-based approval |
| **Escalation** | Handle errors/edge cases |
| **Multi-Approval** | Critical operations |

### Phase 5 Complete!

With Week 9-10 complete, you've learned:
- Safety & Guardrails (input validation, content moderation, rate limiting)
- Human-in-the-Loop patterns (confirmations, approvals, escalation)

Next: Phase 6 - Production Readiness (Week 11-12)
