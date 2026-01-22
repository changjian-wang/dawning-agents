namespace Dawning.Agents.Tests.Scaling;

using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Scaling;
using FluentAssertions;

public class ScalingComponentsInterfaceTests
{
    [Fact]
    public void AgentWorkItem_CanBeCreated()
    {
        var tcs = new TaskCompletionSource<AgentResponse>();

        var item = new AgentWorkItem
        {
            Input = "Test input",
            CompletionSource = tcs,
            Priority = 1,
        };

        item.Id.Should().NotBeNullOrEmpty();
        item.Input.Should().Be("Test input");
        item.CompletionSource.Task.Should().BeSameAs(tcs.Task);
        item.Priority.Should().Be(1);
        item.EnqueuedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void AgentWorkItem_DefaultPriority_IsZero()
    {
        var item = new AgentWorkItem
        {
            Input = "Test",
            CompletionSource = new TaskCompletionSource<AgentResponse>(),
        };

        item.Priority.Should().Be(0);
    }

    [Fact]
    public void AgentInstance_CanBeCreated()
    {
        var mockAgent = new MockAgent();

        var instance = new AgentInstance { Agent = mockAgent, Endpoint = "http://localhost:8080" };

        instance.Id.Should().NotBeNullOrEmpty();
        instance.Agent.Should().Be(mockAgent);
        instance.Endpoint.Should().Be("http://localhost:8080");
        instance.IsHealthy.Should().BeTrue();
        instance.ActiveRequests.Should().Be(0);
        instance.Tags.Should().BeEmpty();
    }

    [Fact]
    public void AgentInstance_IsHealthy_CanBeModified()
    {
        var instance = new AgentInstance { Agent = new MockAgent() };

        instance.IsHealthy = false;

        instance.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public void AgentInstance_ActiveRequests_CanBeModified()
    {
        var instance = new AgentInstance { Agent = new MockAgent() };

        instance.ActiveRequests = 5;

        instance.ActiveRequests.Should().Be(5);
    }

    [Fact]
    public void AgentInstance_Tags_CanBeInitialized()
    {
        var instance = new AgentInstance
        {
            Agent = new MockAgent(),
            Tags = new Dictionary<string, string> { ["region"] = "east", ["tier"] = "premium" },
        };

        instance.Tags.Should().HaveCount(2);
        instance.Tags["region"].Should().Be("east");
        instance.Tags["tier"].Should().Be("premium");
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
