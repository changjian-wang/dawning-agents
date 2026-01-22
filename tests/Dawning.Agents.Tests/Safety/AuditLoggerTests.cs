using Dawning.Agents.Abstractions.Safety;
using Dawning.Agents.Core.Safety;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Tests.Safety;

public class InMemoryAuditLoggerTests
{
    private static IOptions<AuditOptions> CreateOptions(bool enabled = true)
    {
        return Options.Create(
            new AuditOptions
            {
                Enabled = enabled,
                MaxInMemoryEntries = 1000,
                MaxContentLength = 500,
            }
        );
    }

    [Fact]
    public async Task LogAsync_ShouldAddEntry()
    {
        // Arrange
        var options = CreateOptions();
        var logger = new InMemoryAuditLogger(options);

        var entry = new AuditEntry
        {
            EventType = AuditEventType.AgentRunStart,
            AgentName = "TestAgent",
            SessionId = "session1",
            Input = "Hello",
        };

        // Act
        await logger.LogAsync(entry);

        // Assert
        var entries = logger.GetAllEntries();
        entries.Should().HaveCount(1);
        entries[0].AgentName.Should().Be("TestAgent");
    }

    [Fact]
    public async Task LogAsync_WhenDisabled_ShouldNotAddEntry()
    {
        // Arrange
        var options = CreateOptions(enabled: false);
        var logger = new InMemoryAuditLogger(options);

        var entry = new AuditEntry
        {
            EventType = AuditEventType.AgentRunStart,
            AgentName = "TestAgent",
        };

        // Act
        await logger.LogAsync(entry);

        // Assert
        var entries = logger.GetAllEntries();
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryAsync_WithFilter_ShouldFilterByEventType()
    {
        // Arrange
        var options = CreateOptions();
        var logger = new InMemoryAuditLogger(options);

        await logger.LogAsync(
            new AuditEntry { EventType = AuditEventType.AgentRunStart, AgentName = "Agent1" }
        );
        await logger.LogAsync(
            new AuditEntry { EventType = AuditEventType.ToolCall, ToolName = "Tool1" }
        );
        await logger.LogAsync(
            new AuditEntry { EventType = AuditEventType.AgentRunEnd, AgentName = "Agent1" }
        );

        var filter = new AuditFilter { EventType = AuditEventType.AgentRunStart };

        // Act
        var entries = await logger.QueryAsync(filter);

        // Assert
        entries.Should().HaveCount(1);
        entries[0].EventType.Should().Be(AuditEventType.AgentRunStart);
    }

    [Fact]
    public async Task QueryAsync_WithFilter_ShouldFilterByAgentName()
    {
        // Arrange
        var options = CreateOptions();
        var logger = new InMemoryAuditLogger(options);

        await logger.LogAsync(
            new AuditEntry { EventType = AuditEventType.AgentRunStart, AgentName = "Agent1" }
        );
        await logger.LogAsync(
            new AuditEntry { EventType = AuditEventType.AgentRunStart, AgentName = "Agent2" }
        );
        await logger.LogAsync(
            new AuditEntry { EventType = AuditEventType.AgentRunEnd, AgentName = "Agent1" }
        );

        var filter = new AuditFilter { AgentName = "Agent1" };

        // Act
        var entries = await logger.QueryAsync(filter);

        // Assert
        entries.Should().HaveCount(2);
        entries.Should().AllSatisfy(e => e.AgentName.Should().Be("Agent1"));
    }

    [Fact]
    public async Task QueryAsync_WithFilter_ShouldFilterBySessionId()
    {
        // Arrange
        var options = CreateOptions();
        var logger = new InMemoryAuditLogger(options);

        await logger.LogAsync(
            new AuditEntry { EventType = AuditEventType.AgentRunStart, SessionId = "session1" }
        );
        await logger.LogAsync(
            new AuditEntry { EventType = AuditEventType.AgentRunStart, SessionId = "session2" }
        );
        await logger.LogAsync(
            new AuditEntry { EventType = AuditEventType.AgentRunEnd, SessionId = "session1" }
        );

        var filter = new AuditFilter { SessionId = "session1" };

        // Act
        var entries = await logger.QueryAsync(filter);

        // Assert
        entries.Should().HaveCount(2);
        entries.Should().AllSatisfy(e => e.SessionId.Should().Be("session1"));
    }

    [Fact]
    public async Task QueryAsync_WithFilter_ShouldFilterByTimeRange()
    {
        // Arrange
        var options = CreateOptions();
        var logger = new InMemoryAuditLogger(options);

        var now = DateTimeOffset.UtcNow;

        await logger.LogAsync(
            new AuditEntry
            {
                EventType = AuditEventType.AgentRunStart,
                Timestamp = now.AddHours(-2),
            }
        );
        await logger.LogAsync(
            new AuditEntry { EventType = AuditEventType.AgentRunStart, Timestamp = now }
        );
        await logger.LogAsync(
            new AuditEntry
            {
                EventType = AuditEventType.AgentRunStart,
                Timestamp = now.AddHours(2),
            }
        );

        var filter = new AuditFilter
        {
            StartTime = now.AddMinutes(-30),
            EndTime = now.AddMinutes(30),
        };

        // Act
        var entries = await logger.QueryAsync(filter);

        // Assert
        entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task QueryAsync_WithFilter_ShouldFilterByStatus()
    {
        // Arrange
        var options = CreateOptions();
        var logger = new InMemoryAuditLogger(options);

        await logger.LogAsync(
            new AuditEntry { EventType = AuditEventType.AgentRunEnd, Status = AuditResultStatus.Success }
        );
        await logger.LogAsync(
            new AuditEntry { EventType = AuditEventType.AgentRunEnd, Status = AuditResultStatus.Failed }
        );
        await logger.LogAsync(
            new AuditEntry { EventType = AuditEventType.GuardrailTriggered, Status = AuditResultStatus.Blocked }
        );

        var filter = new AuditFilter { Status = AuditResultStatus.Blocked };

        // Act
        var entries = await logger.QueryAsync(filter);

        // Assert
        entries.Should().HaveCount(1);
        entries[0].Status.Should().Be(AuditResultStatus.Blocked);
    }

    [Fact]
    public async Task LogAsync_ShouldRespectMaxEntries()
    {
        // Arrange
        var options = Options.Create(
            new AuditOptions
            {
                Enabled = true,
                MaxInMemoryEntries = 3,
            }
        );
        var logger = new InMemoryAuditLogger(options);

        // Act - Add 5 entries
        for (int i = 0; i < 5; i++)
        {
            await logger.LogAsync(
                new AuditEntry
                {
                    EventType = AuditEventType.AgentRunStart,
                    Input = $"Entry{i}",
                }
            );
        }

        // Assert - Should only keep last 3
        logger.Count.Should().Be(3);
    }

    [Fact]
    public async Task Clear_ShouldRemoveAllEntries()
    {
        // Arrange
        var options = CreateOptions();
        var logger = new InMemoryAuditLogger(options);

        await logger.LogAsync(new AuditEntry { EventType = AuditEventType.AgentRunStart });
        await logger.LogAsync(new AuditEntry { EventType = AuditEventType.AgentRunEnd });

        // Act
        logger.Clear();

        // Assert
        logger.Count.Should().Be(0);
        logger.GetAllEntries().Should().BeEmpty();
    }

    [Fact]
    public async Task LogAsync_ShouldTruncateLongContent()
    {
        // Arrange
        var options = Options.Create(
            new AuditOptions
            {
                Enabled = true,
                MaxContentLength = 20,
                LogInput = true,
            }
        );
        var logger = new InMemoryAuditLogger(options);

        var longInput = new string('A', 100);

        await logger.LogAsync(
            new AuditEntry
            {
                EventType = AuditEventType.AgentRunStart,
                Input = longInput,
            }
        );

        // Assert
        var entries = logger.GetAllEntries();
        entries[0].Input.Should().HaveLength(20 + "[TRUNCATED]".Length + 3); // 20 chars + "...[TRUNCATED]"
        entries[0].Input.Should().EndWith("...[TRUNCATED]");
    }

    [Fact]
    public async Task LogAsync_ShouldRedactContentWhenDisabled()
    {
        // Arrange
        var options = Options.Create(
            new AuditOptions
            {
                Enabled = true,
                LogInput = false,
            }
        );
        var logger = new InMemoryAuditLogger(options);

        await logger.LogAsync(
            new AuditEntry
            {
                EventType = AuditEventType.AgentRunStart,
                Input = "Sensitive data",
            }
        );

        // Assert
        var entries = logger.GetAllEntries();
        entries[0].Input.Should().Be("[REDACTED]");
    }
}
