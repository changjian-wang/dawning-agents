# Tools & Skills

Tools are the "hands and eyes" of agents, allowing them to interact with external systems.

## Built-in Tools (64+ Methods)

| Category | Tool Class | Methods | Description |
|----------|------------|---------|-------------|
| DateTime | `DateTimeTool` | 4 | Current time, date formatting |
| Math | `MathTool` | 8 | Calculations, statistics |
| JSON | `JsonTool` | 4 | Parse, format, query JSON |
| Utility | `UtilityTool` | 5 | GUID, hash, encoding |
| FileSystem | `FileSystemTool` | 13 | Read, write, list files |
| HTTP | `HttpTool` | 6 | HTTP requests |
| Process | `ProcessTool` | 6 | Run commands |
| Git | `GitTool` | 18 | Git operations |
| CSharpier | `CSharpierTool` | 6 | Code formatting |

## Registering Tools

```csharp
// All built-in tools (including risky ones)
services.AddAllBuiltInTools();

// Only safe tools (recommended for production)
services.AddBuiltInTools();

// Specific categories
services.AddMathTools();
services.AddDateTimeTools();
services.AddFileSystemTools();
services.AddHttpTools();
services.AddGitTools();
services.AddCSharpierTools();

// From assembly
services.AddToolsFromAssembly(typeof(MyTool).Assembly);
```

## Creating Custom Tools

### Using Attributes

```csharp
public class WeatherTool
{
    [FunctionTool("Get current weather for a city")]
    public async Task<ToolResult> GetWeather(
        [Description("City name")] string city)
    {
        // Implementation
        var weather = await _weatherService.GetAsync(city);
        return ToolResult.Ok($"Weather in {city}: {weather}");
    }
}
```

### Tool Attributes

```csharp
[FunctionTool(
    Description = "Delete a file",
    RequiresConfirmation = true,  // Requires user approval
    RiskLevel = ToolRiskLevel.High,
    Category = "FileSystem"
)]
public ToolResult DeleteFile(string path)
{
    File.Delete(path);
    return ToolResult.Ok($"Deleted: {path}");
}
```

## Tool Result

```csharp
// Success
return ToolResult.Ok("Operation completed");
return ToolResult.Ok(jsonData);

// Failure
return ToolResult.Fail("Error message");
return ToolResult.Fail(exception);
```

## Tool Sets (Grouping)

```csharp
// Create a tool set
var mathTools = ToolSet.FromType<MathTool>("math", "Math operations");
services.AddToolSet(mathTools);

// Get tools by category
var registry = serviceProvider.GetRequiredService<IToolRegistry>();
var tools = registry.GetToolsByCategory("math");
```

## Virtual Tools (Lazy Loading)

For large tool collections, use virtual tools:

```csharp
// LLM sees summary first, expands when needed
var gitVirtual = VirtualTool.FromType<GitTool>("git", "Git operations");
services.AddVirtualTool(gitVirtual);
```

## Tool Approval (Safety)

```csharp
// Configure approval strategy
services.AddToolApprovalHandler(ApprovalStrategy.RiskBased);

// Strategies:
// - AlwaysApprove: Development/testing
// - AlwaysDeny: Security-sensitive
// - RiskBased: Based on tool risk level (recommended)
// - Interactive: Prompt user for confirmation
```

## Tool Selection

Automatically select relevant tools based on query:

```csharp
services.AddToolSelector();

var selector = serviceProvider.GetRequiredService<IToolSelector>();
var tools = await selector.SelectToolsAsync(
    "Calculate file size",
    allTools,
    maxTools: 10
);
```
