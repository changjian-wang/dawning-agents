using Dawning.Agents.Abstractions.LLM;
using FluentAssertions;

namespace Dawning.Agents.Tests.LLM;

/// <summary>
/// Tests for StreamingChatEvent, extension methods, and accumulator
/// </summary>
public class StreamingChatEventTests
{
    #region StreamingChatEvent Factory Methods

    [Fact]
    public void Content_ShouldCreateContentDeltaEvent()
    {
        var evt = StreamingChatEvent.Content("Hello");

        evt.ContentDelta.Should().Be("Hello");
        evt.ToolCallDelta.Should().BeNull();
        evt.FinishReason.Should().BeNull();
        evt.Usage.Should().BeNull();
    }

    [Fact]
    public void ToolCall_ShouldCreateToolCallDeltaEvent()
    {
        var delta = new ToolCallDelta
        {
            Index = 0,
            Id = "call_1",
            FunctionName = "get_weather",
            ArgumentsDelta = "{\"city\":\"Beijing\"}",
        };
        var evt = StreamingChatEvent.ToolCall(delta);

        evt.ContentDelta.Should().BeNull();
        evt.ToolCallDelta.Should().NotBeNull();
        evt.ToolCallDelta!.FunctionName.Should().Be("get_weather");
        evt.ToolCallDelta.Id.Should().Be("call_1");
    }

    [Fact]
    public void Done_ShouldCreateFinishEvent()
    {
        var usage = new StreamingTokenUsage { PromptTokens = 100, CompletionTokens = 50 };
        var evt = StreamingChatEvent.Done("stop", usage);

        evt.ContentDelta.Should().BeNull();
        evt.FinishReason.Should().Be("stop");
        evt.Usage.Should().NotBeNull();
        evt.Usage!.TotalTokens.Should().Be(150);
    }

    [Fact]
    public void Done_WithoutUsage_ShouldHaveNullUsage()
    {
        var evt = StreamingChatEvent.Done("length");

        evt.FinishReason.Should().Be("length");
        evt.Usage.Should().BeNull();
    }

    #endregion

    #region ToolCallDelta

    [Fact]
    public void ToolCallDelta_ShouldStoreAllProperties()
    {
        var delta = new ToolCallDelta
        {
            Index = 2,
            Id = "call_abc",
            FunctionName = "search",
            ArgumentsDelta = "{\"q\":\"test",
        };

        delta.Index.Should().Be(2);
        delta.Id.Should().Be("call_abc");
        delta.FunctionName.Should().Be("search");
        delta.ArgumentsDelta.Should().Be("{\"q\":\"test");
    }

    #endregion

    #region StreamingTokenUsage

    [Fact]
    public void StreamingTokenUsage_TotalTokens_ShouldBeSumOfPromptAndCompletion()
    {
        var usage = new StreamingTokenUsage { PromptTokens = 200, CompletionTokens = 100 };

        usage.TotalTokens.Should().Be(300);
    }

    #endregion

    #region ToStreamingEvents Extension Method

    [Fact]
    public async Task ToStreamingEvents_ShouldWrapTextAsContentDelta()
    {
        var textStream = AsyncEnumerable(["Hello", " ", "World"]);

        var events = new List<StreamingChatEvent>();
        await foreach (var evt in textStream.ToStreamingEvents())
        {
            events.Add(evt);
        }

        // "Hello", "World" are content events; " " is empty-ish but let's check
        events.Should().Contain(e => e.ContentDelta == "Hello");
        events.Should().Contain(e => e.ContentDelta == "World");
        events.Last().FinishReason.Should().Be("stop");
    }

    [Fact]
    public async Task ToStreamingEvents_ShouldEmitDoneAtEnd()
    {
        var textStream = AsyncEnumerable(["Hi"]);

        var events = new List<StreamingChatEvent>();
        await foreach (var evt in textStream.ToStreamingEvents())
        {
            events.Add(evt);
        }

        events.Last().FinishReason.Should().Be("stop");
    }

    [Fact]
    public async Task ToStreamingEvents_EmptyStream_ShouldEmitDoneOnly()
    {
        var textStream = AsyncEnumerable(Array.Empty<string>());

        var events = new List<StreamingChatEvent>();
        await foreach (var evt in textStream.ToStreamingEvents())
        {
            events.Add(evt);
        }

        events.Should().HaveCount(1);
        events[0].FinishReason.Should().Be("stop");
    }

    #endregion

    #region AsTextStream Extension Method

    [Fact]
    public async Task AsTextStream_ShouldExtractOnlyContentDelta()
    {
        var events = AsyncEnumerable([
            StreamingChatEvent.Content("Hello"),
            StreamingChatEvent.Content(" World"),
            StreamingChatEvent.ToolCall(new ToolCallDelta { Index = 0, FunctionName = "test" }),
            StreamingChatEvent.Done("stop"),
        ]);

        var texts = new List<string>();
        await foreach (var text in events.AsTextStream())
        {
            texts.Add(text);
        }

        texts.Should().Equal("Hello", " World");
    }

    [Fact]
    public async Task AsTextStream_NoContent_ShouldReturnEmpty()
    {
        var events = AsyncEnumerable([StreamingChatEvent.Done("stop")]);

        var texts = new List<string>();
        await foreach (var text in events.AsTextStream())
        {
            texts.Add(text);
        }

        texts.Should().BeEmpty();
    }

    #endregion

    #region StreamingAccumulator

    [Fact]
    public async Task Accumulator_ShouldAccumulateContentDeltas()
    {
        var events = AsyncEnumerable([
            StreamingChatEvent.Content("Hello"),
            StreamingChatEvent.Content(" World"),
            StreamingChatEvent.Done("stop"),
        ]);

        var accumulator = await events.AccumulateAsync();

        accumulator.Content.Should().Be("Hello World");
        accumulator.FinishReason.Should().Be("stop");
        accumulator.HasToolCalls.Should().BeFalse();
    }

