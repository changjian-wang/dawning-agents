namespace Dawning.Agents.Tests.Orchestration;

using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Orchestration;
using Dawning.Agents.Core.Orchestration;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

/// <summary>
/// ParallelOrchestrator 单元测试
/// </summary>
public class ParallelOrchestratorTests
{
    private readonly Mock<IOptions<OrchestratorOptions>> _mockOptions;
    private readonly OrchestratorOptions _options;

    public ParallelOrchestratorTests()
    {
        _options = new OrchestratorOptions
        {
            TimeoutSeconds = 60,
            AgentTimeoutSeconds = 30,
            MaxConcurrency = 5,
            AggregationStrategy = ResultAggregationStrategy.LastResult,
        };
        _mockOptions = new Mock<IOptions<OrchestratorOptions>>();
        _mockOptions.Setup(x => x.Value).Returns(_options);
    }

    [Fact]
    public void Constructor_SetsNameAndDescription()
    {
        // Act
        var orchestrator = new ParallelOrchestrator("test-parallel", _mockOptions.Object);

        // Assert
        orchestrator.Name.Should().Be("test-parallel");
        orchestrator.Description.Should().Contain("并行执行");
    }

    [Fact]
    public async Task RunAsync_WithNoAgents_ReturnsFailure()
    {
        // Arrange
        var orchestrator = new ParallelOrchestrator("test", _mockOptions.Object);

        // Act
        var result = await orchestrator.RunAsync("input");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("没有 Agent");
    }

    [Fact]
    public async Task RunAsync_SingleAgent_ReturnsAgentResult()
    {
        // Arrange
        var orchestrator = new ParallelOrchestrator("test", _mockOptions.Object);
        var mockAgent = CreateMockAgent("Agent1", "Result1");
        orchestrator.AddAgent(mockAgent.Object);

        // Act
        var result = await orchestrator.RunAsync("input");

        // Assert
        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Result1");
        result.AgentResults.Should().HaveCount(1);
    }

    [Fact]
    public async Task RunAsync_MultipleAgents_ExecutesInParallel()
    {
        // Arrange
        var orchestrator = new ParallelOrchestrator("test", _mockOptions.Object);
        var startTimes = new List<DateTimeOffset>();

        var agents = Enumerable
            .Range(1, 3)
            .Select(i =>
            {
                var mock = new Mock<IAgent>();
                mock.Setup(a => a.Name).Returns($"Agent{i}");
                mock.Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns(
                        async (string input, CancellationToken ct) =>
                        {
                            lock (startTimes)
                            {
                                startTimes.Add(DateTimeOffset.UtcNow);
                            }
                            await Task.Delay(50, ct);
                            return AgentResponse.Successful(
                                $"Result{i}",
                                [],
                                TimeSpan.FromMilliseconds(50)
                            );
                        }
                    );
                return mock.Object;
            })
            .ToList();

        orchestrator.AddAgents(agents);

        // Act
        var result = await orchestrator.RunAsync("input");

        // Assert
        result.Success.Should().BeTrue();
        result.AgentResults.Should().HaveCount(3);

        // 所有 Agent 应该几乎同时开始（50ms 内）
        var timeDifference = (startTimes.Max() - startTimes.Min()).TotalMilliseconds;
        timeDifference.Should().BeLessThan(100);
    }

    [Fact]
    public async Task RunAsync_AllAgentsReceiveSameInput()
    {
        // Arrange
        var orchestrator = new ParallelOrchestrator("test", _mockOptions.Object);
        var capturedInputs = new List<string>();

        var agents = Enumerable
            .Range(1, 3)
            .Select(i =>
            {
                var mock = new Mock<IAgent>();
                mock.Setup(a => a.Name).Returns($"Agent{i}");
                mock.Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(
                        (string input, CancellationToken _) =>
                        {
                            lock (capturedInputs)
                            {
                                capturedInputs.Add(input);
                            }
                            return AgentResponse.Successful($"Result{i}", [], TimeSpan.Zero);
                        }
                    );
                return mock.Object;
            })
            .ToList();

        orchestrator.AddAgents(agents);

        // Act
        await orchestrator.RunAsync("shared-input");

        // Assert
        capturedInputs.Should().HaveCount(3);
        capturedInputs.Should().AllBe("shared-input");
    }

    [Fact]
    public async Task RunAsync_WithLastResultStrategy_ReturnsLastResult()
    {
        // Arrange
        _options.AggregationStrategy = ResultAggregationStrategy.LastResult;
        var orchestrator = new ParallelOrchestrator("test", _mockOptions.Object);

        orchestrator.AddAgent(CreateMockAgent("Agent1", "First").Object);
        orchestrator.AddAgent(CreateMockAgent("Agent2", "Second").Object);
        orchestrator.AddAgent(CreateMockAgent("Agent3", "Third").Object);

        // Act
        var result = await orchestrator.RunAsync("input");

        // Assert
        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Third");
    }

    [Fact]
    public async Task RunAsync_WithFirstSuccessStrategy_ReturnsFirstSuccess()
    {
        // Arrange
        _options.AggregationStrategy = ResultAggregationStrategy.FirstSuccess;
        var orchestrator = new ParallelOrchestrator("test", _mockOptions.Object);

        var failAgent = new Mock<IAgent>();
        failAgent.Setup(a => a.Name).Returns("FailAgent");
        failAgent
            .Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResponse.Failed("Error", [], TimeSpan.Zero));

