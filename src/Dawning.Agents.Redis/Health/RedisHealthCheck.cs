using System.Globalization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Dawning.Agents.Redis;

/// <summary>
/// Performs health checks for the Redis connection.
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisHealthCheck> _logger;

    public RedisHealthCheck(IConnectionMultiplexer redis, ILogger<RedisHealthCheck>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(redis);
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
            return HealthCheckResult.Healthy(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Redis healthy, Ping={pong.TotalMilliseconds}ms"
                )
            );
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "RedisHealthCheck: Connection failed");
            return HealthCheckResult.Unhealthy("Redis connection failed", ex);
        }
    }
}
