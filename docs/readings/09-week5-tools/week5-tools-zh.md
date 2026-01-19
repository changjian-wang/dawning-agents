# Week 5: 工具开发与集成

> Phase 3: 工具系统与 RAG 集成
> Week 5 学习资料：构建和管理 Agent 工具

---

## Day 1-2: 工具系统架构

### 1. 什么是工具？

工具是 AI Agent 的**手和眼睛**，使其能够：

- **与外部系统交互**（API、数据库、服务）
- **执行操作**（发送邮件、创建文件、执行代码）
- **检索信息**（搜索、查找、查询）

```text
┌─────────────────────────────────────────────────────────────────┐
│                      工具系统架构                                │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│    ┌─────────────┐                                               │
│    │    Agent    │                                               │
│    └──────┬──────┘                                               │
│           │                                                      │
│           ▼                                                      │
│    ┌─────────────┐     ┌─────────────┐     ┌─────────────┐      │
│    │    工具     │────►│    工具     │────►│    外部     │      │
│    │   注册表    │     │   执行器    │     │    服务     │      │
│    └─────────────┘     └─────────────┘     └─────────────┘      │
│           │                   │                   │              │
│           │                   │                   │              │
│    ┌──────┴──────┐     ┌──────┴──────┐     ┌──────┴──────┐      │
│    │  工具模式   │     │ 结果解析器  │     │    响应     │      │
│    └─────────────┘     └─────────────┘     └─────────────┘      │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. 核心工具接口

```csharp
namespace Dawning.Agents.Core.Tools;

/// <summary>
/// 所有工具的基础接口
/// </summary>
public interface ITool
{
    /// <summary>
    /// 工具的唯一名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 工具功能的人类可读描述
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// 描述工具参数的 JSON 模式
    /// </summary>
    string ParametersSchema { get; }
    
    /// <summary>
    /// 使用给定输入执行工具
    /// </summary>
    Task<ToolResult> ExecuteAsync(
        string input, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 执行前验证输入
    /// </summary>
    bool ValidateInput(string input, out string? error);
}

/// <summary>
/// 工具执行结果
/// </summary>
public record ToolResult
{
    /// <summary>
    /// 输出内容
    /// </summary>
    public required string Content { get; init; }
    
    /// <summary>
    /// 执行是否成功
    /// </summary>
    public bool IsSuccess { get; init; } = true;
    
    /// <summary>
    /// 执行失败时的错误消息
    /// </summary>
    public string? Error { get; init; }
    
    /// <summary>
    /// 执行元数据
    /// </summary>
    public IDictionary<string, object>? Metadata { get; init; }
    
    /// <summary>
    /// 执行持续时间
    /// </summary>
    public TimeSpan? Duration { get; init; }
    
    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static ToolResult Success(string content, IDictionary<string, object>? metadata = null)
        => new() { Content = content, IsSuccess = true, Metadata = metadata };
    
    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static ToolResult Failure(string error)
        => new() { Content = "", IsSuccess = false, Error = error };
}
```

### 3. 工具基类

```csharp
namespace Dawning.Agents.Core.Tools;

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// 包含通用功能的工具基类
/// </summary>
public abstract class ToolBase : ITool
{
    protected readonly ILogger Logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string ParametersSchema { get; }

