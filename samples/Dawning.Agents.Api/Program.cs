using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core;
using Dawning.Agents.Core.LLM;
using Dawning.Agents.Core.Memory;
using Dawning.Agents.Core.Resilience;
using Dawning.Agents.Core.Tools.BuiltIn;
using Dawning.Agents.Core.Validation;

var builder = WebApplication.CreateBuilder(args);

// ===== LLM Provider =====
builder.Services.AddLLMProvider(builder.Configuration);

// ===== Resilience (Polly) =====
builder.Services.AddResilience(builder.Configuration);

// ===== Validation =====
builder.Services.AddValidation();

// ===== Memory =====
builder.Services.AddWindowMemory(windowSize: 10);

// ===== Tools =====
builder.Services.AddBuiltInTools();

// ===== Agent =====
builder.Services.AddReActAgent(builder.Configuration);

// ===== Health Checks =====
builder.Services.AddHealthChecks();

// ===== CORS =====
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

// ===== Health Check Endpoint =====
app.MapHealthChecks("/health");

// ===== Chat Endpoint =====
app.MapPost("/api/chat", async (ChatRequest request, IAgent agent) =>
{
    try
    {
        var response = await agent.RunAsync(request.Message);
        return Results.Ok(new ChatResponse
        {
            Success = response.Success,
            Message = response.FinalAnswer ?? response.Error,
            Steps = response.Steps.Count,
            DurationMs = (int)response.Duration.TotalMilliseconds
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new ChatResponse
        {
            Success = false,
            Message = ex.Message,
            Error = ex.GetType().Name
        });
    }
});

// ===== Simple Chat Endpoint (no agent, direct LLM) =====
app.MapPost("/api/llm/chat", async (ChatRequest request, ILLMProvider llm) =>
{
    try
    {
        var messages = new List<ChatMessage>
        {
            new("user", request.Message)
        };
        
        var response = await llm.ChatAsync(messages);
        
        return Results.Ok(new ChatResponse
        {
            Success = true,
            Message = response.Content
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new ChatResponse
        {
            Success = false,
            Message = ex.Message,
            Error = ex.GetType().Name
        });
    }
});

// ===== Stream Chat Endpoint =====
app.MapPost("/api/llm/stream", async (ChatRequest request, ILLMProvider llm, HttpResponse response) =>
{
    response.ContentType = "text/event-stream";
    
    var messages = new List<ChatMessage>
    {
        new("user", request.Message)
    };
    
    await foreach (var chunk in llm.ChatStreamAsync(messages))
    {
        await response.WriteAsync($"data: {chunk}\n\n");
        await response.Body.FlushAsync();
    }
    
    await response.WriteAsync("data: [DONE]\n\n");
});

// ===== Info Endpoint =====
app.MapGet("/api/info", (IAgent agent, IToolRegistry toolRegistry) =>
{
    var tools = toolRegistry.GetAllTools();
    return Results.Ok(new
    {
        Name = agent.Name,
        Instructions = agent.Instructions,
        ToolCount = tools.Count,
        Tools = tools.Select(t => new { t.Name, t.Description })
    });
});

app.Run();

// ===== Request/Response Models =====
public record ChatRequest(string Message);

public record ChatResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public int Steps { get; init; }
    public int DurationMs { get; init; }
    public string? Error { get; init; }
}
