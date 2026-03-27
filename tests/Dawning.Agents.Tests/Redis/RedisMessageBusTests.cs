using Dawning.Agents.Abstractions.Communication;
using Dawning.Agents.Abstractions.Distributed;
using Dawning.Agents.Redis.Communication;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace Dawning.Agents.Tests.Redis;

/// <summary>
/// Redis distributed message bus tests
/// </summary>
public sealed class RedisMessageBusTests : IDisposable
{
    private readonly Mock<IConnectionMultiplexer> _connectionMock;
    private readonly Mock<ISubscriber> _subscriberMock;
    private readonly RedisOptions _options;
    private readonly RedisMessageBus _messageBus;

    public RedisMessageBusTests()
    {
        _connectionMock = new Mock<IConnectionMultiplexer>();
        _subscriberMock = new Mock<ISubscriber>();
        _options = new RedisOptions { InstanceName = "test:", DefaultDatabase = 0 };

        _connectionMock
            .Setup(c => c.GetSubscriber(It.IsAny<object>()))
            .Returns(_subscriberMock.Object);

        _messageBus = new RedisMessageBus(
            _connectionMock.Object,
            Options.Create(_options),
            NullLogger<RedisMessageBus>.Instance
        );
    }

    public void Dispose() => _messageBus.Dispose();

    [Fact]
    public async Task SendAsync_PublishesToAgentChannel()
    {
        // Arrange
        _subscriberMock
            .Setup(s =>
                s.PublishAsync(
                    It.IsAny<RedisChannel>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(1);

        var message = new TaskMessage
        {
            SenderId = "agent-1",
            ReceiverId = "agent-2",
            Task = "Do something",
        };

        // Act
        await _messageBus.SendAsync(message);

        // Assert
        _subscriberMock.Verify(
            s =>
                s.PublishAsync(
                    It.Is<RedisChannel>(c => c.ToString().Contains("agent:agent-2")),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task SendAsync_ThrowsWhenNoReceiverId()
    {
        // Arrange
        var message = new TaskMessage { SenderId = "agent-1", Task = "Do something" };

        // Act & Assert
        var act = () => _messageBus.SendAsync(message);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task BroadcastAsync_PublishesToBroadcastChannel()
    {
        // Arrange
        _subscriberMock
            .Setup(s =>
                s.PublishAsync(
                    It.IsAny<RedisChannel>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(1);

        var message = new TaskMessage { SenderId = "agent-1", Task = "Broadcast this" };

        // Act
        await _messageBus.BroadcastAsync(message);

        // Assert
        _subscriberMock.Verify(
            s =>
                s.PublishAsync(
                    It.Is<RedisChannel>(c => c.ToString().Contains("broadcast")),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()
                ),
            Times.Once
        );
    }

    [Fact]
    public void Subscribe_AgentChannel_ReturnsDisposable()
    {
        // Act
        var sub = _messageBus.Subscribe("agent-1", _ => { });

        // Assert
        sub.Should().NotBeNull();
        sub.Dispose();
    }

    [Fact]
    public void Subscribe_Topic_ReturnsDisposable()
    {
        // Act
        var sub = _messageBus.Subscribe("agent-1", "events", _ => { });

        // Assert
        sub.Should().NotBeNull();
        sub.Dispose();
    }

    [Fact]
    public async Task PublishAsync_PublishesToTopicChannel()
    {
        // Arrange
        _subscriberMock
            .Setup(s =>
                s.PublishAsync(
                    It.IsAny<RedisChannel>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(1);

        var message = new EventMessage
        {
            SenderId = "agent-1",
            EventType = "task_completed",
            Payload = "done",
        };

        // Act
        await _messageBus.PublishAsync("events", message);

        // Assert
        _subscriberMock.Verify(
            s =>
                s.PublishAsync(
                    It.Is<RedisChannel>(c => c.ToString().Contains("topic:events")),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()
                ),
            Times.Once
        );
    }

    [Fact]
    public void Constructor_ThrowsOnNullConnection()
    {
        var act = () => new RedisMessageBus(null!, Options.Create(_options));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SendAsync_ThrowsOnNullMessage()
    {
        var act = () => _messageBus.SendAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Subscribe_ThrowsOnEmptyAgentId()
    {
        var act = () => _messageBus.Subscribe("", _ => { });
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Dispose_CancelsPendingRequests()
    {
        // Act & Assert — should not throw
        _messageBus.Dispose();
    }
}
