using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Resilience;
using Dawning.Agents.Core.Resilience;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Dawning.Agents.Tests.Resilience;

public class ResilientLLMProviderTests
{
    private readonly Mock<ILLMProvider> _innerProviderMock;
    private readonly Mock<IResilienceProvider> _resilienceProviderMock;
    private readonly ResilientLLMProvider _resilientProvider;

    public ResilientLLMProviderTests()
    {
        _innerProviderMock = new Mock<ILLMProvider>();
        _innerProviderMock.Setup(x => x.Name).Returns("MockProvider");

        _resilienceProviderMock = new Mock<IResilienceProvider>();

        // 默认：直接执行操作
        _resilienceProviderMock
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<Func<CancellationToken, Task<ChatCompletionResponse>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<Func<CancellationToken, Task<ChatCompletionResponse>>, CancellationToken>(
                (op, ct) => op(ct)
            );

        _resilienceProviderMock
            .Setup(x =>
                x.ExecuteAsync(
                    It.IsAny<Func<CancellationToken, Task>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<Func<CancellationToken, Task>, CancellationToken>(
                async (op, ct) => await op(ct)
            );

        _resilientProvider = new ResilientLLMProvider(
            _innerProviderMock.Object,
            _resilienceProviderMock.Object
        );
    }

    [Fact]
    public void Name_ReturnsWrappedProviderName()
    {
        _resilientProvider.Name.Should().Be("Resilient(MockProvider)");
    }

    [Fact]
    public void Constructor_WithNullInnerProvider_ThrowsArgumentNullException()
    {
        var act = () => new ResilientLLMProvider(null!, _resilienceProviderMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("innerProvider");
    }

    [Fact]
    public void Constructor_WithNullResilienceProvider_ThrowsArgumentNullException()
    {
        var act = () => new ResilientLLMProvider(_innerProviderMock.Object, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("resilienceProvider");
    }

    [Fact]
    public async Task ChatAsync_CallsInnerProviderThroughResilience()
    {
        // Arrange
        var messages = new List<ChatMessage> { new("user", "Hello") };
        var expectedResponse = new ChatCompletionResponse
        {
            Content = "Hi there!",
            PromptTokens = 10,
            CompletionTokens = 5,
        };

        _innerProviderMock
            .Setup(x => x.ChatAsync(messages, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _resilientProvider.ChatAsync(messages);

        // Assert
        result.Should().Be(expectedResponse);
        _innerProviderMock.Verify(
            x => x.ChatAsync(messages, null, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ChatAsync_PassesOptionsToInnerProvider()
    {
        // Arrange
        var messages = new List<ChatMessage> { new("user", "Hello") };
        var options = new ChatCompletionOptions { Temperature = 0.7f };
        var expectedResponse = new ChatCompletionResponse { Content = "Response" };

        _innerProviderMock
            .Setup(x => x.ChatAsync(messages, options, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _resilientProvider.ChatAsync(messages, options);

        // Assert
        result.Should().Be(expectedResponse);
        _innerProviderMock.Verify(
            x => x.ChatAsync(messages, options, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ChatAsync_UsesResilienceProvider()
    {
        // Arrange
        var messages = new List<ChatMessage> { new("user", "Hello") };
        var expectedResponse = new ChatCompletionResponse { Content = "Response" };

        _innerProviderMock
            .Setup(x => x.ChatAsync(messages, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        await _resilientProvider.ChatAsync(messages);

        // Assert
        _resilienceProviderMock.Verify(
            x =>
                x.ExecuteAsync(
                    It.IsAny<Func<CancellationToken, Task<ChatCompletionResponse>>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task ChatStreamAsync_YieldsContentFromInnerProvider()
    {
        // Arrange
        var messages = new List<ChatMessage> { new("user", "Hello") };
        var expectedChunks = new[] { "Hello", " ", "World" };

        _innerProviderMock
            .Setup(x => x.ChatStreamAsync(messages, null, It.IsAny<CancellationToken>()))
            .Returns(GetAsyncEnumerable(expectedChunks));

        // Act
        var results = new List<string>();
        await foreach (var chunk in _resilientProvider.ChatStreamAsync(messages))
        {
            results.Add(chunk);
        }

        // Assert
        results.Should().BeEquivalentTo(expectedChunks);
    }

    private static async IAsyncEnumerable<string> GetAsyncEnumerable(IEnumerable<string> items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
