namespace Dawning.Agents.Tests.Observability;

using Dawning.Agents.Abstractions.Observability;
using FluentAssertions;

public class HealthModelsTests
{
    [Fact]
    public void ComponentHealth_ShouldStoreValues()
    {
        // Arrange
        var data = new Dictionary<string, object> { ["latency"] = 100 };

        // Act
        var health = new ComponentHealth
        {
            Name = "LLM",
            Status = HealthStatus.Healthy,
            Message = "响应正常",
            Data = data,
        };

        // Assert
        health.Name.Should().Be("LLM");
        health.Status.Should().Be(HealthStatus.Healthy);
        health.Message.Should().Be("响应正常");
        health.Data.Should().BeEquivalentTo(data);
    }

    [Fact]
    public void ComponentHealth_MessageAndData_CanBeNull()
    {
        // Act
        var health = new ComponentHealth { Name = "Test", Status = HealthStatus.Healthy };

        // Assert
        health.Message.Should().BeNull();
        health.Data.Should().BeNull();
    }

    [Theory]
    [InlineData(HealthStatus.Healthy, 0)]
    [InlineData(HealthStatus.Degraded, 1)]
    [InlineData(HealthStatus.Unhealthy, 2)]
    public void HealthStatus_ShouldHaveCorrectValues(HealthStatus status, int expectedValue)
    {
        // Assert
        ((int)status)
            .Should()
            .Be(expectedValue);
    }

    [Fact]
    public void HealthCheckResult_ShouldStoreValues()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var components = new List<ComponentHealth>
        {
            new() { Name = "LLM", Status = HealthStatus.Healthy },
            new() { Name = "Memory", Status = HealthStatus.Degraded },
        };

        // Act
        var result = new HealthCheckResult
        {
            Status = HealthStatus.Degraded,
            Timestamp = timestamp,
            Components = components,
        };

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Timestamp.Should().Be(timestamp);
        result.Components.Should().HaveCount(2);
    }

    [Fact]
    public void HealthCheckResult_Components_DefaultEmpty()
    {
        // Act
        var result = new HealthCheckResult
        {
            Status = HealthStatus.Healthy,
            Timestamp = DateTime.UtcNow,
        };

        // Assert
        result.Components.Should().BeEmpty();
    }
}
