using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Handoff;
using Dawning.Agents.Core.Handoff;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Dawning.Agents.Tests.Handoff;

public class HandoffServiceCollectionExtensionsTests
{
    [Fact]
    public void AddHandoff_ShouldRegisterHandler()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddHandoff();
        var provider = services.BuildServiceProvider();

        // Assert
        var handler = provider.GetService<IHandoffHandler>();
        handler.Should().NotBeNull();
        handler.Should().BeOfType<HandoffHandler>();
    }

    [Fact]
    public void AddHandoff_ShouldBeSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHandoff();
        var provider = services.BuildServiceProvider();

        // Act
        var handler1 = provider.GetRequiredService<IHandoffHandler>();
        var handler2 = provider.GetRequiredService<IHandoffHandler>();

        // Assert
        handler1.Should().BeSameAs(handler2);
    }

    [Fact]
    public void AddHandoff_WithConfigureOptions_ShouldApplyConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddHandoff(options =>
        {
            options.MaxHandoffDepth = 10;
            options.TimeoutSeconds = 120;
        });

        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IHandoffHandler>();

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public void AddAgentToHandoff_ShouldRegisterAgentForLaterUse()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(a => a.Name).Returns("TestAgent");

        // Act
        services.AddHandoff();
        services.AddAgentToHandoff(mockAgent.Object);
        var provider = services.BuildServiceProvider();

        provider.EnsureHandoffAgentsRegistered();

        var handler = provider.GetRequiredService<IHandoffHandler>();

        // Assert
        var agent = handler.GetAgent("TestAgent");
        agent.Should().NotBeNull();
        agent!.Name.Should().Be("TestAgent");
    }

    [Fact]
    public void AddAgentToHandoff_WithFactory_ShouldRegisterAgent()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddHandoff();
        services.AddAgentToHandoff(sp =>
        {
            var mock = new Mock<IAgent>();
            mock.Setup(a => a.Name).Returns("FactoryAgent");
            return mock.Object;
        });

        var provider = services.BuildServiceProvider();
        provider.EnsureHandoffAgentsRegistered();

        var handler = provider.GetRequiredService<IHandoffHandler>();

        // Assert
        var agent = handler.GetAgent("FactoryAgent");
        agent.Should().NotBeNull();
    }

    [Fact]
    public void EnsureHandoffAgentsRegistered_ShouldRegisterAllMarkedAgents()
    {
        // Arrange
        var services = new ServiceCollection();

        var agent1 = new Mock<IAgent>();
        agent1.Setup(a => a.Name).Returns("Agent1");

        var agent2 = new Mock<IAgent>();
        agent2.Setup(a => a.Name).Returns("Agent2");

        services.AddHandoff();
        services.AddAgentToHandoff(agent1.Object);
        services.AddAgentToHandoff(agent2.Object);

        var provider = services.BuildServiceProvider();

        // Act
        provider.EnsureHandoffAgentsRegistered();

        var handler = provider.GetRequiredService<IHandoffHandler>();

        // Assert
        handler.GetAllAgents().Should().HaveCount(2);
        handler.GetAgent("Agent1").Should().NotBeNull();
        handler.GetAgent("Agent2").Should().NotBeNull();
    }

    [Fact]
    public void EnsureHandoffAgentsRegistered_ShouldNotThrow_WhenNoHandlerRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        // Act
        var action = () => provider.EnsureHandoffAgentsRegistered();

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void AddHandoff_ShouldNotReplaceExistingHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        var customHandler = new Mock<IHandoffHandler>();
        services.AddSingleton(customHandler.Object);

        // Act
        services.AddHandoff();
        var provider = services.BuildServiceProvider();

        // Assert
        var handler = provider.GetRequiredService<IHandoffHandler>();
        handler.Should().BeSameAs(customHandler.Object);
    }
}
