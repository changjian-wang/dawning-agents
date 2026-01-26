using Dawning.Agents.Abstractions.HumanLoop;
using Dawning.Agents.Core.HumanLoop;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Dawning.Agents.Tests.HumanLoop;

public class HumanLoopServiceCollectionExtensionsTests
{
    [Fact]
    public void AddHumanLoop_ShouldRegisterAutoApprovalHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddHumanLoop();
        var provider = services.BuildServiceProvider();

        // Assert
        var handler = provider.GetRequiredService<IHumanInteractionHandler>();
        handler.Should().NotBeNull();
        handler.Should().BeOfType<AutoApprovalHandler>();
    }

    [Fact]
    public void AddHumanLoop_ShouldRegisterApprovalWorkflow()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddHumanLoop();
        var provider = services.BuildServiceProvider();

        // Assert
        var workflow = provider.GetRequiredService<ApprovalWorkflow>();
        workflow.Should().NotBeNull();
    }

    [Fact]
    public void AddHumanLoop_WithConfigure_ShouldApplyConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddHumanLoop(options =>
        {
            options.ConfirmBeforeExecution = true;
            options.MaxRetries = 5;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<HumanLoopOptions>>()
            .Value;
        options.ConfirmBeforeExecution.Should().BeTrue();
        options.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void AddAutoApprovalHandler_ShouldRegisterHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAutoApprovalHandler();
        var provider = services.BuildServiceProvider();

        // Assert
        var handler = provider.GetRequiredService<IHumanInteractionHandler>();
        handler.Should().BeOfType<AutoApprovalHandler>();
    }

    [Fact]
    public void AddAsyncCallbackHandler_ShouldRegisterHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAsyncCallbackHandler();
        var provider = services.BuildServiceProvider();

        // Assert
        var handler = provider.GetRequiredService<IHumanInteractionHandler>();
        handler.Should().BeOfType<AsyncCallbackHandler>();

        // Also verify the concrete type is available
        var asyncHandler = provider.GetRequiredService<AsyncCallbackHandler>();
        asyncHandler.Should().NotBeNull();
    }

    [Fact]
    public void AddApprovalWorkflow_ShouldRegisterWorkflow()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoApprovalHandler();

        // Act
        services.AddApprovalWorkflow(config =>
        {
            config.RequireApprovalForLowRisk = true;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var workflow = provider.GetRequiredService<ApprovalWorkflow>();
        workflow.Should().NotBeNull();
    }

    [Fact]
    public void AddHumanLoop_ShouldBeSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHumanLoop();
        var provider = services.BuildServiceProvider();

        // Act
        var handler1 = provider.GetRequiredService<IHumanInteractionHandler>();
        var handler2 = provider.GetRequiredService<IHumanInteractionHandler>();

        // Assert
        handler1.Should().BeSameAs(handler2);
    }

    [Fact]
    public void AddHumanLoop_ShouldNotReplaceExistingHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAsyncCallbackHandler(); // Register async handler first

        // Act
        services.AddHumanLoop(); // This should not replace it
        var provider = services.BuildServiceProvider();

        // Assert
        var handler = provider.GetRequiredService<IHumanInteractionHandler>();
        handler.Should().BeOfType<AsyncCallbackHandler>();
    }
}
