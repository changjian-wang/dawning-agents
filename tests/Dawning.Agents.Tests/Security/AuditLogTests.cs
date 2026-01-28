using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dawning.Agents.Abstractions.Security;
using Dawning.Agents.Core.Security;
using FluentAssertions;
using Xunit;

namespace Dawning.Agents.Tests.Security;

public class InMemoryAuditLogProviderTests
{
    [Fact]
    public async Task WriteAsync_ShouldStoreEntry()
    {
        // Arrange
        var provider = new InMemoryAuditLogProvider();
        var entry = new AuditLogEntry
        {
            UserId = "user1",
            Action = AuditActions.AgentRequest,
            Resource = "test-agent",
            IsSuccess = true,
        };

        // Act
        await provider.WriteAsync(entry);
        var results = await provider.QueryAsync(new AuditLogQuery { UserId = "user1" });

        // Assert
        results.Should().HaveCount(1);
        results[0].Action.Should().Be(AuditActions.AgentRequest);
    }

    [Fact]
    public async Task WriteAsync_ShouldLimitEntries()
    {
        // Arrange
        var maxEntries = 5;
        var provider = new InMemoryAuditLogProvider(maxEntries: maxEntries);

        // Act - Write more than max entries
        for (int i = 0; i < 10; i++)
        {
            await provider.WriteAsync(
                new AuditLogEntry
                {
                    UserId = $"user{i}",
                    Action = AuditActions.AgentRequest,
                    Resource = "test",
                    IsSuccess = true,
                }
            );
        }

        var results = await provider.QueryAsync(new AuditLogQuery { Take = 100 });

        // Assert - Should only have maxEntries
        results.Should().HaveCount(maxEntries);
    }

