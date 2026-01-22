using System.Collections.Concurrent;
using Dawning.Agents.Abstractions.HumanLoop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.HumanLoop;

/// <summary>
/// 用于 Web/API 交互的异步回调处理器
/// </summary>
public class AsyncCallbackHandler : IHumanInteractionHandler
{
    private readonly ConcurrentDictionary<
        string,
        TaskCompletionSource<ConfirmationResponse>
    > _pendingConfirmations = new();
    private readonly ConcurrentDictionary<
        string,
        TaskCompletionSource<EscalationResult>
    > _pendingEscalations = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingInputs = new();
    private readonly ILogger<AsyncCallbackHandler> _logger;

    /// <summary>
    /// 当请求确认时触发
    /// </summary>
    public event EventHandler<ConfirmationRequest>? ConfirmationRequested;

    /// <summary>
    /// 当请求升级时触发
    /// </summary>
    public event EventHandler<EscalationRequest>? EscalationRequested;

    /// <summary>
    /// 当请求输入时触发
    /// </summary>
    public event EventHandler<(string Id, string Prompt, string? DefaultValue)>? InputRequested;

    /// <summary>
    /// 当发送通知时触发
    /// </summary>
    public event EventHandler<(string Message, NotificationLevel Level)>? NotificationSent;

    /// <summary>
    /// 创建异步回调处理器实例
    /// </summary>
    public AsyncCallbackHandler(ILogger<AsyncCallbackHandler>? logger = null)
    {
        _logger = logger ?? NullLogger<AsyncCallbackHandler>.Instance;
    }

    /// <inheritdoc />
    public async Task<ConfirmationResponse> RequestConfirmationAsync(
        ConfirmationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var tcs = new TaskCompletionSource<ConfirmationResponse>();
        _pendingConfirmations[request.Id] = tcs;

        _logger.LogDebug("发送确认请求 {RequestId}", request.Id);

        // 触发事件供 UI 处理
        ConfirmationRequested?.Invoke(this, request);

        try
        {
            // 等待响应，支持超时
            if (request.Timeout.HasValue)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(request.Timeout.Value);

                try
                {
                    return await tcs.Task.WaitAsync(cts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // 超时 - 返回默认值
                    _logger.LogDebug("确认请求 {RequestId} 超时", request.Id);
                    return new ConfirmationResponse
                    {
                        RequestId = request.Id,
                        SelectedOption = request.DefaultOnTimeout ?? "timeout",
                    };
                }
            }

            return await tcs.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            _pendingConfirmations.TryRemove(request.Id, out _);
        }
    }

    /// <inheritdoc />
    public async Task<string> RequestInputAsync(
        string prompt,
        string? defaultValue = null,
        CancellationToken cancellationToken = default
    )
    {
        var requestId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<string>();
        _pendingInputs[requestId] = tcs;

        _logger.LogDebug("发送输入请求 {RequestId}", requestId);

        // 触发事件供 UI 处理
        InputRequested?.Invoke(this, (requestId, prompt, defaultValue));

        try
        {
            return await tcs.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            _pendingInputs.TryRemove(requestId, out _);
        }
    }

    /// <inheritdoc />
    public Task NotifyAsync(
        string message,
        NotificationLevel level = NotificationLevel.Info,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("发送通知：{Level} - {Message}", level, message);
        NotificationSent?.Invoke(this, (message, level));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<EscalationResult> EscalateAsync(
        EscalationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var tcs = new TaskCompletionSource<EscalationResult>();
        _pendingEscalations[request.Id] = tcs;

        _logger.LogDebug("发送升级请求 {RequestId}", request.Id);

        // 触发事件供 UI 处理
        EscalationRequested?.Invoke(this, request);

        try
        {
            return await tcs.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            _pendingEscalations.TryRemove(request.Id, out _);
        }
    }

    /// <summary>
    /// 完成挂起的确认请求（由 UI/API 调用）
    /// </summary>
    /// <param name="response">确认响应</param>
    /// <returns>是否成功完成</returns>
    public bool CompleteConfirmation(ConfirmationResponse response)
    {
        if (_pendingConfirmations.TryGetValue(response.RequestId, out var tcs))
        {
            _logger.LogDebug("完成确认请求 {RequestId}", response.RequestId);
            return tcs.TrySetResult(response);
        }
        _logger.LogWarning("未找到确认请求 {RequestId}", response.RequestId);
        return false;
    }

    /// <summary>
    /// 完成挂起的升级请求（由 UI/API 调用）
    /// </summary>
    /// <param name="result">升级结果</param>
    /// <returns>是否成功完成</returns>
    public bool CompleteEscalation(EscalationResult result)
    {
        if (_pendingEscalations.TryGetValue(result.RequestId, out var tcs))
        {
            _logger.LogDebug("完成升级请求 {RequestId}", result.RequestId);
            return tcs.TrySetResult(result);
        }
        _logger.LogWarning("未找到升级请求 {RequestId}", result.RequestId);
        return false;
    }

    /// <summary>
    /// 完成挂起的输入请求（由 UI/API 调用）
    /// </summary>
    /// <param name="requestId">请求 ID</param>
    /// <param name="input">用户输入</param>
    /// <returns>是否成功完成</returns>
    public bool CompleteInput(string requestId, string input)
    {
        if (_pendingInputs.TryGetValue(requestId, out var tcs))
        {
            _logger.LogDebug("完成输入请求 {RequestId}", requestId);
            return tcs.TrySetResult(input);
        }
        _logger.LogWarning("未找到输入请求 {RequestId}", requestId);
        return false;
    }

    /// <summary>
    /// 获取所有挂起的确认请求 ID
    /// </summary>
    public IReadOnlyCollection<string> GetPendingConfirmationIds() =>
        _pendingConfirmations.Keys.ToList();

    /// <summary>
    /// 获取所有挂起的升级请求 ID
    /// </summary>
    public IReadOnlyCollection<string> GetPendingEscalationIds() =>
        _pendingEscalations.Keys.ToList();

    /// <summary>
    /// 获取所有挂起的输入请求 ID
    /// </summary>
    public IReadOnlyCollection<string> GetPendingInputIds() => _pendingInputs.Keys.ToList();

    /// <summary>
    /// 取消指定的确认请求
    /// </summary>
    public bool CancelConfirmation(string requestId)
    {
        if (_pendingConfirmations.TryRemove(requestId, out var tcs))
        {
            return tcs.TrySetCanceled();
        }
        return false;
    }

    /// <summary>
    /// 取消指定的升级请求
    /// </summary>
    public bool CancelEscalation(string requestId)
    {
        if (_pendingEscalations.TryRemove(requestId, out var tcs))
        {
            return tcs.TrySetCanceled();
        }
        return false;
    }

    /// <summary>
    /// 取消指定的输入请求
    /// </summary>
    public bool CancelInput(string requestId)
    {
        if (_pendingInputs.TryRemove(requestId, out var tcs))
        {
            return tcs.TrySetCanceled();
        }
        return false;
    }
}
