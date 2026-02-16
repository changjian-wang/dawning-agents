using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Agent;
using Dawning.Agents.Core.Memory;
using Dawning.Agents.Core.Tools;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Dawning.Agents.Tests.Agent;

public class FunctionCallingAgentTests
{
    private readonly Mock<ILLMProvider> _mockProvider;
    private readonly IOptions<AgentOptions> _options;
    private readonly IToolRegistry _toolRegistry;

    public FunctionCallingAgentTests()
    {
        _mockProvider = new Mock<ILLMProvider>();
        _options = Options.Create(
            new AgentOptions
            {
                Name = "FunctionCallingTestAgent",
                Instructions = "You are a helpful assistant with tools.",
                MaxSteps = 5,
            }
        );

        _toolRegistry = new ToolRegistry();
    }

    private static Mock<ITool> CreateMockTool(
        string name,
        string description,
        string output = "tool result"
    )
    {
        var tool = new Mock<ITool>();
        tool.Setup(t => t.Name).Returns(name);
        tool.Setup(t => t.Description).Returns(description);
        tool.Setup(t => t.ParametersSchema).Returns("""{"type":"object","properties":{}}""");
        tool.Setup(t => t.RequiresConfirmation).Returns(false);
        tool.Setup(t => t.RiskLevel).Returns(ToolRiskLevel.Low);
        tool.Setup(t => t.Category).Returns("test");
        tool.Setup(t => t.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResult.Ok(output));
        return tool;
    }

    [Fact]
    public void Constructor_ShouldSetNameAndInstructions()
    {
        var agent = new FunctionCallingAgent(
            _mockProvider.Object,
            _options,
            _toolRegistry,
            null,
            NullLogger<FunctionCallingAgent>.Instance
        );

        agent.Name.Should().Be("FunctionCallingTestAgent");
        agent.Instructions.Should().Be("You are a helpful assistant with tools.");
    }

    [Fact]
    public void Constructor_NullToolRegistry_ShouldThrow()
    {
        var act = () =>
            new FunctionCallingAgent(
                _mockProvider.Object,
                _options,
                null!,
                null,
                NullLogger<FunctionCallingAgent>.Instance
            );

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task RunAsync_DirectAnswer_ShouldReturnSuccess()
    {
        // LLM 直接返回答案（无 ToolCalls）
        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ChatCompletionResponse { Content = "The answer is 42", FinishReason = "stop" }
            );

        var agent = new FunctionCallingAgent(
            _mockProvider.Object,
            _options,
            _toolRegistry,
            null,
            NullLogger<FunctionCallingAgent>.Instance
        );

        var response = await agent.RunAsync("What is 6 * 7?");

        response.Success.Should().BeTrue();
        response.FinalAnswer.Should().Be("The answer is 42");
        response.Steps.Should().HaveCount(1);
    }

    [Fact]
    public async Task RunAsync_WithToolCalls_ShouldExecuteToolsAndReturnFinalAnswer()
    {
        // 注册测试工具
        var calculatorTool = CreateMockTool("Calculator", "Perform math", "42");
        _toolRegistry.Register(calculatorTool.Object);

        var callCount = 0;
        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // 第一次：请求工具调用
                    return new ChatCompletionResponse
                    {
                        Content = "",
                        FinishReason = "tool_calls",
                        ToolCalls = [new ToolCall("call_1", "Calculator", """{"expr":"6*7"}""")],
                    };
                }