    protected ToolBase(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task<ToolResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // 验证输入
            if (!ValidateInput(input, out var error))
            {
                Logger.LogWarning("工具 {Name} 输入验证失败：{Error}", Name, error);
                return ToolResult.Failure($"无效输入：{error}");
            }

            Logger.LogDebug("执行工具 {Name}，输入：{Input}", Name, input);
            
            // 执行工具
            var result = await ExecuteCoreAsync(input, cancellationToken);
            
            stopwatch.Stop();
            result = result with { Duration = stopwatch.Elapsed };
            
            Logger.LogDebug(
                "工具 {Name} 在 {Duration}ms 内完成，成功：{IsSuccess}",
                Name, stopwatch.ElapsedMilliseconds, result.IsSuccess);
            
            return result;
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("工具 {Name} 执行被取消", Name);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "工具 {Name} 执行失败", Name);
            return ToolResult.Failure(ex.Message) with { Duration = stopwatch.Elapsed };
        }
    }

    /// <summary>
    /// 核心执行逻辑 - 由派生类实现
    /// </summary>
    protected abstract Task<ToolResult> ExecuteCoreAsync(
        string input,
        CancellationToken cancellationToken);

    public virtual bool ValidateInput(string input, out string? error)
    {
        error = null;
        
        if (string.IsNullOrWhiteSpace(input))
        {
            error = "输入不能为空";
            return false;
        }
        
        return true;
    }

    /// <summary>
    /// 将 JSON 输入解析为类型化对象
    /// </summary>
    protected T? ParseInput<T>(string input) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(input, _jsonOptions);
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "无法将输入解析为 {Type}", typeof(T).Name);
            return null;
        }
    }

    /// <summary>
    /// 将结果序列化为 JSON
    /// </summary>
    protected string ToJson<T>(T value)
    {
        return JsonSerializer.Serialize(value, _jsonOptions);
    }
}
```

---

## Day 3-4: 工具属性与注册表

### 1. 用于声明式定义的工具属性

```csharp
namespace Dawning.Agents.Core.Tools;

/// <summary>
/// 将类标记为工具
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class ToolAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }

    public ToolAttribute(string name, string description)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }
}

/// <summary>
/// 将方法标记为工具的执行方法
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ToolExecuteAttribute : Attribute
{
}

/// <summary>
/// 描述工具参数
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class ToolParameterAttribute : Attribute
{
    public string Description { get; }
    public bool Required { get; set; } = true;
    public object? DefaultValue { get; set; }

    public ToolParameterAttribute(string description)
    {
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }
}

/// <summary>
/// 工具属性使用示例
/// </summary>
[Tool("weather", "获取城市的当前天气信息")]
public class WeatherToolExample
{
    [ToolExecute]
    public async Task<string> GetWeatherAsync(
        [ToolParameter("要获取天气的城市名称")] string city,
        [ToolParameter("温度单位", Required = false, DefaultValue = "celsius")] string unit = "celsius")
    {
        // 实现
        await Task.Delay(100);
        return $"{city} 的天气：22°{(unit == "celsius" ? "C" : "F")}，晴";
    }
}
```

### 2. 工具模式生成器

```csharp
namespace Dawning.Agents.Core.Tools;

using System.Reflection;
using System.Text.Json;

/// <summary>
/// 为工具生成 JSON 模式
/// </summary>
public class ToolSchemaGenerator
{
    /// <summary>
    /// 从工具类生成 JSON 模式
    /// </summary>
    public ToolSchema GenerateSchema(Type toolType)
    {
        var toolAttr = toolType.GetCustomAttribute<ToolAttribute>()
            ?? throw new InvalidOperationException($"类型 {toolType.Name} 未标记 [Tool]");

        var executeMethod = toolType.GetMethods()
            .FirstOrDefault(m => m.GetCustomAttribute<ToolExecuteAttribute>() != null)
            ?? throw new InvalidOperationException($"在 {toolType.Name} 中未找到 [ToolExecute] 方法");

        var parameters = GenerateParametersSchema(executeMethod);

        return new ToolSchema
        {
            Name = toolAttr.Name,
            Description = toolAttr.Description,
            Parameters = parameters
        };
    }

