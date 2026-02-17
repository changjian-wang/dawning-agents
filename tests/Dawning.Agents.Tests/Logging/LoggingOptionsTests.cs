using Dawning.Agents.Abstractions.Logging;
using FluentAssertions;
using Xunit;

namespace Dawning.Agents.Tests.Logging;

/// <summary>
/// LoggingOptions 测试
/// </summary>
public class LoggingOptionsTests
{
    [Fact]
    public void LoggingOptions_ShouldHaveCorrectSectionName()
    {
        // Assert
        LoggingOptions.SectionName.Should().Be("AgentLogging");
    }

    [Fact]
    public void LoggingOptions_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var options = new LoggingOptions();

        // Assert
        options.MinimumLevel.Should().Be("Information");
        options.EnableConsole.Should().BeTrue();
        options.EnableFile.Should().BeFalse();
        options.FilePath.Should().Be("logs/agent-.log");
        options.RollingInterval.Should().Be(RollingIntervalType.Day);
        options.RetainedFileCount.Should().Be(30);
        options.EnableJsonFormat.Should().BeFalse();
        options.EnrichWithMachineName.Should().BeTrue();
        options.EnrichWithThreadId.Should().BeTrue();
        options.EnrichWithRequestId.Should().BeTrue();
    }

    [Fact]
    public void LoggingOptions_ShouldHaveDefaultOverrides()
    {
        // Arrange & Act
        var options = new LoggingOptions();

        // Assert
        options.Override.Should().ContainKey("Microsoft");
        options.Override.Should().ContainKey("System");
        options.Override["Microsoft"].Should().Be("Warning");
        options.Override["System"].Should().Be("Warning");
    }

    [Fact]
    public void LoggingOptions_ShouldAllowCustomConfiguration()
    {
        // Arrange & Act
        var options = new LoggingOptions
        {
            MinimumLevel = "Debug",
            EnableConsole = false,
            EnableFile = true,
            FilePath = "custom/path.log",
            RollingInterval = RollingIntervalType.Hour,
            RetainedFileCount = 7,
            EnableJsonFormat = true,
            EnrichWithMachineName = false,
            EnrichWithThreadId = false,
        };

        // Assert
        options.MinimumLevel.Should().Be("Debug");
        options.EnableConsole.Should().BeFalse();
        options.EnableFile.Should().BeTrue();
        options.FilePath.Should().Be("custom/path.log");
        options.RollingInterval.Should().Be(RollingIntervalType.Hour);
        options.RetainedFileCount.Should().Be(7);
        options.EnableJsonFormat.Should().BeTrue();
        options.EnrichWithMachineName.Should().BeFalse();
        options.EnrichWithThreadId.Should().BeFalse();
    }

    [Fact]
    public void LoggingOptions_OutputTemplate_ShouldContainAgentName()
    {
        // Arrange & Act
        var options = new LoggingOptions();

        // Assert
        options.OutputTemplate.Should().Contain("{AgentName}");
    }

    [Fact]
    public void LoggingOptions_Validate_WithValidConfig_DoesNotThrow()
    {
        var options = new LoggingOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void LoggingOptions_Validate_WithInvalidMinimumLevel_Throws()
    {
        var options = new LoggingOptions { MinimumLevel = "InvalidLevel" };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*MinimumLevel*");
    }

    [Fact]
    public void LoggingOptions_Validate_WithZeroRetainedFileCount_Throws()
    {
        var options = new LoggingOptions { RetainedFileCount = 0 };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*RetainedFileCount*");
    }

    [Fact]
    public void LoggingOptions_Validate_FileEnabled_EmptyPath_Throws()
    {
        var options = new LoggingOptions { EnableFile = true, FilePath = "" };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*FilePath*");
    }

    [Fact]
    public void LoggingOptions_Validate_CascadesToElasticsearch()
    {
        var options = new LoggingOptions
        {
            Elasticsearch = new ElasticsearchLoggingOptions { BatchSize = 0 },
        };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*BatchSize*");
    }

    [Fact]
    public void LoggingOptions_Validate_CascadesToSeq()
    {
        var options = new LoggingOptions
        {
            Seq = new SeqLoggingOptions { BatchIntervalSeconds = 0 },
        };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*BatchIntervalSeconds*");
    }

    [Fact]
    public void ElasticsearchLoggingOptions_Validate_EnabledWithEmptyUris_Throws()
    {
        var options = new ElasticsearchLoggingOptions { Enabled = true, NodeUris = [] };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*NodeUris*");
    }

    [Fact]
    public void ElasticsearchLoggingOptions_Validate_ZeroBatchIntervalSeconds_Throws()
    {
        var options = new ElasticsearchLoggingOptions { BatchIntervalSeconds = 0 };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*BatchIntervalSeconds*");
    }

    [Fact]
    public void SeqLoggingOptions_Validate_EnabledWithEmptyUrl_Throws()
    {
        var options = new SeqLoggingOptions { Enabled = true, ServerUrl = "" };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*ServerUrl*");
    }

    [Fact]
    public void SeqLoggingOptions_Validate_ZeroBatchIntervalSeconds_Throws()
    {
        var options = new SeqLoggingOptions { BatchIntervalSeconds = 0 };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*BatchIntervalSeconds*");
    }
}
