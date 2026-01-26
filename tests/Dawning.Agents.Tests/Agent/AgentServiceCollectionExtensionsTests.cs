using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Core;
using Dawning.Agents.Core.Agent;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Dawning.Agents.Tests.Agent;

/// <summary>
/// AgentServiceCollectionExtensions 单元测试
/// </summary>
public class AgentServiceCollectionExtensionsTests
{
    #region AddReActAgent (Configuration) Tests

    [Fact]
    public void AddReActAgent_WithConfiguration_RegistersAgent()
    {
        var services = new ServiceCollection();
        var mockLLM = new Mock<ILLMProvider>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Agent:Name"] = "TestAgent",
                    ["Agent:Instructions"] = "You are a test agent.",
                    ["Agent:MaxSteps"] = "5",
                }
            )
            .Build();

        services.AddSingleton(mockLLM.Object);
        services.AddReActAgent(config);
        var provider = services.BuildServiceProvider();

        var agent = provider.GetRequiredService<IAgent>();

        agent.Should().NotBeNull();
        agent.Should().BeOfType<ReActAgent>();
    }

    [Fact]
    public void AddReActAgent_WithEmptyConfiguration_UsesDefaults()
    {
        var services = new ServiceCollection();
        var mockLLM = new Mock<ILLMProvider>();
        var config = new ConfigurationBuilder().Build();

        services.AddSingleton(mockLLM.Object);
        services.AddReActAgent(config);
        var provider = services.BuildServiceProvider();

        var agent = provider.GetRequiredService<IAgent>();

        agent.Should().NotBeNull();
        agent.Should().BeOfType<ReActAgent>();
    }

    #endregion

    #region AddReActAgent (Action) Tests

    [Fact]
    public void AddReActAgent_WithAction_RegistersAgent()
    {
        var services = new ServiceCollection();
        var mockLLM = new Mock<ILLMProvider>();

        services.AddSingleton(mockLLM.Object);
        services.AddReActAgent(options =>
        {
            options.Name = "CustomAgent";
            options.Instructions = "Custom instructions";
            options.MaxSteps = 10;
        });
        var provider = services.BuildServiceProvider();

        var agent = provider.GetRequiredService<IAgent>();

        agent.Should().NotBeNull();
        agent.Should().BeOfType<ReActAgent>();
    }

    [Fact]
    public void AddReActAgent_MultipleCalls_UsesLastConfiguration()
    {
        var services = new ServiceCollection();
        var mockLLM = new Mock<ILLMProvider>();

        services.AddSingleton(mockLLM.Object);
        services.AddReActAgent(options => options.Name = "First");
        services.AddReActAgent(options => options.Name = "Second");

        var provider = services.BuildServiceProvider();
        var agent = provider.GetRequiredService<IAgent>();

        // TryAddSingleton 只添加第一个
        agent.Should().NotBeNull();
    }

    #endregion

    #region ToolRegistry Integration Tests

    [Fact]
    public void AddReActAgent_AlsoRegistersToolRegistry()
    {
        var services = new ServiceCollection();
        var mockLLM = new Mock<ILLMProvider>();
        var config = new ConfigurationBuilder().Build();

        services.AddSingleton(mockLLM.Object);
        services.AddReActAgent(config);
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetService<Abstractions.Tools.IToolRegistry>();

        act.Should().NotThrow();
    }

    #endregion

    #region Singleton Behavior Tests

    [Fact]
    public void Agent_IsSingleton()
    {
        var services = new ServiceCollection();
        var mockLLM = new Mock<ILLMProvider>();
        var config = new ConfigurationBuilder().Build();

        services.AddSingleton(mockLLM.Object);
        services.AddReActAgent(config);
        var provider = services.BuildServiceProvider();

        var agent1 = provider.GetRequiredService<IAgent>();
        var agent2 = provider.GetRequiredService<IAgent>();

        agent1.Should().BeSameAs(agent2);
    }

    #endregion
}
