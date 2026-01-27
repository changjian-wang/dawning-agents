using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dawning.Agents.Abstractions.Discovery;
using Dawning.Agents.Abstractions.Scaling;
using Dawning.Agents.Core.Scaling;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Dawning.Agents.Tests.Scaling;

public class DistributedLoadBalancerTests : IDisposable
{
    private readonly DistributedLoadBalancer _balancer;

    public DistributedLoadBalancerTests()
    {
        var options = Options.Create(new DistributedLoadBalancerOptions
        {
            Strategy = LoadBalancingStrategy.RoundRobin,
            VirtualNodeCount = 150,
            FailoverRetries = 3,
        });
        _balancer = new DistributedLoadBalancer(options: options);
    }

    public void Dispose()
    {
        _balancer.Dispose();
    }

    private static AgentInstance CreateInstance(
        string id,
        bool isHealthy = true,
        int weight = 100,
        int activeRequests = 0)
    {
        return new AgentInstance
        {
            Id = id,
            ServiceName = "test-service",
            Endpoint = $"http://localhost:{8080 + int.Parse(id)}",
            IsHealthy = isHealthy,
            Weight = weight,
            ActiveRequests = activeRequests,
        };
    }

    [Fact]
    public void RegisterInstance_ShouldAddInstance()
    {
        // Arrange
        var instance = CreateInstance("1");

        // Act
        _balancer.RegisterInstance(instance);

        // Assert
        _balancer.GetAllInstances().Should().HaveCount(1);
        _balancer.HealthyInstanceCount.Should().Be(1);
    }

    [Fact]
    public void RegisterInstance_SameId_ShouldUpdate()
    {
        // Arrange
        var instance1 = CreateInstance("1");
        var instance2 = new AgentInstance
        {
            Id = "1",
            ServiceName = "test-service",
            Endpoint = "http://new-endpoint:9090",
            IsHealthy = true,
            Weight = 100,
        };

        // Act
        _balancer.RegisterInstance(instance1);
        _balancer.RegisterInstance(instance2);

        // Assert
        var instances = _balancer.GetAllInstances();
        instances.Should().HaveCount(1);
        instances[0].Endpoint.Should().Be("http://new-endpoint:9090");
    }

