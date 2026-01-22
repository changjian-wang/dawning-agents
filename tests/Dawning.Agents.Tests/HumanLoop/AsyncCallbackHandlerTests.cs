using Dawning.Agents.Abstractions.HumanLoop;
using Dawning.Agents.Core.HumanLoop;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Tests.HumanLoop;

public class AsyncCallbackHandlerTests
{
    [Fact]
    public void Constructor_ShouldCreateHandler()
    {
        // Act
        var handler = new AsyncCallbackHandler();

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public async Task RequestConfirmationAsync_ShouldTriggerEvent()
    {
        // Arrange
        var handler = new AsyncCallbackHandler(NullLogger<AsyncCallbackHandler>.Instance);
        ConfirmationRequest? receivedRequest = null;

        handler.ConfirmationRequested += (sender, req) =>
        {
            receivedRequest = req;
            // Complete the confirmation asynchronously
            Task.Run(() =>
                handler.CompleteConfirmation(
                    new ConfirmationResponse { RequestId = req.Id, SelectedOption = "yes" }
                )
            );
        };

        var request = new ConfirmationRequest { Action = "Test", Description = "Test description" };

        // Act
        var response = await handler.RequestConfirmationAsync(request);

        // Assert
        receivedRequest.Should().NotBeNull();
        receivedRequest!.Id.Should().Be(request.Id);
        response.SelectedOption.Should().Be("yes");
    }

    [Fact]
    public async Task RequestConfirmationAsync_WithTimeout_ShouldReturnDefaultOption()
    {
        // Arrange
        var handler = new AsyncCallbackHandler();
        var request = new ConfirmationRequest
        {
            Action = "Test",
            Description = "Test",
            Timeout = TimeSpan.FromMilliseconds(50),
            DefaultOnTimeout = "timeout-default",
        };

        // Act
        var response = await handler.RequestConfirmationAsync(request);

        // Assert
        response.SelectedOption.Should().Be("timeout-default");
    }

    [Fact]
    public void CompleteConfirmation_WithValidRequest_ShouldReturnTrue()
    {
        // Arrange
        var handler = new AsyncCallbackHandler();
        string? capturedRequestId = null;

        handler.ConfirmationRequested += (_, req) => capturedRequestId = req.Id;

        // Start a confirmation request
        var task = handler.RequestConfirmationAsync(
            new ConfirmationRequest { Action = "Test", Description = "Test" }
        );

        // Wait a bit for the event to be triggered
        Thread.Sleep(50);

        // Act
        var result = handler.CompleteConfirmation(
            new ConfirmationResponse { RequestId = capturedRequestId!, SelectedOption = "yes" }
        );

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CompleteConfirmation_WithInvalidRequest_ShouldReturnFalse()
    {
        // Arrange
        var handler = new AsyncCallbackHandler();

        // Act
        var result = handler.CompleteConfirmation(
            new ConfirmationResponse { RequestId = "non-existent", SelectedOption = "yes" }
        );

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RequestInputAsync_ShouldTriggerEvent()
    {
        // Arrange
        var handler = new AsyncCallbackHandler();
        (string Id, string Prompt, string? DefaultValue)? receivedInput = null;

        handler.InputRequested += (sender, args) =>
        {
            receivedInput = args;
            Task.Run(() => handler.CompleteInput(args.Id, "user input"));
        };

        // Act
        var result = await handler.RequestInputAsync("Enter something");

        // Assert
        receivedInput.Should().NotBeNull();
        receivedInput!.Value.Prompt.Should().Be("Enter something");
        result.Should().Be("user input");
    }

    [Fact]
    public async Task NotifyAsync_ShouldTriggerEvent()
    {
        // Arrange
        var handler = new AsyncCallbackHandler();
        (string Message, NotificationLevel Level)? receivedNotification = null;

        handler.NotificationSent += (sender, args) => receivedNotification = args;

        // Act
        await handler.NotifyAsync("Test message", NotificationLevel.Warning);

        // Assert
        receivedNotification.Should().NotBeNull();
        receivedNotification!.Value.Message.Should().Be("Test message");
        receivedNotification.Value.Level.Should().Be(NotificationLevel.Warning);
    }

    [Fact]
    public async Task EscalateAsync_ShouldTriggerEvent()
    {
        // Arrange
        var handler = new AsyncCallbackHandler();
        EscalationRequest? receivedRequest = null;

        handler.EscalationRequested += (sender, req) =>
        {
            receivedRequest = req;
            Task.Run(() =>
                handler.CompleteEscalation(
                    new EscalationResult
                    {
                        RequestId = req.Id,
                        Action = EscalationAction.Resolved,
                        Resolution = "Fixed",
                    }
                )
            );
        };

        var request = new EscalationRequest { Reason = "Error", Description = "Test error" };

        // Act
        var result = await handler.EscalateAsync(request);

        // Assert
        receivedRequest.Should().NotBeNull();
        result.Action.Should().Be(EscalationAction.Resolved);
        result.Resolution.Should().Be("Fixed");
    }

    [Fact]
    public void GetPendingConfirmationIds_ShouldReturnEmptyInitially()
    {
        // Arrange
        var handler = new AsyncCallbackHandler();

        // Act
        var ids = handler.GetPendingConfirmationIds();

        // Assert
        ids.Should().BeEmpty();
    }

    [Fact]
    public void CancelConfirmation_WithNonExistent_ShouldReturnFalse()
    {
        // Arrange
        var handler = new AsyncCallbackHandler();

        // Act
        var result = handler.CancelConfirmation("non-existent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CancelEscalation_WithNonExistent_ShouldReturnFalse()
    {
        // Arrange
        var handler = new AsyncCallbackHandler();

        // Act
        var result = handler.CancelEscalation("non-existent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CancelInput_WithNonExistent_ShouldReturnFalse()
    {
        // Arrange
        var handler = new AsyncCallbackHandler();

        // Act
        var result = handler.CancelInput("non-existent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CompleteEscalation_WithInvalidRequest_ShouldReturnFalse()
    {
        // Arrange
        var handler = new AsyncCallbackHandler();

        // Act
        var result = handler.CompleteEscalation(
            new EscalationResult { RequestId = "non-existent" }
        );

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CompleteInput_WithInvalidRequest_ShouldReturnFalse()
    {
        // Arrange
        var handler = new AsyncCallbackHandler();

        // Act
        var result = handler.CompleteInput("non-existent", "input");

        // Assert
        result.Should().BeFalse();
    }
}
