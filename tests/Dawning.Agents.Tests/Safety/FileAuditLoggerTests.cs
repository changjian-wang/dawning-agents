using Dawning.Agents.Abstractions.Safety;
using Dawning.Agents.Core.Safety;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Tests.Safety;

/// <summary>
/// 文件审计日志记录器测试
/// </summary>
public sealed class FileAuditLoggerTests : IAsyncLifetime, IAsyncDisposable
{
    private readonly string _testDir;
    private FileAuditLogger _logger = null!;

    public FileAuditLoggerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"audit_test_{Guid.NewGuid():N}");
    }

    public Task InitializeAsync()
    {
        _logger = CreateLogger();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await ((IAsyncDisposable)this).DisposeAsync();
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await _logger.DisposeAsync();

        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public async Task LogAsync_WritesJsonLineToFile()
    {
        // Arrange
        var entry = CreateEntry(AuditEventType.AgentRunStart, "TestAgent");

        // Act
        await _logger.LogAsync(entry);
        await _logger.DisposeAsync();

        // Assert
        var files = Directory.GetFiles(_testDir, "*.jsonl");
        files.Should().HaveCount(1);

        var lines = await File.ReadAllLinesAsync(files[0]);
        lines.Should().HaveCount(1);
        lines[0].Should().Contain("TestAgent");
    }

    [Fact]
    public async Task LogAsync_WhenDisabled_DoesNotWrite()
    {
        // Arrange
        await _logger.DisposeAsync();
        _logger = CreateLogger(auditEnabled: false);

        var entry = CreateEntry(AuditEventType.AgentRunStart, "TestAgent");

        // Act
        await _logger.LogAsync(entry);
        await _logger.DisposeAsync();

        // Assert
        var files = Directory.GetFiles(_testDir, "*.jsonl");
        if (files.Length > 0)
        {
            var content = await File.ReadAllTextAsync(files[0]);
            content.Trim().Should().BeEmpty();
        }
    }

    [Fact]
    public async Task LogAsync_MultipleEntries_AllPersisted()
    {
        // Arrange & Act
        for (var i = 0; i < 10; i++)
        {
            await _logger.LogAsync(CreateEntry(AuditEventType.ToolCall, $"Agent{i}"));
        }

        await _logger.DisposeAsync();

        // Assert
        var lines = await File.ReadAllLinesAsync(Directory.GetFiles(_testDir, "*.jsonl").Single());
        lines.Should().HaveCount(10);
    }

    [Fact]
    public async Task QueryAsync_FiltersBySessionId()
    {
        // Arrange
        await _logger.LogAsync(
            CreateEntry(AuditEventType.AgentRunStart, "Agent1", sessionId: "session-1")
        );
        await _logger.LogAsync(
            CreateEntry(AuditEventType.AgentRunEnd, "Agent2", sessionId: "session-2")
        );

        // Act
        var results = await _logger.QueryAsync(new AuditFilter { SessionId = "session-1" });

        // Assert
        results.Should().HaveCount(1);
        results[0].SessionId.Should().Be("session-1");
    }

    [Fact]
    public async Task QueryAsync_FiltersByAgentName()
    {
        // Arrange
        await _logger.LogAsync(CreateEntry(AuditEventType.AgentRunStart, "Agent1"));
        await _logger.LogAsync(CreateEntry(AuditEventType.AgentRunEnd, "Agent2"));

        // Act
        var results = await _logger.QueryAsync(new AuditFilter { AgentName = "Agent2" });

        // Assert
        results.Should().HaveCount(1);
        results[0].AgentName.Should().Be("Agent2");
    }

    [Fact]
    public async Task QueryAsync_FiltersByEventType()
    {
        // Arrange
        await _logger.LogAsync(CreateEntry(AuditEventType.AgentRunStart, "Agent1"));
        await _logger.LogAsync(CreateEntry(AuditEventType.ToolCall, "Agent1"));
        await _logger.LogAsync(CreateEntry(AuditEventType.AgentRunEnd, "Agent1"));

        // Act
        var results = await _logger.QueryAsync(
            new AuditFilter { EventType = AuditEventType.ToolCall }
        );

        // Assert
        results.Should().HaveCount(1);
        results[0].EventType.Should().Be(AuditEventType.ToolCall);
    }

    [Fact]
    public async Task QueryAsync_RespectsMaxResults()
    {
        // Arrange
        for (var i = 0; i < 20; i++)
        {
            await _logger.LogAsync(CreateEntry(AuditEventType.ToolCall, $"Agent{i}"));
        }

        // Act
        var results = await _logger.QueryAsync(new AuditFilter { MaxResults = 5 });

        // Assert
        results.Should().HaveCount(5);
    }

    [Fact]
    public async Task QueryAsync_OrdersByTimestampDescending()
    {
        // Arrange
        var entries = new List<AuditEntry>();
        for (var i = 0; i < 5; i++)
        {
            var entry = new AuditEntry
            {
                EventType = AuditEventType.ToolCall,
                AgentName = $"Agent{i}",
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(i),
            };
            entries.Add(entry);
            await _logger.LogAsync(entry);
        }

        // Act
        var results = await _logger.QueryAsync(new AuditFilter());

        // Assert
        results.Should().BeInDescendingOrder(e => e.Timestamp);
    }

    [Fact]
    public async Task LogAsync_TruncatesLongContent()
    {
        // Arrange
        var longInput = new string('x', 2000);
        var entry = new AuditEntry
        {
            EventType = AuditEventType.AgentRunStart,
            AgentName = "TestAgent",
            Input = longInput,
        };

        // Act
        await _logger.LogAsync(entry);

        // Assert
        var results = await _logger.QueryAsync(new AuditFilter());
        results.Should().HaveCount(1);
        results[0].Input!.Length.Should().BeLessThan(longInput.Length);
        results[0].Input.Should().EndWith("...[TRUNCATED]");
    }

    [Fact]
    public async Task LogAsync_RotatesWhenFileSizeExceeded()
    {
        // Arrange — use small max size to trigger rotation
        await _logger.DisposeAsync();
        _logger = CreateLogger(maxFileSizeBytes: 500);

        // Act — write enough entries to exceed 500 bytes
        for (var i = 0; i < 20; i++)
        {
            await _logger.LogAsync(
                new AuditEntry
                {
                    EventType = AuditEventType.ToolCall,
                    AgentName = "TestAgent",
                    Input = $"This is a test input {i} with some content to fill space",
                    Output = $"This is a test output {i} with some content to fill space",
                }
            );
        }

        await _logger.DisposeAsync();

        // Assert — should have more than 1 file (current + rotated archives)
        var files = Directory.GetFiles(_testDir, "*.jsonl");
        files.Length.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task DisposeAsync_FlushesBeforeClose()
    {
        // Arrange
        await _logger.LogAsync(CreateEntry(AuditEventType.AgentRunStart, "TestAgent"));

        // Act
        await _logger.DisposeAsync();

        // Assert — file should have content
        var files = Directory.GetFiles(_testDir, "*.jsonl");
        files.Should().NotBeEmpty();
        var content = await File.ReadAllTextAsync(files[0]);
        content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FileAuditOptions_Validate_ThrowsOnEmptyDirectory()
    {
        // Arrange
        var options = new FileAuditOptions { Directory = "" };

        // Act & Assert
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task FileAuditOptions_Validate_ThrowsOnTooSmallFileSize()
    {
        // Arrange
        var options = new FileAuditOptions { MaxFileSizeBytes = 100 };

        // Act & Assert
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>();
    }

    private FileAuditLogger CreateLogger(
        bool auditEnabled = true,
        long maxFileSizeBytes = 50 * 1024 * 1024
    )
    {
        var fileOptions = Options.Create(
            new FileAuditOptions
            {
                Directory = _testDir,
                FilePrefix = "test_audit_",
                MaxFileSizeBytes = maxFileSizeBytes,
                MaxRetainedFiles = 5,
            }
        );

        var auditOptions = Options.Create(
            new AuditOptions { Enabled = auditEnabled, MaxContentLength = 1000 }
        );

        return new FileAuditLogger(fileOptions, auditOptions, NullLogger<FileAuditLogger>.Instance);
    }

    private static AuditEntry CreateEntry(
        AuditEventType eventType,
        string agentName,
        string? sessionId = null
    )
    {
        return new AuditEntry
        {
            EventType = eventType,
            AgentName = agentName,
            SessionId = sessionId ?? Guid.NewGuid().ToString("N"),
            Input = "test input",
            Output = "test output",
        };
    }
}
