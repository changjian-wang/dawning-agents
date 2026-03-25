# 生产环境部署指南

> Dawning.Agents 安全加固与生产部署的完整指南

---

## 📋 部署检查清单

### 必须完成

- [ ] 配置 API 密钥（环境变量 / Key Vault）
- [ ] 配置安全护栏（内容过滤、PII 脱敏、注入防护）
- [ ] 配置结构化日志（Serilog）
- [ ] 启用健康检查端点
- [ ] 配置熔断器和重试策略
- [ ] 设置合理的超时时间
- [ ] 启用指标收集（OpenTelemetry）
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

## 🔐 安全加固

### API Key 安全

#### 1. 永不硬编码

```csharp
// ❌ 极度危险
var options = new LLMOptions
{
    ApiKey = "sk-xxx123..."  // 永远不要这样做！
};

// ✅ 使用配置
services.AddLLMProvider(configuration);  // 从 appsettings 或环境变量读取
```

#### 2. 使用 Secret Manager（开发环境）

```bash
# 初始化
dotnet user-secrets init

# 设置密钥
dotnet user-secrets set "LLM:ApiKey" "sk-xxx..."
dotnet user-secrets set "VectorStore:ApiKey" "qdrant-key..."
```

#### 3. 使用环境变量（生产环境）

```bash
# Linux/macOS
export LLM__ApiKey="sk-xxx..."
export LLM__Endpoint="https://api.openai.com"

# Docker
docker run -e LLM__ApiKey="sk-xxx..." myapp
```

#### 4. 使用 Azure Key Vault

```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri("https://myvault.vault.azure.net/"),
    new DefaultAzureCredential()
);
```

#### 5. Key 轮换策略

```csharp
// 使用 IOptionsMonitor 支持热更新
public class LLMService
{
    private readonly IOptionsMonitor<LLMOptions> _options;
    
    public LLMService(IOptionsMonitor<LLMOptions> options)
    {
        _options = options;
        _options.OnChange(newOptions =>
        {
            // Key 轮换时自动生效
            _logger.LogInformation("LLM API Key 已更新");
        });
    }
}
```

#### 6. Kubernetes Secrets

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

### 输入验证与注入防护

#### 长度和频率限制

```csharp
services.AddRateLimiter(options =>
{
    options.MaxRequestsPerMinute = 30;        // 防止滥用
    options.MaxTokensPerRequest = 4000;       // 防止超大输入
    options.MaxTokensPerSession = 100000;     // 会话级限制
});
```

#### 提示注入防护

```csharp
public class PromptInjectionGuard : IInputGuardrail
{
    public async Task<GuardrailResult> CheckAsync(string input, CancellationToken ct)
    {
        var injectionPatterns = new[]
        {
            @"ignore\s+(all\s+)?(previous|above)\s+instructions",
            @"disregard\s+(all\s+)?rules",
            @"你(现在)?是\s*\w+\s*(而不是|不是)",
            @"以下是(你的)?新指令",
            @"system\s*:\s*",
        };
        
        foreach (var pattern in injectionPatterns)
        {
            if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
            {
                return GuardrailResult.Block("检测到潜在的提示注入攻击");
            }
        }
        
        return GuardrailResult.Pass();
    }
}
```

### 输出过滤

#### PII 脱敏

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

### 工具安全

#### 工具风险等级

```csharp
[FunctionTool("执行命令", RiskLevel = ToolRiskLevel.High, RequiresConfirmation = true)]
public string ExecuteCommand(string command)
{
    // 高风险工具需要确认
}

[FunctionTool("查询天气", RiskLevel = ToolRiskLevel.Low)]
public string GetWeather(string city)
{
    // 低风险工具无需确认
}
```

#### 工具审批策略

```csharp
services.AddToolApprovalHandler(ApprovalStrategy.RiskBased);

public class CustomApprovalHandler : IToolApprovalHandler
{
    public async Task<bool> RequestApprovalAsync(ITool tool, string input, CancellationToken ct)
    {
        if (tool.RiskLevel == ToolRiskLevel.High)
        {
            return await RequestHumanApprovalAsync(tool, input);
        }
        return true;
    }
}
```

### 日志脱敏

```csharp
services.AddSerilog((sp, config) =>
{
    config
        .Enrich.WithProperty("Application", "Dawning.Agents")
        .Destructure.ByTransforming<AgentInput>(input => new
        {
            SessionId = input.SessionId,
            MessageLength = input.Message?.Length ?? 0,
            // 不记录完整消息内容
        })
        .WriteTo.Console();
});
```

### 网络安全

```csharp
app.UseHttpsRedirection();
app.UseHsts();

services.AddCors(options =>
{
    options.AddPolicy("Production", builder =>
    {
        builder
            .WithOrigins("https://myapp.com")
            .WithMethods("POST")
            .WithHeaders("Content-Type", "Authorization");
    });
});
```

### 审计追踪

```csharp
// 普通日志
_logger.LogInformation("Agent 处理请求，会话: {SessionId}", sessionId);

// 审计日志（单独存储）
_auditLogger.LogAudit(new AuditEvent
{
    Type = "AgentExecution",
    SessionId = sessionId,
    UserId = userId,
    Action = "RunAgent",
    Timestamp = DateTimeOffset.UtcNow,
    Result = "Success",
});
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

## ⚡ 性能优化

### Token 优化

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

### 缓存策略

```csharp
services.AddEmbeddingCache(options =>
{
    options.CacheType = "Redis";
    options.ConnectionString = "localhost:6379";
    options.ExpirationMinutes = 60;
});
```

### 连接池

```csharp
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
    options.MaxTokensPerDay = 1000000;
});

// Token 用量追踪
services.AddTokenTracking();
```
