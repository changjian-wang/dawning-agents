namespace Dawning.Agents.Core.Scaling;

using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Scaling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Agent 工作池实现
/// </summary>
public class AgentWorkerPool : IAgentWorkerPool
{
    private readonly IAgent _agent;
    private readonly IAgentRequestQueue _queue;
    private readonly ILogger<AgentWorkerPool> _logger;
    private readonly List<Task> _workers = [];
    private readonly CancellationTokenSource _cts = new();
    private readonly int _workerCount;
    private bool _isRunning;
    private bool _disposed;

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
        if (_isRunning)
        {
            _logger.LogWarning("工作池已在运行中");
            return;
        }

        for (int i = 0; i < _workerCount; i++)
        {
            var workerId = i;
            _workers.Add(Task.Run(() => WorkerLoopAsync(workerId, _cts.Token)));
        }

        _isRunning = true;
        _logger.LogInformation("已启动 {WorkerCount} 个 Agent 工作线程", _workerCount);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            return;
        }

        _logger.LogInformation("正在停止工作池...");
        _cts.Cancel();

        try
        {
            await Task.WhenAll(_workers).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("工作池停止超时");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("工作池停止被取消");
        }

        _workers.Clear();
        _isRunning = false;
        _logger.LogInformation("工作池已停止");
    }

    private async Task WorkerLoopAsync(int workerId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("工作线程 {WorkerId} 已启动", workerId);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var item = await _queue.DequeueAsync(cancellationToken);
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
                    var response = await _agent.RunAsync(item.Input, linkedCts.Token);
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
                        "工作线程 {WorkerId} 处理项 {ItemId} 失败",
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
                _logger.LogError(ex, "工作线程 {WorkerId} 遇到意外错误", workerId);
                await Task.Delay(1000, cancellationToken);
            }
        }

        _logger.LogDebug("工作线程 {WorkerId} 已停止", workerId);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cts.Cancel();

        try
        {
            Task.WhenAll(_workers).Wait(TimeSpan.FromSeconds(30));
        }
        catch
        {
            // 忽略停止时的异常
        }

        _cts.Dispose();
        _disposed = true;
    }
}
