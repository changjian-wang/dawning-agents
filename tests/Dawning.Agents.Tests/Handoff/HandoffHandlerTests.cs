using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Handoff;
using Dawning.Agents.Core.Handoff;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Dawning.Agents.Tests.Handoff;

public class HandoffHandlerTests
{
    private readonly HandoffHandler _handler;

    public HandoffHandlerTests()
    {
        _handler = new HandoffHandler();
    }

    [Fact]
    public void RegisterAgent_ShouldAddAgent()
    {
        // Arrange
        var agent = CreateMockAgent("Agent1");

        // Act
        _handler.RegisterAgent(agent);

        // Assert
        _handler.GetAgent("Agent1").Should().Be(agent);
    }

    [Fact]
    public void RegisterAgent_ShouldBeCaseInsensitive()
    {
        // Arrange
        var agent = CreateMockAgent("TestAgent");

        // Act
        _handler.RegisterAgent(agent);

        // Assert
        _handler.GetAgent("testagent").Should().Be(agent);
        _handler.GetAgent("TESTAGENT").Should().Be(agent);
    }

    [Fact]
    public void RegisterAgents_ShouldAddMultipleAgents()
    {
        // Arrange
        var agents = new[]
        {
            CreateMockAgent("Agent1"),
            CreateMockAgent("Agent2"),
            CreateMockAgent("Agent3"),
        };

        // Act
        _handler.RegisterAgents(agents);

        // Assert
        _handler.GetAllAgents().Should().HaveCount(3);
    }

    [Fact]
    public void GetAgent_ShouldReturnNull_WhenNotFound()
    {
        // Act
        var agent = _handler.GetAgent("NonExistent");

        // Assert
        agent.Should().BeNull();
    }

    [Fact]
    public void GetAllAgents_ShouldReturnAllRegisteredAgents()
    {
        // Arrange
        _handler.RegisterAgent(CreateMockAgent("Agent1"));
        _handler.RegisterAgent(CreateMockAgent("Agent2"));

        // Act
        var agents = _handler.GetAllAgents();

        // Assert
        agents.Should().HaveCount(2);
        agents.Select(a => a.Name).Should().Contain("Agent1", "Agent2");
    }

    [Fact]
    public async Task ExecuteHandoffAsync_ShouldExecuteTargetAgent()
    {
        // Arrange
        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(a => a.Name).Returns("TargetAgent");
        mockAgent
            .Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResponse.Successful("Result", [], TimeSpan.FromMilliseconds(100)));

        _handler.RegisterAgent(mockAgent.Object);

        var request = HandoffRequest.To("TargetAgent", "Test input");

        // Act
        var result = await _handler.ExecuteHandoffAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.ExecutedByAgent.Should().Be("TargetAgent");
        result.Response!.FinalAnswer.Should().Be("Result");
        result.HandoffChain.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteHandoffAsync_ShouldFail_WhenAgentNotFound()
    {
        // Arrange
        var request = HandoffRequest.To("NonExistent", "Test input");

        // Act
        var result = await _handler.ExecuteHandoffAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Agent not found");
    }

    [Fact]
    public async Task ExecuteHandoffAsync_ShouldChainHandoffs()
    {
        // Arrange
        var triageAgent = new Mock<IAgent>();
        triageAgent.Setup(a => a.Name).Returns("Triage");
        triageAgent
            .Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                AgentResponse.Successful(
                    "[HANDOFF:Expert] Please handle this technical question",
                    [],
                    TimeSpan.FromMilliseconds(50)
                )
            );

