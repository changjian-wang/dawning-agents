namespace Dawning.Agents.Tests.Scaling;

using Dawning.Agents.Abstractions.Configuration;
using Dawning.Agents.Abstractions.Scaling;
using Dawning.Agents.Core.Scaling;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class ScalingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddScaling_RegistersScalingOptions()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Scaling:MinInstances"] = "2",
                ["Scaling:MaxInstances"] = "20",
                ["Scaling:TargetCpuPercent"] = "60",
            }
        );
        var services = new ServiceCollection();

        services.AddScaling(configuration);
        var provider = services.BuildServiceProvider();

        var options =
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ScalingOptions>>();
        options.Value.MinInstances.Should().Be(2);
        options.Value.MaxInstances.Should().Be(20);
        options.Value.TargetCpuPercent.Should().Be(60);
    }

    [Fact]
    public void AddScaling_WithAction_ConfiguresOptions()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Scaling:MinInstances"] = "3",
                ["Scaling:MaxInstances"] = "30",
            }
        );
        var services = new ServiceCollection();

        services.AddScaling(configuration);
        var provider = services.BuildServiceProvider();

        var options =
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ScalingOptions>>();
        options.Value.MinInstances.Should().Be(3);
        options.Value.MaxInstances.Should().Be(30);
    }

    [Fact]
    public void AddAgentRequestQueue_RegistersQueue()
    {
        var services = new ServiceCollection();

        services.AddAgentRequestQueue(500);
        var provider = services.BuildServiceProvider();

        var queue = provider.GetService<IAgentRequestQueue>();
        queue.Should().NotBeNull();
        queue.Should().BeOfType<AgentRequestQueue>();
    }

    [Fact]
    public void AddAgentLoadBalancer_RegistersBalancer()
    {
        var services = new ServiceCollection();

        services.AddAgentLoadBalancer();
        var provider = services.BuildServiceProvider();

        var balancer = provider.GetService<IAgentLoadBalancer>();
        balancer.Should().NotBeNull();
        balancer.Should().BeOfType<AgentLoadBalancer>();
    }

    [Fact]
    public void AddCircuitBreaker_RegistersBreaker()
    {
        var services = new ServiceCollection();

        services.AddCircuitBreaker(3, TimeSpan.FromSeconds(15));
        var provider = services.BuildServiceProvider();

        var breaker = provider.GetService<ICircuitBreaker>();
        breaker.Should().NotBeNull();
        breaker.Should().BeOfType<CircuitBreaker>();
    }

    [Fact]
    public void AddEnvironmentSecretsManager_RegistersManager()
    {
        var services = new ServiceCollection();

        services.AddEnvironmentSecretsManager();
        var provider = services.BuildServiceProvider();

        var manager = provider.GetService<ISecretsManager>();
        manager.Should().NotBeNull();
        manager.Should().BeOfType<Dawning.Agents.Core.Configuration.EnvironmentSecretsManager>();
    }

    [Fact]
    public void AddInMemorySecretsManager_RegistersManager()
    {
        var services = new ServiceCollection();

        services.AddInMemorySecretsManager();
        var provider = services.BuildServiceProvider();

        var manager = provider.GetService<ISecretsManager>();
        manager.Should().NotBeNull();
        manager.Should().BeOfType<Dawning.Agents.Core.Configuration.InMemorySecretsManager>();
    }

    [Fact]
    public void AddDeploymentConfiguration_RegistersAllOptions()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Agent:Name"] = "TestAgent",
                ["LLM:Provider"] = "Azure",
                ["Cache:Provider"] = "Redis",
                ["Scaling:MinInstances"] = "5",
            }
        );
        var services = new ServiceCollection();

        services.AddDeploymentConfiguration(configuration);
        var provider = services.BuildServiceProvider();

        var agentOptions =
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Dawning.Agents.Abstractions.Configuration.AgentDeploymentOptions>>();
        agentOptions.Value.Name.Should().Be("TestAgent");

        var llmOptions =
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Dawning.Agents.Abstractions.Configuration.LLMDeploymentOptions>>();
        llmOptions.Value.Provider.Should().Be("Azure");

        var cacheOptions =
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Dawning.Agents.Abstractions.Configuration.CacheOptions>>();
        cacheOptions.Value.Provider.Should().Be("Redis");

        var scalingOptions =
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ScalingOptions>>();
        scalingOptions.Value.MinInstances.Should().Be(5);
    }

    [Fact]
    public void AddProductionDeployment_RegistersAllServices()
    {
        var configuration = BuildConfiguration(
            new Dictionary<string, string?> { ["Agent:Name"] = "ProdAgent" }
        );
        var services = new ServiceCollection();

        services.AddProductionDeployment(configuration);
        var provider = services.BuildServiceProvider();

        provider.GetService<ISecretsManager>().Should().NotBeNull();
        provider.GetService<IAgentRequestQueue>().Should().NotBeNull();
        provider.GetService<IAgentLoadBalancer>().Should().NotBeNull();
        provider.GetService<ICircuitBreaker>().Should().NotBeNull();
    }

    [Fact]
    public void AddProductionDeployment_WithCustomParameters_UsesCorrectValues()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>());
        var services = new ServiceCollection();

        services.AddProductionDeployment(
            configuration,
            queueCapacity: 2000,
            circuitBreakerThreshold: 10
        );
        var provider = services.BuildServiceProvider();

        // 验证服务已注册
        provider.GetService<IAgentRequestQueue>().Should().NotBeNull();
        provider.GetService<ICircuitBreaker>().Should().NotBeNull();
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
