using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Core.Health;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Dawning.Agents.Tests.Health;

public class AgentHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ShouldReturnHealthy()
    {
        // Arrange
        var healthCheck = new AgentHealthCheck();
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("agent", healthCheck, null, null),
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("Agent");
    }
}

public class RedisHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenRedisHealthy_ShouldReturnHealthy()
    {
        // Arrange
        var mockDatabase = new Mock<IDatabase>();
        mockDatabase
            .Setup(db => db.PingAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(TimeSpan.FromMilliseconds(5));

        var mockRedis = new Mock<IConnectionMultiplexer>();
        mockRedis
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDatabase.Object);

        var healthCheck = new RedisHealthCheck(mockRedis.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("redis", healthCheck, null, null),
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("Redis");
        result.Description.Should().Contain("Ping");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenRedisFails_ShouldReturnUnhealthy()
    {
        // Arrange
        var mockDatabase = new Mock<IDatabase>();
        mockDatabase
            .Setup(db => db.PingAsync(It.IsAny<CommandFlags>()))
            .ThrowsAsync(
                new RedisConnectionException(
                    ConnectionFailureType.UnableToConnect,
                    "Connection failed"
                )
            );

        var mockRedis = new Mock<IConnectionMultiplexer>();
        mockRedis
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDatabase.Object);

        var healthCheck = new RedisHealthCheck(mockRedis.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("redis", healthCheck, null, null),
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("失败");
        result.Exception.Should().NotBeNull();
    }
}

public class LLMProviderHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenLLMResponds_ShouldReturnHealthy()
    {
        // Arrange
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider.SetupGet(p => p.Name).Returns("TestProvider");
        mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IReadOnlyList<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ChatCompletionResponse { Content = "pong" });

        var healthCheck = new LLMProviderHealthCheck(mockProvider.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("llm", healthCheck, null, null),
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("TestProvider");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenLLMReturnsEmpty_ShouldReturnDegraded()
    {
        // Arrange
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider.SetupGet(p => p.Name).Returns("TestProvider");
        mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IReadOnlyList<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ChatCompletionResponse { Content = "" });

        var healthCheck = new LLMProviderHealthCheck(mockProvider.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("llm", healthCheck, null, null),
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenLLMThrows_ShouldReturnUnhealthy()
    {
        // Arrange
        var mockProvider = new Mock<ILLMProvider>();
        mockProvider.SetupGet(p => p.Name).Returns("TestProvider");
        mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IReadOnlyList<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new Exception("LLM connection failed"));

        var healthCheck = new LLMProviderHealthCheck(mockProvider.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("llm", healthCheck, null, null),
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception.Should().NotBeNull();
    }
}