        var expertAgent = new Mock<IAgent>();
        expertAgent.Setup(a => a.Name).Returns("Expert");
        expertAgent
            .Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                AgentResponse.Successful("Expert answer", [], TimeSpan.FromMilliseconds(100))
            );

        _handler.RegisterAgent(triageAgent.Object);
        _handler.RegisterAgent(expertAgent.Object);

        var request = HandoffRequest.To("Triage", "Technical question");

        // Act
        var result = await _handler.ExecuteHandoffAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.ExecutedByAgent.Should().Be("Expert");
        result.Response!.FinalAnswer.Should().Be("Expert answer");
        result.HandoffChain.Should().HaveCount(2);
        result.HandoffChain[0].ToAgent.Should().Be("Triage");
        result.HandoffChain[1].ToAgent.Should().Be("Expert");
        result.HandoffChain[1].FromAgent.Should().Be("Triage");
    }

    [Fact]
    public async Task ExecuteHandoffAsync_ShouldRespectMaxDepth()
    {
        // Arrange
        var options = Options.Create(new HandoffOptions { MaxHandoffDepth = 2 });
        var handler = new HandoffHandler(options);

        // Create a chain: Agent1 -> Agent2 -> Agent3 -> Agent4 (should fail at Agent4)
        for (var i = 1; i <= 4; i++)
        {
            var agentName = $"Agent{i}";
            var nextAgent = i < 4 ? $"Agent{i + 1}" : null;

            var mock = new Mock<IAgent>();
            mock.Setup(a => a.Name).Returns(agentName);

            if (nextAgent != null)
            {
                mock.Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(
                        AgentResponse.Successful(
                            $"[HANDOFF:{nextAgent}] Continue",
                            [],
                            TimeSpan.FromMilliseconds(10)
                        )
                    );
            }
            else
            {
                mock.Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(
                        AgentResponse.Successful("Final", [], TimeSpan.FromMilliseconds(10))
                    );
            }

            handler.RegisterAgent(mock.Object);
        }

        var request = HandoffRequest.To("Agent1", "Start");

        // Act
        var result = await handler.ExecuteHandoffAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("depth limit exceeded");
    }

    [Fact]
    public async Task ExecuteHandoffAsync_ShouldDetectCycles_WhenNotAllowed()
    {
        // Arrange
        var options = Options.Create(new HandoffOptions { AllowCycles = false });
        var handler = new HandoffHandler(options);

        // Agent1 -> Agent2 -> Agent1 (cycle)
        var agent1 = new Mock<IAgent>();
        agent1.Setup(a => a.Name).Returns("Agent1");
        agent1
            .Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                AgentResponse.Successful("[HANDOFF:Agent2] next", [], TimeSpan.FromMilliseconds(10))
            );

        var agent2 = new Mock<IAgent>();
        agent2.Setup(a => a.Name).Returns("Agent2");
        agent2
            .Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                AgentResponse.Successful("[HANDOFF:Agent1] back", [], TimeSpan.FromMilliseconds(10))
            );

        handler.RegisterAgent(agent1.Object);
        handler.RegisterAgent(agent2.Object);

        var request = HandoffRequest.To("Agent1", "Start");

        // Act
        var result = await handler.ExecuteHandoffAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("cycle detected");
    }

    [Fact]
    public async Task RunWithHandoffAsync_ShouldBeShortcutForExecuteHandoff()
    {
        // Arrange
        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(a => a.Name).Returns("EntryAgent");
        mockAgent
            .Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResponse.Successful("Done", [], TimeSpan.FromMilliseconds(50)));

        _handler.RegisterAgent(mockAgent.Object);

        // Act
        var result = await _handler.RunWithHandoffAsync("EntryAgent", "Test input");

        // Assert
        result.Success.Should().BeTrue();
        result.ExecutedByAgent.Should().Be("EntryAgent");
    }

    [Fact]
    public async Task ExecuteHandoffAsync_ShouldRecordTimestamps()
    {
        // Arrange
        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(a => a.Name).Returns("Agent1");
        mockAgent
            .Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResponse.Successful("Done", [], TimeSpan.FromMilliseconds(50)));

        _handler.RegisterAgent(mockAgent.Object);

        var request = HandoffRequest.To("Agent1", "Test");

        // Act
        var result = await _handler.ExecuteHandoffAsync(request);

        // Assert
        result.TotalDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    private static IAgent CreateMockAgent(string name)
    {
        var mock = new Mock<IAgent>();
        mock.Setup(a => a.Name).Returns(name);
        mock.Setup(a => a.Instructions).Returns($"Instructions for {name}");
        mock.Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                AgentResponse.Successful($"Response from {name}", [], TimeSpan.FromMilliseconds(10))
            );
        return mock.Object;
    }
}
