using Dawning.Agents.Abstractions.Logging;
using FluentAssertions;
using Xunit;

namespace Dawning.Agents.Tests.Logging;

/// <summary>
/// AgentLogContext 测试
/// </summary>
public class AgentLogContextTests
{
    [Fact]
    public void Current_ShouldBeNullByDefault()
    {
        // Assert
        AgentLogContext.Current.Should().BeNull();
    }

    [Fact]
    public void BeginScope_ShouldSetCurrentContext()
    {
        // Act
        using var scope = AgentLogContext.BeginScope(agentName: "TestAgent");

        // Assert
        AgentLogContext.Current.Should().NotBeNull();
        AgentLogContext.Current!.AgentName.Should().Be("TestAgent");
    }

    [Fact]
    public void BeginScope_ShouldGenerateRequestId()
    {
        // Act
        using var scope = AgentLogContext.BeginScope(agentName: "TestAgent");

        // Assert
        AgentLogContext.Current!.RequestId.Should().NotBeNullOrEmpty();
        AgentLogContext.Current!.RequestId.Should().HaveLength(8);
    }

    [Fact]
    public void BeginScope_ShouldRestorePreviousContextOnDispose()
    {
        // Arrange
        using var outerScope = AgentLogContext.BeginScope(agentName: "OuterAgent");
        var outerRequestId = AgentLogContext.Current!.RequestId;

        // Act
        using (var innerScope = AgentLogContext.BeginScope(agentName: "InnerAgent"))
        {
            AgentLogContext.Current!.AgentName.Should().Be("InnerAgent");
        }

        // Assert - should restore outer context
        AgentLogContext.Current.Should().NotBeNull();
        AgentLogContext.Current!.AgentName.Should().Be("OuterAgent");
        AgentLogContext.Current!.RequestId.Should().Be(outerRequestId);
    }

    [Fact]
    public void BeginScope_ShouldInheritFromPreviousContext()
    {
        // Arrange
        using var outerScope = AgentLogContext.BeginScope(
            agentName: "OuterAgent",
            sessionId: "session-123",
            userId: "user-456"
        );

        // Act
        using var innerScope = AgentLogContext.BeginScope(agentName: "InnerAgent");

        // Assert - should inherit sessionId and userId
        AgentLogContext.Current!.AgentName.Should().Be("InnerAgent");
        AgentLogContext.Current!.SessionId.Should().Be("session-123");
        AgentLogContext.Current!.UserId.Should().Be("user-456");
    }

    [Fact]
    public void BeginScope_ShouldAllowOverridingInheritedValues()
    {
        // Arrange
        using var outerScope = AgentLogContext.BeginScope(
            agentName: "OuterAgent",
            sessionId: "session-123"
        );

        // Act
        using var innerScope = AgentLogContext.BeginScope(sessionId: "session-456");

        // Assert
        AgentLogContext.Current!.AgentName.Should().Be("OuterAgent");
        AgentLogContext.Current!.SessionId.Should().Be("session-456");
    }

    [Fact]
    public void SetTool_ShouldUpdateCurrentContext()
    {
        // Arrange
        using var scope = AgentLogContext.BeginScope(agentName: "TestAgent");

        // Act
        AgentLogContext.SetTool("TestTool");

        // Assert
        AgentLogContext.Current!.ToolName.Should().Be("TestTool");
    }

    [Fact]
    public void SetTool_ShouldDoNothingWhenNoContext()
    {
        // Act - should not throw
        AgentLogContext.SetTool("TestTool");

        // Assert
        AgentLogContext.Current.Should().BeNull();
    }

    [Fact]
    public void SetStep_ShouldUpdateCurrentContext()
    {
        // Arrange
        using var scope = AgentLogContext.BeginScope(agentName: "TestAgent");

        // Act
        AgentLogContext.SetStep(5);

        // Assert
        AgentLogContext.Current!.StepNumber.Should().Be(5);
    }

    [Fact]
    public void SetStep_ShouldDoNothingWhenNoContext()
    {
        // Act - should not throw
        AgentLogContext.SetStep(5);

        // Assert
        AgentLogContext.Current.Should().BeNull();
    }

    [Fact]
    public void BeginScope_ShouldSetAllProperties()
    {
        // Act
        using var scope = AgentLogContext.BeginScope(
            agentName: "TestAgent",
            requestId: "req-123",
            sessionId: "sess-456",
            userId: "user-789",
            tenantId: "tenant-abc"
        );

        // Assert
        var ctx = AgentLogContext.Current!;
        ctx.AgentName.Should().Be("TestAgent");
        ctx.RequestId.Should().Be("req-123");
        ctx.SessionId.Should().Be("sess-456");
        ctx.UserId.Should().Be("user-789");
        ctx.TenantId.Should().Be("tenant-abc");
    }

    [Fact]
    public void Context_ShouldFlowToChildThreads()
    {
        // Arrange
        string? agentNameInThread = null;

        // Act
        using var scope = AgentLogContext.BeginScope(agentName: "MainAgent");

        var thread = new Thread(() =>
        {
            // AsyncLocal flows to child threads
            agentNameInThread = AgentLogContext.Current?.AgentName;
        });
        thread.Start();
        thread.Join();

        // Assert - AsyncLocal flows context to child threads
        agentNameInThread.Should().Be("MainAgent");
        AgentLogContext.Current!.AgentName.Should().Be("MainAgent");
    }

    [Fact]
    public void Context_ChildThreadModification_ShouldNotAffectParent()
    {
        // Arrange
        using var scope = AgentLogContext.BeginScope(agentName: "MainAgent");

        // Act
        var thread = new Thread(() =>
        {
            // Modify in child thread
            using var childScope = AgentLogContext.BeginScope(agentName: "ChildAgent");
            AgentLogContext.Current!.AgentName.Should().Be("ChildAgent");
        });
        thread.Start();
        thread.Join();

        // Assert - parent thread context should be unchanged
        AgentLogContext.Current!.AgentName.Should().Be("MainAgent");
    }
}
