# 第12周：部署与扩展

> 第六阶段：生产就绪
> 第12周学习材料：容器化、配置管理、扩展与生产部署

---

## 第1-2天：容器化

### 1. Agent Docker架构

```
┌─────────────────────────────────────────────────────────────────┐
│                      Agent部署架构                               │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                      负载均衡器                          │   │
│  └─────────────────────────────────────────────────────────┘   │
│                            │                                     │
│         ┌──────────────────┼──────────────────┐                 │
│         ▼                  ▼                  ▼                 │
│  ┌────────────┐    ┌────────────┐    ┌────────────┐            │
│  │   Agent    │    │   Agent    │    │   Agent    │            │
│  │   实例     │    │   实例     │    │   实例     │            │
│  └────────────┘    └────────────┘    └────────────┘            │
│         │                  │                  │                 │
│         └──────────────────┼──────────────────┘                 │
│                            ▼                                     │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │              共享服务 (Redis, 数据库)                    │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. Dockerfile

```dockerfile
# DawningAgents的Dockerfile

# 构建阶段
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 复制项目文件
COPY ["src/DawningAgents.Core/DawningAgents.Core.csproj", "DawningAgents.Core/"]
COPY ["src/DawningAgents.Api/DawningAgents.Api.csproj", "DawningAgents.Api/"]

# 还原依赖
RUN dotnet restore "DawningAgents.Api/DawningAgents.Api.csproj"

# 复制源代码
COPY src/ .

# 构建
RUN dotnet build "DawningAgents.Api/DawningAgents.Api.csproj" -c Release -o /app/build

# 发布阶段
FROM build AS publish
RUN dotnet publish "DawningAgents.Api/DawningAgents.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 运行时阶段
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# 为安全创建非root用户
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

# 复制发布的应用
COPY --from=publish /app/publish .

# 健康检查
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# 暴露端口
EXPOSE 8080

# 设置环境变量
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "DawningAgents.Api.dll"]
```

### 3. Docker Compose

```yaml
# docker-compose.yml

version: '3.8'

services:
  agent-api:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__Redis=redis:6379
      - LLM__Provider=OpenAI
      - LLM__ApiKey=${OPENAI_API_KEY}
      - Telemetry__OtlpEndpoint=http://otel-collector:4317
    depends_on:
      - redis
      - otel-collector
    deploy:
      replicas: 3
      resources:
        limits:
          cpus: '2'
          memory: 4G
        reservations:
          cpus: '0.5'
          memory: 512M
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 10s

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    command: redis-server --appendonly yes

  otel-collector:
    image: otel/opentelemetry-collector-contrib:latest
    ports:
      - "4317:4317"   # OTLP gRPC
      - "4318:4318"   # OTLP HTTP
      - "8888:8888"   # Prometheus指标
    volumes:
      - ./config/otel-collector.yaml:/etc/otelcol-contrib/config.yaml
    command: ["--config=/etc/otelcol-contrib/config.yaml"]

volumes:
  redis-data:
```

---

## 第3-4天：配置管理

### 1. 配置提供者

```csharp
namespace DawningAgents.Core.Configuration;

using Microsoft.Extensions.Configuration;

/// <summary>
/// 带环境特定设置的Agent配置
/// </summary>
public class AgentConfiguration
{
    /// <summary>
    /// 从各种来源加载配置
    /// </summary>
    public static IConfiguration Build(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();
    }
}

/// <summary>
/// 强类型配置选项
/// </summary>
public record AgentOptions
{
    public const string SectionName = "Agent";
    
    public string Name { get; init; } = "DefaultAgent";
    public int MaxIterations { get; init; } = 10;
    public int MaxTokensPerRequest { get; init; } = 4000;
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public bool EnableSafetyGuardrails { get; init; } = true;
}

public record LLMOptions
{
    public const string SectionName = "LLM";
    
    public string Provider { get; init; } = "OpenAI";
    public string? ApiKey { get; init; }
    public string? Endpoint { get; init; }
    public string Model { get; init; } = "gpt-4";
    public double Temperature { get; init; } = 0.7;
    public int MaxRetries { get; init; } = 3;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);
}

public record ScalingOptions
{
    public const string SectionName = "Scaling";
    
    public int MinInstances { get; init; } = 1;
    public int MaxInstances { get; init; } = 10;
    public int TargetCpuPercent { get; init; } = 70;
    public int TargetMemoryPercent { get; init; } = 80;
    public int ScaleUpCooldownSeconds { get; init; } = 60;
    public int ScaleDownCooldownSeconds { get; init; } = 300;
}

