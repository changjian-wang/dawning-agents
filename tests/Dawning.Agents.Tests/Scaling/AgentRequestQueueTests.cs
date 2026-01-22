namespace Dawning.Agents.Tests.Scaling;

using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Scaling;
using Dawning.Agents.Core.Scaling;
using FluentAssertions;

public class AgentRequestQueueTests
{
    [Fact]
    public async Task EnqueueAsync_AddsItemToQueue()
    {
        var queue = new AgentRequestQueue(10);
        var item = CreateWorkItem("test");

        await queue.EnqueueAsync(item);

        queue.Count.Should().Be(1);
    }

    [Fact]
    public async Task DequeueAsync_ReturnsEnqueuedItem()
    {
        var queue = new AgentRequestQueue(10);
        var item = CreateWorkItem("test");
        await queue.EnqueueAsync(item);

        var dequeued = await queue.DequeueAsync();

        dequeued.Should().NotBeNull();
        dequeued!.Id.Should().Be(item.Id);
        dequeued.Input.Should().Be("test");
    }

    [Fact]
    public async Task DequeueAsync_MaintainsFIFOOrder()
    {
        var queue = new AgentRequestQueue(10);
        var item1 = CreateWorkItem("first");
        var item2 = CreateWorkItem("second");
        var item3 = CreateWorkItem("third");

        await queue.EnqueueAsync(item1);
        await queue.EnqueueAsync(item2);
        await queue.EnqueueAsync(item3);

        var d1 = await queue.DequeueAsync();
        var d2 = await queue.DequeueAsync();
        var d3 = await queue.DequeueAsync();

        d1!.Input.Should().Be("first");
        d2!.Input.Should().Be("second");
        d3!.Input.Should().Be("third");
    }

    [Fact]
    public async Task DequeueAsync_ReturnsNullOnCancellation()
    {
        var queue = new AgentRequestQueue(10);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await queue.DequeueAsync(cts.Token);

        result.Should().BeNull();
    }

    [Fact]
    public void Count_ReturnsCorrectCount()
    {
        var queue = new AgentRequestQueue(10);

        queue.Count.Should().Be(0);
    }

    [Fact]
    public void CanWrite_ReturnsTrueWhenNotCompleted()
    {
        var queue = new AgentRequestQueue(10);

        queue.CanWrite.Should().BeTrue();
    }

    [Fact]
    public void Complete_ClosesQueue()
    {
        var queue = new AgentRequestQueue(10);

        queue.Complete();

        queue.CanWrite.Should().BeFalse();
    }

    private static AgentWorkItem CreateWorkItem(string input)
    {
        return new AgentWorkItem
        {
            Input = input,
            CompletionSource = new TaskCompletionSource<AgentResponse>(),
        };
    }
}
