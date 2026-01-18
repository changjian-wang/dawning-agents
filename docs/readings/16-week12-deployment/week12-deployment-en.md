# Week 12: Deployment & Scaling

> Phase 6: Production Readiness
> Week 12 Learning Materials: Containerization, Configuration, Scaling & Production Deployment

---

## Days 1-2: Containerization

### 1. Docker Architecture for Agents

```text
┌─────────────────────────────────────────────────────────────────┐
│                   Agent Deployment Architecture                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                     Load Balancer                        │   │
│  └─────────────────────────────────────────────────────────┘   │
│                            │                                     │
│         ┌──────────────────┼──────────────────┐                 │
│         ▼                  ▼                  ▼                 │
│  ┌────────────┐    ┌────────────┐    ┌────────────┐            │
│  │  Agent     │    │  Agent     │    │  Agent     │            │
│  │  Instance  │    │  Instance  │    │  Instance  │            │
│  └────────────┘    └────────────┘    └────────────┘            │
│         │                  │                  │                 │
│         └──────────────────┼──────────────────┘                 │
│                            ▼                                     │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │              Shared Services (Redis, DB)                 │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. Dockerfile

```dockerfile
# Dockerfile for Dawning.Agents

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/Dawning.Agents.Core/Dawning.Agents.Core.csproj", "Dawning.Agents.Core/"]
COPY ["src/Dawning.Agents.Api/Dawning.Agents.Api.csproj", "Dawning.Agents.Api/"]

# Restore dependencies
RUN dotnet restore "Dawning.Agents.Api/Dawning.Agents.Api.csproj"

# Copy source code
COPY src/ .

# Build
RUN dotnet build "Dawning.Agents.Api/Dawning.Agents.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Dawning.Agents.Api/Dawning.Agents.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create non-root user for security
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

# Copy published app
COPY --from=publish /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Dawning.Agents.Api.dll"]
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
      - "8888:8888"   # Prometheus metrics
    volumes:
      - ./config/otel-collector.yaml:/etc/otelcol-contrib/config.yaml
    command: ["--config=/etc/otelcol-contrib/config.yaml"]

volumes:
  redis-data:
```

---

## Days 3-4: Configuration Management

### 1. Configuration Provider

```csharp
namespace Dawning.Agents.Core.Configuration;

using Microsoft.Extensions.Configuration;

/// <summary>
/// Agent configuration with environment-specific settings
/// </summary>
public class AgentConfiguration
{
    /// <summary>
    /// Load configuration from various sources
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
/// Strongly-typed configuration options
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

### 2. Configuration Files

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
    "ServiceName": "Dawning.Agents",
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
      "Dawning.Agents": "Information"
    }
  }
}
```

### 3. Secrets Management

```csharp
namespace Dawning.Agents.Core.Configuration;

using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

/// <summary>
/// Secure secrets management
/// </summary>
public interface ISecretsManager
{
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default);
    Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default);
}

/// <summary>
/// Azure Key Vault implementation
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
/// Environment-based secrets for development
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

## Days 5-7: Scaling & Production Deployment

### 1. Request Queue & Worker Pool

```csharp
namespace Dawning.Agents.Core.Scaling;

using System.Threading.Channels;
using Microsoft.Extensions.Logging;

/// <summary>
/// Request queue for agent processing
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
    /// Enqueue a work item
    /// </summary>
    public async ValueTask EnqueueAsync(AgentWorkItem item, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(item, cancellationToken);
        _logger.LogDebug("Enqueued work item {WorkItemId}", item.Id);
    }

    /// <summary>
    /// Try to dequeue a work item
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
    /// Get current queue length
    /// </summary>
    public int Count => _channel.Reader.Count;

    /// <summary>
    /// Check if queue can accept more items
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
/// Worker pool for processing agent requests
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
    /// Start the worker pool
    /// </summary>
    public void Start()
    {
        for (int i = 0; i < _workerCount; i++)
        {
            var workerId = i;
            _workers.Add(Task.Run(() => WorkerLoopAsync(workerId, _cts.Token)));
        }
        
        _logger.LogInformation("Started {WorkerCount} agent workers", _workerCount);
    }

    private async Task WorkerLoopAsync(int workerId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Worker {WorkerId} started", workerId);

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
                    _logger.LogError(ex, "Worker {WorkerId} failed processing item {ItemId}", workerId, item.Id);
                    item.CompletionSource.TrySetException(ex);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker {WorkerId} encountered unexpected error", workerId);
                await Task.Delay(1000, cancellationToken);
            }
        }

        _logger.LogDebug("Worker {WorkerId} stopped", workerId);
    }

    public void Dispose()
    {
        _cts.Cancel();
        Task.WhenAll(_workers).Wait(TimeSpan.FromSeconds(30));
        _cts.Dispose();
    }
}
```

