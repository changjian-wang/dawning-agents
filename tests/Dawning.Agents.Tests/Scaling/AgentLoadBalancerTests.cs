namespace Dawning.Agents.Tests.Scaling;

using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Scaling;
using Dawning.Agents.Core.Scaling;
using FluentAssertions;

public class AgentLoadBalancerTests
{
    [Fact]
    public void RegisterInstance_AddsInstance()
    {
        var balancer = new AgentLoadBalancer();
        var instance = CreateInstance();

        balancer.RegisterInstance(instance);

        balancer.TotalInstanceCount.Should().Be(1);
        balancer.HealthyInstanceCount.Should().Be(1);
    }

    [Fact]
    public void RegisterInstance_UpdatesExistingInstance()
    {
        var balancer = new AgentLoadBalancer();
        var instance = CreateInstance();
        instance.IsHealthy = true;

        balancer.RegisterInstance(instance);

        // 更新同一实例
        var updated = new AgentInstance
        {
            Id = instance.Id,
            Agent = instance.Agent,
            IsHealthy = false,
        };
        balancer.RegisterInstance(updated);

        balancer.TotalInstanceCount.Should().Be(1);
        balancer.HealthyInstanceCount.Should().Be(0);
    }

    [Fact]
    public void UnregisterInstance_RemovesInstance()
    {
        var balancer = new AgentLoadBalancer();
        var instance = CreateInstance();
        balancer.RegisterInstance(instance);

        balancer.UnregisterInstance(instance.Id);

        balancer.TotalInstanceCount.Should().Be(0);
    }

    [Fact]
    public void GetNextInstance_ReturnsHealthyInstance()
    {
        var balancer = new AgentLoadBalancer();
        var instance = CreateInstance();
        balancer.RegisterInstance(instance);

        var result = balancer.GetNextInstance();

        result.Should().NotBeNull();
        result!.Id.Should().Be(instance.Id);
    }

    [Fact]
    public void GetNextInstance_SkipsUnhealthyInstances()
    {
        var balancer = new AgentLoadBalancer();
        var unhealthy = CreateInstance();
        unhealthy.IsHealthy = false;
        var healthy = CreateInstance();

        balancer.RegisterInstance(unhealthy);
        balancer.RegisterInstance(healthy);

        var result = balancer.GetNextInstance();

        result.Should().NotBeNull();
        result!.Id.Should().Be(healthy.Id);
    }

    [Fact]
    public void GetNextInstance_ReturnsNullWhenNoHealthyInstances()
    {
        var balancer = new AgentLoadBalancer();
        var instance = CreateInstance();
        instance.IsHealthy = false;
        balancer.RegisterInstance(instance);

        var result = balancer.GetNextInstance();

        result.Should().BeNull();
    }

    [Fact]
    public void GetNextInstance_RoundRobinsAcrossInstances()
    {
        var balancer = new AgentLoadBalancer();
        var instance1 = CreateInstance();
        var instance2 = CreateInstance();
        balancer.RegisterInstance(instance1);
        balancer.RegisterInstance(instance2);

        var results = new List<string>();
        for (int i = 0; i < 4; i++)
        {
            var result = balancer.GetNextInstance();
            results.Add(result!.Id);
        }

        // 应该轮询
        results.Should().Contain(instance1.Id);
        results.Should().Contain(instance2.Id);
    }

    [Fact]
    public void GetLeastLoadedInstance_ReturnsInstanceWithFewestRequests()
    {
        var balancer = new AgentLoadBalancer();
        var loaded = CreateInstance();
        loaded.ActiveRequests = 10;
        var light = CreateInstance();
        light.ActiveRequests = 2;

        balancer.RegisterInstance(loaded);
        balancer.RegisterInstance(light);

        var result = balancer.GetLeastLoadedInstance();

        result.Should().NotBeNull();
        result!.Id.Should().Be(light.Id);
    }

    [Fact]
    public void GetAllInstances_ReturnsAllInstances()
    {
        var balancer = new AgentLoadBalancer();
        var instance1 = CreateInstance();
        var instance2 = CreateInstance();
        instance2.IsHealthy = false;

        balancer.RegisterInstance(instance1);
        balancer.RegisterInstance(instance2);

        var all = balancer.GetAllInstances();

        all.Should().HaveCount(2);
    }

    [Fact]
    public void UpdateInstanceHealth_UpdatesHealthStatus()
    {
        var balancer = new AgentLoadBalancer();
        var instance = CreateInstance();
        balancer.RegisterInstance(instance);

        balancer.UpdateInstanceHealth(instance.Id, false);

        balancer.HealthyInstanceCount.Should().Be(0);
    }

    [Fact]
    public void UpdateInstanceLoad_UpdatesActiveRequests()
    {
        var balancer = new AgentLoadBalancer();
        var instance = CreateInstance();
        balancer.RegisterInstance(instance);

        balancer.UpdateInstanceLoad(instance.Id, 5);

        var retrieved = balancer.GetAllInstances().First(i => i.Id == instance.Id);
        retrieved.ActiveRequests.Should().Be(5);
    }

    private static AgentInstance CreateInstance()
    {
        return new AgentInstance { Agent = new MockAgent() };
    }

    private class MockAgent : IAgent
    {
        public string Name => "MockAgent";
        public string Instructions => "Mock instructions";

        public Task<AgentResponse> RunAsync(
            string input,
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult(AgentResponse.Successful("Mock response", [], TimeSpan.Zero));
        }

        public Task<AgentResponse> RunAsync(
            AgentContext context,
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult(AgentResponse.Successful("Mock response", [], TimeSpan.Zero));
        }
    }
}
