using System.Runtime.CompilerServices;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Core.ModelManagement;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Dawning.Agents.Tests.LLM;

/// <summary>
/// Regression tests for RoutingLLMProvider stream failover.
/// Async iterator methods defer execution until MoveNextAsync; the original
/// try/catch around the call site was dead code. These tests verify that
/// ProbeStreamAsync detects first-element errors and triggers failover.
/// </summary>
public class RoutingLLMProviderStreamFailoverTests
{
    private static IOptions<ModelRouterOptions> FailoverOptions(int retries = 2) =>
        Options.Create(
            new ModelRouterOptions { EnableFailover = true, MaxFailoverRetries = retries }
        );

    /// <summary>
    /// Simulates a provider whose ChatStreamAsync throws on first MoveNextAsync
    /// (e.g., connection refused). Verifies failover to a healthy provider.
    /// </summary>
    [Fact]
    public async Task ChatStreamAsync_FirstProviderThrowsOnIteration_FailsOverToSecond()
    {
        // Arrange
        var failingProvider = new Mock<ILLMProvider>();
        failingProvider.Setup(p => p.Name).Returns("failing");
        failingProvider
            .Setup(p =>
                p.ChatStreamAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ThrowingStreamAsync("connection refused"));

        var healthyProvider = new Mock<ILLMProvider>();
        healthyProvider.Setup(p => p.Name).Returns("healthy");
        healthyProvider
            .Setup(p =>
                p.ChatStreamAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(SuccessfulStreamAsync("Hello", " World"));

        var router = new Mock<IModelRouter>();
        var callCount = 0;
        router
            .Setup(r =>
                r.SelectProviderAsync(
                    It.IsAny<ModelRoutingContext>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => ++callCount == 1 ? failingProvider.Object : healthyProvider.Object);

        var sut = new RoutingLLMProvider(router.Object, FailoverOptions());

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in sut.ChatStreamAsync([ChatMessage.User("Hi")]))
        {
            chunks.Add(chunk);
        }

        // Assert — should have received data from the healthy (second) provider
        chunks.Should().BeEquivalentTo(["Hello", " World"]);
        router.Verify(
            r => r.ReportResult(failingProvider.Object, It.Is<ModelCallResult>(cr => !cr.Success)),
            Times.Once,
            "Should have reported failure for the first provider"
        );
    }

    /// <summary>
    /// Verifies ChatStreamEventsAsync also probes and fails over correctly.
    /// </summary>
    [Fact]
    public async Task ChatStreamEventsAsync_FirstProviderThrowsOnIteration_FailsOverToSecond()
    {
        // Arrange
        var failingProvider = new Mock<ILLMProvider>();
        failingProvider.Setup(p => p.Name).Returns("failing");
        failingProvider
            .Setup(p =>
                p.ChatStreamEventsAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ThrowingEventStreamAsync("connection refused"));

        var healthyProvider = new Mock<ILLMProvider>();
        healthyProvider.Setup(p => p.Name).Returns("healthy");
        healthyProvider
            .Setup(p =>
                p.ChatStreamEventsAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                SuccessfulEventStreamAsync(
                    new StreamingChatEvent { ContentDelta = "Hello" },
                    new StreamingChatEvent { ContentDelta = " World", FinishReason = "stop" }
                )
            );

        var router = new Mock<IModelRouter>();
        var callCount = 0;
        router
            .Setup(r =>
                r.SelectProviderAsync(
                    It.IsAny<ModelRoutingContext>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => ++callCount == 1 ? failingProvider.Object : healthyProvider.Object);

        var sut = new RoutingLLMProvider(router.Object, FailoverOptions());

        // Act
        var events = new List<StreamingChatEvent>();
        await foreach (var evt in sut.ChatStreamEventsAsync([ChatMessage.User("Hi")]))
        {
            events.Add(evt);
        }

        // Assert — should have received events from the healthy provider
        events.Should().HaveCount(2);
        events[0].ContentDelta.Should().Be("Hello");
        events[1].ContentDelta.Should().Be(" World");
    }

    /// <summary>
    /// Verifies that OperationCanceledException during stream iteration
    /// propagates instead of triggering failover.
    /// </summary>
    [Fact]
    public async Task ChatStreamAsync_CancellationDuringIteration_Propagates()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        var provider = new Mock<ILLMProvider>();
        provider.Setup(p => p.Name).Returns("cancelling");
        provider
            .Setup(p =>
                p.ChatStreamAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (IEnumerable<ChatMessage> _, ChatCompletionOptions? _, CancellationToken ct) =>
                    CancellingStreamAsync(ct)
            );

        var router = new Mock<IModelRouter>();
        router
            .Setup(r =>
                r.SelectProviderAsync(
                    It.IsAny<ModelRoutingContext>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(provider.Object);

        var sut = new RoutingLLMProvider(router.Object, FailoverOptions());

        // Act & Assert
        await cts.CancelAsync();
        var act = async () =>
        {
            await foreach (
                var _ in sut.ChatStreamAsync([ChatMessage.User("Hi")], cancellationToken: cts.Token)
            ) { }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // --- Helpers ---

    private static async IAsyncEnumerable<string> ThrowingStreamAsync(
        string errorMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;
        // Task.FromException produces a faulted task; await it to throw on first MoveNextAsync
        await Task.FromException(new InvalidOperationException(errorMessage)).ConfigureAwait(false);
        yield break;
    }

    private static async IAsyncEnumerable<StreamingChatEvent> ThrowingEventStreamAsync(
        string errorMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;
        await Task.FromException(new InvalidOperationException(errorMessage)).ConfigureAwait(false);
        yield break;
    }

    private static async IAsyncEnumerable<string> SuccessfulStreamAsync(params string[] chunks)
    {
        foreach (var chunk in chunks)
        {
            yield return chunk;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static async IAsyncEnumerable<StreamingChatEvent> SuccessfulEventStreamAsync(
        params StreamingChatEvent[] events
    )
    {
        foreach (var evt in events)
        {
            yield return evt;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static async IAsyncEnumerable<string> CancellingStreamAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
        yield break;
    }
}
