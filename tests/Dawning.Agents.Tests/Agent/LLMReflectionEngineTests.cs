using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Agent;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Dawning.Agents.Tests.Agent;

/// <summary>
/// LLMReflectionEngine tests
/// </summary>
public sealed class LLMReflectionEngineTests
{
    private readonly Mock<ILLMProvider> _mockLLM;
    private readonly ReflectionOptions _options;
    private readonly LLMReflectionEngine _engine;

    public LLMReflectionEngineTests()
    {
        _mockLLM = new Mock<ILLMProvider>();
        _options = new ReflectionOptions { Enabled = true, FailureThreshold = 2 };
        _engine = new LLMReflectionEngine(_mockLLM.Object, Options.Create(_options));
    }

    #region ReflectAsync

    [Fact]
    public async Task ReflectAsync_RetryResponse_ShouldReturnRetryAction()
    {
        SetupLLMResponse(
            """{"action": "Retry", "diagnosis": "Temporary error", "confidence": 0.8}"""
        );

        var context = CreateContext();
        var result = await _engine.ReflectAsync(context);

        result.Action.Should().Be(ReflectionAction.Retry);
        result.Diagnosis.Should().Be("Temporary error");
        result.Confidence.Should().Be(0.8f);
        result.RevisedDefinition.Should().BeNull();
    }

    [Fact]
    public async Task ReflectAsync_AbandonResponse_ShouldReturnAbandonAction()
    {
        SetupLLMResponse(
            """{"action": "Abandon", "diagnosis": "Tool is broken", "confidence": 0.9}"""
        );

        var context = CreateContext();
        var result = await _engine.ReflectAsync(context);

        result.Action.Should().Be(ReflectionAction.Abandon);
    }

    [Fact]
    public async Task ReflectAsync_EscalateResponse_ShouldReturnEscalateAction()
    {
        SetupLLMResponse(
            """{"action": "Escalate", "diagnosis": "Needs human", "confidence": 0.95}"""
        );

        var context = CreateContext();
        var result = await _engine.ReflectAsync(context);

        result.Action.Should().Be(ReflectionAction.Escalate);
    }

    [Fact]
    public async Task ReflectAsync_CreateNewResponse_ShouldReturnCreateNewAction()
    {
        SetupLLMResponse(
            """{"action": "CreateNew", "diagnosis": "Tool inadequate", "confidence": 0.7}"""
        );

        var context = CreateContext();
        var result = await _engine.ReflectAsync(context);

        result.Action.Should().Be(ReflectionAction.CreateNew);
    }

    [Fact]
    public async Task ReflectAsync_LLMException_ShouldFallbackToRetry()
    {
        _mockLLM
            .Setup(l =>
                l.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("LLM error"));

        var context = CreateContext();
        var result = await _engine.ReflectAsync(context);

        result.Action.Should().Be(ReflectionAction.Retry);
        result.Confidence.Should().BeLessThan(0.5f);
    }

    [Fact]
    public async Task ReflectAsync_InvalidJson_ShouldFallbackToRetry()
    {
        SetupLLMResponse("this is not json at all");

        var context = CreateContext();
        var result = await _engine.ReflectAsync(context);

        result.Action.Should().Be(ReflectionAction.Retry);
    }

    [Fact]
    public async Task ReflectAsync_JsonInMarkdownBlock_ShouldParse()
    {
        SetupLLMResponse(
            """
            Here's my analysis:
            ```json
            {"action": "Abandon", "diagnosis": "fatal", "confidence": 0.99}
            ```
            """
        );

        var context = CreateContext();
        var result = await _engine.ReflectAsync(context);

        result.Action.Should().Be(ReflectionAction.Abandon);
        result.Confidence.Should().Be(0.99f);
    }

