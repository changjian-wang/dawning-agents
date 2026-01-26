using Dawning.Agents.Abstractions.HumanLoop;
using Dawning.Agents.Core.HumanLoop;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Dawning.Agents.Tests.HumanLoop;

/// <summary>
/// AutoApprovalHandler 单元测试
/// </summary>
public class AutoApprovalHandlerTests
{
    private readonly Mock<ILogger<AutoApprovalHandler>> _mockLogger;

    public AutoApprovalHandlerTests()
    {
        _mockLogger = new Mock<ILogger<AutoApprovalHandler>>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullOptions_UsesDefaults()
    {
        var act = () => new AutoApprovalHandler(options: null);

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithNullLogger_UsesNullLogger()
    {
        var act = () => new AutoApprovalHandler(options: null, logger: null);

        act.Should().NotThrow();
    }

    #endregion

    #region RequestConfirmationAsync Tests - RiskLevel

    [Fact]
    public async Task RequestConfirmationAsync_LowRisk_ApprovesAutomatically()
    {
        var handler = CreateHandler();
        var request = CreateConfirmationRequest(RiskLevel.Low);

        var response = await handler.RequestConfirmationAsync(request);

        response.RequestId.Should().Be(request.Id);
        response.SelectedOption.Should().Be("yes");
    }

    [Fact]
    public async Task RequestConfirmationAsync_MediumRisk_DefaultDenies()
    {
        // Default RequireApprovalForMediumRisk = true, so medium risk is denied
        var handler = CreateHandler();
        var request = CreateConfirmationRequest(RiskLevel.Medium);

        var response = await handler.RequestConfirmationAsync(request);

        response.SelectedOption.Should().Be("no");
    }

    [Fact]
    public async Task RequestConfirmationAsync_MediumRisk_WhenNotRequireApproval_Approves()
    {
        var options = new HumanLoopOptions { RequireApprovalForMediumRisk = false };
        var handler = CreateHandler(options);
        var request = CreateConfirmationRequest(RiskLevel.Medium);

        var response = await handler.RequestConfirmationAsync(request);

        response.SelectedOption.Should().Be("yes");
    }

    [Fact]
    public async Task RequestConfirmationAsync_HighRisk_DeniesAutomatically()
    {
        var handler = CreateHandler();
        var request = CreateConfirmationRequest(RiskLevel.High);

        var response = await handler.RequestConfirmationAsync(request);

        response.SelectedOption.Should().Be("no");
    }

    [Fact]
    public async Task RequestConfirmationAsync_CriticalRisk_DeniesAutomatically()
    {
        var handler = CreateHandler();
        var request = CreateConfirmationRequest(RiskLevel.Critical);

        var response = await handler.RequestConfirmationAsync(request);

        response.SelectedOption.Should().Be("no");
    }

    #endregion

    #region RequestConfirmationAsync Tests - ConfirmationType

    [Fact]
    public async Task RequestConfirmationAsync_BinaryType_ReturnsYesOrNo()
    {
        var handler = CreateHandler();
        var request = CreateConfirmationRequest(RiskLevel.Low, ConfirmationType.Binary);

        var response = await handler.RequestConfirmationAsync(request);

        response.SelectedOption.Should().BeOneOf("yes", "no");
    }

    [Fact]
    public async Task RequestConfirmationAsync_MultiChoiceType_ReturnsDefaultOption()
    {
        var handler = CreateHandler();
        var options = new List<ConfirmationOption>
        {
            new() { Id = "opt1", Label = "Option 1", IsDefault = false },
            new() { Id = "opt2", Label = "Option 2", IsDefault = true },
        };
        var request = new ConfirmationRequest
        {
            Id = Guid.NewGuid().ToString(),
            Action = "Test",
            Description = "Test message",
            Type = ConfirmationType.MultiChoice,
            Options = options,
            RiskLevel = RiskLevel.Low,
        };

        var response = await handler.RequestConfirmationAsync(request);

        response.SelectedOption.Should().Be("opt2");
    }

    [Fact]
    public async Task RequestConfirmationAsync_MultiChoiceType_NoDefault_ReturnsFirst()
    {
        var handler = CreateHandler();
        var options = new List<ConfirmationOption>
        {
            new() { Id = "first", Label = "First Option", IsDefault = false },
            new() { Id = "second", Label = "Second Option", IsDefault = false },
        };
        var request = new ConfirmationRequest
        {
            Id = Guid.NewGuid().ToString(),
            Action = "Test",
            Description = "Test message",
            Type = ConfirmationType.MultiChoice,
            Options = options,
            RiskLevel = RiskLevel.Low,
        };

        var response = await handler.RequestConfirmationAsync(request);

        response.SelectedOption.Should().Be("first");
    }

    [Fact]
    public async Task RequestConfirmationAsync_FreeformInputType_ReturnsAutoApproved()
    {
        var handler = CreateHandler();
        var request = CreateConfirmationRequest(RiskLevel.Low, ConfirmationType.FreeformInput);

        var response = await handler.RequestConfirmationAsync(request);

        response.SelectedOption.Should().Be("auto-approved");
    }

    [Fact]
    public async Task RequestConfirmationAsync_ReviewType_ReturnsApproveOrReject()
    {
        var handler = CreateHandler();
        var request = CreateConfirmationRequest(RiskLevel.Low, ConfirmationType.Review);

        var response = await handler.RequestConfirmationAsync(request);

        response.SelectedOption.Should().BeOneOf("approve", "reject");
    }

    #endregion

    #region RequestInputAsync Tests

    [Fact]
    public async Task RequestInputAsync_WithDefaultValue_ReturnsDefaultValue()
    {
        var handler = CreateHandler();

        var result = await handler.RequestInputAsync("Enter value:", "default-value");

        result.Should().Be("default-value");
    }

    [Fact]
    public async Task RequestInputAsync_WithNoDefaultValue_ReturnsEmptyString()
    {
        var handler = CreateHandler();

        var result = await handler.RequestInputAsync("Enter value:");

        result.Should().BeEmpty();
    }

    #endregion

    #region NotifyAsync Tests

    [Fact]
    public async Task NotifyAsync_Completes()
    {
        var handler = CreateHandler();

        var act = async () => await handler.NotifyAsync("Test message", NotificationLevel.Warning);

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(NotificationLevel.Info)]
    [InlineData(NotificationLevel.Warning)]
    [InlineData(NotificationLevel.Error)]
    [InlineData(NotificationLevel.Success)]
    public async Task NotifyAsync_AllLevels_Completes(NotificationLevel level)
    {
        var handler = CreateHandler();

        var act = async () => await handler.NotifyAsync("Message", level);

        await act.Should().NotThrowAsync();
    }

    #endregion

    #region EscalateAsync Tests

    [Fact]
    public async Task EscalateAsync_ReturnsSkippedAction()
    {
        var handler = CreateHandler();
        var request = new EscalationRequest
        {
            Id = Guid.NewGuid().ToString(),
            Reason = "Test escalation",
            Description = "Test escalation description",
            Severity = EscalationSeverity.Medium,
        };

        var result = await handler.EscalateAsync(request);

        result.RequestId.Should().Be(request.Id);
        result.Action.Should().Be(EscalationAction.Skipped);
        result.Resolution.Should().Contain("跳过");
    }

    #endregion

    #region Helper Methods

    private AutoApprovalHandler CreateHandler(HumanLoopOptions? options = null)
    {
        var optionsWrapper = Options.Create(options ?? new HumanLoopOptions());
        return new AutoApprovalHandler(optionsWrapper, _mockLogger.Object);
    }

    private static ConfirmationRequest CreateConfirmationRequest(
        RiskLevel riskLevel,
        ConfirmationType type = ConfirmationType.Binary
    )
    {
        return new ConfirmationRequest
        {
            Id = Guid.NewGuid().ToString(),
            Action = "Test Confirmation",
            Description = "This is a test message",
            Type = type,
            RiskLevel = riskLevel,
            Options = new List<ConfirmationOption>
            {
                new() { Id = "yes", Label = "Yes" },
                new() { Id = "no", Label = "No" },
            },
        };
    }

    #endregion
}
