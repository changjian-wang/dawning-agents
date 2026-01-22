using Dawning.Agents.Abstractions.Telemetry;
using Dawning.Agents.Core.Telemetry;
using FluentAssertions;

namespace Dawning.Agents.Tests.Telemetry;

public class InMemoryTokenUsageTrackerTests
{
    private readonly InMemoryTokenUsageTracker _tracker = new();

    [Fact]
    public void Initial_ShouldHaveZeroValues()
    {
        // Assert
        _tracker.TotalPromptTokens.Should().Be(0);
        _tracker.TotalCompletionTokens.Should().Be(0);
        _tracker.TotalTokens.Should().Be(0);
        _tracker.CallCount.Should().Be(0);
    }

    [Fact]
    public void Record_ShouldUpdateTotals()
    {
        // Act
        _tracker.Record(TokenUsageRecord.Create("Agent1", 100, 50));

        // Assert
        _tracker.TotalPromptTokens.Should().Be(100);
        _tracker.TotalCompletionTokens.Should().Be(50);
        _tracker.TotalTokens.Should().Be(150);
        _tracker.CallCount.Should().Be(1);
    }

    [Fact]
    public void Record_MultipleRecords_ShouldAccumulateTotals()
    {
        // Act
        _tracker.Record(TokenUsageRecord.Create("Agent1", 100, 50));
        _tracker.Record(TokenUsageRecord.Create("Agent2", 200, 100));

        // Assert
        _tracker.TotalPromptTokens.Should().Be(300);
        _tracker.TotalCompletionTokens.Should().Be(150);
        _tracker.TotalTokens.Should().Be(450);
        _tracker.CallCount.Should().Be(2);
    }

    [Fact]
    public void GetSummary_ShouldReturnCorrectSummary()
    {
        // Arrange
        _tracker.Record(TokenUsageRecord.Create("Agent1", 100, 50));
        _tracker.Record(TokenUsageRecord.Create("Agent1", 50, 25));
        _tracker.Record(TokenUsageRecord.Create("Agent2", 200, 100));

        // Act
        var summary = _tracker.GetSummary();

        // Assert
        summary.TotalPromptTokens.Should().Be(350);
        summary.TotalCompletionTokens.Should().Be(175);
        summary.TotalTokens.Should().Be(525);
        summary.CallCount.Should().Be(3);
        summary.BySource.Should().HaveCount(2);
        summary.BySource["Agent1"].PromptTokens.Should().Be(150);
        summary.BySource["Agent1"].CompletionTokens.Should().Be(75);
        summary.BySource["Agent1"].CallCount.Should().Be(2);
        summary.BySource["Agent2"].PromptTokens.Should().Be(200);
        summary.BySource["Agent2"].CompletionTokens.Should().Be(100);
        summary.BySource["Agent2"].CallCount.Should().Be(1);
    }

    [Fact]
    public void GetRecords_ShouldReturnAllRecords()
    {
        // Arrange
        _tracker.Record(TokenUsageRecord.Create("Agent1", 100, 50));
        _tracker.Record(TokenUsageRecord.Create("Agent2", 200, 100));

        // Act
        var records = _tracker.GetRecords();

        // Assert
        records.Should().HaveCount(2);
    }

    [Fact]
    public void GetRecords_WithSourceFilter_ShouldReturnFilteredRecords()
    {
        // Arrange
        _tracker.Record(TokenUsageRecord.Create("Agent1", 100, 50));
        _tracker.Record(TokenUsageRecord.Create("Agent2", 200, 100));
        _tracker.Record(TokenUsageRecord.Create("Agent1", 50, 25));

        // Act
        var records = _tracker.GetRecords(source: "Agent1");

        // Assert
        records.Should().HaveCount(2);
        records.Should().AllSatisfy(r => r.Source.Should().Be("Agent1"));
    }

    [Fact]
    public void GetRecords_WithSessionFilter_ShouldReturnFilteredRecords()
    {
        // Arrange
        _tracker.Record(TokenUsageRecord.Create("Agent1", 100, 50, sessionId: "session-1"));
        _tracker.Record(TokenUsageRecord.Create("Agent1", 200, 100, sessionId: "session-2"));
        _tracker.Record(TokenUsageRecord.Create("Agent2", 50, 25, sessionId: "session-1"));

        // Act
        var records = _tracker.GetRecords(sessionId: "session-1");

        // Assert
        records.Should().HaveCount(2);
        records.Should().AllSatisfy(r => r.SessionId.Should().Be("session-1"));
    }

    [Fact]
    public void GetRecords_WithBothFilters_ShouldReturnFilteredRecords()
    {
        // Arrange
        _tracker.Record(TokenUsageRecord.Create("Agent1", 100, 50, sessionId: "session-1"));
        _tracker.Record(TokenUsageRecord.Create("Agent1", 200, 100, sessionId: "session-2"));
        _tracker.Record(TokenUsageRecord.Create("Agent2", 50, 25, sessionId: "session-1"));

        // Act
        var records = _tracker.GetRecords(source: "Agent1", sessionId: "session-1");

        // Assert
        records.Should().HaveCount(1);
        records[0].Source.Should().Be("Agent1");
        records[0].SessionId.Should().Be("session-1");
    }

    [Fact]
    public void Reset_ShouldClearAllData()
    {
        // Arrange
        _tracker.Record(TokenUsageRecord.Create("Agent1", 100, 50));
        _tracker.Record(TokenUsageRecord.Create("Agent2", 200, 100));

        // Act
        _tracker.Reset();

        // Assert
        _tracker.TotalPromptTokens.Should().Be(0);
        _tracker.TotalCompletionTokens.Should().Be(0);
        _tracker.TotalTokens.Should().Be(0);
        _tracker.CallCount.Should().Be(0);
        _tracker.GetRecords().Should().BeEmpty();
    }

    [Fact]
    public void ThreadSafety_ConcurrentRecords_ShouldBeAccurate()
    {
        // Arrange
        const int threadCount = 10;
        const int recordsPerThread = 100;
        const int promptTokens = 10;
        const int completionTokens = 5;

        // Act
        Parallel.For(
            0,
            threadCount,
            _ =>
            {
                for (int j = 0; j < recordsPerThread; j++)
                {
                    _tracker.Record(
                        TokenUsageRecord.Create($"Thread", promptTokens, completionTokens)
                    );
                }
            }
        );

        // Assert
        var expectedTotal = threadCount * recordsPerThread;
        _tracker.CallCount.Should().Be(expectedTotal);
        _tracker.TotalPromptTokens.Should().Be(expectedTotal * promptTokens);
        _tracker.TotalCompletionTokens.Should().Be(expectedTotal * completionTokens);
    }
}
