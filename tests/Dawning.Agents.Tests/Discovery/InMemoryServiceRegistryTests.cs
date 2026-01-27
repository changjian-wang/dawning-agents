using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dawning.Agents.Abstractions.Discovery;
using Dawning.Agents.Core.Discovery;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dawning.Agents.Tests.Discovery;

public class InMemoryServiceRegistryTests : IDisposable
{
    private readonly InMemoryServiceRegistry _registry;

    public InMemoryServiceRegistryTests()
    {
        var options = Options.Create(new ServiceRegistryOptions
        {
            HeartbeatIntervalSeconds = 10,
            ServiceExpireSeconds = 30,
            HealthCheckIntervalSeconds = 5,
        });
        _registry = new InMemoryServiceRegistry(options);
    }

    public void Dispose()
    {
        _registry.Dispose();
    }

    private static ServiceInstance CreateInstance(
        string serviceName = "test-service",
        string? id = null,
        string host = "localhost",
        int port = 8080)
    {
        return new ServiceInstance
        {
            Id = id ?? Guid.NewGuid().ToString(),
            ServiceName = serviceName,
            Host = host,
            Port = port,
            Weight = 100,
        };
    }

    [Fact]
    public async Task RegisterAsync_ShouldAddInstance()
    {
        // Arrange
        var instance = CreateInstance();

        // Act
        await _registry.RegisterAsync(instance);
        var instances = await _registry.GetInstancesAsync("test-service");

        // Assert
        instances.Should().HaveCount(1);
        instances[0].Id.Should().Be(instance.Id);
    }

