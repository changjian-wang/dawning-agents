using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;

namespace Dawning.Agents.Demo.Helpers;

/// <summary>
/// 简单的 LLM Agent - 用于演示
/// </summary>
/// <remarks>
/// 与 LLMAgentWithStats 不同，这个类不自己追踪 Token，
/// 而是依赖于传入的 TokenTrackingLLMProvider 进行追踪。
/// </remarks>
public class SimpleLLMAgent : IAgent
{
    private readonly ILLMProvider _provider;
    private readonly string _systemPrompt;

    public SimpleLLMAgent(ILLMProvider provider, string name, string systemPrompt)
    {
        _provider = provider;
        Name = name;
        _systemPrompt = systemPrompt;
    }

    public string Name { get; }
    public string Instructions => _systemPrompt;

    public async Task<AgentResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var messages = new List<ChatMessage>
            {
                new("system", _systemPrompt),
                new("user", input),
            };

            var result = await _provider.ChatAsync(messages, cancellationToken: cancellationToken);
            stopwatch.Stop();

            return AgentResponse.Successful(result.Content ?? "", [], stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return AgentResponse.Failed(ex.Message, [], stopwatch.Elapsed);
        }
    }

    public Task<AgentResponse> RunAsync(
        AgentContext context,
        CancellationToken cancellationToken = default
    )
    {
        return RunAsync(context.UserInput, cancellationToken);
    }
}