    [Fact]
    public async Task WriteBatchAsync_ShouldStoreAllEntries()
    {
        // Arrange
        var provider = new InMemoryAuditLogProvider();
        var entries = new List<AuditLogEntry>
        {
            new()
            {
                UserId = "user1",
                Action = AuditActions.AgentRequest,
                Resource = "agent1",
                IsSuccess = true,
            },
            new()
            {
                UserId = "user2",
                Action = AuditActions.ToolExecute,
                Resource = "tool1",
                IsSuccess = true,
            },
            new()
            {
                UserId = "user3",
                Action = AuditActions.LLMCall,
                Resource = "llm1",
                IsSuccess = false,
            },
        };

        // Act
        await provider.WriteBatchAsync(entries);
        var results = await provider.QueryAsync(new AuditLogQuery { Take = 100 });

        // Assert
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task QueryAsync_WithUserIdFilter_ShouldFilterCorrectly()
    {
        // Arrange
        var provider = new InMemoryAuditLogProvider();
        await provider.WriteAsync(
            new AuditLogEntry
            {
                UserId = "user1",
                Action = "test",
                Resource = "r1",
                IsSuccess = true,
            }
        );
        await provider.WriteAsync(
            new AuditLogEntry
            {
                UserId = "user2",
                Action = "test",
                Resource = "r2",
                IsSuccess = true,
            }
        );
        await provider.WriteAsync(
            new AuditLogEntry
            {
                UserId = "user1",
                Action = "test",
                Resource = "r3",
                IsSuccess = true,
            }
        );

        // Act
        var results = await provider.QueryAsync(new AuditLogQuery { UserId = "user1" });

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(e => e.UserId.Should().Be("user1"));
    }

    [Fact]
    public async Task QueryAsync_WithActionFilter_ShouldFilterCorrectly()
    {
        // Arrange
        var provider = new InMemoryAuditLogProvider();
        await provider.WriteAsync(
            new AuditLogEntry
            {
                UserId = "u1",
                Action = AuditActions.AgentRequest,
                Resource = "r1",
                IsSuccess = true,
            }
        );
        await provider.WriteAsync(
            new AuditLogEntry
            {
                UserId = "u2",
                Action = AuditActions.ToolExecute,
                Resource = "r2",
                IsSuccess = true,
            }
        );
        await provider.WriteAsync(
            new AuditLogEntry
            {
                UserId = "u3",
                Action = AuditActions.AgentRequest,
                Resource = "r3",
                IsSuccess = true,
            }
        );

        // Act
        var results = await provider.QueryAsync(
            new AuditLogQuery { Action = AuditActions.AgentRequest }
        );

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(e => e.Action.Should().Be(AuditActions.AgentRequest));
    }

    [Fact]
    public async Task QueryAsync_WithResourceFilter_ShouldFilterCorrectly()
    {
        // Arrange
        var provider = new InMemoryAuditLogProvider();
        await provider.WriteAsync(
            new AuditLogEntry
            {
                UserId = "u1",
                Action = "test",
                Resource = "agent-a",
                IsSuccess = true,
            }
        );
        await provider.WriteAsync(
            new AuditLogEntry
            {
                UserId = "u2",
                Action = "test",
                Resource = "agent-b",
                IsSuccess = true,
            }
        );

        // Act
        var results = await provider.QueryAsync(new AuditLogQuery { Resource = "agent-a" });

        // Assert
        results.Should().HaveCount(1);
        results[0].Resource.Should().Be("agent-a");
    }

    [Fact]
    public async Task QueryAsync_WithTimeRange_ShouldFilterCorrectly()
    {
        // Arrange
        var provider = new InMemoryAuditLogProvider();
        var now = DateTimeOffset.UtcNow;

        await provider.WriteAsync(
            new AuditLogEntry
            {
                UserId = "u1",
                Action = "test",
                Resource = "r1",
                Timestamp = now.AddHours(-2),
                IsSuccess = true,
            }
        );
        await provider.WriteAsync(
            new AuditLogEntry
            {
                UserId = "u2",
                Action = "test",
                Resource = "r2",
                Timestamp = now.AddMinutes(-30),
                IsSuccess = true,
            }
        );
        await provider.WriteAsync(
            new AuditLogEntry
            {
                UserId = "u3",
                Action = "test",
                Resource = "r3",
                Timestamp = now.AddMinutes(-5),
                IsSuccess = true,
            }
        );

        // Act
        var results = await provider.QueryAsync(
            new AuditLogQuery { StartTime = now.AddHours(-1), EndTime = now }
        );

        // Assert
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task QueryAsync_WithIsSuccessFilter_ShouldFilterCorrectly()
    {
        // Arrange
        var provider = new InMemoryAuditLogProvider();
        await provider.WriteAsync(
            new AuditLogEntry
            {
                UserId = "u1",
                Action = "test",
                Resource = "r1",
                IsSuccess = true,
            }
        );
        await provider.WriteAsync(
            new AuditLogEntry
            {
                UserId = "u2",
                Action = "test",
                Resource = "r2",
                IsSuccess = false,
            }
        );
        await provider.WriteAsync(
            new AuditLogEntry
            {
                UserId = "u3",
                Action = "test",
                Resource = "r3",
                IsSuccess = true,
            }
        );

        // Act
        var failed = await provider.QueryAsync(new AuditLogQuery { IsSuccess = false });

        // Assert
        failed.Should().HaveCount(1);
        failed[0].IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task QueryAsync_WithPagination_ShouldWorkCorrectly()
    {
        // Arrange
        var provider = new InMemoryAuditLogProvider();
        for (int i = 0; i < 10; i++)
        {
            await provider.WriteAsync(
                new AuditLogEntry
                {
                    UserId = $"user{i}",
                    Action = "test",
                    Resource = "r",
                    IsSuccess = true,
                }
            );
        }

        // Act
        var page1 = await provider.QueryAsync(new AuditLogQuery { Skip = 0, Take = 3 });
        var page2 = await provider.QueryAsync(new AuditLogQuery { Skip = 3, Take = 3 });

        // Assert
        page1.Should().HaveCount(3);
        page2.Should().HaveCount(3);
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnOrderedByTimestampDescending()
    {
        // Arrange
        var provider = new InMemoryAuditLogProvider();
        var now = DateTimeOffset.UtcNow;

        await provider.WriteAsync(
            new AuditLogEntry
            {
                Timestamp = now.AddMinutes(-3),
                UserId = "u1",
                Action = "a",
                Resource = "r",
                IsSuccess = true,
            }
        );
        await provider.WriteAsync(
            new AuditLogEntry
            {
                Timestamp = now.AddMinutes(-1),
                UserId = "u2",
                Action = "a",
                Resource = "r",
                IsSuccess = true,
            }
        );
        await provider.WriteAsync(
            new AuditLogEntry
            {
                Timestamp = now.AddMinutes(-2),
                UserId = "u3",
                Action = "a",
                Resource = "r",
                IsSuccess = true,
            }
        );

        // Act
        var results = await provider.QueryAsync(new AuditLogQuery());

        // Assert
        results[0].UserId.Should().Be("u2"); // Most recent
        results[1].UserId.Should().Be("u3");
        results[2].UserId.Should().Be("u1"); // Oldest
    }
}

public class AuditLogEntryTests
{
    [Fact]
    public void DefaultValues_ShouldBeSet()
    {
        // Act
        var entry = new AuditLogEntry();

        // Assert
        entry.Id.Should().NotBeNullOrEmpty();
        entry.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        entry.Action.Should().BeEmpty();
        entry.Resource.Should().BeEmpty();
        entry.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void CanSetAllProperties()
    {
        // Arrange & Act
        var metadata = new Dictionary<string, object> { ["key"] = "value" };
        var entry = new AuditLogEntry
        {
            Id = "custom-id",
            UserId = "user123",
            UserName = "John",
            Action = AuditActions.ToolExecute,
            Resource = "file-tool",
            ResourceId = "file-123",
            IsSuccess = true,
            ErrorMessage = null,
            IpAddress = "192.168.1.1",
            UserAgent = "Test/1.0",
            Duration = TimeSpan.FromMilliseconds(100),
            Metadata = metadata,
        };

        // Assert
        entry.Id.Should().Be("custom-id");
        entry.UserId.Should().Be("user123");
        entry.UserName.Should().Be("John");
        entry.Action.Should().Be(AuditActions.ToolExecute);
        entry.Resource.Should().Be("file-tool");
        entry.ResourceId.Should().Be("file-123");
        entry.IsSuccess.Should().BeTrue();
        entry.IpAddress.Should().Be("192.168.1.1");
        entry.UserAgent.Should().Be("Test/1.0");
        entry.Duration.Should().Be(TimeSpan.FromMilliseconds(100));
        entry.Metadata.Should().ContainKey("key");
    }
}

public class AuditLogQueryTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Act
        var query = new AuditLogQuery();

        // Assert
        query.UserId.Should().BeNull();
        query.Action.Should().BeNull();
        query.Resource.Should().BeNull();
        query.StartTime.Should().BeNull();
        query.EndTime.Should().BeNull();
        query.IsSuccess.Should().BeNull();
        query.Skip.Should().Be(0);
        query.Take.Should().Be(100);
    }
}

public class AuditActionsTests
{
    [Fact]
    public void Constants_ShouldHaveExpectedValues()
    {
        // Assert
        AuditActions.AgentRequest.Should().Be("agent.request");
        AuditActions.AgentResponse.Should().Be("agent.response");
        AuditActions.ToolExecute.Should().Be("tool.execute");
        AuditActions.LLMCall.Should().Be("llm.call");
        AuditActions.Authentication.Should().Be("auth.authenticate");
        AuditActions.AuthenticationFailed.Should().Be("auth.authenticate.failed");
        AuditActions.Authorization.Should().Be("auth.authorize");
        AuditActions.AuthorizationDenied.Should().Be("auth.authorize.denied");
        AuditActions.RateLimitExceeded.Should().Be("ratelimit.exceeded");
        AuditActions.ConfigChange.Should().Be("config.change");
    }
}
