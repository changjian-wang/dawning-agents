namespace Dawning.Agents.Tests.Evaluation;

using Dawning.Agents.Abstractions.Evaluation;
using Dawning.Agents.Core.Evaluation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

public class EvaluationServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAgentEvaluation_CalledTwice_ShouldNotDuplicateEvaluators()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act — call twice
        services.AddAgentEvaluation();
        services.AddAgentEvaluation();

        var sp = services.BuildServiceProvider();
        var evaluators = sp.GetServices<IMetricEvaluator>().ToList();

        // Assert — should have exactly 4 built-in evaluators, not 8
        evaluators.Should().HaveCount(4);
        evaluators.Should().ContainSingle(e => e is KeywordMatchEvaluator);
        evaluators.Should().ContainSingle(e => e is ToolCallAccuracyEvaluator);
        evaluators.Should().ContainSingle(e => e is LatencyEvaluator);
        evaluators.Should().ContainSingle(e => e is ExactMatchEvaluator);
    }
}
