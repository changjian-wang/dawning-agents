namespace Dawning.Agents.Tests.Evaluation;

using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Evaluation;
using Dawning.Agents.Core.Evaluation;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

public class DefaultAgentEvaluatorCancellationTests
{
    [Fact]
    public async Task EvaluateAsync_Should_Propagate_External_Cancellation()
    {
        var evaluator = new DefaultAgentEvaluator(
            new BlockingAgent(),
            Options.Create(new EvaluationOptions { TestTimeoutSeconds = 30 })
        );

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var testCase = new EvaluationTestCase { Id = "cancel-001", Input = "cancel" };

        var act = async () =>
            await evaluator.EvaluateAsync(testCase, cts.Token).ConfigureAwait(false);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task EvaluateAsync_Should_Return_Timeout_When_Internal_Timeout_Triggers()
    {
        var evaluator = new DefaultAgentEvaluator(
            new BlockingAgent(),
            Options.Create(new EvaluationOptions { TestTimeoutSeconds = 1 })
        );

        var testCase = new EvaluationTestCase { Id = "timeout-001", Input = "timeout" };

        var result = await evaluator.EvaluateAsync(testCase);

        result.Passed.Should().BeFalse();
        result.FailureReason.Should().Be("Evaluation timed out");
    }

    private sealed class BlockingAgent : IAgent
    {
        public string Name => "blocking-agent";

        public string Instructions => "test";

        public async Task<AgentResponse> RunAsync(
            string input,
            CancellationToken cancellationToken = default
        )
        {
            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
            return AgentResponse.Successful("ok", [], TimeSpan.Zero);
        }

        public Task<AgentResponse> RunAsync(
            AgentContext context,
            CancellationToken cancellationToken = default
        )
        {
            return RunAsync(context.UserInput, cancellationToken);
        }
    }
}