    [Fact]
    public async Task ReflectAsync_NullContext_ShouldThrow()
    {
        var act = () => _engine.ReflectAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReflectAsync_CancellationRequested_ShouldThrow()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _mockLLM
            .Setup(l =>
                l.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new OperationCanceledException());

        var context = CreateContext();
        var act = () => _engine.ReflectAsync(context, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ReflectAsync_UnknownAction_ShouldDefaultToRetry()
    {
        SetupLLMResponse(
            """{"action": "DoSomethingWeird", "diagnosis": "unknown", "confidence": 0.5}"""
        );

        var context = CreateContext();
        var result = await _engine.ReflectAsync(context);

        result.Action.Should().Be(ReflectionAction.Retry);
    }

    #endregion

    #region Constructor Validation

    [Fact]
    public void Constructor_NullLLMProvider_ShouldThrow()
    {
        var act = () => new LLMReflectionEngine(null!, Options.Create(_options));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullOptions_ShouldThrow()
    {
        var act = () =>
            new LLMReflectionEngine(_mockLLM.Object, (IOptions<ReflectionOptions>)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ReflectionOptions Validation

    [Fact]
    public void ReflectionOptions_Valid_ShouldNotThrow()
    {
        var options = new ReflectionOptions
        {
            Enabled = true,
            FailureThreshold = 2,
            MaxReflections = 3,
        };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void ReflectionOptions_InvalidFailureThreshold_ShouldThrow()
    {
        var options = new ReflectionOptions { FailureThreshold = 0 };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReflectionOptions_InvalidMaxReflections_ShouldThrow()
    {
        var options = new ReflectionOptions { MaxReflections = 0 };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region ReflectionContext / ReflectionResult / ReflectionAction

    [Fact]
    public void ReflectionAction_ShouldContainAllExpectedValues()
    {
        var values = Enum.GetValues<ReflectionAction>();
        values.Should().HaveCount(5);
        values
            .Should()
            .Contain(ReflectionAction.Retry)
            .And.Contain(ReflectionAction.ReviseAndRetry)
            .And.Contain(ReflectionAction.Abandon)
            .And.Contain(ReflectionAction.CreateNew)
            .And.Contain(ReflectionAction.Escalate);
    }

    [Fact]
    public void ReflectionResult_WithRevisedDefinition_ShouldExposeIt()
    {
        var def = new EphemeralToolDefinition
        {
            Name = "tool",
            Description = "desc",
            Script = "echo",
        };

        var result = new ReflectionResult
        {
            Action = ReflectionAction.ReviseAndRetry,
            RevisedDefinition = def,
            Confidence = 0.7f,
        };

        result.RevisedDefinition.Should().NotBeNull();
        result.RevisedDefinition!.Name.Should().Be("tool");
    }

    [Fact]
    public void ReflectionContext_ShouldExposeAllProperties()
    {
        var tool = new Mock<ITool>();
        tool.Setup(t => t.Name).Returns("test");

        var ctx = new ReflectionContext
        {
            FailedTool = tool.Object,
            Input = "input",
            FailedResult = ToolResult.Fail("error"),
            TaskDescription = "task",
            PreviousSteps = [],
            UsageStats = new ToolUsageStats { ToolName = "test", TotalCalls = 5 },
        };

        ctx.FailedTool.Name.Should().Be("test");
        ctx.Input.Should().Be("input");
        ctx.TaskDescription.Should().Be("task");
        ctx.PreviousSteps.Should().BeEmpty();
        ctx.UsageStats!.TotalCalls.Should().Be(5);
    }

    #endregion

    private void SetupLLMResponse(string content)
    {
        _mockLLM
            .Setup(l =>
                l.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ChatCompletionResponse
                {
                    Content = content,
                    PromptTokens = 100,
                    CompletionTokens = 50,
                }
            );
    }

    private static ReflectionContext CreateContext()
    {
        var tool = new Mock<ITool>();
        tool.Setup(t => t.Name).Returns("test_tool");
        tool.Setup(t => t.Description).Returns("a test tool");

        return new ReflectionContext
        {
            FailedTool = tool.Object,
            Input = """{"query": "test"}""",
            FailedResult = ToolResult.Fail("Script failed with exit code 1"),
            TaskDescription = "Find all TODO comments in the codebase",
        };
    }
}
