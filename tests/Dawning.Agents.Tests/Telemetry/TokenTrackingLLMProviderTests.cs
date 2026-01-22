using System.Runtime.CompilerServices;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Telemetry;
using Dawning.Agents.Core.Telemetry;
using FluentAssertions;
using Moq;

namespace Dawning.Agents.Tests.Telemetry;

public class TokenTrackingLLMProviderTests
{
    private readonly Mock<ILLMProvider> _mockProvider;
    private readonly Mock<ITokenUsageTracker> _mockTracker;
    private readonly TokenTrackingLLMProvider _sut;

    public TokenTrackingLLMProviderTests()
    {
        _mockProvider = new Mock<ILLMProvider>();
        _mockProvider.Setup(p => p.Name).Returns("TestProvider");

        _mockTracker = new Mock<ITokenUsageTracker>();

        _sut = new TokenTrackingLLMProvider(
            _mockProvider.Object,
            _mockTracker.Object,
            "TestSource",
            "test-session"
        );
    }

    [Fact]
    public void Constructor_ShouldThrowOnNullProvider()
    {
        // Act & Assert
        var act = () => new TokenTrackingLLMProvider(null!, _mockTracker.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("innerProvider");
    }

    [Fact]
    public void Constructor_ShouldThrowOnNullTracker()
    {
        // Act & Assert
        var act = () => new TokenTrackingLLMProvider(_mockProvider.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("tracker");
    }

    [Fact]
    public void Name_ShouldReturnInnerProviderName()
    {
        // Assert
        _sut.Name.Should().Be("TestProvider");
    }

    [Fact]
    public void Properties_ShouldReturnCorrectValues()
    {
        // Assert
        _sut.InnerProvider.Should().BeSameAs(_mockProvider.Object);
        _sut.Tracker.Should().BeSameAs(_mockTracker.Object);
        _sut.Source.Should().Be("TestSource");
        _sut.SessionId.Should().Be("test-session");
    }

    [Fact]
    public async Task ChatAsync_ShouldCallInnerProviderAndRecordTokens()
    {
        // Arrange
        var messages = new List<ChatMessage> { new("user", "Hello") };
        var response = new ChatCompletionResponse
        {
            Content = "Hello!",
            PromptTokens = 10,
            CompletionTokens = 5,
        };

        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    messages,
                    It.IsAny<ChatCompletionOptions?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(response);

        // Act
        var result = await _sut.ChatAsync(messages);

        // Assert
        result.Should().BeSameAs(response);

        _mockTracker.Verify(
            t =>
                t.Record(
                    It.Is<TokenUsageRecord>(r =>
                        r.Source == "TestSource"
                        && r.PromptTokens == 10
                        && r.CompletionTokens == 5
                        && r.Model == "TestProvider"
                        && r.SessionId == "test-session"
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task ChatAsync_ShouldPassOptionsToInnerProvider()
    {
        // Arrange
        var messages = new List<ChatMessage> { new("user", "Hello") };
        var options = new ChatCompletionOptions { Temperature = 0.5f };
        var response = new ChatCompletionResponse
        {
            Content = "Hello!",
            PromptTokens = 10,
            CompletionTokens = 5,
        };

        _mockProvider
            .Setup(p => p.ChatAsync(messages, options, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        await _sut.ChatAsync(messages, options);

        // Assert
        _mockProvider.Verify(
            p => p.ChatAsync(messages, options, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ChatAsync_ShouldRespectCancellation()
    {
        // Arrange
        var messages = new List<ChatMessage> { new("user", "Hello") };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    messages,
                    It.IsAny<ChatCompletionOptions?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _sut.ChatAsync(messages, cancellationToken: cts.Token)
        );
    }

    [Fact]
    public async Task ChatStreamAsync_ShouldForwardToInnerProvider()
    {
        // Arrange
        var messages = new List<ChatMessage> { new("user", "Hello") };
        var expectedChunks = new[] { "Hello", " ", "World" };

        _mockProvider
            .Setup(p =>
                p.ChatStreamAsync(
                    messages,
                    It.IsAny<ChatCompletionOptions?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(expectedChunks.ToAsyncEnumerable());

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in _sut.ChatStreamAsync(messages))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Should().BeEquivalentTo(expectedChunks);
    }

    [Fact]
    public void WithSource_ShouldCreateNewInstanceWithDifferentSource()
    {
        // Act
        var newProvider = _sut.WithSource("NewSource");

        // Assert
        newProvider.Should().NotBeSameAs(_sut);
        newProvider.Source.Should().Be("NewSource");
        newProvider.SessionId.Should().Be("test-session"); // 保留原 sessionId
        newProvider.InnerProvider.Should().BeSameAs(_mockProvider.Object);
        newProvider.Tracker.Should().BeSameAs(_mockTracker.Object);
    }

    [Fact]
    public void WithSession_ShouldCreateNewInstanceWithDifferentSession()
    {
        // Act
        var newProvider = _sut.WithSession("new-session");

        // Assert
        newProvider.Should().NotBeSameAs(_sut);
        newProvider.Source.Should().Be("TestSource"); // 保留原 source
        newProvider.SessionId.Should().Be("new-session");
        newProvider.InnerProvider.Should().BeSameAs(_mockProvider.Object);
        newProvider.Tracker.Should().BeSameAs(_mockTracker.Object);
    }

    [Fact]
    public async Task Integration_WithInMemoryTracker_ShouldTrackAllCalls()
    {
        // Arrange
        var realTracker = new InMemoryTokenUsageTracker();
        var provider = new TokenTrackingLLMProvider(_mockProvider.Object, realTracker, "TestAgent");

        var messages = new List<ChatMessage> { new("user", "Hello") };

        _mockProvider
            .Setup(p =>
                p.ChatAsync(
                    messages,
                    It.IsAny<ChatCompletionOptions?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ChatCompletionResponse
                {
                    Content = "Response",
                    PromptTokens = 20,
                    CompletionTokens = 10,
                }
            );

        // Act
        await provider.ChatAsync(messages);
        await provider.ChatAsync(messages);

        // Assert
        realTracker.CallCount.Should().Be(2);
        realTracker.TotalPromptTokens.Should().Be(40);
        realTracker.TotalCompletionTokens.Should().Be(20);
    }
}

internal static class AsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(
        this IEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
            await Task.Yield();
        }
    }
}
