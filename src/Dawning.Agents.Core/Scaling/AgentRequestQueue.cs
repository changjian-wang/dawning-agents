namespace Dawning.Agents.Core.Scaling;

using System.Threading.Channels;
using Dawning.Agents.Abstractions.Scaling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Agent 请求队列实现
/// </summary>
public sealed class AgentRequestQueue : IAgentRequestQueue
{
    private readonly Channel<AgentWorkItem> _channel;
    private readonly ILogger<AgentRequestQueue> _logger;

    public AgentRequestQueue(int capacity, ILogger<AgentRequestQueue>? logger = null)
    {
        _channel = Channel.CreateBounded<AgentWorkItem>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false,
            }
        );
        _logger = logger ?? NullLogger<AgentRequestQueue>.Instance;
    }

    /// <inheritdoc />
    public async ValueTask EnqueueAsync(
        AgentWorkItem item,
        CancellationToken cancellationToken = default
    )
    {
        await _channel.Writer.WriteAsync(item, cancellationToken);
        _logger.LogDebug("工作项 {WorkItemId} 已入队", item.Id);
    }

    /// <inheritdoc />
    public async ValueTask<AgentWorkItem?> DequeueAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var item = await _channel.Reader.ReadAsync(cancellationToken);
            _logger.LogDebug("工作项 {WorkItemId} 已出队", item.Id);
            return item;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public int Count => _channel.Reader.Count;

    /// <inheritdoc />
    public bool CanWrite => !_channel.Reader.Completion.IsCompleted;

    /// <summary>
    /// 关闭队列
    /// </summary>
    public void Complete()
    {
        _channel.Writer.Complete();
        _logger.LogInformation("请求队列已关闭");
    }
}
