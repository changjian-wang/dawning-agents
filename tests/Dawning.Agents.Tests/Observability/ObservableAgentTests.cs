namespace Dawning.Agents.Tests.Observability;

using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Observability;
using Dawning.Agents.Core.Observability;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

public class ObservableAgentTests
{
    private readonly Mock<IAgent> _mockInnerAgent;
    private readonly TelemetryConfig _config;
    private readonly AgentTelemetry _telemetry;

    public ObservableAgentTests()
    {
        _mockInnerAgent = new Mock<IAgent>();
        _mockInnerAgent.Setup(a => a.Name).Returns("TestAgent");
        _mockInnerAgent.Setup(a => a.Instructions).Returns("Test instructions");

        _config = new TelemetryConfig
        {
            EnableLogging = true,
            EnableMetrics = true,
            EnableTracing = true,
        };
        _telemetry = new AgentTelemetry(_config);
    }

    private ObservableAgent CreateAgent(ILogger? logger = null)
    {
        return new ObservableAgent(_mockInnerAgent.Object, _telemetry, _config, logger);
    }

    [Fact]
    public void Name_ShouldIncludeObservablePrefix()
    {
        // Arrange
        var agent = CreateAgent();

        // Act & Assert
        agent.Name.Should().Be("Observable(TestAgent)");
    }

    [Fact]
    public void Instructions_ShouldReturnInnerAgentInstructions()
    {
        // Arrange
        var agent = CreateAgent();

        // Act & Assert
        agent.Instructions.Should().Be("Test instructions");
    }

    [Fact]
    public void InnerAgent_ShouldReturnInnerAgent()
    {
        // Arrange
        var agent = CreateAgent();

        // Act & Assert
        agent.InnerAgent.Should().BeSameAs(_mockInnerAgent.Object);
    }

    [Fact]
    public async Task RunAsync_WithStringInput_ShouldCallInnerAgent()
    {
        // Arrange
        _mockInnerAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResponse.Successful("Response", [], TimeSpan.FromMilliseconds(100)));

        var agent = CreateAgent();

        // Act
        var response = await agent.RunAsync("Test input");

        // Assert
        response.Success.Should().BeTrue();
        response.FinalAnswer.Should().Be("Response");
        _mockInnerAgent.Verify(
            a =>
                a.RunAsync(
                    It.Is<AgentContext>(c => c.UserInput == "Test input"),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task RunAsync_WithContext_ShouldCallInnerAgent()
    {
        // Arrange
        _mockInnerAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResponse.Successful("Response", [], TimeSpan.FromMilliseconds(100)));

        var agent = CreateAgent();
        var context = new AgentContext { UserInput = "Test input", SessionId = "session-123" };

        // Act
        var response = await agent.RunAsync(context);

        // Assert
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_ShouldRecordMetrics()
    {
        // Arrange
        _mockInnerAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResponse.Successful("Response", [], TimeSpan.FromMilliseconds(100)));

        var agent = CreateAgent();

        // Act
        await agent.RunAsync("Test input");

        // Assert
        var metrics = agent.GetMetrics();
        metrics.Counters.Should().Contain(c => c.Name == "agent.requests");
        metrics.Histograms.Should().Contain(h => h.Name == "agent.latency_ms");
    }

    [Fact]
    public async Task RunAsync_WhenInnerAgentFails_ShouldRecordErrorMetrics()
    {
        // Arrange
        _mockInnerAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResponse.Failed("Error occurred", [], TimeSpan.FromSeconds(1)));

        var agent = CreateAgent();

        // Act
        var response = await agent.RunAsync("Test input");

        // Assert
        response.Success.Should().BeFalse();
        var metrics = agent.GetMetrics();
        var requestCounter = metrics.Counters.FirstOrDefault(c => c.Name == "agent.requests");
        requestCounter.Should().NotBeNull();
    }

    [Fact]
    public async Task RunAsync_WhenInnerAgentThrows_ShouldRethrow()
    {
        // Arrange
        _mockInnerAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        var agent = CreateAgent();

        // Act
        var act = () => agent.RunAsync("Test input");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Test error");
    }

    [Fact]
    public async Task RunAsync_WhenInnerAgentThrows_ShouldRecordErrorMetrics()
    {
        // Arrange
        _mockInnerAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        var agent = CreateAgent();

        // Act
        try
        {
            await agent.RunAsync("Test input");
        }
        catch
        {
            // 忽略异常
        }

        // Assert
        var metrics = agent.GetMetrics();
        metrics.Counters.Should().Contain(c => c.Name == "agent.errors");
    }

    [Fact]
    public void GetMetrics_ShouldReturnSnapshot()
    {
        // Arrange
        var agent = CreateAgent();

        // Act
        var metrics = agent.GetMetrics();

        // Assert
        metrics.Should().NotBeNull();
        metrics.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void MetricsCollector_ShouldBeAccessible()
    {
        // Arrange
        var agent = CreateAgent();

        // Act & Assert
        agent.MetricsCollector.Should().NotBeNull();
    }
}