    private ParametersSchema GenerateParametersSchema(MethodInfo method)
    {
        var properties = new Dictionary<string, ParameterProperty>();
        var required = new List<string>();

        foreach (var param in method.GetParameters())
        {
            var paramAttr = param.GetCustomAttribute<ToolParameterAttribute>();
            var propName = param.Name!;

            properties[propName] = new ParameterProperty
            {
                Type = GetJsonType(param.ParameterType),
                Description = paramAttr?.Description ?? propName
            };

            if (paramAttr?.Required != false && !param.HasDefaultValue)
            {
                required.Add(propName);
            }
        }

        return new ParametersSchema
        {
            Type = "object",
            Properties = properties,
            Required = required
        };
    }

    private string GetJsonType(Type type)
    {
        if (type == typeof(string)) return "string";
        if (type == typeof(int) || type == typeof(long)) return "integer";
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return "number";
        if (type == typeof(bool)) return "boolean";
        if (type.IsArray || typeof(IEnumerable<>).IsAssignableFrom(type)) return "array";
        return "object";
    }
}

public record ToolSchema
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required ParametersSchema Parameters { get; init; }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    });
}

public record ParametersSchema
{
    public required string Type { get; init; }
    public required Dictionary<string, ParameterProperty> Properties { get; init; }
    public required List<string> Required { get; init; }
}

public record ParameterProperty
{
    public required string Type { get; init; }
    public required string Description { get; init; }
    public List<string>? Enum { get; init; }
}
```

### 3. 工具注册表

```csharp
namespace Dawning.Agents.Core.Tools;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

/// <summary>
/// 管理可用工具的注册表
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// 注册工具
    /// </summary>
    void Register(ITool tool);
    
    /// <summary>
    /// 注册多个工具
    /// </summary>
    void RegisterRange(IEnumerable<ITool> tools);
    
    /// <summary>
    /// 按名称获取工具
    /// </summary>
    ITool? GetTool(string name);
    
    /// <summary>
    /// 获取所有已注册的工具
    /// </summary>
    IReadOnlyList<ITool> GetAllTools();
    
    /// <summary>
    /// 检查工具是否存在
    /// </summary>
    bool HasTool(string name);
    
    /// <summary>
    /// 移除工具
    /// </summary>
    bool Unregister(string name);
}

public class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<ToolRegistry> _logger;

    public ToolRegistry(ILogger<ToolRegistry> logger)
    {
        _logger = logger;
    }

    public void Register(ITool tool)
    {
        if (tool == null) throw new ArgumentNullException(nameof(tool));

        if (_tools.TryAdd(tool.Name, tool))
        {
            _logger.LogInformation("已注册工具：{Name}", tool.Name);
        }
        else
        {
            _logger.LogWarning("工具 {Name} 已注册，跳过", tool.Name);
        }
    }

    public void RegisterRange(IEnumerable<ITool> tools)
    {
        foreach (var tool in tools)
        {
            Register(tool);
        }
    }

    public ITool? GetTool(string name)
    {
        _tools.TryGetValue(name, out var tool);
        return tool;
    }

    public IReadOnlyList<ITool> GetAllTools()
    {
        return _tools.Values.ToList();
    }

    public bool HasTool(string name)
    {
        return _tools.ContainsKey(name);
    }

    public bool Unregister(string name)
    {
        if (_tools.TryRemove(name, out _))
        {
            _logger.LogInformation("已注销工具：{Name}", name);
            return true;
        }
        return false;
    }
}
```

---

## Day 5-7: 内置工具实现

### 1. 计算器工具

```csharp
namespace Dawning.Agents.Core.Tools.BuiltIn;

using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// 用于执行数学计算的工具
/// </summary>
public class CalculatorTool : ToolBase
{
    public override string Name => "calculator";
    public override string Description => "执行数学计算。支持基本算术（+、-、*、/）、括号和常用函数。";
    
    public override string ParametersSchema => """
        {
            "type": "object",
            "properties": {
                "expression": {
                    "type": "string",
                    "description": "要计算的数学表达式（例如 '2 + 3 * 4'、'(10 + 5) / 3'）"
                }
            },
            "required": ["expression"]
        }
        """;

