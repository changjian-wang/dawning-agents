using System.Diagnostics;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.HumanLoop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.HumanLoop;

/// <summary>
/// 在决策点引入人工参与的 Agent 包装器
/// </summary>
public class HumanInLoopAgent : IAgent
{
    private readonly IAgent _innerAgent;
    private readonly IHumanInteractionHandler _handler;
    private readonly ApprovalWorkflow _workflow;
    private readonly HumanLoopOptions _options;
    private readonly ILogger<HumanInLoopAgent> _logger;

    /// <inheritdoc />
    public string Name => $"HumanLoop({_innerAgent.Name})";

    /// <inheritdoc />
    public string Instructions => _innerAgent.Instructions;

    /// <summary>
    /// 创建人机协作 Agent 实例
    /// </summary>
    public HumanInLoopAgent(
        IAgent innerAgent,
        IHumanInteractionHandler handler,
        IOptions<HumanLoopOptions>? options = null,
        ILogger<HumanInLoopAgent>? logger = null
    )
    {
        _innerAgent = innerAgent;
        _handler = handler;
        _options = options?.Value ?? new HumanLoopOptions();
        _logger = logger ?? NullLogger<HumanInLoopAgent>.Instance;
        _workflow = new ApprovalWorkflow(
            handler,
            new ApprovalConfig
            {
                RequireApprovalForMediumRisk = _options.RequireApprovalForMediumRisk,
                ApprovalTimeout = _options.DefaultTimeout,
            },
            options
        );
    }

    /// <summary>
    /// 创建人机协作 Agent 实例（带自定义工作流）
    /// </summary>
    public HumanInLoopAgent(
        IAgent innerAgent,
        IHumanInteractionHandler handler,
        ApprovalWorkflow workflow,
        IOptions<HumanLoopOptions>? options = null,
        ILogger<HumanInLoopAgent>? logger = null
    )
    {
        _innerAgent = innerAgent;
        _handler = handler;
        _workflow = workflow;
        _options = options?.Value ?? new HumanLoopOptions();
        _logger = logger ?? NullLogger<HumanInLoopAgent>.Instance;
    }

