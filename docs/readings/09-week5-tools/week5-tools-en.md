# Week 5: Tool Development & Integration

> Phase 3: Tool System & RAG Integration
> Week 5 Learning Material: Building and Managing Agent Tools

---

## Day 1-2: Tool System Architecture

### 1. What Are Tools?

Tools are the **hands and eyes** of an AI agent, allowing it to:
- **Interact with external systems** (APIs, databases, services)
- **Perform actions** (send emails, create files, execute code)
- **Retrieve information** (search, lookup, query)

```
┌─────────────────────────────────────────────────────────────────┐
│                      Tool System Architecture                    │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│    ┌─────────────┐                                               │
│    │    Agent    │                                               │
│    └──────┬──────┘                                               │
│           │                                                      │
│           ▼                                                      │
│    ┌─────────────┐     ┌─────────────┐     ┌─────────────┐      │
│    │   Tool      │────►│   Tool      │────►│   External  │      │
│    │  Registry   │     │  Executor   │     │   Service   │      │
│    └─────────────┘     └─────────────┘     └─────────────┘      │
│           │                   │                   │              │
│           │                   │                   │              │
│    ┌──────┴──────┐     ┌──────┴──────┐     ┌──────┴──────┐      │
│    │ Tool Schemas│     │Result Parser│     │  Response   │      │
│    └─────────────┘     └─────────────┘     └─────────────┘      │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. Core Tool Interface

```csharp
namespace DawningAgents.Core.Tools;

/// <summary>
/// Base interface for all tools
/// </summary>
public interface ITool
{
    /// <summary>
    /// Unique name of the tool
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Human-readable description of what the tool does
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// JSON schema describing the tool's parameters
    /// </summary>
    string ParametersSchema { get; }
    
    /// <summary>
    /// Execute the tool with the given input
    /// </summary>
    Task<ToolResult> ExecuteAsync(
        string input, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validate input before execution
    /// </summary>
    bool ValidateInput(string input, out string? error);
}

/// <summary>
/// Result of tool execution
/// </summary>
public record ToolResult
{
    /// <summary>
    /// The output content
    /// </summary>
    public required string Content { get; init; }
    
    /// <summary>
    /// Whether the execution was successful
    /// </summary>
    public bool IsSuccess { get; init; } = true;
    
    /// <summary>
    /// Error message if execution failed
    /// </summary>
    public string? Error { get; init; }
    
    /// <summary>
    /// Execution metadata
    /// </summary>
    public IDictionary<string, object>? Metadata { get; init; }
    
    /// <summary>
    /// Execution duration
    /// </summary>
    public TimeSpan? Duration { get; init; }
    
    /// <summary>
    /// Create a successful result
    /// </summary>
    public static ToolResult Success(string content, IDictionary<string, object>? metadata = null)
        => new() { Content = content, IsSuccess = true, Metadata = metadata };
    
    /// <summary>
    /// Create a failed result
    /// </summary>
    public static ToolResult Failure(string error)
        => new() { Content = "", IsSuccess = false, Error = error };
}
```

### 3. Tool Base Class

```csharp
namespace DawningAgents.Core.Tools;

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// Base class for tools with common functionality
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
            // Validate input
            if (!ValidateInput(input, out var error))
            {
                Logger.LogWarning("Tool {Name} input validation failed: {Error}", Name, error);
                return ToolResult.Failure($"Invalid input: {error}");
            }

            Logger.LogDebug("Executing tool {Name} with input: {Input}", Name, input);
            
            // Execute the tool
            var result = await ExecuteCoreAsync(input, cancellationToken);
            
            stopwatch.Stop();
            result = result with { Duration = stopwatch.Elapsed };
            
            Logger.LogDebug(
                "Tool {Name} completed in {Duration}ms, Success: {IsSuccess}",
                Name, stopwatch.ElapsedMilliseconds, result.IsSuccess);
            
            return result;
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("Tool {Name} execution cancelled", Name);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Tool {Name} execution failed", Name);
            return ToolResult.Failure(ex.Message) with { Duration = stopwatch.Elapsed };
        }
    }

    /// <summary>
    /// Core execution logic - implemented by derived classes
    /// </summary>
    protected abstract Task<ToolResult> ExecuteCoreAsync(
        string input,
        CancellationToken cancellationToken);

    public virtual bool ValidateInput(string input, out string? error)
    {
        error = null;
        
        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Input cannot be empty";
            return false;
        }
        
        return true;
    }

    /// <summary>
    /// Parse JSON input into a typed object
    /// </summary>
    protected T? ParseInput<T>(string input) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(input, _jsonOptions);
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Failed to parse input as {Type}", typeof(T).Name);
            return null;
        }
    }

    /// <summary>
    /// Serialize result to JSON
    /// </summary>
    protected string ToJson<T>(T value)
    {
        return JsonSerializer.Serialize(value, _jsonOptions);
    }
}
```

---

## Day 3-4: Tool Attributes & Registry

### 1. Tool Attributes for Declarative Definition

```csharp
namespace DawningAgents.Core.Tools;