### 2. Load Balancer & Circuit Breaker

```csharp
namespace Dawning.Agents.Core.Scaling;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Load balancer for multiple agent instances
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
    /// Register an agent instance
    /// </summary>
    public void RegisterInstance(AgentInstance instance)
    {
        _instances.Add(instance);
        _logger.LogInformation("Registered agent instance {InstanceId}", instance.Id);
    }

    /// <summary>
    /// Get next available instance (round-robin)
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
    /// Get instance with least load
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
/// Circuit breaker for fault tolerance
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
                    _logger.LogInformation("Circuit breaker transitioned to HalfOpen");
                }
                return _state;
            }
        }
    }

    /// <summary>
    /// Execute with circuit breaker protection
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        if (State == CircuitState.Open)
        {
            throw new CircuitBreakerOpenException("Circuit breaker is open");
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
                _logger.LogInformation("Circuit breaker closed after successful request");
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
                _logger.LogWarning("Circuit breaker opened after {FailureCount} failures", _failureCount);
            }
        }
    }
}

public enum CircuitState
{
    Closed,    // Normal operation
    Open,      // Blocking requests
    HalfOpen   // Testing recovery
}

public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string message) : base(message) { }
}
```

### 3. Auto-Scaler

```csharp
namespace Dawning.Agents.Core.Scaling;

using Microsoft.Extensions.Logging;

/// <summary>
/// Auto-scaler based on metrics
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
    /// Evaluate and apply scaling decision
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

        // Check if we need to scale up
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
                    Reason = $"CPU: {metrics.CpuPercent}%, Memory: {metrics.MemoryPercent}%, Queue: {metrics.QueueLength}"
                };
            }
        }

        // Check if we can scale down
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
                    Reason = $"Low utilization - CPU: {metrics.CpuPercent}%, Memory: {metrics.MemoryPercent}%"
                };
            }
        }

        return new ScalingDecision { Action = ScalingAction.None };
    }

    private int CalculateScaleUpDelta(ScalingMetrics metrics)
    {
        // Calculate how many instances we need
        var cpuRatio = metrics.CpuPercent / _options.TargetCpuPercent;
        var memoryRatio = metrics.MemoryPercent / _options.TargetMemoryPercent;
        var targetRatio = Math.Max(cpuRatio, memoryRatio);
        
        var targetInstances = (int)Math.Ceiling(_currentInstances * targetRatio);
        return Math.Max(1, targetInstances - _currentInstances);
    }

    private async Task ApplyScalingAsync(int newCount, ScalingDecision decision)
    {
        _logger.LogInformation(
            "Scaling from {Current} to {New} instances. Reason: {Reason}",
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
            _logger.LogError(ex, "Failed to scale to {NewCount} instances", newCount);
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
    None,
    ScaleUp,
    ScaleDown
}
```

### 4. API Startup Configuration

