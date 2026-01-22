using Dawning.Agents.Abstractions.Telemetry;
using FluentAssertions;

namespace Dawning.Agents.Tests.Telemetry;

public class TokenUsageRecordTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var metadata = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var record = new TokenUsageRecord(
            "TestAgent",
            100,
            50,
            timestamp,
            "gpt-4",
            "session-1",
            metadata
        );

        // Assert
        record.Source.Should().Be("TestAgent");
        record.PromptTokens.Should().Be(100);
        record.CompletionTokens.Should().Be(50);
        record.Timestamp.Should().Be(timestamp);
        record.Model.Should().Be("gpt-4");
        record.SessionId.Should().Be("session-1");
        record.Metadata.Should().ContainKey("key");
    }

    [Fact]
    public void TotalTokens_ShouldReturnSumOfPromptAndCompletion()
    {
        // Arrange
        var record = new TokenUsageRecord("TestAgent", 100, 50, DateTime.UtcNow);

        // Act & Assert
        record.TotalTokens.Should().Be(150);
    }

    [Fact]
    public void Create_ShouldCreateRecordWithCurrentTimestamp()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var record = TokenUsageRecord.Create("TestAgent", 100, 50, "gpt-4", "session-1");
        var after = DateTime.UtcNow;

        // Assert
        record.Source.Should().Be("TestAgent");
        record.PromptTokens.Should().Be(100);
        record.CompletionTokens.Should().Be(50);
        record.Model.Should().Be("gpt-4");
        record.SessionId.Should().Be("session-1");
        record.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Create_ShouldWorkWithMinimalParameters()
    {
        // Act
        var record = TokenUsageRecord.Create("TestAgent", 100, 50);

        // Assert
        record.Source.Should().Be("TestAgent");
        record.PromptTokens.Should().Be(100);
        record.CompletionTokens.Should().Be(50);
        record.Model.Should().BeNull();
        record.SessionId.Should().BeNull();
        record.Metadata.Should().BeNull();
    }
}

public class TokenUsageSummaryTests
{
    [Fact]
    public void TotalTokens_ShouldReturnSumOfPromptAndCompletion()
    {
        // Arrange
        var summary = new TokenUsageSummary(100, 50, 1, new Dictionary<string, SourceUsage>());

        // Act & Assert
        summary.TotalTokens.Should().Be(150);
    }

    [Fact]
    public void Empty_ShouldReturnZeroValues()
    {
        // Act
        var empty = TokenUsageSummary.Empty;

        // Assert
        empty.TotalPromptTokens.Should().Be(0);
        empty.TotalCompletionTokens.Should().Be(0);
        empty.TotalTokens.Should().Be(0);
        empty.CallCount.Should().Be(0);
        empty.BySource.Should().BeEmpty();
    }
}

public class SourceUsageTests
{
    [Fact]
    public void TotalTokens_ShouldReturnSumOfPromptAndCompletion()
    {
        // Arrange
        var usage = new SourceUsage(100, 50, 2);

        // Act & Assert
        usage.TotalTokens.Should().Be(150);
        usage.CallCount.Should().Be(2);
    }
}
