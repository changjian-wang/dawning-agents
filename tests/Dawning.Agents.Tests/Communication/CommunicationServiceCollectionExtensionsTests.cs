namespace Dawning.Agents.Tests.Communication;

using Dawning.Agents.Abstractions.Communication;
using Dawning.Agents.Core.Communication;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Communication DI 扩展测试
/// </summary>
public class CommunicationServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMessageBus_ShouldRegisterIMessageBus()
    {
        var services = new ServiceCollection();

        services.AddMessageBus();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetService<IMessageBus>();

        bus.Should().NotBeNull();
        bus.Should().BeOfType<InMemoryMessageBus>();
    }

    [Fact]
    public void AddMessageBus_ShouldBeSingleton()
    {
        var services = new ServiceCollection();
        services.AddMessageBus();

        var provider = services.BuildServiceProvider();
        var bus1 = provider.GetRequiredService<IMessageBus>();
        var bus2 = provider.GetRequiredService<IMessageBus>();

        bus1.Should().BeSameAs(bus2);
    }

    [Fact]
    public void AddSharedState_ShouldRegisterISharedState()
    {
        var services = new ServiceCollection();

        services.AddSharedState();

        var provider = services.BuildServiceProvider();
        var state = provider.GetService<ISharedState>();

        state.Should().NotBeNull();
        state.Should().BeOfType<InMemorySharedState>();
    }

    [Fact]
    public void AddSharedState_ShouldBeSingleton()
    {
        var services = new ServiceCollection();
        services.AddSharedState();

        var provider = services.BuildServiceProvider();
        var state1 = provider.GetRequiredService<ISharedState>();
        var state2 = provider.GetRequiredService<ISharedState>();

        state1.Should().BeSameAs(state2);
    }

    [Fact]
    public void AddCommunication_ShouldRegisterBothServices()
    {
        var services = new ServiceCollection();

        services.AddCommunication();

        var provider = services.BuildServiceProvider();

        var bus = provider.GetService<IMessageBus>();
        var state = provider.GetService<ISharedState>();

        bus.Should().NotBeNull();
        state.Should().NotBeNull();
    }

    [Fact]
    public void AddCommunication_MultipleCalls_ShouldNotDuplicate()
    {
        var services = new ServiceCollection();

        services.AddCommunication();
        services.AddCommunication();
        services.AddMessageBus();
        services.AddSharedState();

        var provider = services.BuildServiceProvider();

        // 应该只有一个实例
        var bus1 = provider.GetRequiredService<IMessageBus>();
        var bus2 = provider.GetRequiredService<IMessageBus>();
        bus1.Should().BeSameAs(bus2);
    }
}