```csharp
namespace Dawning.Agents.Api;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Dawning.Agents.Core.Configuration;
using Dawning.Agents.Core.Observability;
using Dawning.Agents.Core.Scaling;
using Dawning.Agents.Core.Safety;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Load configuration
        var config = AgentConfiguration.Build(args);
        builder.Configuration.AddConfiguration(config);

        // Configure options
        builder.Services.Configure<AgentOptions>(config.GetSection(AgentOptions.SectionName));
        builder.Services.Configure<LLMOptions>(config.GetSection(LLMOptions.SectionName));
        builder.Services.Configure<ScalingOptions>(config.GetSection(ScalingOptions.SectionName));
        builder.Services.Configure<TelemetryConfig>(config.GetSection("Telemetry"));

        // Register services
        ConfigureServices(builder.Services, config);

        var app = builder.Build();

        // Configure middleware
        ConfigureMiddleware(app);

        app.Run();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        // Telemetry
        var telemetryConfig = config.GetSection("Telemetry").Get<TelemetryConfig>() ?? new TelemetryConfig();
        services.AddSingleton(telemetryConfig);
        services.AddSingleton<AgentTelemetry>();

        // LLM Provider
        var llmOptions = config.GetSection(LLMOptions.SectionName).Get<LLMOptions>() ?? new LLMOptions();
        services.AddSingleton<ILLMProvider>(sp =>
        {
            return llmOptions.Provider switch
            {
                "OpenAI" => new OpenAIProvider(llmOptions),
                "Azure" => new AzureOpenAIProvider(llmOptions),
                _ => throw new InvalidOperationException($"Unknown LLM provider: {llmOptions.Provider}")
            };
        });

        // Agent
        services.AddSingleton<IAgent>(sp =>
        {
            var llm = sp.GetRequiredService<ILLMProvider>();
            var logger = sp.GetRequiredService<ILoggerFactory>();
            var telemetry = sp.GetRequiredService<AgentTelemetry>();
            var safetyConfig = new SafetyConfig();

            // Build agent pipeline
            var innerAgent = new ReActAgent(llm, logger.CreateLogger<ReActAgent>());
            
            // Wrap with safety
            var guardrailPipeline = new GuardrailPipeline(logger.CreateLogger<GuardrailPipeline>())
                .Add(new InputValidator(safetyConfig, logger.CreateLogger<InputValidator>()))
                .Add(new SensitiveDataFilter(safetyConfig, logger.CreateLogger<SensitiveDataFilter>()));
            
            var safeAgent = new SafeAgent(
                innerAgent, guardrailPipeline, safetyConfig, logger.CreateLogger<SafeAgent>());
            
            // Wrap with observability
            return new ObservableAgent(safeAgent, telemetry, logger.CreateLogger<ObservableAgent>(), telemetryConfig);
        });

        // Request queue and worker pool
        services.AddSingleton<AgentRequestQueue>(sp =>
            new AgentRequestQueue(1000, sp.GetRequiredService<ILogger<AgentRequestQueue>>()));
        
        services.AddSingleton<AgentWorkerPool>(sp =>
        {
            var agent = sp.GetRequiredService<IAgent>();
            var queue = sp.GetRequiredService<AgentRequestQueue>();
            var workerCount = Environment.ProcessorCount * 2;
            return new AgentWorkerPool(agent, queue, workerCount, sp.GetRequiredService<ILogger<AgentWorkerPool>>());
        });

        // Health checks
        services.AddHealthChecks()
            .AddCheck<AgentHealthCheck>("agent")
            .AddCheck<LLMHealthCheck>("llm");

        // API controllers
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
    }

    private static void ConfigureMiddleware(WebApplication app)
    {
        // Start worker pool
        var workerPool = app.Services.GetRequiredService<AgentWorkerPool>();
        workerPool.Start();

        // Swagger in development
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // Health check endpoint
        app.MapHealthChecks("/health");

        // API endpoints
        app.MapControllers();

        // Metrics endpoint
        app.MapGet("/metrics", (AgentTelemetry telemetry) =>
        {
            // Return Prometheus-formatted metrics
            return Results.Ok();
        });
    }
}
```

---

## Kubernetes Deployment

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

## Summary

### Week 12 Deliverables

```
src/Dawning.Agents.Core/
├── Configuration/
│   ├── AgentConfiguration.cs      # Config loading
│   ├── AgentOptions.cs            # Typed options
│   └── SecretsManager.cs          # Secrets management
└── Scaling/
    ├── AgentRequestQueue.cs       # Request queue
    ├── AgentWorkerPool.cs         # Worker pool
    ├── AgentLoadBalancer.cs       # Load balancing
    ├── CircuitBreaker.cs          # Fault tolerance
    └── AgentAutoScaler.cs         # Auto-scaling

docker/
├── Dockerfile                     # Container image
└── docker-compose.yml             # Local deployment

kubernetes/
├── deployment.yaml                # K8s deployment
├── service.yaml                   # K8s service
└── hpa.yaml                       # Horizontal pod autoscaler
```

### Production Checklist

| Category | Items |
|----------|-------|
| **Security** | ✅ Non-root container, ✅ Secrets management, ✅ Input validation |
| **Observability** | ✅ Structured logging, ✅ Metrics, ✅ Distributed tracing |
| **Reliability** | ✅ Health checks, ✅ Circuit breaker, ✅ Graceful shutdown |
| **Scalability** | ✅ Horizontal scaling, ✅ Auto-scaling, ✅ Load balancing |
| **Configuration** | ✅ Environment-specific, ✅ Hot reload, ✅ Validation |

### 🎉 Congratulations!

You have completed the 12-week Dawning Agents learning plan!

You now have a comprehensive multi-agent framework with:
- Core agent loop (ReAct, Planning)
- Memory management
- Tool system
- RAG integration
- Multi-agent orchestration
- Agent communication
- Safety guardrails
- Human-in-the-loop
- Full observability
- Production deployment

**Next Steps:**
- Build real applications with your framework
- Contribute to open-source agent projects
- Explore advanced topics like agent learning
