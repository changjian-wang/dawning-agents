using System.ClientModel;
using System.Runtime.CompilerServices;
using Dawning.Agents.Abstractions.LLM;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Dawning.Agents.OpenAI;

// Type aliases to disambiguate
using OpenAIChatMessage = global::OpenAI.Chat.ChatMessage;
using OpenAIChatOptions = global::OpenAI.Chat.ChatCompletionOptions;

/// <summary>
/// Base provider for OpenAI SDK that encapsulates common <see cref="ChatClient"/> interaction logic.
/// Serves as a shared base class for OpenAI and Azure OpenAI providers.
/// </summary>
public abstract class OpenAIProviderBase : ILLMProvider
{
    private readonly ChatClient _chatClient;
    private readonly ILogger _logger;

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <summary>
    /// Gets the model or deployment identifier used for logging.
    /// </summary>
    protected abstract string ModelIdentifier { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIProviderBase"/> class.
    /// </summary>
    /// <param name="chatClient">The OpenAI <see cref="ChatClient"/> instance.</param>
    /// <param name="logger">The logger instance.</param>
    protected OpenAIProviderBase(ChatClient chatClient, ILogger logger)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ChatCompletionResponse> ChatAsync(
        IEnumerable<Abstractions.LLM.ChatMessage> messages,
        Abstractions.LLM.ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= new Abstractions.LLM.ChatCompletionOptions();

        var chatMessages = BuildMessages(messages, options.SystemPrompt);
        var requestOptions = BuildRequestOptions(options);

        _logger.LogDebug(
            "{Provider} ChatAsync started, identifier: {Identifier}, message count: {Count}",
            Name,
            ModelIdentifier,
            chatMessages.Count
        );

        try
        {
            var response = await _chatClient
                .CompleteChatAsync(chatMessages, requestOptions, cancellationToken)
                .ConfigureAwait(false);

            var completion = response.Value;

            // Safely extract text content (Content may be empty, e.g. in tool call responses)
            var content =
                completion.Content.Count > 0
                    ? completion.Content[0].Text ?? string.Empty
                    : string.Empty;

            // Extract tool calls if present
            IReadOnlyList<Abstractions.LLM.ToolCall>? toolCalls = null;
            if (completion.ToolCalls.Count > 0)
            {
                toolCalls = completion
                    .ToolCalls.Select(tc => new Abstractions.LLM.ToolCall(
                        tc.Id,
                        tc.FunctionName,
                        tc.FunctionArguments.ToString()
                    ))
                    .ToList();

                _logger.LogDebug("Received {Count} tool call(s)", toolCalls.Count);
            }

            _logger.LogDebug(
                "{Provider} ChatAsync completed, input tokens: {Input}, output tokens: {Output}, FinishReason: {Reason}",
                Name,
                completion.Usage.InputTokenCount,
                completion.Usage.OutputTokenCount,
                completion.FinishReason
            );

            return new ChatCompletionResponse
            {
                Content = content,
                PromptTokens = completion.Usage.InputTokenCount,
                CompletionTokens = completion.Usage.OutputTokenCount,
                FinishReason = completion.FinishReason.ToString(),
                ToolCalls = toolCalls,
            };
        }
        catch (ClientResultException ex)
        {
            _logger.LogError(
                ex,
                "{Provider} API call failed, identifier: {Identifier}, status code: {StatusCode}",
                Name,
                ModelIdentifier,
                ex.Status
            );
            throw;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<Abstractions.LLM.ChatMessage> messages,
        Abstractions.LLM.ChatCompletionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await foreach (
            var evt in ChatStreamEventsAsync(messages, options, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            if (!string.IsNullOrEmpty(evt.ContentDelta))
            {
                yield return evt.ContentDelta;
            }
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StreamingChatEvent> ChatStreamEventsAsync(
        IEnumerable<Abstractions.LLM.ChatMessage> messages,
        Abstractions.LLM.ChatCompletionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        options ??= new Abstractions.LLM.ChatCompletionOptions();

        var chatMessages = BuildMessages(messages, options.SystemPrompt);
        var requestOptions = BuildRequestOptions(options);

        _logger.LogDebug(
            "{Provider} ChatStreamEventsAsync started, identifier: {Identifier}, message count: {Count}",
            Name,
            ModelIdentifier,
            chatMessages.Count
        );

        string? finishReason = null;
        int? promptTokens = null;
        int? completionTokens = null;

        await foreach (
            var update in _chatClient
                .CompleteChatStreamingAsync(chatMessages, requestOptions, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            // Content delta
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    yield return StreamingChatEvent.Content(part.Text);
                }
            }

            // Tool call delta
            foreach (var toolCallUpdate in update.ToolCallUpdates)
            {
                yield return StreamingChatEvent.ToolCall(
                    new ToolCallDelta
                    {
                        Index = toolCallUpdate.Index,
                        Id = toolCallUpdate.ToolCallId,
                        FunctionName = toolCallUpdate.FunctionName,
                        ArgumentsDelta = toolCallUpdate.FunctionArgumentsUpdate?.ToString(),
                    }
                );
            }

            // Finish reason
            if (update.FinishReason is { } reason)
            {
                finishReason = reason.ToString();
            }

            // Usage (may appear in last chunk)
            if (update.Usage is { } usage)
            {
                promptTokens = usage.InputTokenCount;
                completionTokens = usage.OutputTokenCount;
            }
        }

        // Emit final Done event
        yield return StreamingChatEvent.Done(
            finishReason ?? "stop",
            promptTokens.HasValue || completionTokens.HasValue
                ? new StreamingTokenUsage
                {
                    PromptTokens = promptTokens ?? 0,
                    CompletionTokens = completionTokens ?? 0,
                }
                : null
        );
    }

    /// <summary>
    /// Builds the OpenAI SDK message list from the specified messages.
    /// </summary>
    protected static List<OpenAIChatMessage> BuildMessages(
        IEnumerable<Abstractions.LLM.ChatMessage> messages,
        string? systemPrompt
    )
    {
        var result = new List<OpenAIChatMessage>();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            result.Add(new SystemChatMessage(systemPrompt));
        }

        foreach (var msg in messages)
        {
            result.Add(
                msg.Role.ToLowerInvariant() switch
                {
                    "user" => new UserChatMessage(msg.Content),
                    "assistant" when msg.HasToolCalls => CreateAssistantWithToolCalls(msg),
                    "assistant" => new AssistantChatMessage(msg.Content),
                    "system" => new SystemChatMessage(msg.Content),
                    "tool" => new ToolChatMessage(
                        msg.ToolCallId
                            ?? throw new ArgumentException("Tool message must contain a ToolCallId"),
                        msg.Content
                    ),
                    _ => throw new ArgumentException($"Unknown role: {msg.Role}"),
                }
            );
        }

        return result;
    }

    /// <summary>
    /// Creates an assistant message containing tool calls.
    /// </summary>
    protected static AssistantChatMessage CreateAssistantWithToolCalls(
        Abstractions.LLM.ChatMessage msg
    )
    {
        var toolCalls = msg.ToolCalls!.Select(tc =>
                ChatToolCall.CreateFunctionToolCall(
                    tc.Id,
                    tc.FunctionName,
                    BinaryData.FromString(tc.Arguments ?? "{}")
                )
            )
            .ToList();

        return new AssistantChatMessage(toolCalls);
    }

    /// <summary>
    /// Builds the request options from the specified chat completion options.
    /// </summary>
    protected static OpenAIChatOptions BuildRequestOptions(
        Abstractions.LLM.ChatCompletionOptions options
    )
    {
        var requestOptions = new OpenAIChatOptions
        {
            Temperature = options.Temperature,
            MaxOutputTokenCount = options.MaxTokens,
        };

        // Set response format
        if (options.ResponseFormat is { } format)
        {
            requestOptions.ResponseFormat = format.Type switch
            {
                Abstractions.LLM.ResponseFormatType.JsonObject =>
                    ChatResponseFormat.CreateJsonObjectFormat(),
                Abstractions.LLM.ResponseFormatType.JsonSchema
                    when !string.IsNullOrWhiteSpace(format.SchemaName)
                        && !string.IsNullOrWhiteSpace(format.Schema) =>
                    ChatResponseFormat.CreateJsonSchemaFormat(
                        format.SchemaName!,
                        BinaryData.FromString(format.Schema!),
                        null,
                        format.Strict
                    ),
                _ => ChatResponseFormat.CreateTextFormat(),
            };
        }

        // Set tool definitions
        if (options.Tools is { Count: > 0 })
        {
            foreach (var tool in options.Tools)
            {
                requestOptions.Tools.Add(
                    ChatTool.CreateFunctionTool(
                        tool.Name,
                        tool.Description,
                        BinaryData.FromString(tool.ParametersSchema ?? "{}")
                    )
                );
            }
        }

        return requestOptions;
    }
}
