using Dawning.Agents.Abstractions.Distributed;
using Dawning.Agents.Redis.Cache;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace Dawning.Agents.Tests.Redis;

/// <summary>
/// Redis 分布式缓存测试
/// </summary>
public sealed class RedisDistributedCacheTests
{
    private readonly Mock<IConnectionMultiplexer> _connectionMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly RedisOptions _options;

    public RedisDistributedCacheTests()
    {
        _connectionMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();
        _options = new RedisOptions { InstanceName = "test:", DefaultDatabase = 0 };

        _connectionMock.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_databaseMock.Object);
    }

    [Fact]
    public void Get_WithExistingKey_ReturnsValue()
    {
        // Arrange
        var key = "test-key";
        var expectedValue = "test-value"u8.ToArray();
        _databaseMock
            .Setup(d => d.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns((RedisValue)expectedValue);

        var cache = CreateCache();

        // Act
        var result = cache.Get(key);

        // Assert
        result.Should().BeEquivalentTo(expectedValue);
    }

    [Fact]
    public void Get_WithNonExistingKey_ReturnsNull()
    {
        // Arrange
        var key = "non-existing-key";
        _databaseMock
            .Setup(d => d.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns(RedisValue.Null);

        var cache = CreateCache();

        // Act
        var result = cache.Get(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WithExistingKey_ReturnsValue()
    {
        // Arrange
        var key = "test-key";
        var expectedValue = "test-value"u8.ToArray();
        _databaseMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)expectedValue);

        var cache = CreateCache();

        // Act
        var result = await cache.GetAsync(key);

        // Assert
        result.Should().BeEquivalentTo(expectedValue);
    }

    [Fact]
    public void Set_WithAbsoluteExpiration_SetsValue()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value"u8.ToArray();
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
        };

        _databaseMock
            .Setup(d =>
                d.StringSet(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<bool>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .Returns(true);

        var cache = CreateCache();

        // Act
        cache.Set(key, value, options);

        // Assert
        _databaseMock.Verify(
            d =>
                d.StringSet(
                    It.Is<RedisKey>(k => k.ToString().Contains(key)),
                    It.Is<RedisValue>(v => v == (RedisValue)value),
                    It.Is<TimeSpan?>(t => t.HasValue && t.Value.TotalMinutes == 5),
                    It.IsAny<bool>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task SetAsync_WithSlidingExpiration_SetsValue()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value"u8.ToArray();
        var options = new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(10),
        };

        _databaseMock
            .Setup(d =>
                d.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<bool>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(true);

        var cache = CreateCache();

        // Act
        await cache.SetAsync(key, value, options);

        // Assert
        _databaseMock.Verify(
            d =>
                d.StringSetAsync(
                    It.Is<RedisKey>(k => k.ToString().Contains(key)),
                    It.IsAny<RedisValue>(),
                    It.Is<TimeSpan?>(t => t.HasValue && t.Value.TotalMinutes == 10),
                    It.IsAny<bool>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()
                ),
            Times.Once
        );
    }

    [Fact]
    public void Remove_DeletesKey()
    {
        // Arrange
        var key = "test-key";
        _databaseMock
            .Setup(d => d.KeyDelete(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns(true);

        var cache = CreateCache();

        // Act
        cache.Remove(key);

        // Assert
        _databaseMock.Verify(
            d => d.KeyDelete(It.Is<RedisKey>(k => k.ToString().Contains(key)), It.IsAny<CommandFlags>()),
            Times.Once
        );
    }

    [Fact]
    public async Task RemoveAsync_DeletesKey()
    {
        // Arrange
        var key = "test-key";
        _databaseMock
            .Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var cache = CreateCache();

        // Act
        await cache.RemoveAsync(key);

        // Assert
        _databaseMock.Verify(
            d => d.KeyDeleteAsync(It.Is<RedisKey>(k => k.ToString().Contains(key)), It.IsAny<CommandFlags>()),
            Times.Once
        );
    }

    [Fact]
    public void Get_WithNullKey_ThrowsArgumentException()
    {
        // Arrange
        var cache = CreateCache();

        // Act & Assert
        var act = () => cache.Get(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Set_WithNullKey_ThrowsArgumentException()
    {
        // Arrange
        var cache = CreateCache();
        var options = new DistributedCacheEntryOptions();

        // Act & Assert
        var act = () => cache.Set(null!, "value"u8.ToArray(), options);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Set_WithNullValue_ThrowsArgumentNullException()
    {
        // Arrange
        var cache = CreateCache();
        var options = new DistributedCacheEntryOptions();

        // Act & Assert
        var act = () => cache.Set("key", null!, options);
        act.Should().Throw<ArgumentNullException>();
    }

    private RedisDistributedCache CreateCache()
    {
        return new RedisDistributedCache(
            _connectionMock.Object,
            Options.Create(_options),
            NullLogger<RedisDistributedCache>.Instance
        );
    }
}
