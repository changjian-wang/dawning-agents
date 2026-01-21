namespace Dawning.Agents.Tests.Orchestration;

using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Orchestration;
using Dawning.Agents.Core.Orchestration;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

/// <summary>
/// SequentialOrchestrator 单元测试
/// </summary>
public class SequentialOrchestratorTests
{
    private readonly Mock<IOptions<OrchestratorOptions>> _mockOptions;
    private readonly OrchestratorOptions _options;

    public SequentialOrchestratorTests()
    {
        _options = new OrchestratorOptions
        {
            TimeoutSeconds = 60,
            AgentTimeoutSeconds = 30,
            ContinueOnError = false,
        };
        _mockOptions = new Mock<IOptions<OrchestratorOptions>>();
        _mockOptions.Setup(x => x.Value).Returns(_options);
    }

    [Fact]
    public void Constructor_SetsNameAndDescription()
    {
        // Act
        var orchestrator = new SequentialOrchestrator("test-orchestrator", _mockOptions.Object);

        // Assert
        orchestrator.Name.Should().Be("test-orchestrator");
        orchestrator.Description.Should().Contain("顺序执行");
    }

    [Fact]
    public void AddAgent_AddsAgentToList()
    {
        // Arrange
        var orchestrator = new SequentialOrchestrator("test", _mockOptions.Object);
        var mockAgent = CreateMockAgent("Agent1", "Hello");

        // Act
        orchestrator.AddAgent(mockAgent.Object);

        // Assert
        orchestrator.Agents.Should().HaveCount(1);
        orchestrator.Agents[0].Name.Should().Be("Agent1");
    }

    [Fact]
    public void AddAgents_AddsMultipleAgents()
    {
        // Arrange
        var orchestrator = new SequentialOrchestrator("test", _mockOptions.Object);
        var agents = new[]
        {
            CreateMockAgent("Agent1", "Result1").Object,
            CreateMockAgent("Agent2", "Result2").Object,
        };

        // Act
        orchestrator.AddAgents(agents);

        // Assert
        orchestrator.Agents.Should().HaveCount(2);
    }

    [Fact]
    public async Task RunAsync_WithNoAgents_ReturnsFailure()
    {
        // Arrange
        var orchestrator = new SequentialOrchestrator("test", _mockOptions.Object);

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
        var orchestrator = new SequentialOrchestrator("test", _mockOptions.Object);
        var mockAgent = CreateMockAgent("Agent1", "Hello World");
        orchestrator.AddAgent(mockAgent.Object);

        // Act
        var result = await orchestrator.RunAsync("input");

        // Assert
        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Hello World");
        result.AgentResults.Should().HaveCount(1);
        result.AgentResults[0].AgentName.Should().Be("Agent1");
    }

    [Fact]
    public async Task RunAsync_MultipleAgents_ExecutesInSequence()
    {
        // Arrange
        var orchestrator = new SequentialOrchestrator("test", _mockOptions.Object);
        var executionOrder = new List<string>();

        var agent1 = new Mock<IAgent>();
        agent1.Setup(a => a.Name).Returns("Agent1");
        agent1
            .Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (string input, CancellationToken _) =>
                {
                    executionOrder.Add("Agent1");
                    return AgentResponse.Successful("Result1", [], TimeSpan.FromMilliseconds(10));
                }
            );

