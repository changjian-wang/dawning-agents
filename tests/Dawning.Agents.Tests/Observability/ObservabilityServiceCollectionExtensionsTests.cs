namespace Dawning.Agents.Tests.Observability;

using Dawning.Agents.Abstractions.Observability;
using Dawning.Agents.Core.Observability;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class ObservabilityServiceCollectionExtensionsTests
{
    [Fact]
    public void AddObservability_WithConfiguration_ShouldRegisterServices()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Telemetry:ServiceName"] = "TestService",
                    ["Telemetry:EnableLogging"] = "true",
                }
            )
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddObservability(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<TelemetryConfig>().Should().NotBeNull();
        provider.GetService<AgentTelemetry>().Should().NotBeNull();
        provider.GetService<MetricsCollector>().Should().NotBeNull();
        provider.GetService<AgentHealthCheck>().Should().NotBeNull();
    }

    [Fact]
    public void AddObservability_WithConfiguration_ShouldBindConfig()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Telemetry:ServiceName"] = "CustomService",
                    ["Telemetry:ServiceVersion"] = "2.0.0",
                    ["Telemetry:Environment"] = "production",
                }
            )
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddObservability(configuration);
        var provider = services.BuildServiceProvider();
        var config = provider.GetRequiredService<TelemetryConfig>();

        // Assert
        config.ServiceName.Should().Be("CustomService");
        config.ServiceVersion.Should().Be("2.0.0");
        config.Environment.Should().Be("production");
    }

    [Fact]
    public void AddObservability_WithAction_ShouldRegisterServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddObservability(config =>
        {
            config.ServiceName = "ActionConfiguredService";
            config.EnableMetrics = false;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var config = provider.GetRequiredService<TelemetryConfig>();
        config.ServiceName.Should().Be("ActionConfiguredService");
        config.EnableMetrics.Should().BeFalse();
    }

    [Fact]
    public void AddObservability_WithNullAction_ShouldUseDefaults()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddObservability();
        var provider = services.BuildServiceProvider();

        // Assert
        var config = provider.GetRequiredService<TelemetryConfig>();
        config.ServiceName.Should().Be("Dawning.Agents");
        config.EnableLogging.Should().BeTrue();
    }

    [Fact]
    public void AddObservability_ShouldRegisterAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddObservability();
        var provider = services.BuildServiceProvider();

        // Act
        var telemetry1 = provider.GetRequiredService<AgentTelemetry>();
        var telemetry2 = provider.GetRequiredService<AgentTelemetry>();

        // Assert
        telemetry1.Should().BeSameAs(telemetry2);
    }

    [Fact]
    public void AddObservability_ShouldNotOverrideExisting()
    {
        // Arrange
        var services = new ServiceCollection();
        var customConfig = new TelemetryConfig { ServiceName = "PreRegistered" };
        services.AddSingleton(customConfig);

        // Act
        services.AddObservability();
        var provider = services.BuildServiceProvider();
        var config = provider.GetRequiredService<TelemetryConfig>();

        // Assert
        config.ServiceName.Should().Be("PreRegistered");
    }

    [Fact]
    public void AddHealthCheckProvider_ShouldRegisterProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddObservability();

        // Act
        services.AddHealthCheckProvider<TestHealthCheckProvider>();
        var provider = services.BuildServiceProvider();
        var providers = provider.GetServices<IHealthCheckProvider>();

        // Assert
        providers.Should().HaveCount(1);
        providers.First().Should().BeOfType<TestHealthCheckProvider>();
    }

    [Fact]
    public void AddHealthCheckProvider_MultipleTimes_ShouldRegisterAll()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddObservability();

        // Act
        services.AddHealthCheckProvider<TestHealthCheckProvider>();
        services.AddHealthCheckProvider<AnotherHealthCheckProvider>();
        var provider = services.BuildServiceProvider();
        var providers = provider.GetServices<IHealthCheckProvider>();

        // Assert
        providers.Should().HaveCount(2);
    }

    private class TestHealthCheckProvider : IHealthCheckProvider
    {
        public string Name => "Test";

        public Task<ComponentHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new ComponentHealth { Name = "Test", Status = HealthStatus.Healthy }
            );
        }
    }

    private class AnotherHealthCheckProvider : IHealthCheckProvider
    {
        public string Name => "Another";

        public Task<ComponentHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new ComponentHealth { Name = "Another", Status = HealthStatus.Healthy }
            );
        }
    }
}
