using System.Text.Json;
using Dawning.Agents.Abstractions.LLM;

namespace Dawning.Agents.Api.Endpoints;

public static class ChatEndpoints
{
    public static void MapChatEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/chat").WithTags("Chat");

        group
            .MapPost(
                "/",
                async (ChatRequest request, ILLMProvider provider, CancellationToken ct) =>
                {
                    var messages = new List<ChatMessage>
                    {
                        ChatMessage.System("You are a helpful assistant."),
                        ChatMessage.User(request.Message),
                    };

                    var response = await provider.ChatAsync(messages, cancellationToken: ct);
                    return Results.Ok(response);
                }
            )
            .WithName("Chat")
            .WithDescription("Send a chat message and get a response");

        group
            .MapPost(
                "/stream",
                async (
                    ChatRequest request,
                    ILLMProvider provider,
                    HttpContext context,
                    CancellationToken ct
                ) =>
                {
                    context.Response.ContentType = "text/event-stream";
                    context.Response.Headers.CacheControl = "no-cache";
                    context.Response.Headers.Connection = "keep-alive";

                    var messages = new List<ChatMessage>
                    {
                        ChatMessage.System("You are a helpful assistant."),
                        ChatMessage.User(request.Message),
                    };

                    await foreach (
                        var chunk in provider.ChatStreamAsync(messages, cancellationToken: ct)
                    )
                    {
                        var data = JsonSerializer.Serialize(new { content = chunk });
                        await context.Response.WriteAsync($"data: {data}\n\n", ct);
                        await context.Response.Body.FlushAsync(ct);
                    }

                    await context.Response.WriteAsync("data: [DONE]\n\n", ct);
                }
            )
            .WithName("ChatStream")
            .WithDescription("Send a chat message and get a streaming SSE response");
    }
}

public record ChatRequest(string Message);
