# 🏭 生产环境最佳实践

> Dawning.Agents 生产部署指南

---

## 📋 部署检查清单

### 必须完成

- [ ] 配置结构化日志（Serilog）
- [ ] 启用健康检查端点
- [ ] 配置熔断器和重试策略
- [ ] 设置合理的超时时间
- [ ] 启用指标收集（OpenTelemetry）
- [ ] 配置安全护栏
- [ ] 设置 API 密钥环境变量
- [ ] 配置速率限制

### 推荐完成

- [ ] 配置分布式追踪
- [ ] 设置告警规则
- [ ] 准备回滚方案
- [ ] 编写运维文档

---

## 🔧 配置示例

### appsettings.Production.json

```json
{
  "LLM": {
    "ProviderType": "AzureOpenAI",
    "Endpoint": "${AZURE_OPENAI_ENDPOINT}",
    "Model": "gpt-4o",
    "MaxTokens": 4096,
    "Temperature": 0.3,
    "Timeout": 30
  },
  "Agent": {
    "MaxSteps": 10,
    "DefaultTimeout": 60
  },
  "Memory": {
    "Type": "Window",
    "WindowSize": 20
  },
  "Safety": {
    "EnableContentFilter": true,
    "EnableSensitiveDataFilter": true,
    "MaxInputLength": 10000,
    "BlockedPatterns": ["password", "secret", "key"]
  },
  "Resilience": {
    "RetryCount": 3,
    "RetryDelayMs": 1000,
    "CircuitBreakerThreshold": 5,
    "CircuitBreakerDurationMs": 30000
  },
  "Observability": {
    "EnableLogging": true,
    "EnableMetrics": true,
    "EnableTracing": true,
    "ServiceName": "dawning-agent-prod",
    "OtlpEndpoint": "http://otel-collector:4317"
  },
  "RateLimit": {
    "RequestsPerMinute": 60,
    "TokensPerMinute": 100000
  }
}
```

---

## 📊 监控指标

### 关键指标

| 指标 | 说明 | 告警阈值 |
|------|------|----------|
| `agent_request_total` | 请求总数 | - |
| `agent_request_duration_ms` | 请求延迟 | P99 > 10s |
| `agent_error_total` | 错误总数 | 错误率 > 5% |
| `agent_tool_call_total` | 工具调用次数 | - |
| `llm_token_total` | Token 消耗 | 超预算告警 |
| `circuit_breaker_state` | 熔断器状态 | state = Open |

### Prometheus 配置

```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'dawning-agent'
    static_configs:
      - targets: ['agent-service:8080']
    metrics_path: '/metrics'
    scrape_interval: 15s
```

### Grafana Dashboard 示例

```json
{
  "panels": [
    {
      "title": "请求延迟 (P50/P95/P99)",
      "type": "graph",
      "targets": [
        { "expr": "histogram_quantile(0.50, agent_request_duration_ms)" },
        { "expr": "histogram_quantile(0.95, agent_request_duration_ms)" },
        { "expr": "histogram_quantile(0.99, agent_request_duration_ms)" }
      ]
    },
    {
      "title": "错误率",
      "type": "stat",
      "targets": [
        { "expr": "rate(agent_error_total[5m]) / rate(agent_request_total[5m])" }
      ]
    }
  ]
}
```

---

## 🔐 安全配置

### 环境变量

```bash
# 必须使用环境变量，不要硬编码
export AZURE_OPENAI_API_KEY="your-key"
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com"
export OPENAI_API_KEY="sk-..."
```

### Kubernetes Secrets

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: agent-secrets
type: Opaque
stringData:
  AZURE_OPENAI_API_KEY: "your-key"
  AZURE_OPENAI_ENDPOINT: "https://..."