public record CacheOptions
{
    public const string SectionName = "Cache";
    
    public bool Enabled { get; init; } = true;
    public string Provider { get; init; } = "Redis";
    public string? ConnectionString { get; init; }
    public TimeSpan DefaultExpiration { get; init; } = TimeSpan.FromHours(1);
    public int MaxCacheSize { get; init; } = 10000;
}
```

### 2. 配置文件

```json
// appsettings.json
{
  "Agent": {
    "Name": "DawningAgent",
    "MaxIterations": 10,
    "MaxTokensPerRequest": 4000,
    "RequestTimeout": "00:05:00",
    "EnableSafetyGuardrails": true
  },
  "LLM": {
    "Provider": "OpenAI",
    "Model": "gpt-4",
    "Temperature": 0.7,
    "MaxRetries": 3,
    "RetryDelay": "00:00:01"
  },
  "Scaling": {
    "MinInstances": 1,
    "MaxInstances": 10,
    "TargetCpuPercent": 70,
    "TargetMemoryPercent": 80
  },
  "Cache": {
    "Enabled": true,
    "Provider": "Redis",
    "DefaultExpiration": "01:00:00"
  },
  "Telemetry": {
    "ServiceName": "DawningAgents",
    "EnableLogging": true,
    "EnableMetrics": true,
    "EnableTracing": true,
    "TraceSampleRate": 0.1
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

```json
// appsettings.Production.json
{
  "Agent": {
    "MaxIterations": 15,
    "RequestTimeout": "00:10:00"
  },
  "LLM": {
    "MaxRetries": 5
  },
  "Scaling": {
    "MinInstances": 3,
    "MaxInstances": 50
  },
  "Telemetry": {
    "TraceSampleRate": 0.01,
    "OtlpEndpoint": "http://otel-collector:4317"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "DawningAgents": "Information"
    }
  }
}
```

### 3. 密钥管理

```csharp
namespace DawningAgents.Core.Configuration;

using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

/// <summary>
/// 安全密钥管理
/// </summary>
public interface ISecretsManager
{
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default);
    Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default);
}

/// <summary>
/// Azure Key Vault实现
/// </summary>
public class AzureKeyVaultSecretsManager : ISecretsManager
{
    private readonly SecretClient _client;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

    public AzureKeyVaultSecretsManager(string vaultUri, IMemoryCache cache)
    {
        _client = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
        _cache = cache;
    }

    public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"secret:{name}";
        
        if (_cache.TryGetValue(cacheKey, out string? cachedValue))
        {
            return cachedValue;
        }

        try
        {
            var secret = await _client.GetSecretAsync(name, cancellationToken: cancellationToken);
            var value = secret.Value.Value;
            
            _cache.Set(cacheKey, value, _cacheExpiration);
            return value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        await _client.SetSecretAsync(name, value, cancellationToken);
        _cache.Remove($"secret:{name}");
    }
}

/// <summary>
/// 用于开发的环境变量密钥
/// </summary>
public class EnvironmentSecretsManager : ISecretsManager
{
    public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        var envName = name.Replace("-", "_").Replace(":", "__").ToUpperInvariant();
        return Task.FromResult(Environment.GetEnvironmentVariable(envName));
    }

    public Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        var envName = name.Replace("-", "_").Replace(":", "__").ToUpperInvariant();
        Environment.SetEnvironmentVariable(envName, value);
        return Task.CompletedTask;
    }
}
```

---

## 第5-7天：扩展与生产部署

### 1. 请求队列与工作池

```csharp
namespace DawningAgents.Core.Scaling;

using System.Threading.Channels;
using Microsoft.Extensions.Logging;

/// <summary>
/// Agent处理的请求队列
/// </summary>
public class AgentRequestQueue
{
    private readonly Channel<AgentWorkItem> _channel;
    private readonly ILogger<AgentRequestQueue> _logger;

    public AgentRequestQueue(int capacity, ILogger<AgentRequestQueue> logger)
    {
        _channel = Channel.CreateBounded<AgentWorkItem>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });
        _logger = logger;
    }

    /// <summary>
    /// 入队工作项
    /// </summary>
    public async ValueTask EnqueueAsync(AgentWorkItem item, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(item, cancellationToken);
        _logger.LogDebug("工作项 {WorkItemId} 已入队", item.Id);
    }

    /// <summary>
    /// 尝试出队工作项
    /// </summary>
    public async ValueTask<AgentWorkItem?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _channel.Reader.ReadAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// 获取当前队列长度
    /// </summary>
    public int Count => _channel.Reader.Count;

    /// <summary>
    /// 检查队列是否可以接受更多项
    /// </summary>
    public bool CanWrite => _channel.Writer.TryComplete() == false;
}

public record AgentWorkItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required AgentContext Context { get; init; }
    public required TaskCompletionSource<AgentResponse> CompletionSource { get; init; }
    public DateTime EnqueuedAt { get; init; } = DateTime.UtcNow;
    public string? Priority { get; init; }
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>
/// 处理Agent请求的工作池
/// </summary>
public class AgentWorkerPool : IDisposable
{
    private readonly IAgent _agent;
    private readonly AgentRequestQueue _queue;
    private readonly ILogger<AgentWorkerPool> _logger;
    private readonly List<Task> _workers = [];
    private readonly CancellationTokenSource _cts = new();
    private readonly int _workerCount;

    public AgentWorkerPool(
        IAgent agent,
        AgentRequestQueue queue,
        int workerCount,
        ILogger<AgentWorkerPool> logger)
    {
        _agent = agent;
        _queue = queue;
        _workerCount = workerCount;
        _logger = logger;
    }

    /// <summary>
    /// 启动工作池
    /// </summary>
    public void Start()
    {
        for (int i = 0; i < _workerCount; i++)
        {
            var workerId = i;
            _workers.Add(Task.Run(() => WorkerLoopAsync(workerId, _cts.Token)));
        }
        
        _logger.LogInformation("已启动 {WorkerCount} 个Agent工作线程", _workerCount);
    }

    private async Task WorkerLoopAsync(int workerId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("工作线程 {WorkerId} 已启动", workerId);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var item = await _queue.DequeueAsync(cancellationToken);
                if (item == null) continue;

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, item.CancellationToken);

                try
                {
                    var response = await _agent.ExecuteAsync(item.Context, linkedCts.Token);
                    item.CompletionSource.TrySetResult(response);
                }
                catch (OperationCanceledException)
                {
                    item.CompletionSource.TrySetCanceled();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "工作线程 {WorkerId} 处理项 {ItemId} 失败", workerId, item.Id);
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

    public void Dispose()
    {
        _cts.Cancel();
        Task.WhenAll(_workers).Wait(TimeSpan.FromSeconds(30));
        _cts.Dispose();
    }
}
```

### 2. 负载均衡器与熔断器

```csharp
namespace DawningAgents.Core.Scaling;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

/// <summary>
/// 多Agent实例的负载均衡器
/// </summary>
public class AgentLoadBalancer
{
    private readonly List<AgentInstance> _instances = [];
    private readonly ILogger<AgentLoadBalancer> _logger;
    private int _roundRobinIndex = 0;

    public AgentLoadBalancer(ILogger<AgentLoadBalancer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 注册Agent实例
    /// </summary>
    public void RegisterInstance(AgentInstance instance)
    {
        _instances.Add(instance);
        _logger.LogInformation("已注册Agent实例 {InstanceId}", instance.Id);
    }

    /// <summary>
    /// 获取下一个可用实例（轮询）
    /// </summary>
    public AgentInstance? GetNextInstance()
    {
        var healthyInstances = _instances.Where(i => i.IsHealthy).ToList();
        if (healthyInstances.Count == 0)
            return null;

        var index = Interlocked.Increment(ref _roundRobinIndex) % healthyInstances.Count;
        return healthyInstances[index];
    }

    /// <summary>
    /// 获取负载最小的实例
    /// </summary>
    public AgentInstance? GetLeastLoadedInstance()
    {
        return _instances
            .Where(i => i.IsHealthy)
            .OrderBy(i => i.ActiveRequests)
            .FirstOrDefault();
    }
}

public class AgentInstance
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required IAgent Agent { get; init; }
    public string Endpoint { get; init; } = "";
    public bool IsHealthy { get; set; } = true;
    public int ActiveRequests { get; set; }
    public DateTime LastHealthCheck { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 容错熔断器
/// </summary>
public class CircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _resetTimeout;
    private readonly ILogger<CircuitBreaker> _logger;
    
    private int _failureCount;
    private DateTime _lastFailureTime;
    private CircuitState _state = CircuitState.Closed;
    private readonly object _lock = new();

    public CircuitBreaker(
        int failureThreshold,
        TimeSpan resetTimeout,
        ILogger<CircuitBreaker> logger)
    {
        _failureThreshold = failureThreshold;
        _resetTimeout = resetTimeout;
        _logger = logger;
    }

    public CircuitState State
    {
        get
        {
            lock (_lock)
            {
                if (_state == CircuitState.Open && 
                    DateTime.UtcNow - _lastFailureTime > _resetTimeout)
                {
                    _state = CircuitState.HalfOpen;
                    _logger.LogInformation("熔断器转换为半开状态");
                }
                return _state;
            }
        }
    }

    /// <summary>
    /// 带熔断器保护执行
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        if (State == CircuitState.Open)
        {
            throw new CircuitBreakerOpenException("熔断器处于打开状态");
        }

        try
        {
            var result = await action();
            OnSuccess();
            return result;
        }
        catch (Exception ex)
        {
            OnFailure();
            throw;
        }
    }

    private void OnSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            if (_state == CircuitState.HalfOpen)
            {
                _state = CircuitState.Closed;
                _logger.LogInformation("熔断器在成功请求后关闭");
            }
        }
    }

    private void OnFailure()
    {
        lock (_lock)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_failureCount >= _failureThreshold)
            {
                _state = CircuitState.Open;
                _logger.LogWarning("熔断器在 {FailureCount} 次失败后打开", _failureCount);
            }
        }
    }
}

public enum CircuitState
{
    Closed,    // 正常运行
    Open,      // 阻止请求
    HalfOpen   // 测试恢复
}

public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string message) : base(message) { }
}
```

### 3. 自动扩展器

```csharp
namespace DawningAgents.Core.Scaling;

using Microsoft.Extensions.Logging;

/// <summary>
/// 基于指标的自动扩展器
/// </summary>
public class AgentAutoScaler
{
    private readonly ScalingOptions _options;
    private readonly ILogger<AgentAutoScaler> _logger;
    private readonly Func<Task<ScalingMetrics>> _metricsProvider;
    private readonly Func<int, Task> _scaleAction;
    
    private int _currentInstances;
    private DateTime _lastScaleUp = DateTime.MinValue;
    private DateTime _lastScaleDown = DateTime.MinValue;

    public AgentAutoScaler(
        ScalingOptions options,
        Func<Task<ScalingMetrics>> metricsProvider,
        Func<int, Task> scaleAction,
        ILogger<AgentAutoScaler> logger)
    {
        _options = options;
        _metricsProvider = metricsProvider;
        _scaleAction = scaleAction;
        _logger = logger;
        _currentInstances = options.MinInstances;
    }

    /// <summary>
    /// 评估并应用扩展决策
    /// </summary>
    public async Task EvaluateAsync()
    {
        var metrics = await _metricsProvider();
        var decision = MakeScalingDecision(metrics);

        if (decision.Action == ScalingAction.None)
            return;

        var newCount = decision.Action == ScalingAction.ScaleUp
            ? Math.Min(_currentInstances + decision.Delta, _options.MaxInstances)
            : Math.Max(_currentInstances - decision.Delta, _options.MinInstances);

        if (newCount != _currentInstances)
        {
            await ApplyScalingAsync(newCount, decision);
        }
    }

    private ScalingDecision MakeScalingDecision(ScalingMetrics metrics)
    {
        var now = DateTime.UtcNow;

        // 检查是否需要扩容
        if (metrics.CpuPercent > _options.TargetCpuPercent ||
            metrics.MemoryPercent > _options.TargetMemoryPercent ||
            metrics.QueueLength > _currentInstances * 10)
        {
            if (now - _lastScaleUp > TimeSpan.FromSeconds(_options.ScaleUpCooldownSeconds))
            {
                var delta = CalculateScaleUpDelta(metrics);
                return new ScalingDecision
                {
                    Action = ScalingAction.ScaleUp,
                    Delta = delta,
                    Reason = $"CPU: {metrics.CpuPercent}%, 内存: {metrics.MemoryPercent}%, 队列: {metrics.QueueLength}"
                };
            }
        }

        // 检查是否可以缩容
        if (metrics.CpuPercent < _options.TargetCpuPercent * 0.5 &&
            metrics.MemoryPercent < _options.TargetMemoryPercent * 0.5 &&
            metrics.QueueLength < _currentInstances * 2)
        {
            if (now - _lastScaleDown > TimeSpan.FromSeconds(_options.ScaleDownCooldownSeconds))
            {
                return new ScalingDecision
                {
                    Action = ScalingAction.ScaleDown,
                    Delta = 1,
                    Reason = $"低利用率 - CPU: {metrics.CpuPercent}%, 内存: {metrics.MemoryPercent}%"
                };
            }
        }

        return new ScalingDecision { Action = ScalingAction.None };
    }

    private int CalculateScaleUpDelta(ScalingMetrics metrics)
    {
        // 计算需要多少实例
        var cpuRatio = metrics.CpuPercent / _options.TargetCpuPercent;
        var memoryRatio = metrics.MemoryPercent / _options.TargetMemoryPercent;
        var targetRatio = Math.Max(cpuRatio, memoryRatio);
        
        var targetInstances = (int)Math.Ceiling(_currentInstances * targetRatio);
        return Math.Max(1, targetInstances - _currentInstances);
    }

    private async Task ApplyScalingAsync(int newCount, ScalingDecision decision)
    {
        _logger.LogInformation(
            "从 {Current} 扩展到 {New} 个实例。原因：{Reason}",
            _currentInstances, newCount, decision.Reason);

        try
        {
            await _scaleAction(newCount);
            
            if (decision.Action == ScalingAction.ScaleUp)
                _lastScaleUp = DateTime.UtcNow;
            else
                _lastScaleDown = DateTime.UtcNow;

            _currentInstances = newCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "扩展到 {NewCount} 个实例失败", newCount);
        }
    }
}

public record ScalingMetrics
{
    public double CpuPercent { get; init; }
    public double MemoryPercent { get; init; }
    public int QueueLength { get; init; }
    public int ActiveRequests { get; init; }
    public double AvgLatencyMs { get; init; }
}

public record ScalingDecision
{
    public ScalingAction Action { get; init; }
    public int Delta { get; init; }
    public string? Reason { get; init; }
}

public enum ScalingAction
{
    None,      // 无操作
    ScaleUp,   // 扩容
    ScaleDown  // 缩容
}
```

### 4. API启动配置

```csharp
namespace DawningAgents.Api;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DawningAgents.Core.Configuration;
using DawningAgents.Core.Observability;
using DawningAgents.Core.Scaling;
using DawningAgents.Core.Safety;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // 加载配置
        var config = AgentConfiguration.Build(args);
        builder.Configuration.AddConfiguration(config);

        // 配置选项
        builder.Services.Configure<AgentOptions>(config.GetSection(AgentOptions.SectionName));
        builder.Services.Configure<LLMOptions>(config.GetSection(LLMOptions.SectionName));
        builder.Services.Configure<ScalingOptions>(config.GetSection(ScalingOptions.SectionName));
        builder.Services.Configure<TelemetryConfig>(config.GetSection("Telemetry"));

        // 注册服务
        ConfigureServices(builder.Services, config);

        var app = builder.Build();

        // 配置中间件
        ConfigureMiddleware(app);

        app.Run();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        // 遥测
        var telemetryConfig = config.GetSection("Telemetry").Get<TelemetryConfig>() ?? new TelemetryConfig();
        services.AddSingleton(telemetryConfig);
        services.AddSingleton<AgentTelemetry>();

        // LLM提供者
        var llmOptions = config.GetSection(LLMOptions.SectionName).Get<LLMOptions>() ?? new LLMOptions();
        services.AddSingleton<ILLMProvider>(sp =>
        {
            return llmOptions.Provider switch
            {
                "OpenAI" => new OpenAIProvider(llmOptions),
                "Azure" => new AzureOpenAIProvider(llmOptions),
                _ => throw new InvalidOperationException($"未知的LLM提供者：{llmOptions.Provider}")
            };
        });

        // Agent
        services.AddSingleton<IAgent>(sp =>
        {
            var llm = sp.GetRequiredService<ILLMProvider>();
            var logger = sp.GetRequiredService<ILoggerFactory>();
            var telemetry = sp.GetRequiredService<AgentTelemetry>();
            var safetyConfig = new SafetyConfig();

            // 构建Agent流水线
            var innerAgent = new ReActAgent(llm, logger.CreateLogger<ReActAgent>());
            
            // 包装安全性
            var guardrailPipeline = new GuardrailPipeline(logger.CreateLogger<GuardrailPipeline>())
                .Add(new InputValidator(safetyConfig, logger.CreateLogger<InputValidator>()))
                .Add(new SensitiveDataFilter(safetyConfig, logger.CreateLogger<SensitiveDataFilter>()));
            
            var safeAgent = new SafeAgent(
                innerAgent, guardrailPipeline, safetyConfig, logger.CreateLogger<SafeAgent>());
            
            // 包装可观测性
            return new ObservableAgent(safeAgent, telemetry, logger.CreateLogger<ObservableAgent>(), telemetryConfig);
        });

        // 请求队列和工作池
        services.AddSingleton<AgentRequestQueue>(sp =>
            new AgentRequestQueue(1000, sp.GetRequiredService<ILogger<AgentRequestQueue>>()));
        
        services.AddSingleton<AgentWorkerPool>(sp =>
        {
            var agent = sp.GetRequiredService<IAgent>();
            var queue = sp.GetRequiredService<AgentRequestQueue>();
            var workerCount = Environment.ProcessorCount * 2;
            return new AgentWorkerPool(agent, queue, workerCount, sp.GetRequiredService<ILogger<AgentWorkerPool>>());
        });

        // 健康检查
        services.AddHealthChecks()
            .AddCheck<AgentHealthCheck>("agent")
            .AddCheck<LLMHealthCheck>("llm");

        // API控制器
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
    }

    private static void ConfigureMiddleware(WebApplication app)
    {
        // 启动工作池
        var workerPool = app.Services.GetRequiredService<AgentWorkerPool>();
        workerPool.Start();

        // 开发环境Swagger
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // 健康检查端点
        app.MapHealthChecks("/health");

        // API端点
        app.MapControllers();

        // 指标端点
        app.MapGet("/metrics", (AgentTelemetry telemetry) =>
        {
            // 返回Prometheus格式的指标
            return Results.Ok();
        });
    }
}
```

---

## Kubernetes部署

```yaml
# kubernetes/deployment.yaml

apiVersion: apps/v1
kind: Deployment
metadata:
  name: dawning-agents
  labels:
    app: dawning-agents
spec:
  replicas: 3
  selector:
    matchLabels:
      app: dawning-agents
  template:
    metadata:
      labels:
        app: dawning-agents
    spec:
      containers:
      - name: agent
        image: dawning-agents:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: LLM__ApiKey
          valueFrom:
            secretKeyRef:
              name: llm-secrets
              key: api-key
        resources:
          requests:
            memory: "512Mi"
            cpu: "500m"
          limits:
            memory: "4Gi"
            cpu: "2000m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 10
---
apiVersion: v1
kind: Service
metadata:
  name: dawning-agents
spec:
  selector:
    app: dawning-agents
  ports:
  - port: 80
    targetPort: 8080
  type: ClusterIP
---
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: dawning-agents-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: dawning-agents
  minReplicas: 3
  maxReplicas: 50
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
```

---

## 总结

### 第12周交付物

```
src/DawningAgents.Core/
├── Configuration/
│   ├── AgentConfiguration.cs      # 配置加载
│   ├── AgentOptions.cs            # 类型化选项
│   └── SecretsManager.cs          # 密钥管理
└── Scaling/
    ├── AgentRequestQueue.cs       # 请求队列
    ├── AgentWorkerPool.cs         # 工作池
    ├── AgentLoadBalancer.cs       # 负载均衡
    ├── CircuitBreaker.cs          # 容错
    └── AgentAutoScaler.cs         # 自动扩展

docker/
├── Dockerfile                     # 容器镜像
└── docker-compose.yml             # 本地部署

kubernetes/
├── deployment.yaml                # K8s部署
├── service.yaml                   # K8s服务
└── hpa.yaml                       # 水平Pod自动扩展
```

### 生产检查清单

| 类别 | 项目 |
|------|------|
| **安全性** | ✅ 非root容器，✅ 密钥管理，✅ 输入验证 |
| **可观测性** | ✅ 结构化日志，✅ 指标，✅ 分布式追踪 |
| **可靠性** | ✅ 健康检查，✅ 熔断器，✅ 优雅关闭 |
| **可扩展性** | ✅ 水平扩展，✅ 自动扩展，✅ 负载均衡 |
| **配置** | ✅ 环境特定，✅ 热重载，✅ 验证 |

### 🎉 恭喜！

您已完成12周的Dawning Agents学习计划！

现在您拥有一个完整的多Agent框架，包括：
- 核心Agent循环（ReAct、规划）
- 记忆管理
- 工具系统
- RAG集成
- 多Agent编排
- Agent通信
- 安全护栏
- 人机协作
- 完整可观测性
- 生产部署

**下一步：**
- 使用您的框架构建实际应用
- 贡献开源Agent项目
- 探索高级主题如Agent学习