    public CalculatorTool(ILogger<CalculatorTool> logger) : base(logger)
    {
    }

    protected override Task<ToolResult> ExecuteCoreAsync(
        string input,
        CancellationToken cancellationToken)
    {
        var request = ParseInput<CalculatorRequest>(input);
        if (request == null)
        {
            // 尝试将输入作为原始表达式处理
            request = new CalculatorRequest { Expression = input.Trim('"') };
        }

        try
        {
            var result = EvaluateExpression(request.Expression);
            
            return Task.FromResult(ToolResult.Success(
                $"结果：{result}",
                new Dictionary<string, object>
                {
                    ["expression"] = request.Expression,
                    ["result"] = result
                }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Failure($"计算错误：{ex.Message}"));
        }
    }

    private double EvaluateExpression(string expression)
    {
        // 清理表达式
        expression = expression.Replace(" ", "");
        
        // 使用 DataTable.Compute 处理基本表达式
        var table = new DataTable();
        var result = table.Compute(expression, null);
        
        return Convert.ToDouble(result);
    }

    private record CalculatorRequest
    {
        public string Expression { get; init; } = "";
    }
}
```

### 2. 日期时间工具

```csharp
namespace Dawning.Agents.Core.Tools.BuiltIn;

using System.Globalization;
using Microsoft.Extensions.Logging;

/// <summary>
/// 用于日期和时间操作的工具
/// </summary>
public class DateTimeTool : ToolBase
{
    public override string Name => "datetime";
    public override string Description => "获取当前日期/时间、转换时区、计算日期差异和格式化日期。";
    
    public override string ParametersSchema => """
        {
            "type": "object",
            "properties": {
                "operation": {
                    "type": "string",
                    "enum": ["now", "format", "add", "diff", "timezone"],
                    "description": "要执行的操作"
                },
                "date": {
                    "type": "string",
                    "description": "操作的日期字符串（ISO 8601 格式）"
                },
                "format": {
                    "type": "string",
                    "description": "输出格式（例如 'yyyy-MM-dd'、'MMMM dd, yyyy'）"
                },
                "timezone": {
                    "type": "string",
                    "description": "目标时区（例如 'UTC'、'America/New_York'）"
                },
                "addValue": {
                    "type": "integer",
                    "description": "'add' 操作的值"
                },
                "addUnit": {
                    "type": "string",
                    "enum": ["days", "hours", "minutes", "months", "years"],
                    "description": "'add' 操作的单位"
                },
                "endDate": {
                    "type": "string",
                    "description": "'diff' 操作的结束日期"
                }
            },
            "required": ["operation"]
        }
        """;

    public DateTimeTool(ILogger<DateTimeTool> logger) : base(logger)
    {
    }

