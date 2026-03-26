using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.Distributed;

/// <summary>
/// Configuration options for Redis.
/// </summary>
/// <remarks>
/// Example appsettings.json configuration:
/// <code>
/// {
///   "Redis": {
///     "ConnectionString": "localhost:6379",
///     "InstanceName": "dawning:",
///     "DefaultDatabase": 0
///   }
/// }
/// </code>
/// </remarks>
public sealed class RedisOptions : IValidatableOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Redis";

    /// <summary>
    /// The Redis connection string.
    /// </summary>
    /// <example>localhost:6379,password=xxx,ssl=false,abortConnect=false</example>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// The instance name prefix.
    /// </summary>
    /// <remarks>All keys are prefixed with this value to distinguish between different applications.</remarks>
    public string InstanceName { get; set; } = "dawning:";

    /// <summary>
    /// The default database index.
    /// </summary>
    public int DefaultDatabase { get; set; } = 0;

    /// <summary>
    /// The connection timeout in milliseconds.
    /// </summary>
    public int ConnectTimeout { get; set; } = 5000;

    /// <summary>
    /// The synchronous operation timeout in milliseconds.
    /// </summary>
    public int SyncTimeout { get; set; } = 5000;

    /// <summary>
    /// The asynchronous operation timeout in milliseconds.
    /// </summary>
    public int AsyncTimeout { get; set; } = 5000;

    /// <summary>
    /// Gets or sets a value indicating whether SSL is enabled.
    /// </summary>
    public bool UseSsl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to abort on connection failure.
    /// </summary>
    public bool AbortOnConnectFail { get; set; } = false;

    /// <summary>
    /// The connection pool size.
    /// </summary>
    public int PoolSize { get; set; } = 10;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException("Redis ConnectionString is required");
        }

        if (DefaultDatabase < 0 || DefaultDatabase > 15)
        {
            throw new InvalidOperationException("Redis DefaultDatabase must be between 0 and 15");
        }

        if (string.IsNullOrWhiteSpace(InstanceName))
        {
            throw new InvalidOperationException("Redis InstanceName is required");
        }

        if (ConnectTimeout <= 0)
        {
            throw new InvalidOperationException("Redis ConnectTimeout must be greater than 0");
        }

        if (SyncTimeout <= 0)
        {
            throw new InvalidOperationException("Redis SyncTimeout must be greater than 0");
        }

        if (AsyncTimeout <= 0)
        {
            throw new InvalidOperationException("Redis AsyncTimeout must be greater than 0");
        }

        if (PoolSize < 1)
        {
            throw new InvalidOperationException("Redis PoolSize must be at least 1");
        }
    }
}

/// <summary>
/// Configuration options for distributed queue.
/// </summary>
public sealed class DistributedQueueOptions : IValidatableOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "DistributedQueue";

    /// <summary>
    /// The queue name.
    /// </summary>
    public string QueueName { get; set; } = "agent:queue";

    /// <summary>
    /// The consumer group name.
    /// </summary>
    public string ConsumerGroup { get; set; } = "agent-workers";

    /// <summary>
    /// The consumer name prefix.
    /// </summary>
    public string ConsumerNamePrefix { get; set; } = "worker";

    /// <summary>
    /// The dead letter queue name.
    /// </summary>
    public string DeadLetterQueue { get; set; } = "agent:deadletter";

    /// <summary>
    /// Maximum retry count
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// The message visibility timeout in seconds.
    /// </summary>
    public int VisibilityTimeout { get; set; } = 30;

    /// <summary>
    /// The batch read size.
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// The poll interval in milliseconds.
    /// </summary>
    public int PollInterval { get; set; } = 100;

    /// <inheritdoc />
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(QueueName))
        {
            throw new InvalidOperationException("QueueName is required");
        }

        if (string.IsNullOrWhiteSpace(ConsumerGroup))
        {
            throw new InvalidOperationException("ConsumerGroup is required");
        }

        if (string.IsNullOrWhiteSpace(ConsumerNamePrefix))
        {
            throw new InvalidOperationException("ConsumerNamePrefix is required");
        }

        if (string.IsNullOrWhiteSpace(DeadLetterQueue))
        {
            throw new InvalidOperationException("DeadLetterQueue is required");
        }

        if (MaxRetries < 0)
        {
            throw new InvalidOperationException("MaxRetries must be non-negative");
        }

        if (VisibilityTimeout <= 0)
        {
            throw new InvalidOperationException("VisibilityTimeout must be greater than 0");
        }

        if (BatchSize <= 0)
        {
            throw new InvalidOperationException("BatchSize must be greater than 0");
        }

        if (PollInterval <= 0)
        {
            throw new InvalidOperationException("PollInterval must be greater than 0");
        }
    }
}

/// <summary>
/// Configuration options for distributed locks.
/// </summary>
public sealed class DistributedLockOptions : IValidatableOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "DistributedLock";

    /// <summary>
    /// The lock key prefix.
    /// </summary>
    public string KeyPrefix { get; set; } = "lock:";

    /// <summary>
    /// The default lock expiration time in seconds.
    /// </summary>
    public int DefaultExpiry { get; set; } = 30;

    /// <summary>
    /// The default wait timeout in seconds.
    /// </summary>
    public int DefaultWaitTimeout { get; set; } = 10;

    /// <summary>
    /// The retry interval in milliseconds.
    /// </summary>
    public int RetryInterval { get; set; } = 100;

    /// <summary>
    /// Gets or sets a value indicating whether automatic renewal is enabled.
    /// </summary>
    public bool EnableAutoRenewal { get; set; } = true;

    /// <summary>
    /// The automatic renewal interval as a ratio of the lock expiration time.
    /// </summary>
    public double RenewalInterval { get; set; } = 0.5;

    /// <inheritdoc />
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(KeyPrefix))
        {
            throw new InvalidOperationException("DistributedLock KeyPrefix is required");
        }

        if (DefaultExpiry <= 0)
        {
            throw new InvalidOperationException("DefaultExpiry must be greater than 0");
        }

        if (DefaultWaitTimeout <= 0)
        {
            throw new InvalidOperationException("DefaultWaitTimeout must be greater than 0");
        }

        if (RetryInterval <= 0)
        {
            throw new InvalidOperationException("RetryInterval must be greater than 0");
        }

        if (RenewalInterval is <= 0 or >= 1)
        {
            throw new InvalidOperationException(
                "RenewalInterval must be between 0 and 1 (exclusive)"
            );
        }
    }
}

/// <summary>
/// Configuration options for distributed sessions.
/// </summary>
public sealed class DistributedSessionOptions : IValidatableOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "DistributedSession";

    /// <summary>
    /// The session key prefix.
    /// </summary>
    public string KeyPrefix { get; set; } = "session:";

    /// <summary>
    /// The default session expiration time in minutes.
    /// </summary>
    public int DefaultExpiry { get; set; } = 30;

    /// <summary>
    /// Gets or sets a value indicating whether sliding expiration is enabled.
    /// </summary>
    public bool EnableSlidingExpiry { get; set; } = true;

    /// <summary>
    /// The maximum number of messages.
    /// </summary>
    public int MaxMessages { get; set; } = 100;

    /// <inheritdoc />
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(KeyPrefix))
        {
            throw new InvalidOperationException("DistributedSession KeyPrefix is required");
        }

        if (DefaultExpiry <= 0)
        {
            throw new InvalidOperationException("DefaultExpiry must be greater than 0");
        }

        if (MaxMessages <= 0)
        {
            throw new InvalidOperationException("MaxMessages must be greater than 0");
        }
    }
}
