using Dawning.Agents.Abstractions.Agent;

namespace Dawning.Agents.Api.Endpoints;

public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/agent").WithTags("Agent");

        group
            .MapPost(
                "/run",
                async (AgentRunRequest request, IAgent agent, CancellationToken ct) =>
                {
                    var response = await agent.RunAsync(request.Input, ct);
                    return Results.Ok(response);
                }
            )
            .WithName("AgentRun")
            .WithDescription("Execute the agent with the given input");

        group
            .MapGet(
                "/health",
                () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow })
            )
            .WithName("AgentHealth")
            .WithDescription("Health check endpoint");
    }
}

public record AgentRunRequest(string Input);
