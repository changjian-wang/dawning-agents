using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Dawning.Agents.Redis;

/// <summary>
/// Redis 连接健康检查
/// </summary>
public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisHealthCheck> _logger;

    public RedisHealthCheck(IConnectionMultiplexer redis, ILogger<RedisHealthCheck>? logger = null)
    {
        _redis = redis;
        _logger =
            logger
            ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RedisHealthCheck>.Instance;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var db = _redis.GetDatabase();
            var pong = await db.PingAsync().ConfigureAwait(false);
            _logger.LogDebug("RedisHealthCheck: Ping={PingMs}ms", pong.TotalMilliseconds);
            return HealthCheckResult.Healthy($"Redis 正常, Ping={pong.TotalMilliseconds}ms");
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "RedisHealthCheck: 连接失败");
            return HealthCheckResult.Unhealthy("Redis 连接失败", ex);
        }
    }
}
