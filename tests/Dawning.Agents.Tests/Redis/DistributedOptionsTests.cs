using Dawning.Agents.Abstractions.Distributed;
using FluentAssertions;

namespace Dawning.Agents.Tests.Redis;

/// <summary>
/// 分布式配置选项测试
/// </summary>
public sealed class DistributedOptionsTests
{
    [Fact]
    public void RedisOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new RedisOptions();

        // Assert
        options.ConnectionString.Should().Be("localhost:6379");
        options.InstanceName.Should().Be("dawning:");
        options.DefaultDatabase.Should().Be(0);
        options.ConnectTimeout.Should().Be(5000);
        options.SyncTimeout.Should().Be(5000);
        options.AsyncTimeout.Should().Be(5000);
        options.UseSsl.Should().BeFalse();
        options.AbortOnConnectFail.Should().BeFalse();
        options.PoolSize.Should().Be(10);
    }

    [Fact]
    public void RedisOptions_Validate_WithValidConfig_DoesNotThrow()
    {
        // Arrange
        var options = new RedisOptions { ConnectionString = "localhost:6379", DefaultDatabase = 5 };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RedisOptions_Validate_WithEmptyConnectionString_ThrowsException()
    {
        // Arrange
        var options = new RedisOptions { ConnectionString = "" };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*ConnectionString*");
    }

    [Fact]
    public void RedisOptions_Validate_WithInvalidDatabase_ThrowsException()
    {
        // Arrange
        var options = new RedisOptions { ConnectionString = "localhost:6379", DefaultDatabase = 16 };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*DefaultDatabase*");
    }

    [Fact]
    public void DistributedQueueOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new DistributedQueueOptions();

        // Assert
        options.QueueName.Should().Be("agent:queue");
        options.ConsumerGroup.Should().Be("agent-workers");
        options.ConsumerNamePrefix.Should().Be("worker");
        options.DeadLetterQueue.Should().Be("agent:deadletter");
        options.MaxRetries.Should().Be(3);
        options.VisibilityTimeout.Should().Be(30);
        options.BatchSize.Should().Be(10);
        options.PollInterval.Should().Be(100);
    }

    [Fact]
    public void DistributedLockOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new DistributedLockOptions();

        // Assert
        options.KeyPrefix.Should().Be("lock:");
        options.DefaultExpiry.Should().Be(30);
        options.DefaultWaitTimeout.Should().Be(10);
        options.RetryInterval.Should().Be(100);
        options.EnableAutoRenewal.Should().BeTrue();
        options.RenewalInterval.Should().Be(0.5);
    }

    [Fact]
    public void DistributedSessionOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new DistributedSessionOptions();

        // Assert
        options.KeyPrefix.Should().Be("session:");
        options.DefaultExpiry.Should().Be(30);
        options.EnableSlidingExpiry.Should().BeTrue();
        options.MaxMessages.Should().Be(100);
    }

    [Fact]
    public void RedisOptions_SectionName_IsCorrect()
    {
        // Assert
        RedisOptions.SectionName.Should().Be("Redis");
    }

    [Fact]
    public void DistributedQueueOptions_SectionName_IsCorrect()
    {
        // Assert
        DistributedQueueOptions.SectionName.Should().Be("DistributedQueue");
    }

    [Fact]
    public void DistributedLockOptions_SectionName_IsCorrect()
    {
        // Assert
        DistributedLockOptions.SectionName.Should().Be("DistributedLock");
    }

    [Fact]
    public void DistributedSessionOptions_SectionName_IsCorrect()
    {
        // Assert
        DistributedSessionOptions.SectionName.Should().Be("DistributedSession");
    }
}