    [Fact]
    public async Task Accumulator_ShouldAccumulateToolCalls()
    {
        var events = AsyncEnumerable([
            // First tool call chunks
            StreamingChatEvent.ToolCall(
                new ToolCallDelta
                {
                    Index = 0,
                    Id = "call_1",
                    FunctionName = "get_weather",
                }
            ),
            StreamingChatEvent.ToolCall(
                new ToolCallDelta { Index = 0, ArgumentsDelta = "{\"city\":" }
            ),
            StreamingChatEvent.ToolCall(
                new ToolCallDelta { Index = 0, ArgumentsDelta = "\"Beijing\"}" }
            ),
            StreamingChatEvent.Done(
                "tool_calls",
                new StreamingTokenUsage { PromptTokens = 50, CompletionTokens = 20 }
            ),
        ]);

        var accumulator = await events.AccumulateAsync();

        accumulator.HasToolCalls.Should().BeTrue();
        accumulator.ToolCalls.Should().HaveCount(1);
        accumulator.ToolCalls[0].Id.Should().Be("call_1");
        accumulator.ToolCalls[0].FunctionName.Should().Be("get_weather");
        accumulator.ToolCalls[0].Arguments.Should().Be("{\"city\":\"Beijing\"}");
        accumulator.FinishReason.Should().Be("tool_calls");
        accumulator.Usage.Should().NotBeNull();
        accumulator.Usage!.TotalTokens.Should().Be(70);
    }

    [Fact]
    public async Task Accumulator_ShouldHandleMultipleToolCalls()
    {
        var events = AsyncEnumerable([
            StreamingChatEvent.ToolCall(
                new ToolCallDelta
                {
                    Index = 0,
                    Id = "call_a",
                    FunctionName = "search",
                    ArgumentsDelta = "{\"q\":\"test\"}",
                }
            ),
            StreamingChatEvent.ToolCall(
                new ToolCallDelta
                {
                    Index = 1,
                    Id = "call_b",
                    FunctionName = "calculate",
                    ArgumentsDelta = "{\"expr\":\"2+2\"}",
                }
            ),
            StreamingChatEvent.Done("tool_calls"),
        ]);

        var accumulator = await events.AccumulateAsync();

        accumulator.ToolCalls.Should().HaveCount(2);
        accumulator.ToolCalls[0].FunctionName.Should().Be("search");
        accumulator.ToolCalls[1].FunctionName.Should().Be("calculate");
    }

    [Fact]
    public async Task Accumulator_ToChatCompletionResponse_ShouldConvert()
    {
        var events = AsyncEnumerable([
            StreamingChatEvent.Content("Answer: 42"),
            StreamingChatEvent.Done(
                "stop",
                new StreamingTokenUsage { PromptTokens = 10, CompletionTokens = 5 }
            ),
        ]);

        var accumulator = await events.AccumulateAsync();
        var response = accumulator.ToChatCompletionResponse();

        response.Content.Should().Be("Answer: 42");
        response.FinishReason.Should().Be("stop");
        response.PromptTokens.Should().Be(10);
        response.CompletionTokens.Should().Be(5);
        response.TotalTokens.Should().Be(15);
        response.HasToolCalls.Should().BeFalse();
    }

    [Fact]
    public async Task Accumulator_ToChatCompletionResponse_WithToolCalls()
    {
        var events = AsyncEnumerable([
            StreamingChatEvent.ToolCall(
                new ToolCallDelta
                {
                    Index = 0,
                    Id = "call_1",
                    FunctionName = "test",
                    ArgumentsDelta = "{}",
                }
            ),
            StreamingChatEvent.Done("tool_calls"),
        ]);

        var accumulator = await events.AccumulateAsync();
        var response = accumulator.ToChatCompletionResponse();

        response.HasToolCalls.Should().BeTrue();
        response.ToolCalls.Should().HaveCount(1);
    }

    #endregion

    #region ILLMProvider Default Implementation

    [Fact]
    public async Task ILLMProvider_ChatStreamEventsAsync_DefaultImplementation_ShouldWork()
    {
        // A mock that only implements ChatStreamAsync (not ChatStreamEventsAsync)
        ILLMProvider provider = new MinimalProvider();

        var messages = new[] { ChatMessage.User("Hi") };
        var events = new List<StreamingChatEvent>();
        await foreach (var evt in provider.ChatStreamEventsAsync(messages))
        {
            events.Add(evt);
        }

        events.Should().Contain(e => e.ContentDelta == "Hello");
        events.Should().Contain(e => e.ContentDelta == " World");
        events.Last().FinishReason.Should().Be("stop");
    }

    /// <summary>
    /// Minimal ILLMProvider implementation, only implements basic methods, verifies the default implementation of ChatStreamEventsAsync
    /// </summary>
    private class MinimalProvider : ILLMProvider
    {
        public string Name => "Minimal";

        public Task<ChatCompletionResponse> ChatAsync(
            IEnumerable<ChatMessage> messages,
            ChatCompletionOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult(new ChatCompletionResponse { Content = "Hello World" });
        }

        public async IAsyncEnumerable<string> ChatStreamAsync(
            IEnumerable<ChatMessage> messages,
            ChatCompletionOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
                CancellationToken cancellationToken = default
        )
        {
            yield return "Hello";
            yield return " World";
            await Task.CompletedTask;
        }
    }

    #endregion

    #region Helpers

    private static async IAsyncEnumerable<T> AsyncEnumerable<T>(
        T[] items,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default
    )
    {
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }

        await Task.CompletedTask;
    }

    #endregion
}