    /// <inheritdoc />
    public async Task<AgentResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        var context = new AgentContext { UserInput = input };
        return await RunAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AgentResponse> RunAsync(
        AgentContext context,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 执行前确认（如果配置）
            if (_options.ConfirmBeforeExecution)
            {
                var approval = await _workflow.RequestApprovalAsync(
                    "执行 Agent 任务",
                    $"Agent '{_innerAgent.Name}' 将处理：{context.UserInput}",
                    new Dictionary<string, object> { ["sessionId"] = context.SessionId },
                    cancellationToken
                );

                if (!approval.IsApproved)
                {
                    _logger.LogWarning("任务未获得批准：{Reason}", approval.RejectionReason);
                    return AgentResponse.Failed(
                        $"任务未批准：{approval.RejectionReason}",
                        [],
                        stopwatch.Elapsed
                    );
                }

                _logger.LogInformation("任务已获得批准");
            }

            // 带升级处理的执行
            var response = await ExecuteWithEscalationAsync(context, cancellationToken);

            // 返回前审查（如果配置）
            if (_options.ReviewBeforeReturn && response.Success)
            {
                response = await ReviewResponseAsync(
                    response,
                    stopwatch.Elapsed,
                    cancellationToken
                );
            }

            return response;
        }
        catch (AgentEscalationException ex)
        {
            _logger.LogWarning("Agent 升级：{Reason}", ex.Reason);

            var escalation = await _handler.EscalateAsync(
                new EscalationRequest
                {
                    Reason = ex.Reason,
                    Description = ex.Description,
                    Severity = EscalationSeverity.High,
                    AgentName = _innerAgent.Name,
                    TaskId = context.SessionId,
                    Context = ex.Context,
                    AttemptedSolutions = ex.AttemptedSolutions,
                },
                cancellationToken
            );

            return HandleEscalationResult(escalation, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent 执行过程中发生错误");
            return AgentResponse.Failed($"执行过程中发生错误：{ex.Message}", [], stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// 带升级处理的执行
    /// </summary>
    private async Task<AgentResponse> ExecuteWithEscalationAsync(
        AgentContext context,
        CancellationToken cancellationToken
    )
    {
        Exception? lastException = null;
        var attemptedSolutions = new List<string>();

        for (int attempt = 0; attempt < _options.MaxRetries; attempt++)
        {
            try
            {
                _logger.LogDebug("执行第 {Attempt} 次尝试", attempt + 1);
                return await _innerAgent.RunAsync(context, cancellationToken);
            }
            catch (AgentEscalationException)
            {
                // AgentEscalationException 应该直接向上传播
                throw;
            }
            catch (OperationCanceledException)
            {
                // 取消操作应该直接向上传播
                throw;
            }
            catch (Exception ex) when (attempt < _options.MaxRetries - 1)
            {
                lastException = ex;
                attemptedSolutions.Add($"第 {attempt + 1} 次尝试失败：{ex.Message}");
                _logger.LogWarning(ex, "第 {Attempt} 次尝试失败，请求指导", attempt + 1);

                var input = await _handler.RequestInputAsync(
                    $"Agent 遇到错误：{ex.Message}\n请提供指导或输入 'abort' 停止：",
                    cancellationToken: cancellationToken
                );

                if (input.Equals("abort", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("用户选择中止操作");
                    throw new OperationCanceledException("用户中止");
                }

                // 带指导重试 - 创建新的上下文
                context = new AgentContext
                {
                    SessionId = context.SessionId,
                    UserInput = $"{context.UserInput}\n\n额外指导：{input}",
                    MaxSteps = context.MaxSteps,
                };

                // 复制元数据
                foreach (var kvp in context.Metadata)
                {
                    context.Metadata[kvp.Key] = kvp.Value;
                }
            }
        }

        // 所有重试都失败，抛出升级异常
        if (lastException != null)
        {
            throw new AgentEscalationException(
                "多次尝试后仍然失败",
                lastException.Message,
                lastException,
                new Dictionary<string, object>
                {
                    ["attempts"] = _options.MaxRetries,
                    ["lastError"] = lastException.Message,
                },
                attemptedSolutions
            );
        }
        else
        {
            throw new AgentEscalationException(
                "多次尝试后仍然失败",
                "未知错误",
                new Dictionary<string, object> { ["attempts"] = _options.MaxRetries },
                attemptedSolutions
            );
        }
    }

    /// <summary>
    /// 审查响应
    /// </summary>
    private async Task<AgentResponse> ReviewResponseAsync(
        AgentResponse response,
        TimeSpan currentDuration,
        CancellationToken cancellationToken
    )
    {
        var review = await _handler.RequestConfirmationAsync(
            new ConfirmationRequest
            {
                Type = ConfirmationType.Review,
                Action = "审查响应",
                Description = $"Agent 响应：\n\n{response.FinalAnswer}",
                RiskLevel = RiskLevel.Low,
                Options =
                [
                    new ConfirmationOption
                    {
                        Id = "approve",
                        Label = "批准",
                        IsDefault = true,
                    },
                    new ConfirmationOption { Id = "edit", Label = "编辑响应" },
                    new ConfirmationOption { Id = "reject", Label = "拒绝" },
                ],
            },
            cancellationToken
        );

        return review.SelectedOption switch
        {
            "approve" => response,
            "edit" => response with
            {
                FinalAnswer = review.ModifiedContent ?? response.FinalAnswer,
            },
            "reject" => AgentResponse.Failed("响应被审查者拒绝", response.Steps, currentDuration),
            _ => response,
        };
    }

    /// <summary>
    /// 处理升级结果
    /// </summary>
    private AgentResponse HandleEscalationResult(EscalationResult result, TimeSpan duration)
    {
        return result.Action switch
        {
            EscalationAction.Resolved => AgentResponse.Successful(
                result.Resolution ?? "已由人工解决",
                [],
                duration
            ),
            EscalationAction.Skipped => AgentResponse.Successful("步骤被人工跳过", [], duration),
            _ => AgentResponse.Failed("操作被人工中止", [], duration),
        };
    }
}
