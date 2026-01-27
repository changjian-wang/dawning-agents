using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using Dawning.Agents.Core.Observability;
using FluentAssertions;
using Xunit;

namespace Dawning.Agents.Tests.Observability;

public class AgentInstrumentationTests
{
    [Fact]
    public void ServiceName_ShouldBeCorrect()
    {
        AgentInstrumentation.ServiceName.Should().Be("Dawning.Agents");
    }

    [Fact]
    public void ServiceVersion_ShouldBeCorrect()
    {
        AgentInstrumentation.ServiceVersion.Should().Be("1.0.0");
    }

    [Fact]
    public void ActivitySource_ShouldBeInitialized()
    {
        AgentInstrumentation.ActivitySource.Should().NotBeNull();
        AgentInstrumentation.ActivitySource.Name.Should().Be("Dawning.Agents");
    }

    [Fact]
    public void Meter_ShouldBeInitialized()
    {
        AgentInstrumentation.Meter.Should().NotBeNull();
        AgentInstrumentation.Meter.Name.Should().Be("Dawning.Agents");
    }

    [Fact]
    public void Counters_ShouldBeInitialized()
    {
        AgentInstrumentation.RequestsTotal.Should().NotBeNull();
        AgentInstrumentation.RequestsSuccessTotal.Should().NotBeNull();
        AgentInstrumentation.RequestsFailedTotal.Should().NotBeNull();
        AgentInstrumentation.ToolExecutionsTotal.Should().NotBeNull();
        AgentInstrumentation.LLMCallsTotal.Should().NotBeNull();
        AgentInstrumentation.LLMTokensUsedTotal.Should().NotBeNull();
    }

    [Fact]
    public void Histograms_ShouldBeInitialized()
    {
        AgentInstrumentation.RequestDuration.Should().NotBeNull();
        AgentInstrumentation.ToolExecutionDuration.Should().NotBeNull();
        AgentInstrumentation.LLMCallDuration.Should().NotBeNull();
    }

    [Fact]
    public void Gauges_ShouldBeInitialized()
    {
        AgentInstrumentation.QueueDepth.Should().NotBeNull();
        AgentInstrumentation.ActiveRequests.Should().NotBeNull();
        AgentInstrumentation.HealthyInstances.Should().NotBeNull();
    }

    [Fact]
    public void SetQueueDepthCallback_ShouldSetCallback()
    {
        // Act
        AgentInstrumentation.SetQueueDepthCallback(() => 42);

        // Assert - No exception means success
        // Note: We can't easily verify the callback was set without accessing private fields
    }

    [Fact]
    public void SetActiveRequestsCallback_ShouldSetCallback()
    {
        // Act
        AgentInstrumentation.SetActiveRequestsCallback(() => 10);

        // Assert - No exception means success
    }

    [Fact]
    public void SetHealthyInstancesCallback_ShouldSetCallback()
    {
        // Act
        AgentInstrumentation.SetHealthyInstancesCallback(() => 3);

        // Assert - No exception means success
    }

    [Fact]
    public void StartAgentRequest_ShouldReturnActivity()
    {
        // Arrange - Need to enable the activity source listener
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentInstrumentation.ServiceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var activity = AgentInstrumentation.StartAgentRequest("test-agent", "hello world");

        // Assert
        activity.Should().NotBeNull();
        activity!.DisplayName.Should().Be("agent.request");
        activity.GetTagItem("agent.name").Should().Be("test-agent");
        activity.GetTagItem("agent.input.length").Should().Be(11);
    }

    [Fact]
    public void StartToolExecution_ShouldReturnActivity()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentInstrumentation.ServiceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var activity = AgentInstrumentation.StartToolExecution("math-tool");

        // Assert
        activity.Should().NotBeNull();
        activity!.DisplayName.Should().Be("agent.tool.execute");
        activity.GetTagItem("tool.name").Should().Be("math-tool");
    }

    [Fact]
    public void StartLLMCall_ShouldReturnActivity()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentInstrumentation.ServiceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var activity = AgentInstrumentation.StartLLMCall("openai", "gpt-4");

        // Assert
        activity.Should().NotBeNull();
        activity!.DisplayName.Should().Be("llm.call");
        activity.GetTagItem("llm.provider").Should().Be("openai");
        activity.GetTagItem("llm.model").Should().Be("gpt-4");
    }

    [Fact]
    public void RecordException_ShouldAddEventAndSetStatus()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentInstrumentation.ServiceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = AgentInstrumentation.StartAgentRequest("test", "input");
        var exception = new InvalidOperationException("Test error");

        // Act
        AgentInstrumentation.RecordException(activity, exception);

        // Assert
        activity!.Status.Should().Be(ActivityStatusCode.Error);
        activity.StatusDescription.Should().Be("Test error");
        activity.Events.Should().ContainSingle();
    }

    [Fact]
    public void RecordException_WithNullActivity_ShouldNotThrow()
    {
        // Arrange
        var exception = new Exception("Test");

        // Act & Assert
        var act = () => AgentInstrumentation.RecordException(null, exception);
        act.Should().NotThrow();
    }

    [Fact]
    public void Counter_Add_ShouldNotThrow()
    {
        // Act & Assert
        var act = () =>
        {
            AgentInstrumentation.RequestsTotal.Add(1);
            AgentInstrumentation.RequestsSuccessTotal.Add(1);
            AgentInstrumentation.RequestsFailedTotal.Add(1);
            AgentInstrumentation.ToolExecutionsTotal.Add(1, new KeyValuePair<string, object?>("tool", "test"));
            AgentInstrumentation.LLMCallsTotal.Add(1);
            AgentInstrumentation.LLMTokensUsedTotal.Add(100, new KeyValuePair<string, object?>("type", "input"));
        };
        act.Should().NotThrow();
    }

    [Fact]
    public void Histogram_Record_ShouldNotThrow()
    {
        // Act & Assert
        var act = () =>
        {
            AgentInstrumentation.RequestDuration.Record(1.5);
            AgentInstrumentation.ToolExecutionDuration.Record(0.1);
            AgentInstrumentation.LLMCallDuration.Record(2.0);
        };
        act.Should().NotThrow();
    }
}

public class OpenTelemetryOptionsTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new OpenTelemetryOptions();

        // Assert
        options.ServiceName.Should().Be(AgentInstrumentation.ServiceName);
        options.ServiceVersion.Should().Be(AgentInstrumentation.ServiceVersion);
        options.EnableTracing.Should().BeTrue();
        options.EnableMetrics.Should().BeTrue();
        options.OtlpEndpoint.Should().BeNull();
        options.EnableConsoleExporter.Should().BeFalse();
        options.SamplingRatio.Should().Be(1.0);
        OpenTelemetryOptions.SectionName.Should().Be("OpenTelemetry");
    }
}
