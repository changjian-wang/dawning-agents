namespace Dawning.Agents.Tests.Orchestration;

using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Orchestration;
using Dawning.Agents.Core.Orchestration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

/// <summary>
/// OrchestrationServiceCollectionExtensions 单元测试
/// </summary>
public class OrchestrationServiceCollectionExtensionsTests
{
    [Fact]
    public void AddOrchestration_FromConfiguration_ConfiguresOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Orchestration:MaxConcurrency"] = "10",
                    ["Orchestration:TimeoutSeconds"] = "120",
                    ["Orchestration:ContinueOnError"] = "true",
                }
            )
            .Build();

        // Act
        services.AddOrchestration(configuration);
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OrchestratorOptions>>();

        // Assert
        options.Value.MaxConcurrency.Should().Be(10);
        options.Value.TimeoutSeconds.Should().Be(120);
        options.Value.ContinueOnError.Should().BeTrue();
    }

    [Fact]
    public void AddOrchestration_FromDelegate_ConfiguresOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOrchestration(opt =>
        {
            opt.MaxConcurrency = 8;
            opt.TimeoutSeconds = 90;
        });
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OrchestratorOptions>>();

        // Assert
        options.Value.MaxConcurrency.Should().Be(8);
        options.Value.TimeoutSeconds.Should().Be(90);
    }

    [Fact]
    public void AddSequentialOrchestrator_WithConfigureAgents_RegistersOrchestrator()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(a => a.Name).Returns("TestAgent");

        // Act
        services.AddSequentialOrchestrator(
            "test-sequential",
            (sp, orch) => orch.AddAgent(mockAgent.Object)
        );
        var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<IOrchestrator>();

        // Assert
        orchestrator.Should().BeOfType<SequentialOrchestrator>();
        orchestrator.Name.Should().Be("test-sequential");
        orchestrator.Agents.Should().HaveCount(1);
    }

    [Fact]
    public void AddSequentialOrchestrator_WithAgents_RegistersOrchestrator()
    {
        // Arrange
        var services = new ServiceCollection();
        var agent1 = new Mock<IAgent>();
        agent1.Setup(a => a.Name).Returns("Agent1");
        var agent2 = new Mock<IAgent>();
        agent2.Setup(a => a.Name).Returns("Agent2");

        // Act
        services.AddSequentialOrchestrator("pipeline", agent1.Object, agent2.Object);
        var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<IOrchestrator>();

        // Assert
        orchestrator.Agents.Should().HaveCount(2);
    }

    [Fact]
    public void AddParallelOrchestrator_WithConfigureAgents_RegistersOrchestrator()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockAgent = new Mock<IAgent>();
        mockAgent.Setup(a => a.Name).Returns("TestAgent");

        // Act
        services.AddParallelOrchestrator(
            "test-parallel",
            (sp, orch) => orch.AddAgent(mockAgent.Object)
        );
        var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<IOrchestrator>();

        // Assert
        orchestrator.Should().BeOfType<ParallelOrchestrator>();
        orchestrator.Name.Should().Be("test-parallel");
    }

    [Fact]
    public void AddParallelOrchestrator_WithAgents_RegistersOrchestrator()
    {
        // Arrange
        var services = new ServiceCollection();
        var agent1 = new Mock<IAgent>();
        agent1.Setup(a => a.Name).Returns("Agent1");
        var agent2 = new Mock<IAgent>();
        agent2.Setup(a => a.Name).Returns("Agent2");

        // Act
        services.AddParallelOrchestrator("parallel", agent1.Object, agent2.Object);
        var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<IOrchestrator>();

        // Assert
        orchestrator.Agents.Should().HaveCount(2);
    }

    [Fact]
    public void AddSequentialOrchestratorWithAllAgents_RegistersWithAllAgents()
    {
        // Arrange
        var services = new ServiceCollection();
        var agent1 = new Mock<IAgent>();
        agent1.Setup(a => a.Name).Returns("Agent1");
        var agent2 = new Mock<IAgent>();
        agent2.Setup(a => a.Name).Returns("Agent2");

        services.AddSingleton<IAgent>(agent1.Object);
        services.AddSingleton<IAgent>(agent2.Object);

        // Act
        services.AddSequentialOrchestratorWithAllAgents("auto-sequential");
        var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<IOrchestrator>();

        // Assert
        orchestrator.Name.Should().Be("auto-sequential");
        orchestrator.Agents.Should().HaveCount(2);
    }

    [Fact]
    public void AddParallelOrchestratorWithAllAgents_RegistersWithAllAgents()
    {
        // Arrange
        var services = new ServiceCollection();
        var agent1 = new Mock<IAgent>();
        agent1.Setup(a => a.Name).Returns("Agent1");
        var agent2 = new Mock<IAgent>();
        agent2.Setup(a => a.Name).Returns("Agent2");

        services.AddSingleton<IAgent>(agent1.Object);
        services.AddSingleton<IAgent>(agent2.Object);

        // Act
        services.AddParallelOrchestratorWithAllAgents("auto-parallel");
        var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<IOrchestrator>();

        // Assert
        orchestrator.Name.Should().Be("auto-parallel");
        orchestrator.Agents.Should().HaveCount(2);
    }
}
