# 安全加固指南

本指南介绍如何在生产环境中安全部署 Dawning.Agents。

## 目录

- [API Key 安全](#api-key-安全)
- [输入验证](#输入验证)
- [输出过滤](#输出过滤)
- [工具安全](#工具安全)
- [日志脱敏](#日志脱敏)
- [网络安全](#网络安全)
- [审计追踪](#审计追踪)
- [安全清单](#安全清单)

---

## API Key 安全

### 1. 永不硬编码

```csharp
// ❌ 极度危险
var options = new LLMOptions
{
    ApiKey = "sk-xxx123..."  // 永远不要这样做！
};

// ✅ 使用配置
services.AddLLMProvider(configuration);  // 从 appsettings 或环境变量读取
```

### 2. 使用 Secret Manager（开发环境）

```bash
# 初始化
dotnet user-secrets init

# 设置密钥
dotnet user-secrets set "LLM:ApiKey" "sk-xxx..."
dotnet user-secrets set "VectorStore:ApiKey" "qdrant-key..."
```

### 3. 使用环境变量（生产环境）

```bash
# Linux/macOS
export LLM__ApiKey="sk-xxx..."
export LLM__Endpoint="https://api.openai.com"

# Docker
docker run -e LLM__ApiKey="sk-xxx..." myapp
```

### 4. 使用 Azure Key Vault

```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri("https://myvault.vault.azure.net/"),
    new DefaultAzureCredential()
);
```

### 5. Key 轮换策略

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

---

## 输入验证

### 1. 使用 FluentValidation

```csharp
public class UserInputValidator : AbstractValidator<AgentInput>
{
    public UserInputValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty()
            .MaximumLength(10000)
            .Must(NotContainInjection).WithMessage("输入包含非法内容");
            
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .Matches("^[a-zA-Z0-9-]+$");
    }
    
    private bool NotContainInjection(string input)
    {
        var dangerousPatterns = new[]
        {
            "ignore previous instructions",
            "disregard all rules",
            "system prompt",
            "你现在是",
        };
        
        return !dangerousPatterns.Any(p => 
            input.Contains(p, StringComparison.OrdinalIgnoreCase));
    }
}
```

### 2. 注入防护

```csharp
public class PromptInjectionGuard : IInputGuardrail
{
    public async Task<GuardrailResult> CheckAsync(string input, CancellationToken ct)
    {
        // 检测常见的提示注入模式
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

### 3. 长度和频率限制

```csharp
services.AddRateLimiter(options =>
{
    options.MaxRequestsPerMinute = 30;        // 防止滥用
    options.MaxTokensPerRequest = 4000;       // 防止超大输入
    options.MaxTokensPerSession = 100000;     // 会话级限制
});
```

---

## 输出过滤

### 1. PII 脱敏

```csharp
public class PIIFilter : IOutputGuardrail
{
    private readonly Dictionary<string, string> _patterns = new()
    {
        // 手机号
        [@"\b1[3-9]\d{9}\b"] = "[手机号]",
        // 身份证
        [@"\b\d{17}[\dXx]\b"] = "[身份证]",
        // 邮箱
        [@"\b[\w.-]+@[\w.-]+\.\w+\b"] = "[邮箱]",
        // 银行卡
        [@"\b\d{16,19}\b"] = "[银行卡]",
    };
    
    public async Task<GuardrailResult> FilterAsync(string output, CancellationToken ct)
    {
        var filtered = output;
        
        foreach (var (pattern, replacement) in _patterns)
        {
            filtered = Regex.Replace(filtered, pattern, replacement);
        }
        
        return GuardrailResult.Pass(filtered);
    }
}
```

### 2. 敏感词过滤

```csharp
public class ContentFilter : IOutputGuardrail
{
    private readonly HashSet<string> _blocklist;
    
    public async Task<GuardrailResult> FilterAsync(string output, CancellationToken ct)
    {
        foreach (var word in _blocklist)
        {
            if (output.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return GuardrailResult.Block("输出包含敏感内容");
            }
        }
        
        return GuardrailResult.Pass();
    }
}
```

### 3. 组合护栏

```csharp
var agent = new AgentBuilder()
    .WithInputGuardrails(
        new PromptInjectionGuard(),
        new LengthValidator(maxLength: 10000)
    )
    .WithOutputGuardrails(
        new PIIFilter(),
        new ContentFilter()
    )
    .Build();
```

---

## 工具安全

### 1. 工具风险等级

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

### 2. 工具审批策略

```csharp
services.AddToolApprovalHandler(ApprovalStrategy.RiskBased);

// 或自定义策略
services.AddToolApprovalHandler<CustomApprovalHandler>();

public class CustomApprovalHandler : IToolApprovalHandler
{
    public async Task<bool> RequestApprovalAsync(ITool tool, string input, CancellationToken ct)
    {
        // 生产环境：高风险工具需要人工审批
        if (tool.RiskLevel == ToolRiskLevel.High)
        {
            return await RequestHumanApprovalAsync(tool, input);
        }
        
        return true;
    }
}
```

### 3. 文件系统隔离

```csharp
[FunctionTool("读取文件")]
public string ReadFile(string path)
{
    // 限制访问范围
    var allowedPaths = new[] { "/data", "/tmp" };
    var fullPath = Path.GetFullPath(path);
    
    if (!allowedPaths.Any(p => fullPath.StartsWith(p)))
    {
        throw new UnauthorizedAccessException("不允许访问此路径");
    }
    
    return File.ReadAllText(fullPath);
}
```

### 4. 命令白名单

```csharp
[FunctionTool("执行命令")]
public string ExecuteCommand(string command)
{
    var allowedCommands = new[] { "ls", "pwd", "whoami", "date" };
    var cmd = command.Split(' ')[0];
    
    if (!allowedCommands.Contains(cmd))
    {
        throw new SecurityException($"不允许执行命令: {cmd}");
    }
    
    // 执行命令...
}
```

---

## 日志脱敏

### 1. Serilog 脱敏

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

### 2. 自定义脱敏器

```csharp
public class LogSanitizer
{
    public string Sanitize(string log)
    {
        // API Key
        log = Regex.Replace(log, @"sk-[a-zA-Z0-9]{20,}", "[API_KEY]");
        
        // Bearer Token
        log = Regex.Replace(log, @"Bearer\s+[a-zA-Z0-9._-]+", "Bearer [TOKEN]");
        
        // 密码字段
        log = Regex.Replace(log, @"""password""\s*:\s*""[^""]+""", @"""password"": ""[REDACTED]""");
        
        return log;
    }
}
```

### 3. 审计日志分离

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

## 网络安全

### 1. HTTPS 强制

```csharp
app.UseHttpsRedirection();
app.UseHsts();
```

### 2. CORS 配置

```csharp
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

### 3. 请求验证

```csharp
app.UseMiddleware<RequestValidationMiddleware>();

public class RequestValidationMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        // 验证 Content-Type
        if (context.Request.ContentType != "application/json")
        {
            context.Response.StatusCode = 415;
            return;
        }
        
        // 验证请求大小
        if (context.Request.ContentLength > 1024 * 1024) // 1MB
        {
            context.Response.StatusCode = 413;
            return;
        }
        
        await _next(context);
    }
}
```

---

## 审计追踪

### 1. 操作审计

```csharp
public class AuditMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        var auditEntry = new AuditEntry
        {
            TraceId = Activity.Current?.TraceId.ToString(),
            UserId = context.User?.Identity?.Name,
            Action = $"{context.Request.Method} {context.Request.Path}",
            Timestamp = DateTimeOffset.UtcNow,
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
        };
        
        try
        {
            await _next(context);
            auditEntry.StatusCode = context.Response.StatusCode;
        }
        catch (Exception ex)
        {
            auditEntry.Error = ex.Message;
            throw;
        }
        finally
        {
            await _auditStore.SaveAsync(auditEntry);
        }
    }
}
```

### 2. 工具调用审计

```csharp
public class AuditingToolDecorator : ITool
{
    private readonly ITool _inner;
    private readonly IAuditLogger _audit;
    
    public async Task<ToolResult> ExecuteAsync(string input, CancellationToken ct)
    {
        var startTime = DateTimeOffset.UtcNow;
        
        try
        {
            var result = await _inner.ExecuteAsync(input, ct);
            
            await _audit.LogToolExecutionAsync(new ToolAuditEntry
            {
                ToolName = _inner.Name,
                Input = SanitizeInput(input),
                Result = result.IsSuccess ? "Success" : "Failed",
                Duration = DateTimeOffset.UtcNow - startTime,
            });
            
            return result;
        }
        catch (Exception ex)
        {
            await _audit.LogToolExecutionAsync(new ToolAuditEntry
            {
                ToolName = _inner.Name,
                Input = SanitizeInput(input),
                Result = "Exception",
                Error = ex.Message,
            });
            
            throw;
        }
    }
}
```

---

## 安全清单

### 部署前检查

- [ ] API Key 使用环境变量或 Key Vault
- [ ] 输入验证已启用
- [ ] PII 过滤已配置
- [ ] Rate Limiter 已设置
- [ ] 高风险工具需要确认
- [ ] HTTPS 已启用
- [ ] CORS 已配置
- [ ] 日志已脱敏
- [ ] 审计日志已启用

### 定期检查

- [ ] 轮换 API Key（每 90 天）
- [ ] 审查审计日志
- [ ] 检查异常访问模式
- [ ] 更新敏感词库
- [ ] 安全测试（渗透测试）

### 事件响应

1. **检测到注入攻击**
   - 立即阻断请求
   - 记录详细日志
   - 通知安全团队

2. **API Key 泄露**
   - 立即轮换 Key
   - 检查访问日志
   - 评估影响范围

3. **数据泄露**
   - 隔离受影响系统
   - 通知数据保护官
   - 启动应急预案

---

## 常见攻击防护

| 攻击类型 | 防护措施 |
|----------|----------|
| Prompt Injection | 输入验证 + Guardrails |
| Token Theft | 环境变量 + Key Vault |
| DoS | Rate Limiter |
| Data Leak | 输出过滤 + 日志脱敏 |
| Unauthorized Access | 认证 + 工具审批 |

---

*最后更新: 2026-02-04*