        orchestrator.AddAgent(failAgent.Object);
        orchestrator.AddAgent(CreateMockAgent("SuccessAgent", "Success").Object);

        // Act
        var result = await orchestrator.RunAsync("input");

        // Assert
        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Success");
    }

    [Fact]
    public async Task RunAsync_WithMergeStrategy_MergesAllResults()
    {
        // Arrange
        _options.AggregationStrategy = ResultAggregationStrategy.Merge;
        var orchestrator = new ParallelOrchestrator("test", _mockOptions.Object);

        orchestrator.AddAgent(CreateMockAgent("Agent1", "Result1").Object);
        orchestrator.AddAgent(CreateMockAgent("Agent2", "Result2").Object);

        // Act
        var result = await orchestrator.RunAsync("input");

        // Assert
        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Contain("[Agent1]");
        result.FinalOutput.Should().Contain("Result1");
        result.FinalOutput.Should().Contain("[Agent2]");
        result.FinalOutput.Should().Contain("Result2");
    }

    [Fact]
    public async Task RunAsync_WithVoteStrategy_ReturnsPopularAnswer()
    {
        // Arrange
        _options.AggregationStrategy = ResultAggregationStrategy.Vote;
        var orchestrator = new ParallelOrchestrator("test", _mockOptions.Object);

        orchestrator.AddAgent(CreateMockAgent("Agent1", "Yes").Object);
        orchestrator.AddAgent(CreateMockAgent("Agent2", "Yes").Object);
        orchestrator.AddAgent(CreateMockAgent("Agent3", "No").Object);

        // Act
        var result = await orchestrator.RunAsync("input");

        // Assert
        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Yes");
    }

    [Fact]
    public async Task RunAsync_WithCustomAggregator_UsesCustomLogic()
    {
        // Arrange
        var orchestrator = new ParallelOrchestrator("test", _mockOptions.Object);
        orchestrator.WithAggregator(records =>
        {
            var count = records.Count(r => r.Response.Success);
            return $"成功: {count}/{records.Count}";
        });

        orchestrator.AddAgent(CreateMockAgent("Agent1", "A").Object);
        orchestrator.AddAgent(CreateMockAgent("Agent2", "B").Object);

        // Act
        var result = await orchestrator.RunAsync("input");

        // Assert
        result.FinalOutput.Should().Be("成功: 2/2");
    }

    [Fact]
    public async Task RunAsync_AllAgentsFail_ReturnsFailure()
    {
        // Arrange
        var orchestrator = new ParallelOrchestrator("test", _mockOptions.Object);

        var failAgent1 = new Mock<IAgent>();
        failAgent1.Setup(a => a.Name).Returns("FailAgent1");
        failAgent1
            .Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResponse.Failed("Error1", [], TimeSpan.Zero));

        var failAgent2 = new Mock<IAgent>();
        failAgent2.Setup(a => a.Name).Returns("FailAgent2");
        failAgent2
            .Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResponse.Failed("Error2", [], TimeSpan.Zero));

        orchestrator.AddAgent(failAgent1.Object);
        orchestrator.AddAgent(failAgent2.Object);

        // Act
        var result = await orchestrator.RunAsync("input");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("都执行失败");
    }

    [Fact]
    public async Task RunAsync_RespectsConcurrencyLimit()
    {
        // Arrange
        _options.MaxConcurrency = 2;
        var orchestrator = new ParallelOrchestrator("test", _mockOptions.Object);
        var concurrentCount = 0;
        var maxConcurrent = 0;
        var lockObj = new object();

        var agents = Enumerable
            .Range(1, 5)
            .Select(i =>
            {
                var mock = new Mock<IAgent>();
                mock.Setup(a => a.Name).Returns($"Agent{i}");
                mock.Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns(
                        async (string input, CancellationToken ct) =>
                        {
                            lock (lockObj)
                            {
                                concurrentCount++;
                                maxConcurrent = Math.Max(maxConcurrent, concurrentCount);
                            }

                            await Task.Delay(50, ct);

                            lock (lockObj)
                            {
                                concurrentCount--;
                            }

                            return AgentResponse.Successful(
                                $"Result{i}",
                                [],
                                TimeSpan.FromMilliseconds(50)
                            );
                        }
                    );
                return mock.Object;
            })
            .ToList();

        orchestrator.AddAgents(agents);

        // Act
        await orchestrator.RunAsync("input");

        // Assert
        maxConcurrent.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task RunAsync_RecordsDuration()
    {
        // Arrange
        var orchestrator = new ParallelOrchestrator("test", _mockOptions.Object);
        orchestrator.AddAgent(CreateMockAgent("Agent1", "Result").Object);

        // Act
        var result = await orchestrator.RunAsync("input");

        // Assert
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    private static Mock<IAgent> CreateMockAgent(string name, string result)
    {
        var mock = new Mock<IAgent>();
        mock.Setup(a => a.Name).Returns(name);
        mock.Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResponse.Successful(result, [], TimeSpan.FromMilliseconds(10)));
        return mock;
    }
}
