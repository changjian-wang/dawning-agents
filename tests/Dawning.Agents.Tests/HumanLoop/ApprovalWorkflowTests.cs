using Dawning.Agents.Abstractions.HumanLoop;
using Dawning.Agents.Core.HumanLoop;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Dawning.Agents.Tests.HumanLoop;

public class ApprovalWorkflowTests
{
    private readonly Mock<IHumanInteractionHandler> _mockHandler;
    private readonly ApprovalConfig _config;

    public ApprovalWorkflowTests()
    {
        _mockHandler = new Mock<IHumanInteractionHandler>();
        _config = new ApprovalConfig
        {
            RequireApprovalForLowRisk = false,
            RequireApprovalForMediumRisk = true,
            ApprovalTimeout = TimeSpan.FromMinutes(5),
            DefaultOnTimeout = "reject",
        };
    }

    private ApprovalWorkflow CreateWorkflow(HumanLoopOptions? options = null)
    {
        return new ApprovalWorkflow(
            _mockHandler.Object,
            _config,
            options != null ? Options.Create(options) : null
        );
    }

    [Fact]
    public async Task RequestApprovalAsync_ForLowRisk_ShouldAutoApprove()
    {
        // Arrange
        var workflow = CreateWorkflow();

        // Act
        var result = await workflow.RequestApprovalAsync(
            "simple-action",
            "A simple action",
            new Dictionary<string, object> { ["riskLevel"] = "Low" }
        );

        // Assert
        result.IsApproved.Should().BeTrue();
        result.IsAutoApproved.Should().BeTrue();
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
    public async Task RequestApprovalAsync_ForHighRisk_ShouldRequestApproval()
    {
        // Arrange
        _mockHandler
            .Setup(h =>
                h.RequestConfirmationAsync(
                    It.IsAny<ConfirmationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ConfirmationResponse { RequestId = "test", SelectedOption = "approve" }
            );

        var workflow = CreateWorkflow();

        // Act
        var result = await workflow.RequestApprovalAsync(
            "delete user data",
            "Deleting all user data"
        );

        // Assert
        result.IsApproved.Should().BeTrue();
        result.IsAutoApproved.Should().BeFalse();
        _mockHandler.Verify(
            h =>
                h.RequestConfirmationAsync(
                    It.Is<ConfirmationRequest>(r => r.RiskLevel == RiskLevel.High),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task RequestApprovalAsync_WithRejection_ShouldReturnRejected()
    {
        // Arrange
        _mockHandler
            .Setup(h =>
                h.RequestConfirmationAsync(
                    It.IsAny<ConfirmationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ConfirmationResponse
                {
                    RequestId = "test",
                    SelectedOption = "reject",
                    Reason = "Not allowed",
                    RespondedBy = "admin",
                }
            );

        var workflow = CreateWorkflow();

        // Act
        var result = await workflow.RequestApprovalAsync("delete production data", "Deleting prod");

        // Assert
        result.IsApproved.Should().BeFalse();
        result.RejectionReason.Should().Be("Not allowed");
        result.ApprovedBy.Should().Be("admin");
    }

    [Fact]
    public async Task RequestApprovalAsync_WithModify_ShouldReturnModified()
    {
        // Arrange
        _mockHandler
            .Setup(h =>
                h.RequestConfirmationAsync(
                    It.IsAny<ConfirmationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ConfirmationResponse
                {
                    RequestId = "test",
                    SelectedOption = "modify",
                    ModifiedContent = "modified action",
                    RespondedBy = "admin",
                }
            );

        var workflow = CreateWorkflow();

        // Act
        var result = await workflow.RequestApprovalAsync("delete data", "Deleting data");

        // Assert
        result.IsApproved.Should().BeTrue();
        result.ModifiedAction.Should().Be("modified action");
    }

    [Fact]
    public async Task RequestApprovalAsync_WithTimeout_AndDefaultReject_ShouldReturnTimedOut()
    {
        // Arrange
        _mockHandler
            .Setup(h =>
                h.RequestConfirmationAsync(
                    It.IsAny<ConfirmationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ConfirmationResponse { RequestId = "test", SelectedOption = "timeout" }
            );

        var workflow = CreateWorkflow();

        // Act
        var result = await workflow.RequestApprovalAsync("delete data", "Deleting data");

        // Assert
        result.IsApproved.Should().BeFalse();
        result.IsTimedOut.Should().BeTrue();
    }

    [Fact]
    public async Task RequestApprovalAsync_WithTimeout_AndDefaultApprove_ShouldAutoApprove()
    {
        // Arrange
        var config = new ApprovalConfig { DefaultOnTimeout = "approve" };

        _mockHandler
            .Setup(h =>
                h.RequestConfirmationAsync(
                    It.IsAny<ConfirmationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ConfirmationResponse { RequestId = "test", SelectedOption = "timeout" }
            );

        var workflow = new ApprovalWorkflow(_mockHandler.Object, config);

        // Act
        var result = await workflow.RequestApprovalAsync("delete data", "Deleting data");

        // Assert
        result.IsApproved.Should().BeTrue();
        result.IsAutoApproved.Should().BeTrue();
    }

    [Fact]
    public void AssessRiskLevel_WithCriticalKeyword_ShouldReturnCritical()
    {
        // Arrange
        var workflow = CreateWorkflow();

        // Act
        var level = workflow.AssessRiskLevel("Access production database", null);

        // Assert
        level.Should().Be(RiskLevel.Critical);
    }

    [Fact]
    public void AssessRiskLevel_WithHighRiskKeyword_ShouldReturnHigh()
    {
        // Arrange
        var workflow = CreateWorkflow();

        // Act
        var level = workflow.AssessRiskLevel("Delete user account", null);

        // Assert
        level.Should().Be(RiskLevel.High);
    }

    [Fact]
    public void AssessRiskLevel_WithLargeAmount_ShouldReturnHigh()
    {
        // Arrange
        var workflow = CreateWorkflow();

        // Act
        var level = workflow.AssessRiskLevel(
            "Process transaction",
            new Dictionary<string, object> { ["amount"] = 50000m }
        );

        // Assert
        level.Should().Be(RiskLevel.High);
    }

    [Fact]
    public void AssessRiskLevel_WithProductionEnvironment_ShouldReturnCritical()
    {
        // Arrange
        var workflow = CreateWorkflow();

        // Act
        var level = workflow.AssessRiskLevel(
            "Deploy application",
            new Dictionary<string, object> { ["environment"] = "Production" }
        );

        // Assert
        level.Should().Be(RiskLevel.Critical);
    }

    [Fact]
    public void AssessRiskLevel_WithExplicitRiskLevel_ShouldUseExplicit()
    {
        // Arrange
        var workflow = CreateWorkflow();

        // Act
        var level = workflow.AssessRiskLevel(
            "Some action",
            new Dictionary<string, object> { ["riskLevel"] = "Low" }
        );

        // Assert
        level.Should().Be(RiskLevel.Low);
    }

    [Fact]
    public void AssessRiskLevel_WithNoIndicators_ShouldReturnMedium()
    {
        // Arrange
        var workflow = CreateWorkflow();

        // Act
        var level = workflow.AssessRiskLevel("Process document", null);

        // Assert
        level.Should().Be(RiskLevel.Medium);
    }

    [Fact]
    public async Task RequestMultiApprovalAsync_WithEnoughApprovals_ShouldSucceed()
    {
        // Arrange
        var approvalCount = 0;
        _mockHandler
            .Setup(h =>
                h.RequestConfirmationAsync(
                    It.IsAny<ConfirmationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() =>
            {
                approvalCount++;
                return new ConfirmationResponse
                {
                    RequestId = "test",
                    SelectedOption = "approve",
                    RespondedBy = $"approver-{approvalCount}",
                };
            });

        var workflow = CreateWorkflow();

        // Act
        var result = await workflow.RequestMultiApprovalAsync(
            "critical action",
            "Critical operation",
            2
        );

        // Assert
        result.IsApproved.Should().BeTrue();
        result.ApprovedBy.Should().Contain("approver-1");
        result.ApprovedBy.Should().Contain("approver-2");
    }

    [Fact]
    public async Task RequestMultiApprovalAsync_WithRejections_ShouldFail()
    {
        // Arrange
        _mockHandler
            .Setup(h =>
                h.RequestConfirmationAsync(
                    It.IsAny<ConfirmationRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ConfirmationResponse
                {
                    RequestId = "test",
                    SelectedOption = "reject",
                    Reason = "Not allowed",
                }
            );

        var workflow = CreateWorkflow();

        // Act
        var result = await workflow.RequestMultiApprovalAsync(
            "critical action",
            "Critical operation",
            2
        );

        // Assert
        result.IsApproved.Should().BeFalse();
        result.RejectionReason.Should().Contain("审批数量不足");
    }
}
