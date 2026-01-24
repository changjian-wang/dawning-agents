namespace Dawning.Agents.Tests.Communication;

using Dawning.Agents.Abstractions.Communication;
using Dawning.Agents.Core.Communication;
using FluentAssertions;

/// <summary>
/// InMemoryMessageBus 测试
/// </summary>
public class InMemoryMessageBusTests
{
    #region SendAsync Tests

    [Fact]
    public async Task SendAsync_WithoutReceiverId_ShouldThrow()
    {
        var bus = new InMemoryMessageBus();
        var message = new TaskMessage { SenderId = "agent1", Task = "test" };

        var act = () => bus.SendAsync(message);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*ReceiverId*");
    }

    [Fact]
    public async Task SendAsync_ToSubscribedAgent_ShouldDeliverMessage()
    {
        var bus = new InMemoryMessageBus();
        AgentMessage? receivedMessage = null;

        bus.Subscribe("agent2", msg => receivedMessage = msg);

        var message = new TaskMessage
        {
            SenderId = "agent1",
            ReceiverId = "agent2",
            Task = "test",
        };

        await bus.SendAsync(message);

        receivedMessage.Should().NotBeNull();
        receivedMessage.Should().BeOfType<TaskMessage>();
        ((TaskMessage)receivedMessage!).Task.Should().Be("test");
    }

    [Fact]
    public async Task SendAsync_ToUnsubscribedAgent_ShouldNotThrow()
    {
        var bus = new InMemoryMessageBus();

        var message = new TaskMessage
        {
            SenderId = "agent1",
            ReceiverId = "agent2",
            Task = "test",
        };

        var act = () => bus.SendAsync(message);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendAsync_ResponseMessage_ShouldResolveWaitingRequest()
    {
        var bus = new InMemoryMessageBus();
        var requestReceived = new TaskCompletionSource<TaskMessage>();

        bus.Subscribe(
            "agent2",
            msg =>
            {
                if (msg is TaskMessage task)
                {
                    requestReceived.SetResult(task);

                    // 模拟发送响应
                    _ = bus.SendAsync(
                        new ResponseMessage
                        {
                            SenderId = "agent2",
                            ReceiverId = "agent1",
                            CorrelationId = task.CorrelationId!,
                            Result = "done",
                            IsSuccess = true,
                        }
                    );
                }
            }
        );

        var request = new TaskMessage
        {
            SenderId = "agent1",
            ReceiverId = "agent2",
            Task = "process",
        };

        var responseTask = bus.RequestAsync(request, TimeSpan.FromSeconds(5));

        var response = await responseTask;
        response.IsSuccess.Should().BeTrue();
        response.Result.Should().Be("done");
    }

    #endregion

    #region BroadcastAsync Tests

    [Fact]
    public async Task BroadcastAsync_ShouldDeliverToAllSubscribers()
    {
        var bus = new InMemoryMessageBus();
        var receivedCount = 0;

        bus.Subscribe("agent1", _ => Interlocked.Increment(ref receivedCount));
        bus.Subscribe("agent2", _ => Interlocked.Increment(ref receivedCount));
        bus.Subscribe("agent3", _ => Interlocked.Increment(ref receivedCount));

        var message = new StatusMessage
        {
            SenderId = "system",
            Status = AgentStatus.Idle,
        };

        await bus.BroadcastAsync(message);

        receivedCount.Should().Be(3);
    }

    [Fact]
    public async Task BroadcastAsync_WithNoSubscribers_ShouldNotThrow()
    {
        var bus = new InMemoryMessageBus();

        var message = new StatusMessage
        {
            SenderId = "system",
            Status = AgentStatus.Idle,
        };

        var act = () => bus.BroadcastAsync(message);

        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Subscribe Tests

    [Fact]
    public void Subscribe_ShouldIncreaseSubscriberCount()
    {
        var bus = new InMemoryMessageBus();

        bus.SubscriberCount.Should().Be(0);

        bus.Subscribe("agent1", _ => { });
        bus.SubscriberCount.Should().Be(1);

        bus.Subscribe("agent2", _ => { });
        bus.SubscriberCount.Should().Be(2);
    }

    [Fact]
    public async Task Subscribe_Dispose_ShouldUnsubscribe()
    {
        var bus = new InMemoryMessageBus();
        var callCount = 0;

        var subscription = bus.Subscribe("agent1", _ => callCount++);

        // 发送消息应该触发
        await bus.SendAsync(
            new TaskMessage
            {
                SenderId = "agent2",
                ReceiverId = "agent1",
                Task = "test",
            }
        );

        callCount.Should().Be(1);

        // 取消订阅
        subscription.Dispose();

        // 再次发送不应触发
        await bus.SendAsync(
            new TaskMessage
            {
                SenderId = "agent2",
                ReceiverId = "agent1",
                Task = "test2",
            }
        );

        callCount.Should().Be(1);
    }

    #endregion

    #region Topic Subscribe/Publish Tests

    [Fact]
    public async Task PublishAsync_ShouldDeliverToTopicSubscribers()
    {
        var bus = new InMemoryMessageBus();
        EventMessage? receivedEvent = null;

        bus.Subscribe("agent1", "data-events", evt => receivedEvent = evt);

        var eventMessage = new EventMessage
        {
            SenderId = "system",
            EventType = "DataUpdated",
            Payload = new { Id = 1, Name = "test" },
        };

        await bus.PublishAsync("data-events", eventMessage);

        receivedEvent.Should().NotBeNull();
        receivedEvent!.EventType.Should().Be("DataUpdated");
    }

    [Fact]
    public async Task PublishAsync_DifferentTopic_ShouldNotDeliver()
    {
        var bus = new InMemoryMessageBus();
        var received = false;

        bus.Subscribe("agent1", "topic-a", _ => received = true);

        await bus.PublishAsync(
            "topic-b",
            new EventMessage
            {
                SenderId = "system",
                EventType = "Test",
                Payload = "data",
            }
        );

        received.Should().BeFalse();
    }

    [Fact]
    public async Task TopicSubscribe_Dispose_ShouldUnsubscribe()
    {
        var bus = new InMemoryMessageBus();
        var callCount = 0;

        var subscription = bus.Subscribe("agent1", "events", _ => callCount++);

        await bus.PublishAsync(
            "events",
            new EventMessage
            {
                SenderId = "system",
                EventType = "Test",
                Payload = "data",
            }
        );

        callCount.Should().Be(1);

        subscription.Dispose();

        await bus.PublishAsync(
            "events",
            new EventMessage
            {
                SenderId = "system",
                EventType = "Test2",
                Payload = "data",
            }
        );

        callCount.Should().Be(1);
    }

    #endregion

    #region RequestAsync Tests

    [Fact]
    public async Task RequestAsync_Timeout_ShouldThrowTimeoutException()
    {
        var bus = new InMemoryMessageBus();

        bus.Subscribe("agent2", _ => { }); // 订阅但不响应

        var request = new TaskMessage
        {
            SenderId = "agent1",
            ReceiverId = "agent2",
            Task = "long-task",
        };

        var act = () => bus.RequestAsync(request, TimeSpan.FromMilliseconds(100));

        await act.Should().ThrowAsync<TimeoutException>();
    }

    #endregion
}
