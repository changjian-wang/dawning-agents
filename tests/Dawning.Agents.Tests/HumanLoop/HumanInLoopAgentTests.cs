using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.HumanLoop;
using Dawning.Agents.Core.HumanLoop;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Dawning.Agents.Tests.HumanLoop;

public class HumanInLoopAgentTests
{
    private readonly Mock<IAgent> _mockAgent;
    private readonly Mock<IHumanInteractionHandler> _mockHandler;

    public HumanInLoopAgentTests()
    {
        _mockAgent = new Mock<IAgent>();
        _mockHandler = new Mock<IHumanInteractionHandler>();

        // Default setup
        _mockAgent.Setup(a => a.Name).Returns("TestAgent");
        _mockAgent.Setup(a => a.Instructions).Returns("Test instructions");

        _mockHandler
            .Setup(h =>
                h.RequestConfirmationAsync(
                    It.IsAny<ConfirmationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (ConfirmationRequest req, CancellationToken _) =>
                    new ConfirmationResponse { RequestId = req.Id, SelectedOption = "approve" }
            );
    }

    private HumanInLoopAgent CreateAgent(HumanLoopOptions? options = null)
    {
        return new HumanInLoopAgent(
            _mockAgent.Object,
            _mockHandler.Object,
            options != null ? Options.Create(options) : null
        );
    }

    [Fact]
    public void Name_ShouldIncludeInnerAgentName()
    {
        // Arrange
        var agent = CreateAgent();

        // Assert
        agent.Name.Should().Be("HumanLoop(TestAgent)");
    }

    [Fact]
    public void Instructions_ShouldDelegateToInnerAgent()
    {
        // Arrange
        var agent = CreateAgent();

        // Assert
        agent.Instructions.Should().Be("Test instructions");
    }

    [Fact]
    public async Task RunAsync_WithoutConfirmation_ShouldCallInnerAgentDirectly()
    {
        // Arrange
        var options = new HumanLoopOptions { ConfirmBeforeExecution = false };

        _mockAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResponse.Successful("Result", [], TimeSpan.FromSeconds(1)));

        var agent = CreateAgent(options);

        // Act
        var result = await agent.RunAsync("Test input");

        // Assert
        result.Success.Should().BeTrue();
        result.FinalAnswer.Should().Be("Result");
        _mockHandler.Verify(
            h =>
                h.RequestConfirmationAsync(
                    It.IsAny<ConfirmationRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task RunAsync_WithConfirmation_ShouldRequestApproval()
    {
        // Arrange
        var options = new HumanLoopOptions { ConfirmBeforeExecution = true };

        _mockAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResponse.Successful("Result", [], TimeSpan.FromSeconds(1)));

        var agent = CreateAgent(options);

        // Act
        var result = await agent.RunAsync("Test input");

        // Assert
        result.Success.Should().BeTrue();
        _mockHandler.Verify(
            h =>
                h.RequestConfirmationAsync(
                    It.IsAny<ConfirmationRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.AtLeastOnce
        );
    }

    [Fact]
    public async Task RunAsync_WithRejectedConfirmation_ShouldReturnFailed()
    {
        // Arrange
        var options = new HumanLoopOptions { ConfirmBeforeExecution = true };

        _mockHandler
            .Setup(h =>
                h.RequestConfirmationAsync(
                    It.IsAny<ConfirmationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (ConfirmationRequest req, CancellationToken _) =>
                    new ConfirmationResponse
                    {
                        RequestId = req.Id,
                        SelectedOption = "reject",
                        Reason = "Not allowed",
                    }
            );

        var agent = CreateAgent(options);

        // Act
        var result = await agent.RunAsync("Test input");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("未批准");
    }

    [Fact]
    public async Task RunAsync_WithReview_ShouldRequestReview()
    {
        // Arrange
        var options = new HumanLoopOptions
        {
            ConfirmBeforeExecution = false,
            ReviewBeforeReturn = true,
        };

        _mockAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResponse.Successful("Original result", [], TimeSpan.FromSeconds(1)));

        var agent = CreateAgent(options);

        // Act
        var result = await agent.RunAsync("Test input");

        // Assert
        result.Success.Should().BeTrue();
        _mockHandler.Verify(
            h =>
                h.RequestConfirmationAsync(
                    It.Is<ConfirmationRequest>(r => r.Type == ConfirmationType.Review),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task RunAsync_WithReviewEdit_ShouldReturnModifiedContent()
    {
        // Arrange
        var options = new HumanLoopOptions
        {
            ConfirmBeforeExecution = false,
            ReviewBeforeReturn = true,
        };

        _mockAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResponse.Successful("Original result", [], TimeSpan.FromSeconds(1)));

        _mockHandler
            .Setup(h =>
                h.RequestConfirmationAsync(
                    It.Is<ConfirmationRequest>(r => r.Type == ConfirmationType.Review),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (ConfirmationRequest req, CancellationToken _) =>
                    new ConfirmationResponse
                    {
                        RequestId = req.Id,
                        SelectedOption = "edit",
                        ModifiedContent = "Modified result",
                    }
            );

        var agent = CreateAgent(options);

        // Act
        var result = await agent.RunAsync("Test input");

        // Assert
        result.FinalAnswer.Should().Be("Modified result");
    }

    [Fact]
    public async Task RunAsync_WithReviewReject_ShouldReturnFailed()
    {
        // Arrange
        var options = new HumanLoopOptions
        {
            ConfirmBeforeExecution = false,
            ReviewBeforeReturn = true,
        };

        _mockAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResponse.Successful("Original result", [], TimeSpan.FromSeconds(1)));

        _mockHandler
            .Setup(h =>
                h.RequestConfirmationAsync(
                    It.Is<ConfirmationRequest>(r => r.Type == ConfirmationType.Review),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (ConfirmationRequest req, CancellationToken _) =>
                    new ConfirmationResponse { RequestId = req.Id, SelectedOption = "reject" }
            );

        var agent = CreateAgent(options);

        // Act
        var result = await agent.RunAsync("Test input");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("被审查者拒绝");
    }

    [Fact]
    public async Task RunAsync_WithException_ShouldReturnFailed()
    {
        // Arrange
        _mockAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        _mockHandler
            .Setup(h =>
                h.RequestInputAsync(
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync("abort");

        var agent = CreateAgent();

        // Act
        var result = await agent.RunAsync("Test input");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("发生错误");
    }

    [Fact]
    public async Task RunAsync_WithEscalationException_ShouldEscalate()
    {
        // Arrange
        _mockAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AgentEscalationException("Need help", "Agent needs human assistance"));

        _mockHandler
            .Setup(h =>
                h.EscalateAsync(It.IsAny<EscalationRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                (EscalationRequest req, CancellationToken _) =>
                    new EscalationResult
                    {
                        RequestId = req.Id,
                        Action = EscalationAction.Resolved,
                        Resolution = "Human fixed it",
                    }
            );

        var agent = CreateAgent();

        // Act
        var result = await agent.RunAsync("Test input");

        // Assert
        result.Success.Should().BeTrue();
        result.FinalAnswer.Should().Be("Human fixed it");
    }

    [Fact]
    public async Task RunAsync_WithEscalationSkipped_ShouldReturnSkipped()
    {
        // Arrange
        _mockAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AgentEscalationException("Need help", "Agent needs human assistance"));

        _mockHandler
            .Setup(h =>
                h.EscalateAsync(It.IsAny<EscalationRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                (EscalationRequest req, CancellationToken _) =>
                    new EscalationResult { RequestId = req.Id, Action = EscalationAction.Skipped }
            );

        var agent = CreateAgent();

        // Act
        var result = await agent.RunAsync("Test input");

        // Assert
        result.Success.Should().BeTrue();
        result.FinalAnswer.Should().Contain("跳过");
    }

    [Fact]
    public async Task RunAsync_WithEscalationAborted_ShouldReturnFailed()
    {
        // Arrange
        _mockAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AgentEscalationException("Need help", "Agent needs human assistance"));

        _mockHandler
            .Setup(h =>
                h.EscalateAsync(It.IsAny<EscalationRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                (EscalationRequest req, CancellationToken _) =>
                    new EscalationResult { RequestId = req.Id, Action = EscalationAction.Aborted }
            );

        var agent = CreateAgent();

        // Act
        var result = await agent.RunAsync("Test input");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("中止");
    }

    [Fact]
    public async Task RunAsync_WithContext_ShouldUseContext()
    {
        // Arrange
        _mockAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentResponse.Successful("Result", [], TimeSpan.FromSeconds(1)));

        var agent = CreateAgent();
        var context = new AgentContext { UserInput = "Test input", SessionId = "session-123" };

        // Act
        var result = await agent.RunAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        _mockAgent.Verify(
            a =>
                a.RunAsync(
                    It.Is<AgentContext>(c => c.SessionId == "session-123"),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
