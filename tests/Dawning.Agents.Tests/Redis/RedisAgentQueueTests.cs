namespace Dawning.Agents.Tests.Redis;

using System.Collections.Concurrent;
using System.Reflection;
using Dawning.Agents.Abstractions.Distributed;
using Dawning.Agents.Redis.Queue;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

public sealed class RedisAgentQueueTests
{
    [Fact]
    public async Task RequeueAsync_ShouldIncrementCount_WhenReinsertedToMainQueue()
    {
        var connectionMock = new Mock<IConnectionMultiplexer>();
        var databaseMock = new Mock<IDatabase>();

        connectionMock
            .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(databaseMock.Object);

        databaseMock
            .Setup(d =>
                d.StreamAcknowledgeAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(1);

        databaseMock
            .Setup(d =>
                d.StreamAddAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<NameValueEntry[]>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<int?>(),
                    It.IsAny<bool>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync("1-0");

        var queueOptions = Options.Create(new DistributedQueueOptions());
        var redisOptions = Options.Create(new RedisOptions { InstanceName = "test:" });

        var queue = new RedisAgentQueue(
            connectionMock.Object,
            queueOptions,
            redisOptions,
            NullLogger<RedisAgentQueue>.Instance
        );

        SetPrivateField(queue, "_initialized", true);
        SetPrivateField(queue, "_count", -1);

        var map = GetPrivateField<ConcurrentDictionary<string, (RedisValue StreamId, string Data)>>(
            queue,
            "_messageIdToStreamEntry"
        );
        map["msg-1"] = ("1-0", "null");

        await queue.RequeueAsync("msg-1");

        queue.Count.Should().Be(0);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        var field = target
            .GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        return (T)field!.GetValue(target)!;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target
            .GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        field!.SetValue(target, value);
    }
}
