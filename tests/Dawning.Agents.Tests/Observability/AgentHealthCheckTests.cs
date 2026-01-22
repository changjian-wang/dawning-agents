namespace Dawning.Agents.Tests.Observability;

using Dawning.Agents.Abstractions.Observability;
using Dawning.Agents.Core.Observability;
using FluentAssertions;
using Moq;

public class AgentHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WithNoProviders_ShouldReturnHealthy()
    {
        // Arrange
        var healthCheck = new AgentHealthCheck();

        // Act
        var result = await healthCheck.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Components.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckHealthAsync_WithHealthyProvider_ShouldReturnHealthy()
    {
        // Arrange
        var healthCheck = new AgentHealthCheck();
        var mockProvider = new Mock<IHealthCheckProvider>();
        mockProvider.Setup(p => p.Name).Returns("TestProvider");
        mockProvider
            .Setup(p => p.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComponentHealth { Name = "TestProvider", Status = HealthStatus.Healthy });

        healthCheck.AddProvider(mockProvider.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Components.Should().HaveCount(1);
        result.Components[0].Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WithUnhealthyProvider_ShouldReturnUnhealthy()
    {
        // Arrange
        var healthCheck = new AgentHealthCheck();
        var mockProvider = new Mock<IHealthCheckProvider>();
        mockProvider.Setup(p => p.Name).Returns("TestProvider");
        mockProvider
            .Setup(p => p.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ComponentHealth
                {
                    Name = "TestProvider",
                    Status = HealthStatus.Unhealthy,
                    Message = "Service unavailable",
                }
            );

        healthCheck.AddProvider(mockProvider.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Components[0].Message.Should().Be("Service unavailable");
    }

    [Fact]
    public async Task CheckHealthAsync_WithDegradedProvider_ShouldReturnUnhealthy()
    {
        // Arrange
        var healthCheck = new AgentHealthCheck();
        var mockProvider = new Mock<IHealthCheckProvider>();
        mockProvider.Setup(p => p.Name).Returns("TestProvider");
        mockProvider
            .Setup(p => p.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ComponentHealth { Name = "TestProvider", Status = HealthStatus.Degraded }
            );

        healthCheck.AddProvider(mockProvider.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenProviderThrows_ShouldReturnUnhealthy()
    {
        // Arrange
        var healthCheck = new AgentHealthCheck();
        var mockProvider = new Mock<IHealthCheckProvider>();
        mockProvider.Setup(p => p.Name).Returns("FailingProvider");
        mockProvider
            .Setup(p => p.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection failed"));

        healthCheck.AddProvider(mockProvider.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Components.Should().HaveCount(1);
        result.Components[0].Status.Should().Be(HealthStatus.Unhealthy);
        result.Components[0].Message.Should().Be("Connection failed");
    }

    [Fact]
    public async Task CheckHealthAsync_WithMixedProviders_ShouldReturnUnhealthy()
    {
        // Arrange
        var healthCheck = new AgentHealthCheck();

        var healthyProvider = new Mock<IHealthCheckProvider>();
        healthyProvider.Setup(p => p.Name).Returns("HealthyProvider");
        healthyProvider
            .Setup(p => p.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ComponentHealth { Name = "HealthyProvider", Status = HealthStatus.Healthy }
            );

        var unhealthyProvider = new Mock<IHealthCheckProvider>();
        unhealthyProvider.Setup(p => p.Name).Returns("UnhealthyProvider");
        unhealthyProvider
            .Setup(p => p.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ComponentHealth { Name = "UnhealthyProvider", Status = HealthStatus.Unhealthy }
            );

        healthCheck.AddProvider(healthyProvider.Object);
        healthCheck.AddProvider(unhealthyProvider.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Components.Should().HaveCount(2);
    }

    [Fact]
    public void Providers_ShouldReturnAddedProviders()
    {
        // Arrange
        var healthCheck = new AgentHealthCheck();
        var mockProvider = new Mock<IHealthCheckProvider>();
        mockProvider.Setup(p => p.Name).Returns("TestProvider");

        // Act
        healthCheck.AddProvider(mockProvider.Object);

        // Assert
        healthCheck.Providers.Should().HaveCount(1);
    }

    [Fact]
    public async Task CheckHealthAsync_ShouldSetTimestamp()
    {
        // Arrange
        var healthCheck = new AgentHealthCheck();
        var before = DateTime.UtcNow;

        // Act
        var result = await healthCheck.CheckHealthAsync();
        var after = DateTime.UtcNow;

        // Assert
        result.Timestamp.Should().BeOnOrAfter(before);
        result.Timestamp.Should().BeOnOrBefore(after);
    }
}
