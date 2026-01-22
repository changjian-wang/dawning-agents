using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Telemetry;
using Dawning.Agents.Core.Telemetry;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Dawning.Agents.Tests.Telemetry;

public class TokenTrackingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddTokenTracking_ShouldRegisterTracker()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTokenTracking();
        var provider = services.BuildServiceProvider();

        // Assert
        var tracker = provider.GetService<ITokenUsageTracker>();
        tracker.Should().NotBeNull();
        tracker.Should().BeOfType<InMemoryTokenUsageTracker>();
    }

    [Fact]
    public void AddTokenTracking_ShouldRegisterAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTokenTracking();
        var provider = services.BuildServiceProvider();

        // Assert
        var tracker1 = provider.GetRequiredService<ITokenUsageTracker>();
        var tracker2 = provider.GetRequiredService<ITokenUsageTracker>();
        tracker1.Should().BeSameAs(tracker2);
    }

    [Fact]
    public void AddLLMProviderWithTracking_ShouldDecorateExistingProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider.Setup(p => p.Name).Returns("MockProvider");
        services.AddSingleton(mockProvider.Object);

        // Act
        services.AddLLMProviderWithTracking("TestSource");
        var provider = services.BuildServiceProvider();

        // Assert
        var tracker = provider.GetService<ITokenUsageTracker>();
        tracker.Should().NotBeNull();

        var llmProvider = provider.GetService<ILLMProvider>();
        llmProvider.Should().NotBeNull();
        llmProvider.Should().BeOfType<TokenTrackingLLMProvider>();

        var trackingProvider = (TokenTrackingLLMProvider)llmProvider!;
        trackingProvider.Source.Should().Be("TestSource");
        trackingProvider.InnerProvider.Should().BeSameAs(mockProvider.Object);
    }

    [Fact]
    public void AddLLMProviderWithTracking_ShouldShareTrackerInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider.Setup(p => p.Name).Returns("MockProvider");
        services.AddSingleton(mockProvider.Object);

        // Act
        services.AddLLMProviderWithTracking();
        var provider = services.BuildServiceProvider();

        // Assert
        var tracker = provider.GetRequiredService<ITokenUsageTracker>();
        var llmProvider = (TokenTrackingLLMProvider)provider.GetRequiredService<ILLMProvider>();

        llmProvider.Tracker.Should().BeSameAs(tracker);
    }

    [Fact]
    public void AddLLMProviderWithTracking_ShouldUseExistingTracker()
    {
        // Arrange
        var services = new ServiceCollection();
        var existingTracker = new InMemoryTokenUsageTracker();
        services.AddSingleton<ITokenUsageTracker>(existingTracker);

        var mockProvider = new Mock<ILLMProvider>();
        mockProvider.Setup(p => p.Name).Returns("MockProvider");
        services.AddSingleton(mockProvider.Object);

        // Act
        services.AddLLMProviderWithTracking();
        var provider = services.BuildServiceProvider();

        // Assert
        var tracker = provider.GetRequiredService<ITokenUsageTracker>();
        tracker.Should().BeSameAs(existingTracker);

        var llmProvider = (TokenTrackingLLMProvider)provider.GetRequiredService<ILLMProvider>();
        llmProvider.Tracker.Should().BeSameAs(existingTracker);
    }

    [Fact]
    public void AddLLMProviderWithTracking_DefaultSource_ShouldBeDefault()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider.Setup(p => p.Name).Returns("MockProvider");
        services.AddSingleton(mockProvider.Object);

        // Act
        services.AddLLMProviderWithTracking();
        var provider = services.BuildServiceProvider();

        // Assert
        var llmProvider = (TokenTrackingLLMProvider)provider.GetRequiredService<ILLMProvider>();
        llmProvider.Source.Should().Be("Default");
    }

    [Fact]
    public void AddLLMProviderWithTracking_WithoutExistingProvider_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert - should throw at decoration time
        var act = () => services.AddLLMProviderWithTracking();
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*ILLMProvider*not registered*");
    }
}