    [Fact]
    public void RegisterInstance_WithNullInstance_ShouldThrow()
    {
        // Act & Assert
        var act = () => _balancer.RegisterInstance(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UnregisterInstance_ShouldRemoveInstance()
    {
        // Arrange
        var instance = CreateInstance("1");
        _balancer.RegisterInstance(instance);

        // Act
        _balancer.UnregisterInstance("1");

        // Assert
        _balancer.GetAllInstances().Should().BeEmpty();
    }

    [Fact]
    public void UnregisterInstance_NonExistent_ShouldNotThrow()
    {
        // Act & Assert
        var act = () => _balancer.UnregisterInstance("non-existent");
        act.Should().NotThrow();
    }

    [Fact]
    public void GetNextInstance_NoInstances_ShouldReturnNull()
    {
        // Act
        var result = _balancer.GetNextInstance();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetNextInstance_AllUnhealthy_ShouldReturnNull()
    {
        // Arrange
        _balancer.RegisterInstance(CreateInstance("1", isHealthy: false));
        _balancer.RegisterInstance(CreateInstance("2", isHealthy: false));

        // Act
        var result = _balancer.GetNextInstance();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetNextInstance_RoundRobin_ShouldCycle()
    {
        // Arrange
        _balancer.RegisterInstance(CreateInstance("1"));
        _balancer.RegisterInstance(CreateInstance("2"));
        _balancer.RegisterInstance(CreateInstance("3"));

        // Act
        var results = new List<string>();
        for (int i = 0; i < 6; i++)
        {
            var instance = _balancer.GetNextInstance();
            results.Add(instance!.Id);
        }

        // Assert - Should see each instance at least once
        results.Should().Contain("1");
        results.Should().Contain("2");
        results.Should().Contain("3");
    }

    [Fact]
    public void GetLeastLoadedInstance_ShouldReturnLowestActiveRequests()
    {
        // Arrange
        _balancer.RegisterInstance(CreateInstance("1", activeRequests: 10));
        _balancer.RegisterInstance(CreateInstance("2", activeRequests: 5));
        _balancer.RegisterInstance(CreateInstance("3", activeRequests: 15));

        // Act
        var result = _balancer.GetLeastLoadedInstance();

        // Assert
        result!.Id.Should().Be("2");
    }

    [Fact]
    public void GetLeastLoadedInstance_OnlyHealthy_ShouldExcludeUnhealthy()
    {
        // Arrange
        _balancer.RegisterInstance(CreateInstance("1", activeRequests: 10));
        _balancer.RegisterInstance(CreateInstance("2", isHealthy: false, activeRequests: 1));

        // Act
        var result = _balancer.GetLeastLoadedInstance();

        // Assert
        result!.Id.Should().Be("1");
    }

    [Fact]
    public void UpdateInstanceHealth_ShouldChangeHealth()
    {
        // Arrange
        _balancer.RegisterInstance(CreateInstance("1", isHealthy: true));

        // Act
        _balancer.UpdateInstanceHealth("1", false);

        // Assert
        _balancer.HealthyInstanceCount.Should().Be(0);
    }

    [Fact]
    public void UpdateInstanceLoad_ShouldChangeActiveRequests()
    {
        // Arrange
        _balancer.RegisterInstance(CreateInstance("1", activeRequests: 0));

        // Act
        _balancer.UpdateInstanceLoad("1", 50);

        // Assert
        _balancer.GetAllInstances()[0].ActiveRequests.Should().Be(50);
    }

    [Fact]
    public void HealthyInstanceCount_ShouldOnlyCountHealthy()
    {
        // Arrange
        _balancer.RegisterInstance(CreateInstance("1", isHealthy: true));
        _balancer.RegisterInstance(CreateInstance("2", isHealthy: false));
        _balancer.RegisterInstance(CreateInstance("3", isHealthy: true));

        // Assert
        _balancer.HealthyInstanceCount.Should().Be(2);
    }

    [Fact]
    public void GetInstance_WithSessionKey_ConsistentHash_ShouldReturnSameInstance()
    {
        // Arrange
        var options = Options.Create(new DistributedLoadBalancerOptions
        {
            Strategy = LoadBalancingStrategy.ConsistentHash,
            VirtualNodeCount = 150,
        });
        using var balancer = new DistributedLoadBalancer(options: options);

        balancer.RegisterInstance(CreateInstance("1"));
        balancer.RegisterInstance(CreateInstance("2"));
        balancer.RegisterInstance(CreateInstance("3"));

        // Act - Same key should return same instance
        var sessionKey = "user-session-12345";
        var results = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            var instance = balancer.GetInstance(sessionKey);
            results.Add(instance!.Id);
        }

        // Assert - All results should be the same
        results.Distinct().Should().HaveCount(1);
    }

    [Fact]
    public void GetInstance_ConsistentHash_NullKey_ShouldFallbackToRoundRobin()
    {
        // Arrange
        var options = Options.Create(new DistributedLoadBalancerOptions
        {
            Strategy = LoadBalancingStrategy.ConsistentHash,
        });
        using var balancer = new DistributedLoadBalancer(options: options);

        balancer.RegisterInstance(CreateInstance("1"));
        balancer.RegisterInstance(CreateInstance("2"));

        // Act
        var result = balancer.GetInstance(null);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteWithFailoverAsync_ShouldRetryOnFailure()
    {
        // Arrange
        _balancer.RegisterInstance(CreateInstance("1"));
        _balancer.RegisterInstance(CreateInstance("2"));
        _balancer.RegisterInstance(CreateInstance("3"));

        var callCount = 0;

        // Act
        var result = await _balancer.ExecuteWithFailoverAsync<string>(async instance =>
        {
            callCount++;
            if (callCount < 2)
            {
                throw new Exception("Simulated failure");
            }
            return await Task.FromResult("success");
        });

        // Assert
        result.Should().Be("success");
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteWithFailoverAsync_AllFail_ShouldThrow()
    {
        // Arrange
        _balancer.RegisterInstance(CreateInstance("1"));
        _balancer.RegisterInstance(CreateInstance("2"));
        _balancer.RegisterInstance(CreateInstance("3"));

        // Act & Assert
        var act = async () => await _balancer.ExecuteWithFailoverAsync<string>(
            _ => throw new Exception("Always fail"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*故障转移失败*");
    }

    [Fact]
    public async Task ExecuteWithFailoverAsync_NoInstances_ShouldThrow()
    {
        // Act & Assert
        var act = async () => await _balancer.ExecuteWithFailoverAsync<string>(
            _ => Task.FromResult("success"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*没有可用的健康实例*");
    }
}

public class LoadBalancingStrategyTests
{
    [Fact]
    public void LeastConnections_ShouldSelectLowest()
    {
        // Arrange
        var options = Options.Create(new DistributedLoadBalancerOptions
        {
            Strategy = LoadBalancingStrategy.LeastConnections,
        });
        using var balancer = new DistributedLoadBalancer(options: options);

        balancer.RegisterInstance(new AgentInstance
        {
            Id = "1",
            ServiceName = "test",
            Endpoint = "http://localhost:8081",
            ActiveRequests = 10,
        });
        balancer.RegisterInstance(new AgentInstance
        {
            Id = "2",
            ServiceName = "test",
            Endpoint = "http://localhost:8082",
            ActiveRequests = 2,
        });

        // Act
        var result = balancer.GetNextInstance();

        // Assert
        result!.Id.Should().Be("2");
    }

    [Fact]
    public void Random_ShouldReturnAnyInstance()
    {
        // Arrange
        var options = Options.Create(new DistributedLoadBalancerOptions
        {
            Strategy = LoadBalancingStrategy.Random,
        });
        using var balancer = new DistributedLoadBalancer(options: options);

        balancer.RegisterInstance(new AgentInstance
        {
            Id = "1",
            ServiceName = "test",
            Endpoint = "http://localhost:8081",
        });
        balancer.RegisterInstance(new AgentInstance
        {
            Id = "2",
            ServiceName = "test",
            Endpoint = "http://localhost:8082",
        });

        // Act
        var result = balancer.GetNextInstance();

        // Assert
        result.Should().NotBeNull();
        new[] { "1", "2" }.Should().Contain(result!.Id);
    }

    [Fact]
    public void WeightedRoundRobin_ShouldFavorHigherWeight()
    {
        // Arrange
        var options = Options.Create(new DistributedLoadBalancerOptions
        {
            Strategy = LoadBalancingStrategy.WeightedRoundRobin,
        });
        using var balancer = new DistributedLoadBalancer(options: options);

        // Instance 2 has 3x the weight
        balancer.RegisterInstance(new AgentInstance
        {
            Id = "1",
            ServiceName = "test",
            Endpoint = "http://localhost:8081",
            Weight = 100,
        });
        balancer.RegisterInstance(new AgentInstance
        {
            Id = "2",
            ServiceName = "test",
            Endpoint = "http://localhost:8082",
            Weight = 300,
        });

        // Act - Get many instances
        var counts = new Dictionary<string, int> { ["1"] = 0, ["2"] = 0 };
        for (int i = 0; i < 400; i++)
        {
            var instance = balancer.GetNextInstance();
            counts[instance!.Id]++;
        }

        // Assert - Instance 2 should get approximately 3x more requests
        counts["2"].Should().BeGreaterThan(counts["1"]);
    }
}

public class DistributedLoadBalancerOptionsTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new DistributedLoadBalancerOptions();

        // Assert
        options.Strategy.Should().Be(LoadBalancingStrategy.RoundRobin);
        options.VirtualNodeCount.Should().Be(150);
        options.FailoverRetries.Should().Be(3);
        options.HealthCheckTimeoutMs.Should().Be(5000);
        options.EnableSessionAffinity.Should().BeTrue();
        DistributedLoadBalancerOptions.SectionName.Should().Be("DistributedLoadBalancer");
    }
}

public class DistributedLoadBalancerServiceRegistryIntegrationTests : IDisposable
{
    private readonly Mock<IServiceRegistry> _mockRegistry;
    private readonly DistributedLoadBalancer _balancer;

    public DistributedLoadBalancerServiceRegistryIntegrationTests()
    {
        _mockRegistry = new Mock<IServiceRegistry>();
        _balancer = new DistributedLoadBalancer(serviceRegistry: _mockRegistry.Object);
    }

    public void Dispose()
    {
        _balancer.Dispose();
    }

    [Fact]
    public async Task SyncFromServiceRegistryAsync_ShouldPopulateInstances()
    {
        // Arrange
        var serviceInstances = new List<ServiceInstance>
        {
            new()
            {
                Id = "svc-1",
                ServiceName = "test-service",
                Host = "localhost",
                Port = 8081,
                Weight = 100,
                IsHealthy = true,
            },
            new()
            {
                Id = "svc-2",
                ServiceName = "test-service",
                Host = "localhost",
                Port = 8082,
                Weight = 200,
                IsHealthy = true,
            },
        };

        _mockRegistry
            .Setup(r => r.GetInstancesAsync("test-service", It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceInstances);

        // Act
        await _balancer.SyncFromServiceRegistryAsync("test-service");

        // Assert
        _balancer.GetAllInstances().Should().HaveCount(2);
        _balancer.HealthyInstanceCount.Should().Be(2);
    }

    [Fact]
    public async Task SyncFromServiceRegistryAsync_WithoutRegistry_ShouldNotThrow()
    {
        // Arrange
        using var balancerWithoutRegistry = new DistributedLoadBalancer();

        // Act & Assert
        var act = async () => await balancerWithoutRegistry.SyncFromServiceRegistryAsync("test-service");
        await act.Should().NotThrowAsync();
    }
}