/// <summary>
/// Marks a class as a tool
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
/// Marks a method as the tool's execution method
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ToolExecuteAttribute : Attribute
{
}

/// <summary>
/// Describes a tool parameter
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
/// Example usage of tool attributes
/// </summary>
[Tool("weather", "Get current weather information for a city")]
public class WeatherToolExample
{
    [ToolExecute]
    public async Task<string> GetWeatherAsync(
        [ToolParameter("The city name to get weather for")] string city,
        [ToolParameter("Temperature unit", Required = false, DefaultValue = "celsius")] string unit = "celsius")
    {
        // Implementation
        await Task.Delay(100);
        return $"Weather in {city}: 22°{(unit == "celsius" ? "C" : "F")}, Sunny";
    }
}
```

### 2. Tool Schema Generator

```csharp
namespace DawningAgents.Core.Tools;

using System.Reflection;
using System.Text.Json;

/// <summary>
/// Generates JSON schemas for tools
/// </summary>
public class ToolSchemaGenerator
{
    /// <summary>
    /// Generate JSON schema from a tool class
    /// </summary>
    public ToolSchema GenerateSchema(Type toolType)
    {
        var toolAttr = toolType.GetCustomAttribute<ToolAttribute>()
            ?? throw new InvalidOperationException($"Type {toolType.Name} is not marked with [Tool]");

        var executeMethod = toolType.GetMethods()
            .FirstOrDefault(m => m.GetCustomAttribute<ToolExecuteAttribute>() != null)
            ?? throw new InvalidOperationException($"No [ToolExecute] method found in {toolType.Name}");

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

### 3. Tool Registry

```csharp
namespace DawningAgents.Core.Tools;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Registry for managing available tools
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Register a tool
    /// </summary>
    void Register(ITool tool);
    
    /// <summary>
    /// Register multiple tools
    /// </summary>
    void RegisterRange(IEnumerable<ITool> tools);
    
    /// <summary>
    /// Get a tool by name
    /// </summary>
    ITool? GetTool(string name);
    
    /// <summary>
    /// Get all registered tools
    /// </summary>
    IReadOnlyList<ITool> GetAllTools();
    
    /// <summary>
    /// Check if a tool exists
    /// </summary>
    bool HasTool(string name);
    
    /// <summary>
    /// Remove a tool
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
            _logger.LogInformation("Registered tool: {Name}", tool.Name);
        }
        else
        {
            _logger.LogWarning("Tool {Name} already registered, skipping", tool.Name);
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
            _logger.LogInformation("Unregistered tool: {Name}", name);
            return true;
        }
        return false;
    }
}
```

---

## Day 5-7: Built-in Tools Implementation

### 1. Calculator Tool

```csharp
namespace DawningAgents.Core.Tools.BuiltIn;

using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tool for performing mathematical calculations
/// </summary>
public class CalculatorTool : ToolBase
{
    public override string Name => "calculator";
    public override string Description => "Perform mathematical calculations. Supports basic arithmetic (+, -, *, /), parentheses, and common functions.";
    
    public override string ParametersSchema => """
        {
            "type": "object",
            "properties": {
                "expression": {
                    "type": "string",
                    "description": "Mathematical expression to evaluate (e.g., '2 + 3 * 4', '(10 + 5) / 3')"
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
            // Try treating input as raw expression
            request = new CalculatorRequest { Expression = input.Trim('"') };
        }

        try
        {
            var result = EvaluateExpression(request.Expression);
            
            return Task.FromResult(ToolResult.Success(
                $"Result: {result}",
                new Dictionary<string, object>
                {
                    ["expression"] = request.Expression,
                    ["result"] = result
                }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Failure($"Calculation error: {ex.Message}"));
        }
    }

    private double EvaluateExpression(string expression)
    {
        // Sanitize expression
        expression = expression.Replace(" ", "");
        
        // Use DataTable.Compute for basic expressions
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

### 2. DateTime Tool

```csharp
namespace DawningAgents.Core.Tools.BuiltIn;

using System.Globalization;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tool for date and time operations
/// </summary>
public class DateTimeTool : ToolBase
{
    public override string Name => "datetime";
    public override string Description => "Get current date/time, convert between timezones, calculate date differences, and format dates.";
    
    public override string ParametersSchema => """
        {
            "type": "object",
            "properties": {
                "operation": {
                    "type": "string",
                    "enum": ["now", "format", "add", "diff", "timezone"],
                    "description": "The operation to perform"
                },
                "date": {
                    "type": "string",
                    "description": "Date string for operations (ISO 8601 format)"
                },
                "format": {
                    "type": "string",
                    "description": "Output format (e.g., 'yyyy-MM-dd', 'MMMM dd, yyyy')"
                },
                "timezone": {
                    "type": "string",
                    "description": "Target timezone (e.g., 'UTC', 'America/New_York')"
                },
                "addValue": {
                    "type": "integer",
                    "description": "Value to add for 'add' operation"
                },
                "addUnit": {
                    "type": "string",
                    "enum": ["days", "hours", "minutes", "months", "years"],
                    "description": "Unit for 'add' operation"
                },
                "endDate": {
                    "type": "string",
                    "description": "End date for 'diff' operation"
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
            return Task.FromResult(ToolResult.Failure("Invalid input format"));
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
                _ => throw new ArgumentException($"Unknown operation: {request.Operation}")
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
        return $"Current UTC time: {now.ToString(format, CultureInfo.InvariantCulture)}";
    }

    private string HandleFormat(DateTimeRequest request)
    {
        if (string.IsNullOrEmpty(request.Date))
            throw new ArgumentException("Date is required for format operation");

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
            _ => throw new ArgumentException($"Unknown unit: {request.AddUnit}")
        };

        return result.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private string HandleDiff(DateTimeRequest request)
    {
        if (string.IsNullOrEmpty(request.Date) || string.IsNullOrEmpty(request.EndDate))
            throw new ArgumentException("Both date and endDate are required for diff operation");

        var start = DateTime.Parse(request.Date, CultureInfo.InvariantCulture);
        var end = DateTime.Parse(request.EndDate, CultureInfo.InvariantCulture);
        var diff = end - start;

        return $"Difference: {diff.Days} days, {diff.Hours} hours, {diff.Minutes} minutes";
    }

    private string HandleTimezone(DateTimeRequest request)
    {
        var date = string.IsNullOrEmpty(request.Date)
            ? DateTime.UtcNow
            : DateTime.Parse(request.Date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

        var targetTz = TimeZoneInfo.FindSystemTimeZoneById(request.Timezone ?? "UTC");
        var converted = TimeZoneInfo.ConvertTimeFromUtc(date, targetTz);

        return $"{converted:yyyy-MM-dd HH:mm:ss} ({targetTz.DisplayName})";
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

### 3. HTTP Request Tool

```csharp
namespace DawningAgents.Core.Tools.BuiltIn;

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tool for making HTTP requests
/// </summary>
public class HttpTool : ToolBase
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public override string Name => "http";
    public override string Description => "Make HTTP requests to APIs and web services. Supports GET, POST, PUT, DELETE methods.";
    
    public override string ParametersSchema => """
        {
            "type": "object",
            "properties": {
                "url": {
                    "type": "string",
                    "description": "The URL to send the request to"
                },
                "method": {
                    "type": "string",
                    "enum": ["GET", "POST", "PUT", "DELETE"],
                    "description": "HTTP method (default: GET)"
                },
                "headers": {
                    "type": "object",
                    "description": "Request headers as key-value pairs"
                },
                "body": {
                    "type": "string",
                    "description": "Request body (for POST/PUT)"
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
            return ToolResult.Failure("Invalid request: URL is required");
        }

        try
        {
            var httpRequest = new HttpRequestMessage(
                GetHttpMethod(request.Method ?? "GET"),
                request.Url);

            // Add headers
            if (request.Headers != null)
            {
                foreach (var (key, value) in request.Headers)
                {
                    httpRequest.Headers.TryAddWithoutValidation(key, value);
                }
            }

            // Add body
            if (!string.IsNullOrEmpty(request.Body))
            {
                httpRequest.Content = new StringContent(
                    request.Body,
                    Encoding.UTF8,
                    "application/json");
            }

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            // Truncate if too long
            if (content.Length > 5000)
            {
                content = content[..5000] + "\n... (truncated)";
            }

            return ToolResult.Success(
                $"Status: {(int)response.StatusCode} {response.StatusCode}\n\n{content}",
                new Dictionary<string, object>
                {
                    ["statusCode"] = (int)response.StatusCode,
                    ["url"] = request.Url
                });
        }
        catch (HttpRequestException ex)
        {
            return ToolResult.Failure($"HTTP error: {ex.Message}");
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

### 4. Search Tool (Web Search)

```csharp
namespace DawningAgents.Core.Tools.BuiltIn;

using System.Web;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tool for searching the web
/// </summary>
public class SearchTool : ToolBase
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string? _searchEngineId;

    public override string Name => "search";
    public override string Description => "Search the web for information. Returns relevant search results.";
    
    public override string ParametersSchema => """
        {
            "type": "object",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "The search query"
                },
                "numResults": {
                    "type": "integer",
                    "description": "Number of results to return (default: 5, max: 10)"
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
            // Try treating input as raw query
            request = new SearchRequest { Query = input.Trim('"') };
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return ToolResult.Failure("Search query is required");
        }

        var numResults = Math.Min(request.NumResults ?? 5, 10);

        // If no API key, return simulated results (for development)
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
            Logger.LogError(ex, "Search failed for query: {Query}", request.Query);
            return ToolResult.Failure($"Search failed: {ex.Message}");
        }
    }

    private async Task<string> SearchWithApiAsync(
        string query,
        int numResults,
        CancellationToken cancellationToken)
    {
        // Example using Google Custom Search API
        var url = $"https://www.googleapis.com/customsearch/v1" +
                  $"?key={_apiKey}" +
                  $"&cx={_searchEngineId}" +
                  $"&q={HttpUtility.UrlEncode(query)}" +
                  $"&num={numResults}";

        var response = await _httpClient.GetStringAsync(url, cancellationToken);
        
        // Parse and format results
        // (simplified - actual implementation would parse JSON)
        return response;
    }

    private ToolResult GetSimulatedResults(string query, int numResults)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Search results for: {query}");
        sb.AppendLine();

        for (int i = 1; i <= numResults; i++)
        {
            sb.AppendLine($"{i}. [Simulated Result {i}]");
            sb.AppendLine($"   URL: https://example.com/result-{i}");
            sb.AppendLine($"   This is a simulated search result for query: {query}");
            sb.AppendLine();
        }

        sb.AppendLine("Note: Using simulated results. Configure API key for real search.");

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

## Tool Executor

```csharp
namespace DawningAgents.Core.Tools;

using Microsoft.Extensions.Logging;

/// <summary>
/// Executes tools and handles errors
/// </summary>
public interface IToolExecutor
{
    /// <summary>
    /// Execute a tool by name
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
            _logger.LogWarning("Tool not found: {ToolName}", toolName);
            return ToolResult.Failure(
                $"Tool '{toolName}' not found. Available tools: {string.Join(", ", _registry.GetAllTools().Select(t => t.Name))}");
        }

        // Execute with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_options.Timeout);

        try
        {
            return await tool.ExecuteAsync(input, cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Tool {ToolName} timed out", toolName);
            return ToolResult.Failure($"Tool execution timed out after {_options.Timeout.TotalSeconds}s");
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

## Summary

### Week 5 Deliverables

```
src/DawningAgents.Core/
├── Tools/
│   ├── ITool.cs               # Tool interface
│   ├── ToolBase.cs            # Base class
│   ├── ToolResult.cs          # Execution result
│   ├── ToolAttribute.cs       # Declarative attributes
│   ├── ToolSchemaGenerator.cs # Schema generation
│   ├── IToolRegistry.cs       # Registry interface
│   ├── ToolRegistry.cs        # Registry implementation
│   ├── IToolExecutor.cs       # Executor interface
│   ├── ToolExecutor.cs        # Executor implementation
│   └── BuiltIn/
│       ├── CalculatorTool.cs  # Math calculations
│       ├── DateTimeTool.cs    # Date/time operations
│       ├── HttpTool.cs        # HTTP requests
│       └── SearchTool.cs      # Web search
```

### Key Concepts

| Concept | Description |
|---------|-------------|
| **Tool Interface** | Standard contract for all tools |
| **Tool Attributes** | Declarative tool definition |
| **Schema Generation** | Auto-generate JSON schemas |
| **Tool Registry** | Central management of tools |
| **Tool Executor** | Execute with timeout/retries |

### Next: Week 6

Week 6 will cover RAG integration:
- Vector stores and embeddings
- Document chunking
- Retrieval and context injection
