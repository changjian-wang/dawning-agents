using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Core.Agent;
using Dawning.Agents.Core.Memory;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Dawning.Agents.Tests.Agent;

public class ReActAgentTests
{
    private readonly Mock<ILLMProvider> _mockProvider;
    private readonly IOptions<AgentOptions> _options;

    public ReActAgentTests()
    {
        _mockProvider = new Mock<ILLMProvider>();
        _options = Options.Create(
            new AgentOptions
            {
                Name = "TestAgent",
                Instructions = "You are a helpful assistant.",
                MaxSteps = 3,
            }
        );
    }

    [Fact]
    public void Constructor_ShouldSetNameAndInstructions()
    {
        // Act
        var agent = new ReActAgent(_mockProvider.Object, _options, null, null, null);

        // Assert
        agent.Name.Should().Be("TestAgent");
        agent.Instructions.Should().Be("You are a helpful assistant.");
    }

    [Fact]
    public async Task RunAsync_ShouldReturnFailedWhenExceedsMaxSteps()
    {
        // Arrange - LLM 一直返回需要继续执行的响应
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
                    Content =
                        "Thought: I need to search for information.\nAction: Search\nAction Input: test query",
                }
            );

        var agent = new ReActAgent(
            _mockProvider.Object,
            _options,
            null,
            null,
            NullLogger<ReActAgent>.Instance
        );

        // Act
        var response = await agent.RunAsync("What is the answer?");

        // Assert
        response.Success.Should().BeFalse();
        response.Error.Should().Contain("Exceeded maximum steps");
        response.Steps.Should().HaveCount(3); // MaxSteps = 3
    }

    [Fact]
    public async Task RunAsync_ShouldHandleCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var agent = new ReActAgent(
            _mockProvider.Object,
            _options,
            null,
            null,
            NullLogger<ReActAgent>.Instance
        );

        // Act
        var response = await agent.RunAsync("test", cts.Token);

        // Assert
        response.Success.Should().BeFalse();
        response.Error.Should().Contain("cancelled");
    }

    [Fact]
    public async Task RunAsync_ShouldHandleException()
    {
        // Arrange
        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("LLM Error"));

        var agent = new ReActAgent(
            _mockProvider.Object,
            _options,
            null,
            null,
            NullLogger<ReActAgent>.Instance
        );

        // Act
        var response = await agent.RunAsync("test");

        // Assert
        response.Success.Should().BeFalse();
        response.Error.Should().Be("LLM Error");
    }

    [Fact]
    public async Task RunAsync_ShouldCallLLMWithCorrectMessages()
    {
        // Arrange
        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ChatCompletionResponse { Content = "Final Answer: The answer is 42" }
            );

        var agent = new ReActAgent(
            _mockProvider.Object,
            _options,
            null,
            null,
            NullLogger<ReActAgent>.Instance
        );

        // Act
        await agent.RunAsync("What is the answer?");

        // Assert
        _mockProvider.Verify(
            p =>
                p.ChatAsync(
                    It.Is<IEnumerable<ChatMessage>>(msgs =>
                        msgs.Count() == 2
                        && msgs.First().Role == "system"
                        && msgs.Last().Role == "user"
                    ),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.AtLeastOnce
        );
    }

    [Fact]
    public async Task RunAsync_WithContext_ShouldUseProvidedContext()
    {
        // Arrange
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
                    Content = "Thought: Searching...\nAction: Search\nAction Input: test",
                }
            );

        var agent = new ReActAgent(
            _mockProvider.Object,
            _options,
            null,
            null,
            NullLogger<ReActAgent>.Instance
        );
        var context = new AgentContext { UserInput = "Custom input", MaxSteps = 1 };

        // Act
        var response = await agent.RunAsync(context);

        // Assert
        response.Steps.Should().HaveCount(1);
    }

    [Fact]
    public async Task RunAsync_WithMemory_ShouldSaveConversationToMemory()
    {
        // Arrange
        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ChatCompletionResponse { Content = "Final Answer: The answer is 42" }
            );

        var memory = new BufferMemory(new SimpleTokenCounter());
        var agent = new ReActAgent(
            _mockProvider.Object,
            _options,
            null,
            memory,
            NullLogger<ReActAgent>.Instance
        );

        // Act
        var response = await agent.RunAsync("What is the answer?");

        // Assert
        response.Success.Should().BeTrue();
        response.FinalAnswer.Should().Be("The answer is 42");

        // 验证 Memory 保存了对话
        memory.MessageCount.Should().Be(2);
        var messages = await memory.GetMessagesAsync();
        messages[0].Role.Should().Be("user");
        messages[0].Content.Should().Be("What is the answer?");
        messages[1].Role.Should().Be("assistant");
        messages[1].Content.Should().Be("The answer is 42");
    }

    [Fact]
    public async Task RunAsync_WithoutMemory_ShouldNotThrow()
    {
        // Arrange
        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ChatCompletionResponse { Content = "Final Answer: Hello" }
            );

        var agent = new ReActAgent(
            _mockProvider.Object,
            _options,
            null,
            null, // No memory
            NullLogger<ReActAgent>.Instance
        );

        // Act
        var response = await agent.RunAsync("Hi");

        // Assert
        response.Success.Should().BeTrue();
        response.FinalAnswer.Should().Be("Hello");
    }

    [Fact]
    public async Task RunAsync_MultipleConversations_ShouldAccumulateInMemory()
    {
        // Arrange
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
                return new ChatCompletionResponse
                {
                    Content = $"Final Answer: Response {callCount}",
                };
            });

        var memory = new BufferMemory(new SimpleTokenCounter());
        var agent = new ReActAgent(
            _mockProvider.Object,
            _options,
            null,
            memory,
            NullLogger<ReActAgent>.Instance
        );

        // Act - 执行两次对话
        await agent.RunAsync("Question 1");
        await agent.RunAsync("Question 2");

        // Assert
        memory.MessageCount.Should().Be(4); // 2 user + 2 assistant messages
        var messages = await memory.GetMessagesAsync();
        messages[0].Content.Should().Be("Question 1");
        messages[1].Content.Should().Be("Response 1");
        messages[2].Content.Should().Be("Question 2");
        messages[3].Content.Should().Be("Response 2");
    }
}
