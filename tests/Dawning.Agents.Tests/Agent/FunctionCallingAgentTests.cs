using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Agent;
using Dawning.Agents.Core.Memory;
using Dawning.Agents.Core.Tools;
using Dawning.Agents.Core.Tools.Core;
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

    /// <summary>
    /// Helper to create a FunctionCallingAgent with default test parameters
    /// </summary>
    private FunctionCallingAgent CreateAgent(
        IOptions<AgentOptions>? options = null,
        IConversationMemory? memory = null,
        IToolSession? session = null
    )
    {
        return new FunctionCallingAgent(
            _mockProvider.Object,
            options ?? _options,
            _toolRegistry,
            memory,
            session,
            NullLogger<FunctionCallingAgent>.Instance
        );
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
        var agent = CreateAgent();

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
                null,
                NullLogger<FunctionCallingAgent>.Instance
            );

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task RunAsync_DirectAnswer_ShouldReturnSuccess()
    {
        // LLM returns a direct answer (no ToolCalls)
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

        var agent = CreateAgent();

        var response = await agent.RunAsync("What is 6 * 7?");

        response.Success.Should().BeTrue();
        response.FinalAnswer.Should().Be("The answer is 42");
        response.Steps.Should().HaveCount(1);
    }

    [Fact]
    public async Task RunAsync_WithToolCalls_ShouldExecuteToolsAndReturnFinalAnswer()
    {
        // Register test tool
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
                    // First call: request tool invocation
                    return new ChatCompletionResponse
                    {
                        Content = "",
                        FinishReason = "tool_calls",
                        ToolCalls = [new ToolCall("call_1", "Calculator", """{"expr":"6*7"}""")],
                    };
                }

                // Second call: return final answer
                return new ChatCompletionResponse
                {
                    Content = "The answer is 42",
                    FinishReason = "stop",
                };
            });

        var agent = CreateAgent();

        var response = await agent.RunAsync("What is 6 * 7?");

        response.Success.Should().BeTrue();
        response.FinalAnswer.Should().Be("The answer is 42");
        response.Steps.Should().HaveCount(2);

        // First step should be a tool call
        response.Steps[0].Action.Should().Contain("Calculator");
        response.Steps[0].Observation.Should().Contain("42");

        // Tool should be called once
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
                    // Return multiple tool calls at once
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

        var agent = CreateAgent();

        var response = await agent.RunAsync("Search and calculate");

        response.Success.Should().BeTrue();
        response.Steps.Should().HaveCount(2);

        // Both tools should be called
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

        var agent = CreateAgent();

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

        var agent = CreateAgent();

        var response = await agent.RunAsync("Use the tool");

        response.Success.Should().BeTrue();
        response.Steps[0].Observation.Should().Contain("Tool crashed");
    }

    [Fact]
    public async Task RunAsync_ToolCancellation_ShouldReturnFailed()
    {
        var cancellableTool = new Mock<ITool>();
        cancellableTool.Setup(t => t.Name).Returns("CancelableTool");
        cancellableTool.Setup(t => t.Description).Returns("Cancels execution");
        cancellableTool
            .Setup(t => t.ParametersSchema)
            .Returns("""{"type":"object","properties":{}}""");
        cancellableTool.Setup(t => t.RequiresConfirmation).Returns(false);
        cancellableTool.Setup(t => t.RiskLevel).Returns(ToolRiskLevel.Low);
        cancellableTool.Setup(t => t.Category).Returns("test");
        cancellableTool
            .Setup(t => t.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        _toolRegistry.Register(cancellableTool.Object);

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
                    ToolCalls = [new ToolCall("call_1", "CancelableTool", """{}""")],
                }
            );

        var agent = CreateAgent();

        var response = await agent.RunAsync("Use cancelable tool");

        response.Success.Should().BeFalse();
        response.Error.Should().Contain("cancelled");
    }

    [Fact]
    public async Task RunAsync_ExceedsMaxSteps_ShouldReturnFailed()
    {
        var searchTool = CreateMockTool("Search", "Search", "more info needed");
        _toolRegistry.Register(searchTool.Object);

        // LLM keeps requesting tool calls
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

        var agent = CreateAgent(options: options);

        var response = await agent.RunAsync("Search forever");

        response.Success.Should().BeFalse();
        response.Error.Should().Contain("Exceeded maximum steps");
        response.Steps.Should().HaveCount(3);
    }

    [Fact]
    public async Task RunAsync_Cancellation_ShouldThrowOperationCanceledException()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var agent = CreateAgent();

        // User cancellation should propagate OperationCanceledException
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            agent.RunAsync("test", cts.Token)
        );
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

        var agent = CreateAgent();

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

        var agent = CreateAgent();

        await agent.RunAsync("What is 1+1?");

        // Verify tool definitions were passed
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

        var agent = CreateAgent();

        await agent.RunAsync("What is 6*7?");

        // Second call should contain tool result messages
        capturedMessages.Should().HaveCount(2);
        var secondCall = capturedMessages[1];

        // Should have: system, user, assistant(with tool calls), tool(result)
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
        var agent = CreateAgent(memory: memory);

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
        // Empty tool registry, Agent should answer directly
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

        var agent = CreateAgent();

        var response = await agent.RunAsync("Who are you?");

        response.Success.Should().BeTrue();
        response.FinalAnswer.Should().Be("I'm an AI assistant");

        // Should not pass tools options
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

    [Fact]
    public async Task RunAsync_WithSession_ShouldIncludeCreateToolInDefinitions()
    {
        // Agent with session should expose create_tool
        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ChatCompletionResponse { Content = "OK", FinishReason = "stop" });

        var session = new Mock<IToolSession>();
        session.Setup(s => s.GetSessionTools()).Returns(new List<ITool>());

        var agent = CreateAgent(session: session.Object);

        await agent.RunAsync("Hello");

        // Should include create_tool in tool definitions
        _mockProvider.Verify(
            p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.Is<ChatCompletionOptions>(o =>
                        o.Tools != null && o.Tools.Any(t => t.Name == "create_tool")
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task RunAsync_WithSession_ShouldResolveCreateToolCall()
    {
        // When LLM calls create_tool, the agent should resolve it via the session
        var session = new Mock<IToolSession>();
        session.Setup(s => s.GetSessionTools()).Returns(new List<ITool>());

        // Mock create_tool — simulate creating a tool
        var createdTool = CreateMockTool("my_script", "Custom script", "script output");
        session
            .Setup(s => s.CreateTool(It.IsAny<EphemeralToolDefinition>()))
            .Returns(createdTool.Object);

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
                    // LLM requests create_tool
                    return new ChatCompletionResponse
                    {
                        Content = "",
                        FinishReason = "tool_calls",
                        ToolCalls =
                        [
                            new ToolCall(
                                "call_1",
                                "create_tool",
                                """{"name":"my_script","description":"test script","script":"echo hello"}"""
                            ),
                        ],
                    };
                }

                return new ChatCompletionResponse { Content = "Done", FinishReason = "stop" };
            });

        var agent = CreateAgent(session: session.Object);

        var response = await agent.RunAsync("Create a tool");

        response.Success.Should().BeTrue();
        response.Steps.Should().HaveCount(2);
        response.Steps[0].Observation.Should().Contain("Created tool");
    }

    [Fact]
    public async Task RunAsync_WithSession_ShouldResolveSessionTools()
    {
        // Session tools should be available for execution
        var sessionTool = CreateMockTool("my_counter", "Count lines", "42 lines");

        var session = new Mock<IToolSession>();
        session.Setup(s => s.GetSessionTools()).Returns(new List<ITool> { sessionTool.Object });

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
                        ToolCalls = [new ToolCall("call_1", "my_counter", """{}""")],
                    };
                }

                return new ChatCompletionResponse { Content = "42 lines", FinishReason = "stop" };
            });

        var agent = CreateAgent(session: session.Object);

        var response = await agent.RunAsync("Count lines");

        response.Success.Should().BeTrue();
        response.Steps[0].Observation.Should().Contain("42 lines");

        sessionTool.Verify(
            t => t.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task RunAsync_WithSession_ShouldIncludeSessionToolsInDefinitions()
    {
        // Verify that session tools appear in the tool definitions sent to LLM
        var sessionTool = CreateMockTool("custom_tool", "A custom tool");
        var session = new Mock<IToolSession>();
        session.Setup(s => s.GetSessionTools()).Returns(new List<ITool> { sessionTool.Object });

        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ChatCompletionResponse { Content = "OK", FinishReason = "stop" });

        var agent = CreateAgent(session: session.Object);

        await agent.RunAsync("Hello");

        // Should include both create_tool AND custom_tool
        _mockProvider.Verify(
            p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.Is<ChatCompletionOptions>(o =>
                        o.Tools != null
                        && o.Tools.Any(t => t.Name == "create_tool")
                        && o.Tools.Any(t => t.Name == "custom_tool")
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task RunAsync_WithoutSession_ShouldNotIncludeCreateTool()
    {
        // Without session, create_tool should NOT appear
        var calcTool = CreateMockTool("Calculator", "Math");
        _toolRegistry.Register(calcTool.Object);

        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ChatCompletionResponse { Content = "OK", FinishReason = "stop" });

        var agent = CreateAgent(); // no session

        await agent.RunAsync("Hello");

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
    public async Task RunAsync_ViaIAgentInterface_ShouldDispatchToFunctionCallingAgent()
    {
        // Regression test: call RunAsync via IAgent interface, verify polymorphic dispatch to FunctionCallingAgent
        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ChatCompletionResponse { Content = "Hello from FC!", FinishReason = "stop" }
            );

        IAgent agent = CreateAgent();

        var response = await agent.RunAsync("test");

        response.Success.Should().BeTrue();
        response.FinalAnswer.Should().Be("Hello from FC!");
        response.Steps.Should().HaveCount(1);
    }

    [Fact]
    public async Task RunAsync_ViaIAgentInterface_WithContext_ShouldDispatchToFunctionCallingAgent()
    {
        // Regression test: call RunAsync(AgentContext) via IAgent interface, verify polymorphic dispatch
        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ChatCompletionResponse { Content = "Context works!", FinishReason = "stop" }
            );

        IAgent agent = CreateAgent();
        var context = new AgentContext { UserInput = "test", MaxSteps = 5 };

        var response = await agent.RunAsync(context);

        response.Success.Should().BeTrue();
        response.FinalAnswer.Should().Be("Context works!");
    }

    [Fact]
    public async Task RunAsync_RegistryToolTakesPriorityOverSessionTool()
    {
        // When same name exists in registry and session, registry wins
        var registryTool = CreateMockTool("my_tool", "Registry version", "registry output");
        _toolRegistry.Register(registryTool.Object);

        var sessionTool = CreateMockTool("my_tool", "Session version", "session output");
        var session = new Mock<IToolSession>();
        session.Setup(s => s.GetSessionTools()).Returns(new List<ITool> { sessionTool.Object });

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
                        ToolCalls = [new ToolCall("call_1", "my_tool", """{}""")],
                    };
                }

                return new ChatCompletionResponse { Content = "Done", FinishReason = "stop" };
            });

        var agent = CreateAgent(session: session.Object);

        var response = await agent.RunAsync("Use my_tool");

        response.Success.Should().BeTrue();
        // Registry tool should be used, not session tool
        response.Steps[0].Observation.Should().Contain("registry output");
        registryTool.Verify(
            t => t.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        sessionTool.Verify(
            t => t.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