        var agent2 = new Mock<IAgent>();
        agent2.Setup(a => a.Name).Returns("Agent2");
        agent2
            .Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (string input, CancellationToken _) =>
                {
                    executionOrder.Add("Agent2");
                    return AgentResponse.Successful("Result2", [], TimeSpan.FromMilliseconds(10));
                }
            );

        orchestrator.AddAgent(agent1.Object);
        orchestrator.AddAgent(agent2.Object);

        // Act
        var result = await orchestrator.RunAsync("input");

        // Assert
        result.Success.Should().BeTrue();
        executionOrder.Should().Equal("Agent1", "Agent2");
        result.AgentResults.Should().HaveCount(2);
        result.AgentResults[0].ExecutionOrder.Should().Be(0);
        result.AgentResults[1].ExecutionOrder.Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_PassesOutputAsNextInput()
    {
        // Arrange
        var orchestrator = new SequentialOrchestrator("test", _mockOptions.Object);
        var capturedInputs = new List<string>();

        var agent1 = new Mock<IAgent>();
        agent1.Setup(a => a.Name).Returns("Agent1");
        agent1
            .Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (string input, CancellationToken _) =>
                {
                    capturedInputs.Add(input);
                    return AgentResponse.Successful("Processed: " + input, [], TimeSpan.Zero);
                }
            );

        var agent2 = new Mock<IAgent>();
        agent2.Setup(a => a.Name).Returns("Agent2");
        agent2
            .Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (string input, CancellationToken _) =>
                {
                    capturedInputs.Add(input);
                    return AgentResponse.Successful("Final: " + input, [], TimeSpan.Zero);
                }
            );

        orchestrator.AddAgent(agent1.Object);
        orchestrator.AddAgent(agent2.Object);

        // Act
        var result = await orchestrator.RunAsync("Hello");

        // Assert
        capturedInputs.Should().Equal("Hello", "Processed: Hello");
        result.FinalOutput.Should().Be("Final: Processed: Hello");
    }

    [Fact]
    public async Task RunAsync_WithInputTransformer_UsesTransformedInput()
    {
        // Arrange
        var orchestrator = new SequentialOrchestrator("test", _mockOptions.Object);
        orchestrator.WithInputTransformer(record =>
            $"[{record.AgentName}] {record.Response.FinalAnswer}"
        );

        var capturedInputs = new List<string>();

        var agent1 = CreateMockAgentWithCapture("Agent1", "Result1", capturedInputs);
        var agent2 = CreateMockAgentWithCapture("Agent2", "Result2", capturedInputs);

        orchestrator.AddAgent(agent1.Object);
        orchestrator.AddAgent(agent2.Object);

        // Act
        var result = await orchestrator.RunAsync("Initial");

        // Assert
        capturedInputs[1].Should().Be("[Agent1] Result1");
    }

    [Fact]
    public async Task RunAsync_AgentFailure_StopsExecution()
    {
        // Arrange
        var orchestrator = new SequentialOrchestrator("test", _mockOptions.Object);

        var agent1 = new Mock<IAgent>();
        agent1.Setup(a => a.Name).Returns("Agent1");
        agent1
            .Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResponse.Failed("Error occurred", [], TimeSpan.Zero));

        var agent2 = CreateMockAgent("Agent2", "Should not run");

        orchestrator.AddAgent(agent1.Object);
        orchestrator.AddAgent(agent2.Object);

        // Act
        var result = await orchestrator.RunAsync("input");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Agent1");
        result.AgentResults.Should().HaveCount(1);
        agent2.Verify(
            a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task RunAsync_WithContinueOnError_ContinuesAfterFailure()
    {
        // Arrange
        _options.ContinueOnError = true;
        var orchestrator = new SequentialOrchestrator("test", _mockOptions.Object);

        var agent1 = new Mock<IAgent>();
        agent1.Setup(a => a.Name).Returns("Agent1");
        agent1
            .Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResponse.Failed("Error", [], TimeSpan.Zero));

        var agent2 = CreateMockAgent("Agent2", "Success");

        orchestrator.AddAgent(agent1.Object);
        orchestrator.AddAgent(agent2.Object);

        // Act
        var result = await orchestrator.RunAsync("input");

        // Assert
        result.Success.Should().BeTrue();
        result.AgentResults.Should().HaveCount(2);
    }

    [Fact]
    public async Task RunAsync_WithContext_UsesShouldStopFlag()
    {
        // Arrange
        var orchestrator = new SequentialOrchestrator("test", _mockOptions.Object);

        var agent1 = new Mock<IAgent>();
        agent1.Setup(a => a.Name).Returns("Agent1");
        agent1
            .Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResponse.Successful("Result1", [], TimeSpan.Zero));

        var agent2 = CreateMockAgent("Agent2", "Should not run");

        orchestrator.AddAgent(agent1.Object);
        orchestrator.AddAgent(agent2.Object);

        var context = new OrchestrationContext
        {
            UserInput = "input",
            CurrentInput = "input",
            ShouldStop = true,
            StopReason = "User requested stop",
        };

        // Act
        var result = await orchestrator.RunAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        result.AgentResults.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_RecordsDuration()
    {
        // Arrange
        var orchestrator = new SequentialOrchestrator("test", _mockOptions.Object);
        var mockAgent = CreateMockAgent("Agent1", "Result");
        orchestrator.AddAgent(mockAgent.Object);

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

    private static Mock<IAgent> CreateMockAgentWithCapture(
        string name,
        string result,
        List<string> capturedInputs
    )
    {
        var mock = new Mock<IAgent>();
        mock.Setup(a => a.Name).Returns(name);
        mock.Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (string input, CancellationToken _) =>
                {
                    capturedInputs.Add(input);
                    return AgentResponse.Successful(result, [], TimeSpan.FromMilliseconds(10));
                }
            );
        return mock;
    }
}
