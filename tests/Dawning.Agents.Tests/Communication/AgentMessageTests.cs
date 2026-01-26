namespace Dawning.Agents.Tests.Communication;

using Dawning.Agents.Abstractions.Communication;
using FluentAssertions;

/// <summary>
/// AgentMessage 及其派生类型测试
/// </summary>
public class AgentMessageTests
{
    #region AgentMessage Tests

    [Fact]
    public void AgentMessage_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var message = new TaskMessage { SenderId = "agent1", Task = "test" };

        // Assert
        message.Id.Should().NotBeNullOrEmpty();
        message.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        message.ReceiverId.Should().BeNull();
        message.Metadata.Should().BeEmpty();
    }

    #endregion

    #region TaskMessage Tests

    [Fact]
    public void TaskMessage_DefaultPriority_ShouldBeZero()
    {
        var message = new TaskMessage { SenderId = "agent1", Task = "test" };
        message.Priority.Should().Be(0);
    }

    [Fact]
    public void TaskMessage_WithAllProperties_ShouldSetCorrectly()
    {
        var message = new TaskMessage
        {
            SenderId = "agent1",
            ReceiverId = "agent2",
            Task = "process data",
            Priority = 5,
            Timeout = TimeSpan.FromMinutes(1),
            CorrelationId = "corr-123",
        };

        message.SenderId.Should().Be("agent1");
        message.ReceiverId.Should().Be("agent2");
        message.Task.Should().Be("process data");
        message.Priority.Should().Be(5);
        message.Timeout.Should().Be(TimeSpan.FromMinutes(1));
        message.CorrelationId.Should().Be("corr-123");
    }

    #endregion

    #region ResponseMessage Tests

    [Fact]
    public void ResponseMessage_SuccessResponse_ShouldSetCorrectly()
    {
        var response = new ResponseMessage
        {
            SenderId = "agent2",
            ReceiverId = "agent1",
            CorrelationId = "corr-123",
            Result = "success result",
            IsSuccess = true,
        };

        response.IsSuccess.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Result.Should().Be("success result");
    }

    [Fact]
    public void ResponseMessage_FailureResponse_ShouldSetCorrectly()
    {
        var response = new ResponseMessage
        {
            SenderId = "agent2",
            ReceiverId = "agent1",
            CorrelationId = "corr-123",
            Result = "",
            IsSuccess = false,
            Error = "something went wrong",
        };

        response.IsSuccess.Should().BeFalse();
        response.Error.Should().Be("something went wrong");
    }

    #endregion

    #region StatusMessage Tests

    [Fact]
    public void StatusMessage_AllStatuses_ShouldBeValid()
    {
        var statuses = Enum.GetValues<AgentStatus>();

        foreach (var status in statuses)
        {
            var message = new StatusMessage
            {
                SenderId = "agent1",
                Status = status,
                CurrentTask = "task",
                Progress = 0.5,
            };

            message.Status.Should().Be(status);
        }
    }

    [Fact]
    public void StatusMessage_WithProgress_ShouldSetCorrectly()
    {
        var message = new StatusMessage
        {
            SenderId = "agent1",
            Status = AgentStatus.Busy,
            CurrentTask = "processing",
            Progress = 0.75,
        };

        message.Progress.Should().Be(0.75);
        message.CurrentTask.Should().Be("processing");
    }

    #endregion

    #region EventMessage Tests

    [Fact]
    public void EventMessage_WithPayload_ShouldSetCorrectly()
    {
        var payload = new { Data = "test", Count = 42 };

        var message = new EventMessage
        {
            SenderId = "agent1",
            EventType = "DataProcessed",
            Payload = payload,
        };

        message.EventType.Should().Be("DataProcessed");
        message.Payload.Should().BeEquivalentTo(payload);
    }

    #endregion

    #region AgentStatus Tests

    [Fact]
    public void AgentStatus_AllValues_ShouldBeDefinedCorrectly()
    {
        Enum.GetValues<AgentStatus>().Should().HaveCount(4);
        Enum.IsDefined(AgentStatus.Idle).Should().BeTrue();
        Enum.IsDefined(AgentStatus.Busy).Should().BeTrue();
        Enum.IsDefined(AgentStatus.Error).Should().BeTrue();
        Enum.IsDefined(AgentStatus.Offline).Should().BeTrue();
    }

    #endregion
}