    protected override Task<ToolResult> ExecuteCoreAsync(
        string input,
        CancellationToken cancellationToken)
    {
        var request = ParseInput<DateTimeRequest>(input);
        if (request == null)
        {
            return Task.FromResult(ToolResult.Failure("无效的输入格式"));
        }

        try
        {
            var result = request.Operation?.ToLower() switch
            {
                "now" => HandleNow(request),
                "format" => HandleFormat(request),
                "add" => HandleAdd(request),
                "diff" => HandleDiff(request),
                "timezone" => HandleTimezone(request),
                _ => throw new ArgumentException($"未知操作：{request.Operation}")
            };

            return Task.FromResult(ToolResult.Success(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Failure(ex.Message));
        }
    }

    private string HandleNow(DateTimeRequest request)
    {
        var now = DateTime.UtcNow;
        var format = request.Format ?? "yyyy-MM-dd HH:mm:ss";
        return $"当前 UTC 时间：{now.ToString(format, CultureInfo.InvariantCulture)}";
    }

    private string HandleFormat(DateTimeRequest request)
    {
        if (string.IsNullOrEmpty(request.Date))
            throw new ArgumentException("format 操作需要日期");

        var date = DateTime.Parse(request.Date, CultureInfo.InvariantCulture);
        var format = request.Format ?? "yyyy-MM-dd";
        return date.ToString(format, CultureInfo.InvariantCulture);
    }

    private string HandleAdd(DateTimeRequest request)
    {
        var date = string.IsNullOrEmpty(request.Date) 
            ? DateTime.UtcNow 
            : DateTime.Parse(request.Date, CultureInfo.InvariantCulture);

        var value = request.AddValue ?? 0;
        
        var result = request.AddUnit?.ToLower() switch
        {
            "days" => date.AddDays(value),
            "hours" => date.AddHours(value),
            "minutes" => date.AddMinutes(value),
            "months" => date.AddMonths(value),
            "years" => date.AddYears(value),
            _ => throw new ArgumentException($"未知单位：{request.AddUnit}")
        };

        return result.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private string HandleDiff(DateTimeRequest request)
    {
        if (string.IsNullOrEmpty(request.Date) || string.IsNullOrEmpty(request.EndDate))
            throw new ArgumentException("diff 操作需要 date 和 endDate");

        var start = DateTime.Parse(request.Date, CultureInfo.InvariantCulture);
        var end = DateTime.Parse(request.EndDate, CultureInfo.InvariantCulture);
        var diff = end - start;

        return $"差异：{diff.Days} 天，{diff.Hours} 小时，{diff.Minutes} 分钟";
    }

    private string HandleTimezone(DateTimeRequest request)
    {
        var date = string.IsNullOrEmpty(request.Date)
            ? DateTime.UtcNow
            : DateTime.Parse(request.Date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

        var targetTz = TimeZoneInfo.FindSystemTimeZoneById(request.Timezone ?? "UTC");
        var converted = TimeZoneInfo.ConvertTimeFromUtc(date, targetTz);

        return $"{converted:yyyy-MM-dd HH:mm:ss}（{targetTz.DisplayName}）";
    }

    private record DateTimeRequest
    {
        public string? Operation { get; init; }
        public string? Date { get; init; }
        public string? Format { get; init; }
        public string? Timezone { get; init; }
        public int? AddValue { get; init; }
        public string? AddUnit { get; init; }
        public string? EndDate { get; init; }
    }
}
```

### 3. HTTP 请求工具

```csharp
namespace Dawning.Agents.Core.Tools.BuiltIn;

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// 用于发送 HTTP 请求的工具
/// </summary>
public class HttpTool : ToolBase
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public override string Name => "http";
    public override string Description => "向 API 和 Web 服务发送 HTTP 请求。支持 GET、POST、PUT、DELETE 方法。";
    
    public override string ParametersSchema => """
        {
            "type": "object",
            "properties": {
                "url": {
                    "type": "string",
                    "description": "发送请求的 URL"
                },
                "method": {
                    "type": "string",
                    "enum": ["GET", "POST", "PUT", "DELETE"],
                    "description": "HTTP 方法（默认：GET）"
                },
                "headers": {
                    "type": "object",
                    "description": "请求头，键值对形式"
                },
                "body": {
                    "type": "string",
                    "description": "请求体（用于 POST/PUT）"
                }
            },
            "required": ["url"]
        }
        """;

    public HttpTool(ILogger<HttpTool> logger, HttpClient? httpClient = null) : base(logger)
    {
        _httpClient = httpClient ?? new HttpClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    protected override async Task<ToolResult> ExecuteCoreAsync(
        string input,
        CancellationToken cancellationToken)
    {
        var request = ParseInput<HttpRequest>(input);
        if (request == null || string.IsNullOrEmpty(request.Url))
        {
            return ToolResult.Failure("无效请求：需要 URL");
        }

        try
        {
            var httpRequest = new HttpRequestMessage(
                GetHttpMethod(request.Method ?? "GET"),
                request.Url);

            // 添加请求头
            if (request.Headers != null)
            {
                foreach (var (key, value) in request.Headers)
                {
                    httpRequest.Headers.TryAddWithoutValidation(key, value);
                }
            }

            // 添加请求体
            if (!string.IsNullOrEmpty(request.Body))
            {
                httpRequest.Content = new StringContent(
                    request.Body,
                    Encoding.UTF8,
                    "application/json");
            }

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            // 如果太长则截断
            if (content.Length > 5000)
            {
                content = content[..5000] + "\n...（已截断）";
            }

            return ToolResult.Success(
                $"状态：{(int)response.StatusCode} {response.StatusCode}\n\n{content}",
                new Dictionary<string, object>
                {
                    ["statusCode"] = (int)response.StatusCode,
                    ["url"] = request.Url
                });
        }
        catch (HttpRequestException ex)
        {
            return ToolResult.Failure($"HTTP 错误：{ex.Message}");
        }
    }

    private static HttpMethod GetHttpMethod(string method) => method.ToUpper() switch
    {
        "GET" => HttpMethod.Get,
        "POST" => HttpMethod.Post,
        "PUT" => HttpMethod.Put,
        "DELETE" => HttpMethod.Delete,
        _ => HttpMethod.Get
    };

    private record HttpRequest
    {
        public string? Url { get; init; }
        public string? Method { get; init; }
        public Dictionary<string, string>? Headers { get; init; }
        public string? Body { get; init; }
    }
}
```

### 4. 搜索工具（网络搜索）

```csharp
namespace Dawning.Agents.Core.Tools.BuiltIn;

using System.Web;
using Microsoft.Extensions.Logging;

/// <summary>
/// 用于网络搜索的工具
/// </summary>
public class SearchTool : ToolBase
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string? _searchEngineId;

    public override string Name => "search";
    public override string Description => "搜索网络获取信息。返回相关搜索结果。";
    
    public override string ParametersSchema => """
        {
            "type": "object",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "搜索查询"
                },
                "numResults": {
                    "type": "integer",
                    "description": "返回的结果数量（默认：5，最大：10）"
                }
            },
            "required": ["query"]
        }
        """;

    public SearchTool(
        ILogger<SearchTool> logger,
        HttpClient? httpClient = null,
        string? apiKey = null,
        string? searchEngineId = null) : base(logger)
    {
        _httpClient = httpClient ?? new HttpClient();
        _apiKey = apiKey;
        _searchEngineId = searchEngineId;
    }

    protected override async Task<ToolResult> ExecuteCoreAsync(
        string input,
        CancellationToken cancellationToken)
    {
        var request = ParseInput<SearchRequest>(input);
        if (request == null)
        {
            // 尝试将输入作为原始查询处理
            request = new SearchRequest { Query = input.Trim('"') };
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return ToolResult.Failure("需要搜索查询");
        }

        var numResults = Math.Min(request.NumResults ?? 5, 10);

        // 如果没有 API 密钥，返回模拟结果（用于开发）
        if (string.IsNullOrEmpty(_apiKey))
        {
            return GetSimulatedResults(request.Query, numResults);
        }

        try
        {
            var results = await SearchWithApiAsync(request.Query, numResults, cancellationToken);
            return ToolResult.Success(results);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "搜索失败，查询：{Query}", request.Query);
            return ToolResult.Failure($"搜索失败：{ex.Message}");
        }
    }

    private async Task<string> SearchWithApiAsync(
        string query,
        int numResults,
        CancellationToken cancellationToken)
    {
        // 使用 Google Custom Search API 的示例
        var url = $"https://www.googleapis.com/customsearch/v1" +
                  $"?key={_apiKey}" +
                  $"&cx={_searchEngineId}" +
                  $"&q={HttpUtility.UrlEncode(query)}" +
                  $"&num={numResults}";

        var response = await _httpClient.GetStringAsync(url, cancellationToken);
        
        // 解析和格式化结果
        // （简化版 - 实际实现会解析 JSON）
        return response;
    }

    private ToolResult GetSimulatedResults(string query, int numResults)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"搜索结果：{query}");
        sb.AppendLine();

