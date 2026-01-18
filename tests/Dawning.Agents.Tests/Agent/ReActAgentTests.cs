using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Core.Agent;
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
        _options = Options.Create(new AgentOptions
        {
            Name = "TestAgent",
            Instructions = "You are a helpful assistant.",
            MaxSteps = 3
        });
    }

    [Fact]
    public void Constructor_ShouldSetNameAndInstructions()
    {
        // Act
        var agent = new ReActAgent(_mockProvider.Object, _options);

        // Assert
        agent.Name.Should().Be("TestAgent");
        agent.Instructions.Should().Be("You are a helpful assistant.");
    }

    [Fact]
    public async Task RunAsync_ShouldReturnFailedWhenExceedsMaxSteps()
    {
        // Arrange - LLM 一直返回需要继续执行的响应
        _mockProvider.Setup(p => p.ChatAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatCompletionOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatCompletionResponse
            {
                Content = "Thought: I need to search for information.\nAction: Search\nAction Input: test query"
            });

        var agent = new ReActAgent(_mockProvider.Object, _options, NullLogger<ReActAgent>.Instance);

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

        var agent = new ReActAgent(_mockProvider.Object, _options, NullLogger<ReActAgent>.Instance);

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
        _mockProvider.Setup(p => p.ChatAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatCompletionOptions>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM Error"));

        var agent = new ReActAgent(_mockProvider.Object, _options, NullLogger<ReActAgent>.Instance);

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
        _mockProvider.Setup(p => p.ChatAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatCompletionOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatCompletionResponse
            {
                Content = "Final Answer: The answer is 42"
            });

        var agent = new ReActAgent(_mockProvider.Object, _options, NullLogger<ReActAgent>.Instance);

        // Act
        await agent.RunAsync("What is the answer?");

        // Assert
        _mockProvider.Verify(p => p.ChatAsync(
            It.Is<IEnumerable<ChatMessage>>(msgs =>
                msgs.Count() == 2 &&
                msgs.First().Role == "system" &&
                msgs.Last().Role == "user"),
            It.IsAny<ChatCompletionOptions>(),
            It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunAsync_WithContext_ShouldUseProvidedContext()
    {
        // Arrange
        _mockProvider.Setup(p => p.ChatAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatCompletionOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatCompletionResponse
            {
                Content = "Thought: Searching...\nAction: Search\nAction Input: test"
            });

        var agent = new ReActAgent(_mockProvider.Object, _options, NullLogger<ReActAgent>.Instance);
        var context = new AgentContext
        {
            UserInput = "Custom input",
            MaxSteps = 1
        };

        // Act
        var response = await agent.RunAsync(context);

        // Assert
        response.Steps.Should().HaveCount(1);
    }
}