```

### 敏感数据过滤

```csharp
services.AddSafetyGuardrails(options =>
{
    options.EnableSensitiveDataFilter = true;
    options.SensitivePatterns =
    [
        @"\b\d{16}\b",           // 信用卡号
        @"\b\d{11}\b",           // 手机号
        @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",  // 邮箱
        @"\b\d{18}|\d{15}\b",    // 身份证号
    ];
});
```

---

## ⚡ 性能优化

### 1. Token 优化

```csharp
// 使用更小的模型处理简单任务
services.AddModelRouter(options =>
{
    options.Strategy = ModelRoutingStrategy.CostOptimized;
    options.CustomPricing["gpt-4o-mini"] = new ModelPricing
    {
        InputPricePerKToken = 0.00015m,
        OutputPricePerKToken = 0.0006m,
    };
});
```

### 2. 缓存策略

```csharp
// 缓存 Embedding 结果
services.AddEmbeddingCache(options =>
{
    options.CacheType = "Redis";
    options.ConnectionString = "localhost:6379";
    options.ExpirationMinutes = 60;
});
```

### 3. 批量处理

```csharp
// 批量 Embedding
var embeddings = await embeddingProvider.EmbedBatchAsync(texts, batchSize: 100);
```

### 4. 连接池

```csharp
// HttpClient 连接池
services.AddHttpClient("OpenAI")
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        MaxConnectionsPerServer = 100,
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
    });
```

---

## 🔄 高可用部署

### Kubernetes 部署

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: dawning-agent
spec:
  replicas: 3
  selector:
    matchLabels:
      app: dawning-agent
  template:
    metadata:
      labels:
        app: dawning-agent
    spec:
      containers:
      - name: agent
        image: dawning-agent:latest
        resources:
          requests:
            memory: "512Mi"
            cpu: "500m"
          limits:
            memory: "2Gi"
            cpu: "2000m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5
        envFrom:
        - secretRef:
            name: agent-secrets
```

### 健康检查端点

```csharp
// Program.cs
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});
```

---

## 🚨 故障处理

### 熔断器配置

```csharp
services.AddResiliencePipeline("llm-pipeline", builder =>
{
    builder
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
        })
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 10,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(30),
        })
        .AddTimeout(TimeSpan.FromSeconds(30));
});
```

### 降级策略

```csharp
// 当主模型不可用时降级到备用模型
services.AddModelRouter(options =>
{
    options.EnableFailover = true;
    options.MaxFailoverRetries = 2;
    options.FallbackProviders = ["gpt-4o-mini", "ollama-local"];
});
```

---

## 📈 成本控制

### Token 预算

```csharp
services.Configure<LLMOptions>(options =>
{
    options.MaxTokensPerRequest = 4096;
    options.DailyTokenBudget = 1_000_000;
    options.MonthlyBudgetUSD = 100;
});
```

### 成本追踪

```csharp
// 在每次调用后记录成本
router.ReportResult(provider, ModelCallResult.Succeeded(
    latencyMs: 500,
    inputTokens: 1000,
    outputTokens: 450,
    cost: 0.005m
));

// 查询统计
var stats = router.GetStatistics("gpt-4o");
Console.WriteLine($"总成本: ${stats.TotalCost}");
Console.WriteLine($"总请求: {stats.TotalRequests}");
```

---

## 📝 日志最佳实践

### 结构化日志配置

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "DawningAgent")
    .Enrich.WithProperty("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"))
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://elasticsearch:9200"))
    {
        IndexFormat = "dawning-agent-{0:yyyy.MM}",
        AutoRegisterTemplate = true,
    })
    .CreateLogger();
```

### 关键日志点

```csharp
// Agent 执行开始
_logger.LogInformation("Agent {AgentName} 开始执行，输入长度: {InputLength}",
    agent.Name, input.Length);

// 工具调用
_logger.LogInformation("调用工具 {ToolName}，参数: {Parameters}",
    tool.Name, parameters);

// 执行完成
_logger.LogInformation("Agent {AgentName} 执行完成，步骤数: {Steps}，耗时: {Duration}ms",
    agent.Name, response.Steps.Count, stopwatch.ElapsedMilliseconds);

// 错误处理
_logger.LogError(ex, "Agent {AgentName} 执行失败: {Error}",
    agent.Name, ex.Message);
```

---

> 📌 **更多资源**: 查看 [API 参考](../API_REFERENCE.md) 了解详细接口
