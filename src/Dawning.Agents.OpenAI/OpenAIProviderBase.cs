using System.ClientModel;
using System.Runtime.CompilerServices;
using Dawning.Agents.Abstractions.LLM;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Dawning.Agents.OpenAI;

// 类型别名消除歧义
using OpenAIChatMessage = global::OpenAI.Chat.ChatMessage;
using OpenAIChatOptions = global::OpenAI.Chat.ChatCompletionOptions;

/// <summary>
/// OpenAI SDK 基础提供者，封装 ChatClient 的通用交互逻辑。
/// 用于 OpenAI 和 Azure OpenAI 提供者的共享基类。
/// </summary>
public abstract class OpenAIProviderBase : ILLMProvider
{
    private readonly ChatClient _chatClient;
    private readonly ILogger _logger;

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <summary>
    /// 模型/部署标识名（用于日志）
    /// </summary>
    protected abstract string ModelIdentifier { get; }

    /// <summary>
    /// 创建 OpenAI 基础提供者
    /// </summary>
    /// <param name="chatClient">OpenAI ChatClient 实例</param>
    /// <param name="logger">日志记录器</param>
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
            "{Provider} ChatAsync 开始，标识: {Identifier}，消息数: {Count}",
            Name,
            ModelIdentifier,
            chatMessages.Count
        );

        try
        {
            var response = await _chatClient.CompleteChatAsync(
                chatMessages,
                requestOptions,
                cancellationToken
            );

            var completion = response.Value;

            // 安全提取文本内容（Content 可能为空，如 tool call 响应）
            var content =
                completion.Content.Count > 0
                    ? completion.Content[0].Text ?? string.Empty
                    : string.Empty;

            // 提取 tool calls（如有）
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

                _logger.LogDebug("收到 {Count} 个 tool calls", toolCalls.Count);
            }

            _logger.LogDebug(
                "{Provider} ChatAsync 完成，输入 tokens: {Input}，输出 tokens: {Output}，FinishReason: {Reason}",
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
                "{Provider} API 调用失败，标识: {Identifier}，状态码: {StatusCode}",
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
        await foreach (var evt in ChatStreamEventsAsync(messages, options, cancellationToken))
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
            "{Provider} ChatStreamEventsAsync 开始，标识: {Identifier}，消息数: {Count}",
            Name,
            ModelIdentifier,
            chatMessages.Count
        );

        string? finishReason = null;
        int? promptTokens = null;
        int? completionTokens = null;

        await foreach (
            var update in _chatClient.CompleteChatStreamingAsync(
                chatMessages,
                requestOptions,
                cancellationToken
            )
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
    /// 构建 OpenAI SDK 消息列表
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
                            ?? throw new ArgumentException("Tool 消息必须包含 ToolCallId"),
                        msg.Content
                    ),
                    _ => throw new ArgumentException($"未知角色: {msg.Role}"),
                }
            );
        }

        return result;
    }

    /// <summary>
    /// 创建包含 tool calls 的助手消息
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
    /// 构建请求选项
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

        // 设置响应格式
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

        // 设置工具定义
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
