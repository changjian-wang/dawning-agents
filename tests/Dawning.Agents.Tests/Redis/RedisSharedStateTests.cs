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
/// Redis 分布式共享状态测试
/// </summary>
public sealed class RedisSharedStateTests : IDisposable
{
    private readonly Mock<IConnectionMultiplexer> _connectionMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly Mock<ISubscriber> _subscriberMock;
    private readonly RedisOptions _options;
    private readonly RedisSharedState _sharedState;

    public RedisSharedStateTests()
    {
        _connectionMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();
        _subscriberMock = new Mock<ISubscriber>();
        _options = new RedisOptions { InstanceName = "test:", DefaultDatabase = 0 };

        _connectionMock
            .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);
        _connectionMock
            .Setup(c => c.GetSubscriber(It.IsAny<object>()))
            .Returns(_subscriberMock.Object);

        _sharedState = new RedisSharedState(
            _connectionMock.Object,
            Options.Create(_options),
            NullLogger<RedisSharedState>.Instance
        );
    }

    public void Dispose() => _sharedState.Dispose();

    [Fact]
    public async Task GetAsync_ReturnsDeserializedValue()
    {
        // Arrange
        _databaseMock
            .Setup(d =>
                d.HashGetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync((RedisValue)"\"hello\"");

        // Act
        var result = await _sharedState.GetAsync<string>("key1");

        // Assert
        result.Should().Be("hello");
    }

    [Fact]
    public async Task GetAsync_ReturnsDefault_WhenKeyNotExists()
    {
        // Arrange
        _databaseMock
            .Setup(d =>
                d.HashGetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _sharedState.GetAsync<string>("key1");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_StoresSerializedValueAndPublishes()
    {
        // Arrange
        _databaseMock
            .Setup(d =>
                d.HashSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(true);
        _subscriberMock
            .Setup(s =>
                s.PublishAsync(
                    It.IsAny<RedisChannel>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(1);

        // Act
        await _sharedState.SetAsync("key1", "value1");

        // Assert
        _databaseMock.Verify(
            d =>
                d.HashSetAsync(
                    It.Is<RedisKey>(k => k.ToString().Contains("shared_state")),
                    It.Is<RedisValue>(v => v.ToString() == "key1"),
                    It.Is<RedisValue>(v => v.ToString().Contains("value1")),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()
                ),
            Times.Once
        );

        _subscriberMock.Verify(
            s =>
                s.PublishAsync(
                    It.IsAny<RedisChannel>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task DeleteAsync_RemovesAndNotifies()
    {
        // Arrange
        _databaseMock
            .Setup(d =>
                d.HashDeleteAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(true);
        _subscriberMock
            .Setup(s =>
                s.PublishAsync(
                    It.IsAny<RedisChannel>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(1);

        // Act
        var result = await _sharedState.DeleteAsync("key1");

        // Assert
        result.Should().BeTrue();
        _subscriberMock.Verify(
            s =>
                s.PublishAsync(
                    It.IsAny<RedisChannel>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task ExistsAsync_ReturnsHashExists()
    {
        // Arrange
        _databaseMock
            .Setup(d =>
                d.HashExistsAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(true);

        // Act
        var result = await _sharedState.ExistsAsync("key1");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetKeysAsync_WildcardReturnsAll()
    {
        // Arrange
        _databaseMock
            .Setup(d => d.HashKeysAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync([new RedisValue("key1"), new RedisValue("key2")]);

        // Act
        var result = await _sharedState.GetKeysAsync("*");

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain("key1");
        result.Should().Contain("key2");
    }

    [Fact]
    public async Task ClearAsync_DeletesHashKey()
    {
        // Arrange
        _databaseMock
            .Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _sharedState.ClearAsync();

        // Assert
        _databaseMock.Verify(
            d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
            Times.Once
        );
    }

    [Fact]
    public void OnChange_ReturnsDisposable()
    {
        // Act
        var subscription = _sharedState.OnChange("key1", (_, _) => { });

        // Assert
        subscription.Should().NotBeNull();
        subscription.Dispose(); // should not throw
    }

    [Fact]
    public void Constructor_ThrowsOnNullConnection()
    {
        var act = () => new RedisSharedState(null!, Options.Create(_options));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GetAsync_ThrowsOnEmptyKey()
    {
        var act = () => _sharedState.GetAsync<string>("");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetAsync_ThrowsOnEmptyKey()
    {
        var act = () => _sharedState.SetAsync("", "value");
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
