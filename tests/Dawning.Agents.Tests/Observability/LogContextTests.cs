namespace Dawning.Agents.Tests.Observability;

using Dawning.Agents.Core.Observability;
using FluentAssertions;

public class LogContextTests
{
    [Fact]
    public void Current_WithoutPush_ShouldBeNull()
    {
        // Assert
        LogContext.Current.Should().BeNull();
    }

    [Fact]
    public void Push_ShouldCreateContext()
    {
        // Act
        using var context = LogContext.Push();

        // Assert
        LogContext.Current.Should().NotBeNull();
        LogContext.Current.Should().BeSameAs(context);
    }

    [Fact]
    public void Dispose_ShouldRestoreParent()
    {
        // Arrange
        using var outer = LogContext.Push();
        outer.Set("outer", "value");

        // Act
        using (var inner = LogContext.Push())
        {
            inner.Set("inner", "value");
            LogContext.Current.Should().BeSameAs(inner);
        }

        // Assert
        LogContext.Current.Should().BeSameAs(outer);
    }

    [Fact]
    public void Set_ShouldStoreProperty()
    {
        // Arrange
        using var context = LogContext.Push();

        // Act
        context.Set("key", "value");

        // Assert
        context.Get("key").Should().Be("value");
    }

    [Fact]
    public void Set_ShouldReturnContext()
    {
        // Arrange
        using var context = LogContext.Push();

        // Act
        var result = context.Set("key", "value");

        // Assert
        result.Should().BeSameAs(context);
    }

    [Fact]
    public void Get_WithNonExistent_ShouldReturnNull()
    {
        // Arrange
        using var context = LogContext.Push();

        // Act
        var value = context.Get("nonexistent");

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public void Get_ShouldInheritFromParent()
    {
        // Arrange
        using var outer = LogContext.Push();
        outer.Set("parent", "parentValue");

        using var inner = LogContext.Push();
        inner.Set("child", "childValue");

        // Act & Assert
        inner.Get("parent").Should().Be("parentValue");
        inner.Get("child").Should().Be("childValue");
    }

    [Fact]
    public void GetAllProperties_ShouldReturnAllProperties()
    {
        // Arrange
        using var outer = LogContext.Push();
        outer.Set("key1", "value1");

        using var inner = LogContext.Push();
        inner.Set("key2", "value2");

        // Act
        var properties = inner.GetAllProperties();

        // Assert
        properties.Should().HaveCount(2);
        properties["key1"].Should().Be("value1");
        properties["key2"].Should().Be("value2");
    }

    [Fact]
    public void GetAllProperties_ChildShouldOverrideParent()
    {
        // Arrange
        using var outer = LogContext.Push();
        outer.Set("key", "parentValue");

        using var inner = LogContext.Push();
        inner.Set("key", "childValue");

        // Act
        var properties = inner.GetAllProperties();

        // Assert
        properties["key"].Should().Be("childValue");
    }

    [Fact]
    public void Extensions_ShouldSetCorrectKeys()
    {
        // Arrange
        using var context = LogContext.Push();

        // Act
        context
            .WithRequestId("req-123")
            .WithAgentName("TestAgent")
            .WithUserId("user-456")
            .WithSessionId("session-789")
            .WithCorrelationId("corr-000");

        // Assert
        context.Get("RequestId").Should().Be("req-123");
        context.Get("AgentName").Should().Be("TestAgent");
        context.Get("UserId").Should().Be("user-456");
        context.Get("SessionId").Should().Be("session-789");
        context.Get("CorrelationId").Should().Be("corr-000");
    }
}
