using System.Collections.Concurrent;
using Dawning.Agents.Abstractions.HumanLoop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.HumanLoop;

/// <summary>
/// Async callback handler for Web/API interactions.
/// </summary>
public sealed class AsyncCallbackHandler : IHumanInteractionHandler, IDisposable
{
    private readonly ConcurrentDictionary<
        string,
        TaskCompletionSource<ConfirmationResponse>
    > _pendingConfirmations = new();
    private readonly ConcurrentDictionary<
        string,
        TaskCompletionSource<EscalationResult>
    > _pendingEscalations = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingInputs =
        new();
    private readonly ILogger<AsyncCallbackHandler> _logger;
    private volatile bool _disposed;

    /// <summary>
    /// Raised when a confirmation is requested.
    /// </summary>
    public event EventHandler<ConfirmationRequest>? ConfirmationRequested;

    /// <summary>
    /// Raised when an escalation is requested.
    /// </summary>
    public event EventHandler<EscalationRequest>? EscalationRequested;

    /// <summary>
    /// Raised when user input is requested.
    /// </summary>
    public event EventHandler<(string Id, string Prompt, string? DefaultValue)>? InputRequested;

    /// <summary>
    /// Raised when a notification is sent.
    /// </summary>
    public event EventHandler<(string Message, NotificationLevel Level)>? NotificationSent;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncCallbackHandler"/> class.
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);

        var tcs = new TaskCompletionSource<ConfirmationResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        _pendingConfirmations[request.Id] = tcs;

        _logger.LogDebug("Sending confirmation request {RequestId}", request.Id);

        // Raise event for UI handling
        try
        {
            ConfirmationRequested?.Invoke(this, request);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Confirmation request event handler failed {RequestId}",
                request.Id
            );
        }

        try
        {
            // Wait for response with timeout support
            if (request.Timeout.HasValue)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(request.Timeout.Value);

                try
                {
                    return await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Timeout - return default value
                    _logger.LogDebug("Confirmation request {RequestId} timed out", request.Id);
                    return new ConfirmationResponse
                    {
                        RequestId = request.Id,
                        SelectedOption = request.DefaultOnTimeout ?? "timeout",
                    };
                }
            }

            return await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var requestId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        _pendingInputs[requestId] = tcs;

        _logger.LogDebug("Sending input request {RequestId}", requestId);

        // Raise event for UI handling
        try
        {
            InputRequested?.Invoke(this, (requestId, prompt, defaultValue));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Input request event handler failed {RequestId}", requestId);
        }

        try
        {
            return await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
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
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        _logger.LogDebug("Sending notification: {Level} - {Message}", level, message);
        try
        {
            NotificationSent?.Invoke(this, (message, level));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Notification event handler failed");
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<EscalationResult> EscalateAsync(
        EscalationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);

        var tcs = new TaskCompletionSource<EscalationResult>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        _pendingEscalations[request.Id] = tcs;

        _logger.LogDebug("Sending escalation request {RequestId}", request.Id);

        // Raise event for UI handling
        try
        {
            EscalationRequested?.Invoke(this, request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Escalation request event handler failed {RequestId}", request.Id);
        }

        try
        {
            return await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _pendingEscalations.TryRemove(request.Id, out _);
        }
    }

    /// <summary>
    /// Completes a pending confirmation request (called by UI/API).
    /// </summary>
    /// <param name="response">The confirmation response.</param>
    /// <returns><see langword="true"/> if the request was completed successfully; otherwise, <see langword="false"/>.</returns>
    public bool CompleteConfirmation(ConfirmationResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (_pendingConfirmations.TryGetValue(response.RequestId, out var tcs))
        {
            _logger.LogDebug("Completed confirmation request {RequestId}", response.RequestId);
            return tcs.TrySetResult(response);
        }
        _logger.LogWarning("Confirmation request {RequestId} not found", response.RequestId);
        return false;
    }

    /// <summary>
    /// Completes a pending escalation request (called by UI/API).
    /// </summary>
    /// <param name="result">The escalation result.</param>
    /// <returns><see langword="true"/> if the request was completed successfully; otherwise, <see langword="false"/>.</returns>
    public bool CompleteEscalation(EscalationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (_pendingEscalations.TryGetValue(result.RequestId, out var tcs))
        {
            _logger.LogDebug("Completed escalation request {RequestId}", result.RequestId);
            return tcs.TrySetResult(result);
        }
        _logger.LogWarning("Escalation request {RequestId} not found", result.RequestId);
        return false;
    }

    /// <summary>
    /// Completes a pending input request (called by UI/API).
    /// </summary>
    /// <param name="requestId">The request ID.</param>
    /// <param name="input">The user input.</param>
    /// <returns><see langword="true"/> if the request was completed successfully; otherwise, <see langword="false"/>.</returns>
    public bool CompleteInput(string requestId, string input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(input);

        if (_pendingInputs.TryGetValue(requestId, out var tcs))
        {
            _logger.LogDebug("Completed input request {RequestId}", requestId);
            return tcs.TrySetResult(input);
        }
        _logger.LogWarning("Input request {RequestId} not found", requestId);
        return false;
    }

    /// <summary>
    /// Gets all pending confirmation request IDs.
    /// </summary>
    public IReadOnlyCollection<string> GetPendingConfirmationIds() =>
        _pendingConfirmations.Keys.ToList();

    /// <summary>
    /// Gets all pending escalation request IDs.
    /// </summary>
    public IReadOnlyCollection<string> GetPendingEscalationIds() =>
        _pendingEscalations.Keys.ToList();

    /// <summary>
    /// Gets all pending input request IDs.
    /// </summary>
    public IReadOnlyCollection<string> GetPendingInputIds() => _pendingInputs.Keys.ToList();

    /// <summary>
    /// Cancels a specific confirmation request.
    /// </summary>
    public bool CancelConfirmation(string requestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        if (_pendingConfirmations.TryRemove(requestId, out var tcs))
        {
            return tcs.TrySetCanceled();
        }
        return false;
    }

    /// <summary>
    /// Cancels a specific escalation request.
    /// </summary>
    public bool CancelEscalation(string requestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        if (_pendingEscalations.TryRemove(requestId, out var tcs))
        {
            return tcs.TrySetCanceled();
        }
        return false;
    }

    /// <summary>
    /// Cancels a specific input request.
    /// </summary>
    public bool CancelInput(string requestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        if (_pendingInputs.TryRemove(requestId, out var tcs))
        {
            return tcs.TrySetCanceled();
        }
        return false;
    }

    /// <summary>
    /// Cancels all pending requests and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var tcs in _pendingConfirmations.Values)
        {
            tcs.TrySetCanceled();
        }
        _pendingConfirmations.Clear();

        foreach (var tcs in _pendingEscalations.Values)
        {
            tcs.TrySetCanceled();
        }
        _pendingEscalations.Clear();

        foreach (var tcs in _pendingInputs.Values)
        {
            tcs.TrySetCanceled();
        }
        _pendingInputs.Clear();

        ConfirmationRequested = null;
        EscalationRequested = null;
        InputRequested = null;
        NotificationSent = null;
    }
}
