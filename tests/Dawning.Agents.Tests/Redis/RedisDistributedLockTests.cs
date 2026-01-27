using Dawning.Agents.Abstractions.Distributed;
using Dawning.Agents.Redis.Lock;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace Dawning.Agents.Tests.Redis;

/// <summary>
/// Redis 分布式锁测试 - 单元测试（无需 Redis 连接）
/// </summary>
public sealed class RedisDistributedLockTests
{
    private readonly DistributedLockOptions _options;

    public RedisDistributedLockTests()
    {
        _options = new DistributedLockOptions
        {
            KeyPrefix = "lock:",
            DefaultExpiry = 30,
            DefaultWaitTimeout = 10,
            RetryInterval = 50,
            EnableAutoRenewal = false,
        };
    }

    [Fact]
    public void Resource_ReturnsCorrectValue()
    {
        // Arrange
        var resource = "test-resource";
        var databaseMock = new Mock<IDatabase>();

        // Act
        var lockInstance = new RedisDistributedLock(
            databaseMock.Object,
            resource,
            TimeSpan.FromSeconds(30),
            _options,
            NullLogger<RedisDistributedLock>.Instance
        );

        // Assert
        lockInstance.Resource.Should().Be(resource);
    }

    [Fact]
    public void LockId_IsNotEmpty()
    {
        // Arrange
        var databaseMock = new Mock<IDatabase>();

        // Act
        var lockInstance = new RedisDistributedLock(
            databaseMock.Object,
            "test-resource",
            TimeSpan.FromSeconds(30),
            _options,
            NullLogger<RedisDistributedLock>.Instance
        );

        // Assert
        lockInstance.LockId.Should().NotBeNullOrEmpty();
        lockInstance.LockId.Should().HaveLength(32); // GUID without hyphens
    }

    [Fact]
    public void IsAcquired_InitiallyFalse()
    {
        // Arrange
        var databaseMock = new Mock<IDatabase>();

        // Act
        var lockInstance = new RedisDistributedLock(
            databaseMock.Object,
            "test-resource",
            TimeSpan.FromSeconds(30),
            _options,
            NullLogger<RedisDistributedLock>.Instance
        );

        // Assert
        lockInstance.IsAcquired.Should().BeFalse();
    }

    [Fact]
    public void ExpiresAt_InitiallyNull()
    {
        // Arrange
        var databaseMock = new Mock<IDatabase>();

        // Act
        var lockInstance = new RedisDistributedLock(
            databaseMock.Object,
            "test-resource",
            TimeSpan.FromSeconds(30),
            _options,
            NullLogger<RedisDistributedLock>.Instance
        );

        // Assert
        lockInstance.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task ExtendAsync_WhenLockNotHeld_ReturnsFalse()
    {
        // Arrange
        var databaseMock = new Mock<IDatabase>();
        var lockInstance = new RedisDistributedLock(
            databaseMock.Object,
            "test-resource",
            TimeSpan.FromSeconds(30),
            _options,
            NullLogger<RedisDistributedLock>.Instance
        );

        // Act
        var extended = await lockInstance.ExtendAsync(TimeSpan.FromSeconds(60));

        // Assert
        extended.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseAsync_WhenLockNotHeld_DoesNotThrow()
    {
        // Arrange
        var databaseMock = new Mock<IDatabase>();
        var lockInstance = new RedisDistributedLock(
            databaseMock.Object,
            "test-resource",
            TimeSpan.FromSeconds(30),
            _options,
            NullLogger<RedisDistributedLock>.Instance
        );

        // Act
        var act = async () => await lockInstance.ReleaseAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_WhenLockNotHeld_DoesNotThrow()
    {
        // Arrange
        var databaseMock = new Mock<IDatabase>();
        var lockInstance = new RedisDistributedLock(
            databaseMock.Object,
            "test-resource",
            TimeSpan.FromSeconds(30),
            _options,
            NullLogger<RedisDistributedLock>.Instance
        );

        // Act
        var act = async () => await lockInstance.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }
}

/// <summary>
/// Redis 分布式锁工厂测试
/// </summary>
public sealed class RedisDistributedLockFactoryTests
{
    private readonly Mock<IConnectionMultiplexer> _connectionMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly RedisOptions _redisOptions;
    private readonly DistributedLockOptions _lockOptions;

    public RedisDistributedLockFactoryTests()
    {
        _connectionMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();
        _redisOptions = new RedisOptions { InstanceName = "test:", DefaultDatabase = 0 };
        _lockOptions = new DistributedLockOptions
        {
            KeyPrefix = "lock:",
            DefaultExpiry = 30,
            DefaultWaitTimeout = 5,
            RetryInterval = 50,
            EnableAutoRenewal = false,
        };

        _connectionMock.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_databaseMock.Object);
    }

    [Fact]
    public void CreateLock_ReturnsNewLockInstance()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var lockInstance = factory.CreateLock("test-resource", TimeSpan.FromSeconds(30));

        // Assert
        lockInstance.Should().NotBeNull();
        lockInstance.Resource.Should().Be("test-resource");
        lockInstance.IsAcquired.Should().BeFalse();
    }

    [Fact]
    public void CreateLock_WithEmptyResource_ThrowsArgumentException()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var act = () => factory.CreateLock("", TimeSpan.FromSeconds(30));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateLock_MultipleCalls_ReturnsDifferentInstances()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var lock1 = factory.CreateLock("resource-1", TimeSpan.FromSeconds(30));
        var lock2 = factory.CreateLock("resource-2", TimeSpan.FromSeconds(30));

        // Assert
        lock1.Should().NotBeSameAs(lock2);
        lock1.LockId.Should().NotBe(lock2.LockId);
    }

    private RedisDistributedLockFactory CreateFactory()
    {
        return new RedisDistributedLockFactory(
            _connectionMock.Object,
            Options.Create(_redisOptions),
            Options.Create(_lockOptions)
        );
    }
}
