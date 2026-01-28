using Dawning.Agents.Abstractions.Resilience;
using Dawning.Agents.Core.Resilience;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Tests.Resilience;

public class ResilienceServiceCollectionExtensionsTests
{
    [Fact]
    public void AddResilience_WithConfiguration_RegistersServices()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Resilience:Retry:MaxRetryAttempts"] = "5",
                    ["Resilience:Retry:BaseDelayMs"] = "2000",
                    ["Resilience:CircuitBreaker:FailureRatio"] = "0.3",
                    ["Resilience:Timeout:TimeoutSeconds"] = "60",
                }
            )
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddResilience(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var resilienceProvider = provider.GetService<IResilienceProvider>();
        resilienceProvider.Should().NotBeNull();
        resilienceProvider.Should().BeOfType<PollyResilienceProvider>();

        var options = provider.GetRequiredService<IOptions<ResilienceOptions>>().Value;
        options.Retry.MaxRetryAttempts.Should().Be(5);
        options.Retry.BaseDelayMs.Should().Be(2000);
        options.CircuitBreaker.FailureRatio.Should().Be(0.3);
        options.Timeout.TimeoutSeconds.Should().Be(60);
    }

    [Fact]
    public void AddResilience_WithDelegate_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddResilience(options =>
        {
            options.Retry.MaxRetryAttempts = 10;
            options.CircuitBreaker.Enabled = false;
        });

        var provider = services.BuildServiceProvider();

        // Assert
        var resilienceProvider = provider.GetService<IResilienceProvider>();
        resilienceProvider.Should().NotBeNull();

        var options = provider.GetRequiredService<IOptions<ResilienceOptions>>().Value;
        options.Retry.MaxRetryAttempts.Should().Be(10);
        options.CircuitBreaker.Enabled.Should().BeFalse();
    }

    [Fact]
    public void AddResilience_WithDefaults_RegistersServicesWithDefaultOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddResilience();
        var provider = services.BuildServiceProvider();

        // Assert
        var resilienceProvider = provider.GetService<IResilienceProvider>();
        resilienceProvider.Should().NotBeNull();

        var options = provider.GetRequiredService<IOptions<ResilienceOptions>>().Value;
        options.Retry.MaxRetryAttempts.Should().Be(3); // Default
        options.CircuitBreaker.Enabled.Should().BeTrue(); // Default
        options.Timeout.TimeoutSeconds.Should().Be(120); // Default
    }

    [Fact]
    public void AddResilience_CalledMultipleTimes_UsesSingleInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddResilience();
        services.AddResilience(); // Called again
        var provider = services.BuildServiceProvider();

        // Assert
        var instance1 = provider.GetService<IResilienceProvider>();
        var instance2 = provider.GetService<IResilienceProvider>();

        instance1.Should().BeSameAs(instance2);
    }
}
