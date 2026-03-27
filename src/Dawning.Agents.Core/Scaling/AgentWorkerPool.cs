namespace Dawning.Agents.Core.Scaling;

using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Scaling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Agent worker pool implementation.
/// </summary>
public sealed class AgentWorkerPool : IAgentWorkerPool
{
    private readonly IAgent _agent;
    private readonly IAgentRequestQueue _queue;
    private readonly ILogger<AgentWorkerPool> _logger;
    private readonly List<Task> _workers = [];
    private CancellationTokenSource? _runCts;
    private readonly Lock _lock = new();
    private readonly int _workerCount;
    private bool _isRunning;
    private volatile bool _disposed;

    public AgentWorkerPool(
        IAgent agent,
        IAgentRequestQueue queue,
        int workerCount,
        ILogger<AgentWorkerPool>? logger = null
    )
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _workerCount = workerCount > 0 ? workerCount : Environment.ProcessorCount * 2;
        _logger = logger ?? NullLogger<AgentWorkerPool>.Instance;
    }

    /// <inheritdoc />
    public int WorkerCount => _workerCount;

    /// <inheritdoc />
    public bool IsRunning => _isRunning;

    /// <inheritdoc />
    public void Start()
    {
        CancellationTokenSource runCts;

        lock (_lock)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AgentWorkerPool));
            }

            if (_isRunning)
            {
                _logger.LogWarning("Worker pool is already running");
                return;
            }

            _runCts?.Dispose();
            _runCts = new CancellationTokenSource();
            runCts = _runCts;
            _workers.Clear();

            for (int i = 0; i < _workerCount; i++)
            {
                var workerId = i;
                _workers.Add(Task.Run(() => WorkerLoopAsync(workerId, runCts.Token), runCts.Token));
            }

            _isRunning = true;
        }

        _logger.LogInformation("Started {WorkerCount} agent worker threads", _workerCount);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task[] snapshot;
        CancellationTokenSource? runCts;
        lock (_lock)
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            snapshot = [.. _workers];
            _workers.Clear();
            runCts = _runCts;
            _runCts = null;
        }

        _logger.LogInformation("Stopping worker pool...");

        if (runCts != null)
        {
            await runCts.CancelAsync().ConfigureAwait(false);
        }

        try
        {
            await Task.WhenAll(snapshot)
                .WaitAsync(TimeSpan.FromSeconds(30), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Worker pool stop timed out");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Worker pool stop was cancelled");
        }

        runCts?.Dispose();

        _logger.LogInformation("Worker pool stopped");
    }

    private async Task WorkerLoopAsync(int workerId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Worker {WorkerId} started", workerId);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var item = await _queue.DequeueAsync(cancellationToken).ConfigureAwait(false);
                if (item == null)
                {
                    continue;
                }

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    item.CancellationToken
                );

                try
                {
                    var response = await _agent
                        .RunAsync(item.Input, linkedCts.Token)
                        .ConfigureAwait(false);
                    item.CompletionSource.TrySetResult(response);
                }
                catch (OperationCanceledException)
                {
                    item.CompletionSource.TrySetCanceled();
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Worker {WorkerId} failed to process item {ItemId}",
                        workerId,
                        item.Id
                    );
                    item.CompletionSource.TrySetException(ex);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker {WorkerId} encountered an unexpected error", workerId);
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.LogDebug("Worker {WorkerId} stopped", workerId);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Task[] snapshot;
        CancellationTokenSource? runCts;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _isRunning = false;
            snapshot = [.. _workers];
            _workers.Clear();
            runCts = _runCts;
            _runCts = null;
        }

        runCts?.Cancel();
        runCts?.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Task[] snapshot;
        CancellationTokenSource? runCts;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _isRunning = false;
            snapshot = [.. _workers];
            _workers.Clear();
            runCts = _runCts;
            _runCts = null;
        }

        if (runCts != null)
        {
            await runCts.CancelAsync().ConfigureAwait(false);
        }

        try
        {
            await Task.WhenAll(snapshot).WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Ignore exceptions during shutdown
        }

        runCts?.Dispose();
    }
}