    [Fact]
    public async Task RegisterAsync_MultipleInstances_ShouldAddAll()
    {
        // Arrange
        var instance1 = CreateInstance(id: "instance-1", port: 8081);
        var instance2 = CreateInstance(id: "instance-2", port: 8082);

        // Act
        await _registry.RegisterAsync(instance1);
        await _registry.RegisterAsync(instance2);
        var instances = await _registry.GetInstancesAsync("test-service");

        // Assert
        instances.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeregisterAsync_ShouldRemoveInstance()
    {
        // Arrange
        var instance = CreateInstance();
        await _registry.RegisterAsync(instance);

        // Act
        await _registry.DeregisterAsync(instance.Id);
        var instances = await _registry.GetInstancesAsync("test-service");

        // Assert
        instances.Should().BeEmpty();
    }

    [Fact]
    public async Task DeregisterAsync_NonExistent_ShouldNotThrow()
    {
        // Act & Assert
        var act = async () => await _registry.DeregisterAsync("non-existent-id");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HeartbeatAsync_ShouldUpdateLastHeartbeat()
    {
        // Arrange
        var instance = CreateInstance();
        await _registry.RegisterAsync(instance);
        var originalTime = instance.LastHeartbeat;
        await Task.Delay(10); // Small delay to ensure time difference

        // Act
        await _registry.HeartbeatAsync(instance.Id);

        // Assert
        var instances = await _registry.GetInstancesAsync("test-service");
        instances[0].LastHeartbeat.Should().BeOnOrAfter(originalTime);
    }

    [Fact]
    public async Task HeartbeatAsync_NonExistent_ShouldNotThrow()
    {
        // Act & Assert
        var act = async () => await _registry.HeartbeatAsync("non-existent-id");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetInstancesAsync_ShouldOnlyReturnHealthyInstances()
    {
        // Arrange
        var healthy = CreateInstance(id: "healthy-1");
        var unhealthy = CreateInstance(id: "unhealthy-1");
        unhealthy.IsHealthy = false;

        await _registry.RegisterAsync(healthy);
        await _registry.RegisterAsync(unhealthy);

        // Act
        var instances = await _registry.GetInstancesAsync("test-service");

        // Assert
        instances.Should().HaveCount(1);
        instances[0].Id.Should().Be("healthy-1");
    }

    [Fact]
    public async Task GetInstancesAsync_DifferentServices_ShouldFilterByName()
    {
        // Arrange
        var service1 = CreateInstance(serviceName: "service-a");
        var service2 = CreateInstance(serviceName: "service-b");

        await _registry.RegisterAsync(service1);
        await _registry.RegisterAsync(service2);

        // Act
        var instancesA = await _registry.GetInstancesAsync("service-a");
        var instancesB = await _registry.GetInstancesAsync("service-b");

        // Assert
        instancesA.Should().HaveCount(1);
        instancesA[0].ServiceName.Should().Be("service-a");
        instancesB.Should().HaveCount(1);
        instancesB[0].ServiceName.Should().Be("service-b");
    }

    [Fact]
    public async Task GetServicesAsync_ShouldReturnDistinctServiceNames()
    {
        // Arrange
        await _registry.RegisterAsync(CreateInstance(serviceName: "service-a", id: "a1"));
        await _registry.RegisterAsync(CreateInstance(serviceName: "service-a", id: "a2"));
        await _registry.RegisterAsync(CreateInstance(serviceName: "service-b", id: "b1"));

        // Act
        var services = await _registry.GetServicesAsync();

        // Assert
        services.Should().HaveCount(2);
        services.Should().Contain("service-a");
        services.Should().Contain("service-b");
    }

    [Fact]
    public async Task GetServicesAsync_NoServices_ShouldReturnEmpty()
    {
        // Act
        var services = await _registry.GetServicesAsync();

        // Assert
        services.Should().BeEmpty();
    }

    [Fact]
    public async Task WatchAsync_ShouldReturnInitialState()
    {
        // Arrange
        var instance = CreateInstance();
        await _registry.RegisterAsync(instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        // Act
        var firstBatch = new List<ServiceInstance>();
        await foreach (var instances in _registry.WatchAsync("test-service", cts.Token))
        {
            firstBatch.AddRange(instances);
            break; // Get only the first batch
        }

        // Assert
        firstBatch.Should().HaveCount(1);
        firstBatch[0].Id.Should().Be(instance.Id);
    }

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var registry = new InMemoryServiceRegistry();

        // Act & Assert
        var act = () => registry.Dispose();
        act.Should().NotThrow();

        // Double dispose should not throw
        act.Should().NotThrow();
    }
}

public class ServiceInstanceTests
{
    [Fact]
    public void GetUri_WithHttpScheme_ShouldReturnCorrectUri()
    {
        // Arrange
        var instance = new ServiceInstance
        {
            Id = "test-id",
            ServiceName = "test-service",
            Host = "localhost",
            Port = 8080,
        };

        // Act
        var uri = instance.GetUri("http");

        // Assert
        uri.Should().Be(new Uri("http://localhost:8080"));
    }

    [Fact]
    public void GetUri_WithHttpsScheme_ShouldReturnCorrectUri()
    {
        // Arrange
        var instance = new ServiceInstance
        {
            Id = "test-id",
            ServiceName = "test-service",
            Host = "api.example.com",
            Port = 443,
        };

        // Act
        var uri = instance.GetUri("https");

        // Assert
        uri.Should().Be(new Uri("https://api.example.com:443"));
    }

    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var instance = new ServiceInstance
        {
            Id = "id",
            ServiceName = "svc",
            Host = "localhost",
            Port = 80,
        };

        // Assert
        instance.Weight.Should().Be(100);
        instance.Tags.Should().BeEmpty();
        instance.HealthCheckUrl.Should().BeNull();
        instance.IsHealthy.Should().BeTrue();
        instance.RegisteredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        instance.LastHeartbeat.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Tags_ShouldBeSettable()
    {
        // Arrange & Act
        var instance = new ServiceInstance
        {
            Id = "id",
            ServiceName = "svc",
            Host = "localhost",
            Port = 80,
            Tags = new Dictionary<string, string>
            {
                ["env"] = "production",
                ["version"] = "1.0.0",
            },
        };

        // Assert
        instance.Tags.Should().HaveCount(2);
        instance.Tags["env"].Should().Be("production");
        instance.Tags["version"].Should().Be("1.0.0");
    }
}

public class ServiceRegistryOptionsTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new ServiceRegistryOptions();

        // Assert
        options.HeartbeatIntervalSeconds.Should().Be(10);
        ServiceRegistryOptions.SectionName.Should().Be("ServiceRegistry");
    }
}
