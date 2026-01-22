using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Handoff;
using FluentAssertions;

namespace Dawning.Agents.Tests.Handoff;

public class HandoffRequestTests
{
    [Fact]
    public void To_ShouldCreateRequestWithRequiredFields()
    {
        // Act
        var request = HandoffRequest.To("TargetAgent", "Test input");

        // Assert
        request.TargetAgentName.Should().Be("TargetAgent");
        request.Input.Should().Be("Test input");
        request.Reason.Should().BeNull();
        request.PreserveHistory.Should().BeTrue();
    }

    [Fact]
    public void To_ShouldCreateRequestWithReason()
    {
        // Act
        var request = HandoffRequest.To("TargetAgent", "Test input", "Need expert help");

        // Assert
        request.TargetAgentName.Should().Be("TargetAgent");
        request.Input.Should().Be("Test input");
        request.Reason.Should().Be("Need expert help");
    }

    [Fact]
    public void Request_ShouldSupportContext()
    {
        // Arrange
        var context = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var request = new HandoffRequest
        {
            TargetAgentName = "Agent",
            Input = "Input",
            Context = context,
        };

        // Assert
        request.Context.Should().ContainKey("key");
        request.Context!["key"].Should().Be("value");
    }
}

public class HandoffResultTests
{
    [Fact]
    public void Successful_ShouldCreateSuccessResult()
    {
        // Arrange
        var response = AgentResponse.Successful("Answer", [], TimeSpan.FromSeconds(1));
        var chain = new List<HandoffRecord>
        {
            new() { ToAgent = "Agent1", Input = "input" },
        };

        // Act
        var result = HandoffResult.Successful("Agent1", response, chain, TimeSpan.FromSeconds(2));

        // Assert
        result.Success.Should().BeTrue();
        result.ExecutedByAgent.Should().Be("Agent1");
        result.Response.Should().Be(response);
        result.HandoffChain.Should().HaveCount(1);
        result.TotalDuration.Should().Be(TimeSpan.FromSeconds(2));
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failed_ShouldCreateFailureResult()
    {
        // Arrange
        var chain = new List<HandoffRecord>();

        // Act
        var result = HandoffResult.Failed(
            "Agent1",
            "Something went wrong",
            chain,
            TimeSpan.FromSeconds(1)
        );

        // Assert
        result.Success.Should().BeFalse();
        result.ExecutedByAgent.Should().Be("Agent1");
        result.Error.Should().Be("Something went wrong");
        result.Response.Should().BeNull();
    }
}

public class HandoffRecordTests
{
    [Fact]
    public void Record_ShouldCaptureHandoffDetails()
    {
        // Act
        var record = new HandoffRecord
        {
            FromAgent = "Agent1",
            ToAgent = "Agent2",
            Reason = "Need expertise",
            Input = "User question",
        };

        // Assert
        record.FromAgent.Should().Be("Agent1");
        record.ToAgent.Should().Be("Agent2");
        record.Reason.Should().Be("Need expertise");
        record.Input.Should().Be("User question");
        record.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Record_ShouldAllowNullFromAgent()
    {
        // Act
        var record = new HandoffRecord
        {
            FromAgent = null,
            ToAgent = "Agent1",
            Input = "Initial input",
        };

        // Assert
        record.FromAgent.Should().BeNull();
        record.ToAgent.Should().Be("Agent1");
    }
}

public class AgentResponseHandoffExtensionsTests
{
    [Fact]
    public void IsHandoffRequest_ShouldReturnTrue_WhenResponseContainsHandoff()
    {
        // Arrange
        var response = AgentResponse.Successful(
            "[HANDOFF:ExpertAgent] Please help with this",
            [],
            TimeSpan.Zero
        );

        // Act & Assert
        response.IsHandoffRequest().Should().BeTrue();
    }

    [Fact]
    public void IsHandoffRequest_ShouldReturnFalse_WhenResponseIsNormal()
    {
        // Arrange
        var response = AgentResponse.Successful("Normal answer", [], TimeSpan.Zero);

        // Act & Assert
        response.IsHandoffRequest().Should().BeFalse();
    }

    [Fact]
    public void IsHandoffRequest_ShouldReturnFalse_WhenResponseFailed()
    {
        // Arrange
        var response = AgentResponse.Failed("[HANDOFF:Agent] input", [], TimeSpan.Zero);

        // Act & Assert
        response.IsHandoffRequest().Should().BeFalse();
    }

    [Fact]
    public void ParseHandoffRequest_ShouldParseSimpleHandoff()
    {
        // Arrange
        var response = AgentResponse.Successful(
            "[HANDOFF:ExpertAgent] Please analyze this data",
            [],
            TimeSpan.Zero
        );

        // Act
        var request = response.ParseHandoffRequest();

        // Assert
        request.Should().NotBeNull();
        request!.TargetAgentName.Should().Be("ExpertAgent");
        request.Input.Should().Be("Please analyze this data");
        request.Reason.Should().BeNull();
    }

    [Fact]
    public void ParseHandoffRequest_ShouldParseHandoffWithReason()
    {
        // Arrange
        var response = AgentResponse.Successful(
            "[HANDOFF:LegalExpert|Legal question detected] Review this contract",
            [],
            TimeSpan.Zero
        );

        // Act
        var request = response.ParseHandoffRequest();

        // Assert
        request.Should().NotBeNull();
        request!.TargetAgentName.Should().Be("LegalExpert");
        request.Input.Should().Be("Review this contract");
        request.Reason.Should().Be("Legal question detected");
    }

    [Fact]
    public void ParseHandoffRequest_ShouldReturnNull_WhenNotHandoff()
    {
        // Arrange
        var response = AgentResponse.Successful("Normal response", [], TimeSpan.Zero);

        // Act
        var request = response.ParseHandoffRequest();

        // Assert
        request.Should().BeNull();
    }

    [Fact]
    public void CreateHandoffResponse_ShouldFormatCorrectly()
    {
        // Act
        var result = AgentResponseHandoffExtensions.CreateHandoffResponse(
            "TargetAgent",
            "Please help"
        );

        // Assert
        result.Should().Be("[HANDOFF:TargetAgent] Please help");
    }

    [Fact]
    public void CreateHandoffResponse_ShouldIncludeReason()
    {
        // Act
        var result = AgentResponseHandoffExtensions.CreateHandoffResponse(
            "TargetAgent",
            "Please help",
            "Need expertise"
        );

        // Assert
        result.Should().Be("[HANDOFF:TargetAgent|Need expertise] Please help");
    }
}