                // 第二次：返回最终答案
                return new ChatCompletionResponse
                {
                    Content = "The answer is 42",
                    FinishReason = "stop",
                };
            });

        var agent = new FunctionCallingAgent(
            _mockProvider.Object,
            _options,
            _toolRegistry,
            null,
            NullLogger<FunctionCallingAgent>.Instance
        );

        var response = await agent.RunAsync("What is 6 * 7?");

        response.Success.Should().BeTrue();
        response.FinalAnswer.Should().Be("The answer is 42");
        response.Steps.Should().HaveCount(2);

        // 第一步应该是工具调用
        response.Steps[0].Action.Should().Contain("Calculator");
        response.Steps[0].Observation.Should().Contain("42");

        // 工具应该被调用一次
        calculatorTool.Verify(
            t => t.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task RunAsync_MultipleToolCalls_ShouldExecuteAllTools()
    {
        var searchTool = CreateMockTool("Search", "Search the web", "search result");
        var calcTool = CreateMockTool("Calculator", "Do math", "42");
        _toolRegistry.Register(searchTool.Object);
        _toolRegistry.Register(calcTool.Object);

        var callCount = 0;
        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // 一次返回多个工具调用
                    return new ChatCompletionResponse
                    {
                        Content = "",
                        FinishReason = "tool_calls",
                        ToolCalls =
                        [
                            new ToolCall("call_1", "Search", """{"q":"test"}"""),
                            new ToolCall("call_2", "Calculator", """{"expr":"1+1"}"""),
                        ],
                    };
                }

                return new ChatCompletionResponse { Content = "Done", FinishReason = "stop" };
            });

        var agent = new FunctionCallingAgent(
            _mockProvider.Object,
            _options,
            _toolRegistry,
            null,
            NullLogger<FunctionCallingAgent>.Instance
        );

        var response = await agent.RunAsync("Search and calculate");

        response.Success.Should().BeTrue();
        response.Steps.Should().HaveCount(2);

        // 两个工具都应该被调用
        searchTool.Verify(
            t => t.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        calcTool.Verify(
            t => t.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task RunAsync_UnknownTool_ShouldReturnError()
    {
        var callCount = 0;
        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new ChatCompletionResponse
                    {
                        Content = "",
                        FinishReason = "tool_calls",
                        ToolCalls = [new ToolCall("call_1", "NonExistentTool", """{}""")],
                    };
                }

                return new ChatCompletionResponse
                {
                    Content = "I couldn't find that tool",
                    FinishReason = "stop",
                };
            });

        var agent = new FunctionCallingAgent(
            _mockProvider.Object,
            _options,
            _toolRegistry,
            null,
            NullLogger<FunctionCallingAgent>.Instance
        );

        var response = await agent.RunAsync("Use a tool");

        response.Success.Should().BeTrue();
        response.Steps[0].Observation.Should().Contain("not found");
    }

    [Fact]
    public async Task RunAsync_ToolThrows_ShouldReturnErrorInObservation()
    {
        var failingTool = new Mock<ITool>();
        failingTool.Setup(t => t.Name).Returns("FailTool");
        failingTool.Setup(t => t.Description).Returns("Always fails");
        failingTool.Setup(t => t.ParametersSchema).Returns("""{"type":"object","properties":{}}""");
        failingTool.Setup(t => t.RequiresConfirmation).Returns(false);
        failingTool.Setup(t => t.RiskLevel).Returns(ToolRiskLevel.Low);
        failingTool.Setup(t => t.Category).Returns("test");
        failingTool
            .Setup(t => t.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Tool crashed"));
        _toolRegistry.Register(failingTool.Object);

        var callCount = 0;
        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new ChatCompletionResponse
                    {
                        Content = "",
                        FinishReason = "tool_calls",
                        ToolCalls = [new ToolCall("call_1", "FailTool", """{}""")],
                    };
                }

                return new ChatCompletionResponse
                {
                    Content = "The tool failed",
                    FinishReason = "stop",
                };
            });

        var agent = new FunctionCallingAgent(
            _mockProvider.Object,
            _options,
            _toolRegistry,
            null,
            NullLogger<FunctionCallingAgent>.Instance
        );

        var response = await agent.RunAsync("Use the tool");

        response.Success.Should().BeTrue();
        response.Steps[0].Observation.Should().Contain("Tool crashed");
    }

    [Fact]
    public async Task RunAsync_ExceedsMaxSteps_ShouldReturnFailed()
    {
        var searchTool = CreateMockTool("Search", "Search", "more info needed");
        _toolRegistry.Register(searchTool.Object);

        // LLM 一直请求工具调用
        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ChatCompletionResponse
                {
                    Content = "",
                    FinishReason = "tool_calls",
                    ToolCalls = [new ToolCall("call_1", "Search", """{"q":"test"}""")],
                }
            );

        var options = Options.Create(
            new AgentOptions
            {
                Name = "Test",
                Instructions = "Test",
                MaxSteps = 3,
            }
        );

        var agent = new FunctionCallingAgent(
            _mockProvider.Object,
            options,
            _toolRegistry,
            null,
            NullLogger<FunctionCallingAgent>.Instance
        );

        var response = await agent.RunAsync("Search forever");

        response.Success.Should().BeFalse();
        response.Error.Should().Contain("Exceeded maximum steps");
        response.Steps.Should().HaveCount(3);
    }

    [Fact]
    public async Task RunAsync_Cancellation_ShouldReturnFailed()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var agent = new FunctionCallingAgent(
            _mockProvider.Object,
            _options,
            _toolRegistry,
            null,
            NullLogger<FunctionCallingAgent>.Instance
        );

        var response = await agent.RunAsync("test", cts.Token);

        response.Success.Should().BeFalse();
        response.Error.Should().Contain("cancelled");
    }

    [Fact]
    public async Task RunAsync_LLMThrows_ShouldReturnFailedWithException()
    {
        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("LLM Error"));

        var agent = new FunctionCallingAgent(
            _mockProvider.Object,
            _options,
            _toolRegistry,
            null,
            NullLogger<FunctionCallingAgent>.Instance
        );

        var response = await agent.RunAsync("test");

        response.Success.Should().BeFalse();
        response.Error.Should().Be("LLM Error");
        response.Exception.Should().NotBeNull();
        response.Exception.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task RunAsync_ShouldPassToolDefinitionsToLLM()
    {
        var calcTool = CreateMockTool("Calculator", "Math calculations");
        _toolRegistry.Register(calcTool.Object);

        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ChatCompletionResponse { Content = "Answer", FinishReason = "stop" });

        var agent = new FunctionCallingAgent(
            _mockProvider.Object,
            _options,
            _toolRegistry,
            null,
            NullLogger<FunctionCallingAgent>.Instance
        );

        await agent.RunAsync("What is 1+1?");

        // 验证传递了工具定义
        _mockProvider.Verify(
            p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.Is<ChatCompletionOptions>(o =>
                        o.Tools != null && o.Tools.Count == 1 && o.Tools[0].Name == "Calculator"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task RunAsync_ShouldPassToolResultsBackToLLM()
    {
        var calcTool = CreateMockTool("Calculator", "Math", "42");
        _toolRegistry.Register(calcTool.Object);

        var capturedMessages = new List<List<ChatMessage>>();
        var callCount = 0;

        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<IEnumerable<ChatMessage>, ChatCompletionOptions, CancellationToken>(
                (msgs, _, _) =>
                {
                    capturedMessages.Add(msgs.ToList());
                }
            )
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new ChatCompletionResponse
                    {
                        Content = "",
                        FinishReason = "tool_calls",
                        ToolCalls = [new ToolCall("call_1", "Calculator", """{"expr":"6*7"}""")],
                    };
                }

                return new ChatCompletionResponse { Content = "42", FinishReason = "stop" };
            });

        var agent = new FunctionCallingAgent(
            _mockProvider.Object,
            _options,
            _toolRegistry,
            null,
            NullLogger<FunctionCallingAgent>.Instance
        );

        await agent.RunAsync("What is 6*7?");

        // 第二次调用应该包含 tool result 消息
        capturedMessages.Should().HaveCount(2);
        var secondCall = capturedMessages[1];

        // 应有: system, user, assistant(with tool calls), tool(result)
        secondCall.Should().Contain(m => m.Role == "tool" && m.Content == "42");
        secondCall.Should().Contain(m => m.Role == "assistant" && m.HasToolCalls);
    }

    [Fact]
    public async Task RunAsync_WithMemory_ShouldSaveConversation()
    {
        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ChatCompletionResponse { Content = "Hello!", FinishReason = "stop" });

        var memory = new BufferMemory(new SimpleTokenCounter());
        var agent = new FunctionCallingAgent(
            _mockProvider.Object,
            _options,
            _toolRegistry,
            memory,
            NullLogger<FunctionCallingAgent>.Instance
        );

        var response = await agent.RunAsync("Hi");

        response.Success.Should().BeTrue();
        memory.MessageCount.Should().Be(2);

        var messages = await memory.GetMessagesAsync();
        messages[0].Role.Should().Be("user");
        messages[0].Content.Should().Be("Hi");
        messages[1].Role.Should().Be("assistant");
        messages[1].Content.Should().Be("Hello!");
    }

    [Fact]
    public async Task RunAsync_EmptyToolRegistry_ShouldWorkWithoutTools()
    {
        // 空工具注册表，Agent 应该直接回答
        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ChatCompletionResponse
                {
                    Content = "I'm an AI assistant",
                    FinishReason = "stop",
                }
            );

        var agent = new FunctionCallingAgent(
            _mockProvider.Object,
            _options,
            _toolRegistry, // empty
            null,
            NullLogger<FunctionCallingAgent>.Instance
        );

        var response = await agent.RunAsync("Who are you?");

        response.Success.Should().BeTrue();
        response.FinalAnswer.Should().Be("I'm an AI assistant");

        // 不应该传递 tools 选项
        _mockProvider.Verify(
            p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.Is<ChatCompletionOptions>(o => o.Tools == null),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
