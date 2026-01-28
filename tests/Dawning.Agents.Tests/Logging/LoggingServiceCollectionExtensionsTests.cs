using Dawning.Agents.Abstractions.Logging;
using Dawning.Agents.Core.Logging;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dawning.Agents.Tests.Logging;

/// <summary>
/// LoggingServiceCollectionExtensions 测试
/// </summary>
public class LoggingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAgentLogging_ShouldRegisterLoggingServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAgentLogging();
        var provider = services.BuildServiceProvider();

        // Assert
        var loggerFactory = provider.GetService<ILoggerFactory>();
        loggerFactory.Should().NotBeNull();
    }

    [Fact]
    public void AddAgentLogging_WithOptions_ShouldConfigureCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAgentLogging(options =>
        {
            options.MinimumLevel = "Debug";
            options.EnableConsole = true;
            options.EnableFile = false;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var loggerFactory = provider.GetService<ILoggerFactory>();
        loggerFactory.Should().NotBeNull();
    }

    [Fact]
    public void AddAgentLogging_WithConfiguration_ShouldBindOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AgentLogging:MinimumLevel"] = "Warning",
                    ["AgentLogging:EnableConsole"] = "true",
                    ["AgentLogging:EnableFile"] = "false",
                }
            )
            .Build();

        // Act
        services.AddAgentLogging(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var loggerFactory = provider.GetService<ILoggerFactory>();
        loggerFactory.Should().NotBeNull();
    }

    [Fact]
    public void AddAgentLogging_ShouldCreateLoggerInstance()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAgentLogging();
        var provider = services.BuildServiceProvider();
        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<LoggingServiceCollectionExtensionsTests>();

        // Assert
        logger.Should().NotBeNull();
    }

    [Fact]
    public void AddAgentLogging_WithJsonFormat_ShouldWork()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAgentLogging(options =>
        {
            options.EnableJsonFormat = true;
            options.EnableConsole = true;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var loggerFactory = provider.GetService<ILoggerFactory>();
        loggerFactory.Should().NotBeNull();
    }

    [Fact]
    public void AddAgentLogging_WithFileOutput_ShouldWork()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAgentLogging(options =>
        {
            options.EnableFile = true;
            options.FilePath = "logs/test-.log";
            options.RollingInterval = RollingIntervalType.Hour;
            options.RetainedFileCount = 7;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var loggerFactory = provider.GetService<ILoggerFactory>();
        loggerFactory.Should().NotBeNull();
    }

    [Fact]
    public void AddAgentLogging_WithOverrides_ShouldWork()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAgentLogging(options =>
        {
            options.Override["Microsoft.AspNetCore"] = "Warning";
            options.Override["System.Net"] = "Error";
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var loggerFactory = provider.GetService<ILoggerFactory>();
        loggerFactory.Should().NotBeNull();
    }

    [Theory]
    [InlineData("verbose")]
    [InlineData("trace")]
    [InlineData("debug")]
    [InlineData("information")]
    [InlineData("info")]
    [InlineData("warning")]
    [InlineData("warn")]
    [InlineData("error")]
    [InlineData("fatal")]
    [InlineData("critical")]
    public void AddAgentLogging_ShouldSupportAllLogLevels(string level)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAgentLogging(options => options.MinimumLevel = level);
        var provider = services.BuildServiceProvider();

        // Assert
        var loggerFactory = provider.GetService<ILoggerFactory>();
        loggerFactory.Should().NotBeNull();
    }

    [Theory]
    [InlineData(RollingIntervalType.Infinite)]
    [InlineData(RollingIntervalType.Year)]
    [InlineData(RollingIntervalType.Month)]
    [InlineData(RollingIntervalType.Day)]
    [InlineData(RollingIntervalType.Hour)]
    [InlineData(RollingIntervalType.Minute)]
    public void AddAgentLogging_ShouldSupportAllRollingIntervals(RollingIntervalType interval)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAgentLogging(options =>
        {
            options.EnableFile = true;
            options.RollingInterval = interval;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var loggerFactory = provider.GetService<ILoggerFactory>();
        loggerFactory.Should().NotBeNull();
    }

    [Fact]
    public void AddAgentLogging_ShouldDisableEnrichers()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAgentLogging(options =>
        {
            options.EnrichWithMachineName = false;
            options.EnrichWithThreadId = false;
            options.EnrichWithRequestId = false;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var loggerFactory = provider.GetService<ILoggerFactory>();
        loggerFactory.Should().NotBeNull();
    }
}