        for (int i = 1; i <= numResults; i++)
        {
            sb.AppendLine($"{i}. [模拟结果 {i}]");
            sb.AppendLine($"   URL: https://example.com/result-{i}");
            sb.AppendLine($"   这是查询 {query} 的模拟搜索结果");
            sb.AppendLine();
        }

        sb.AppendLine("注意：使用模拟结果。配置 API 密钥以获取真实搜索。");

        return ToolResult.Success(sb.ToString());
    }

    private record SearchRequest
    {
        public string Query { get; init; } = "";
        public int? NumResults { get; init; }
    }
}
```

---

## 工具执行器

```csharp
namespace Dawning.Agents.Core.Tools;

using Microsoft.Extensions.Logging;

/// <summary>
/// 执行工具并处理错误
/// </summary>
public interface IToolExecutor
{
    /// <summary>
    /// 按名称执行工具
    /// </summary>
    Task<ToolResult> ExecuteAsync(
        string toolName,
        string input,
        CancellationToken cancellationToken = default);
}

public class ToolExecutor : IToolExecutor
{
    private readonly IToolRegistry _registry;
    private readonly ILogger<ToolExecutor> _logger;
    private readonly ToolExecutorOptions _options;

    public ToolExecutor(
        IToolRegistry registry,
        ILogger<ToolExecutor> logger,
        ToolExecutorOptions? options = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new ToolExecutorOptions();
    }

