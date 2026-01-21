namespace Dawning.Agents.Tests.Orchestration;

using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Orchestration;
using FluentAssertions;
using Xunit;

/// <summary>
/// OrchestrationResult 和 OrchestrationContext 单元测试
/// </summary>
public class OrchestrationResultTests
{
    [Fact]
    public void Successful_CreatesCorrectResult()
    {
        // Arrange
        var agentResults = new List<AgentExecutionRecord>
        {
            new()
            {
                AgentName = "Agent1",
                Input = "input",
                Response = AgentResponse.Successful("output", [], TimeSpan.Zero),
                ExecutionOrder = 0,
                StartTime = DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow,
            },
        };
        var metadata = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var result = OrchestrationResult.Successful(
            "Final output",
            agentResults,
            TimeSpan.FromSeconds(1),
            metadata
        );

        // Assert
        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Final output");
        result.Error.Should().BeNull();
        result.AgentResults.Should().HaveCount(1);
        result.Duration.Should().Be(TimeSpan.FromSeconds(1));
        result.Metadata.Should().ContainKey("key");
    }

    [Fact]
    public void Failed_CreatesCorrectResult()
    {
        // Arrange
        var agentResults = new List<AgentExecutionRecord>();

        // Act
        var result = OrchestrationResult.Failed(
            "Something went wrong",
            agentResults,
            TimeSpan.FromMilliseconds(100)
        );

        // Assert
        result.Success.Should().BeFalse();
        result.FinalOutput.Should().BeNull();
        result.Error.Should().Be("Something went wrong");
        result.AgentResults.Should().BeEmpty();
    }

    [Fact]
    public void AgentExecutionRecord_HasAllProperties()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddSeconds(1);
        var response = AgentResponse.Successful("result", [], TimeSpan.FromSeconds(1));

        // Act
        var record = new AgentExecutionRecord
        {
            AgentName = "TestAgent",
            Input = "test input",
            Response = response,
            ExecutionOrder = 0,
            StartTime = startTime,
            EndTime = endTime,
        };

        // Assert
        record.AgentName.Should().Be("TestAgent");
        record.Input.Should().Be("test input");
        record.Response.Should().Be(response);
        record.ExecutionOrder.Should().Be(0);
        record.StartTime.Should().Be(startTime);
        record.EndTime.Should().Be(endTime);
    }
}

/// <summary>
/// OrchestrationContext 单元测试
/// </summary>
public class OrchestrationContextTests
{
    [Fact]
    public void Constructor_GeneratesSessionId()
    {
        // Act
        var context1 = new OrchestrationContext { UserInput = "test" };
        var context2 = new OrchestrationContext { UserInput = "test" };

        // Assert
        context1.SessionId.Should().NotBeNullOrEmpty();
        context2.SessionId.Should().NotBeNullOrEmpty();
        context1.SessionId.Should().NotBe(context2.SessionId);
    }

    [Fact]
    public void Properties_AreInitializedCorrectly()
    {
        // Act
        var context = new OrchestrationContext
        {
            UserInput = "Hello",
            CurrentInput = "Processed Hello",
        };

        // Assert
        context.UserInput.Should().Be("Hello");
        context.CurrentInput.Should().Be("Processed Hello");
        context.ExecutionHistory.Should().BeEmpty();
        context.Metadata.Should().BeEmpty();
        context.ShouldStop.Should().BeFalse();
        context.StopReason.Should().BeNull();
    }

    [Fact]
    public void ExecutionHistory_CanBeModified()
    {
        // Arrange
        var context = new OrchestrationContext { UserInput = "test" };
        var record = new AgentExecutionRecord
        {
            AgentName = "Agent1",
            Input = "input",
            Response = AgentResponse.Successful("output", [], TimeSpan.Zero),
            ExecutionOrder = 0,
            StartTime = DateTimeOffset.UtcNow,
            EndTime = DateTimeOffset.UtcNow,
        };

        // Act
        context.ExecutionHistory.Add(record);

        // Assert
        context.ExecutionHistory.Should().HaveCount(1);
        context.ExecutionHistory[0].AgentName.Should().Be("Agent1");
    }

    [Fact]
    public void Metadata_CanBeModified()
    {
        // Arrange
        var context = new OrchestrationContext { UserInput = "test" };

        // Act
        context.Metadata["key1"] = "value1";
        context.Metadata["key2"] = 42;

        // Assert
        context.Metadata.Should().HaveCount(2);
        context.Metadata["key1"].Should().Be("value1");
        context.Metadata["key2"].Should().Be(42);
    }

    [Fact]
    public void ShouldStop_CanBeSet()
    {
        // Arrange
        var context = new OrchestrationContext { UserInput = "test" };

        // Act
        context.ShouldStop = true;
        context.StopReason = "User cancelled";

        // Assert
        context.ShouldStop.Should().BeTrue();
        context.StopReason.Should().Be("User cancelled");
    }
}
