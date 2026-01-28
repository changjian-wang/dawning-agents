using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Dawning.Agents.Core.Health;

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
            var pong = await db.PingAsync();
            _logger.LogDebug($"RedisHealthCheck: Ping={pong.TotalMilliseconds}ms");
            return HealthCheckResult.Healthy($"Redis 正常, Ping={pong.TotalMilliseconds}ms");
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "RedisHealthCheck: 连接失败");
            return HealthCheckResult.Unhealthy("Redis 连接失败", ex);
        }
    }
}