    public async Task<ToolResult> ExecuteAsync(
        string toolName,
        string input,
        CancellationToken cancellationToken = default)
    {
        var tool = _registry.GetTool(toolName);
        
        if (tool == null)
        {
            _logger.LogWarning("未找到工具：{ToolName}", toolName);
            return ToolResult.Failure(
                $"未找到工具 '{toolName}'。可用工具：{string.Join(", ", _registry.GetAllTools().Select(t => t.Name))}");
        }

        // 带超时执行
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_options.Timeout);

        try
        {
            return await tool.ExecuteAsync(input, cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("工具 {ToolName} 超时", toolName);
            return ToolResult.Failure($"工具执行在 {_options.Timeout.TotalSeconds} 秒后超时");
        }
    }
}

public class ToolExecutorOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxRetries { get; set; } = 0;
}
```

---

## 总结

### Week 5 产出物

```
src/Dawning.Agents.Core/
├── Tools/
│   ├── ITool.cs               # 工具接口
│   ├── ToolBase.cs            # 基类
│   ├── ToolResult.cs          # 执行结果
│   ├── ToolAttribute.cs       # 声明式属性
│   ├── ToolSchemaGenerator.cs # 模式生成
│   ├── IToolRegistry.cs       # 注册表接口
│   ├── ToolRegistry.cs        # 注册表实现
│   ├── IToolExecutor.cs       # 执行器接口
│   ├── ToolExecutor.cs        # 执行器实现
│   └── BuiltIn/
│       ├── CalculatorTool.cs  # 数学计算
│       ├── DateTimeTool.cs    # 日期/时间操作
│       ├── HttpTool.cs        # HTTP 请求
│       └── SearchTool.cs      # 网络搜索
```

### 关键概念

| 概念 | 描述 |
|------|------|
| **工具接口** | 所有工具的标准契约 |
| **工具属性** | 声明式工具定义 |
| **模式生成** | 自动生成 JSON 模式 |
| **工具注册表** | 集中管理工具 |
| **工具执行器** | 带超时/重试的执行 |

### 下一步：Week 6

Week 6 将涵盖 RAG 集成：

- 向量存储和嵌入
- 文档分块
- 检索和上下文注入
