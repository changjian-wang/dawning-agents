namespace Dawning.Agents.Abstractions.Distributed;

/// <summary>
/// Redis 配置选项
/// </summary>
/// <remarks>
/// appsettings.json 示例:
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
public sealed class RedisOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Redis";

    /// <summary>
    /// Redis 连接字符串
    /// </summary>
    /// <example>localhost:6379,password=xxx,ssl=false,abortConnect=false</example>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// 实例名称前缀
    /// </summary>
    /// <remarks>所有 key 都会以此前缀开头，用于区分不同应用</remarks>
    public string InstanceName { get; set; } = "dawning:";

    /// <summary>
    /// 默认数据库索引
    /// </summary>
    public int DefaultDatabase { get; set; } = 0;

    /// <summary>
    /// 连接超时（毫秒）
    /// </summary>
    public int ConnectTimeout { get; set; } = 5000;

    /// <summary>
    /// 同步操作超时（毫秒）
    /// </summary>
    public int SyncTimeout { get; set; } = 5000;

    /// <summary>
    /// 异步操作超时（毫秒）
    /// </summary>
    public int AsyncTimeout { get; set; } = 5000;

    /// <summary>
    /// 是否启用 SSL
    /// </summary>
    public bool UseSsl { get; set; }

    /// <summary>
    /// 连接失败时是否中止
    /// </summary>
    public bool AbortOnConnectFail { get; set; } = false;

    /// <summary>
    /// 连接池大小
    /// </summary>
    public int PoolSize { get; set; } = 10;

    /// <summary>
    /// 验证配置
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
    }
}

/// <summary>
/// 分布式队列配置选项
/// </summary>
public sealed class DistributedQueueOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "DistributedQueue";

    /// <summary>
    /// 队列名称
    /// </summary>
    public string QueueName { get; set; } = "agent:queue";

    /// <summary>
    /// 消费者组名称
    /// </summary>
    public string ConsumerGroup { get; set; } = "agent-workers";

    /// <summary>
    /// 消费者名称前缀
    /// </summary>
    public string ConsumerNamePrefix { get; set; } = "worker";

    /// <summary>
    /// 死信队列名称
    /// </summary>
    public string DeadLetterQueue { get; set; } = "agent:deadletter";

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// 消息可见性超时（秒）
    /// </summary>
    public int VisibilityTimeout { get; set; } = 30;

    /// <summary>
    /// 批量读取大小
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// 轮询间隔（毫秒）
    /// </summary>
    public int PollInterval { get; set; } = 100;
}

/// <summary>
/// 分布式锁配置选项
/// </summary>
public sealed class DistributedLockOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "DistributedLock";

    /// <summary>
    /// 锁 key 前缀
    /// </summary>
    public string KeyPrefix { get; set; } = "lock:";

    /// <summary>
    /// 默认锁过期时间（秒）
    /// </summary>
    public int DefaultExpiry { get; set; } = 30;

    /// <summary>
    /// 默认等待超时（秒）
    /// </summary>
    public int DefaultWaitTimeout { get; set; } = 10;

    /// <summary>
    /// 重试间隔（毫秒）
    /// </summary>
    public int RetryInterval { get; set; } = 100;

    /// <summary>
    /// 是否启用自动续期
    /// </summary>
    public bool EnableAutoRenewal { get; set; } = true;

    /// <summary>
    /// 自动续期间隔（锁过期时间的比例）
    /// </summary>
    public double RenewalInterval { get; set; } = 0.5;
}

/// <summary>
/// 分布式会话配置选项
/// </summary>
public sealed class DistributedSessionOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "DistributedSession";

    /// <summary>
    /// 会话 key 前缀
    /// </summary>
    public string KeyPrefix { get; set; } = "session:";

    /// <summary>
    /// 默认会话过期时间（分钟）
    /// </summary>
    public int DefaultExpiry { get; set; } = 30;

    /// <summary>
    /// 是否启用滑动过期
    /// </summary>
    public bool EnableSlidingExpiry { get; set; } = true;

    /// <summary>
    /// 最大消息数量
    /// </summary>
    public int MaxMessages { get; set; } = 100;
}
