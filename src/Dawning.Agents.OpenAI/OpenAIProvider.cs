using System.ClientModel;
using System.Runtime.CompilerServices;
using Dawning.Agents.Abstractions.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI;
using OpenAI.Chat;

namespace Dawning.Agents.OpenAI;

// 类型别名消除歧义
using OpenAIChatMessage = global::OpenAI.Chat.ChatMessage;
using OpenAIChatOptions = global::OpenAI.Chat.ChatCompletionOptions;

/// <summary>
/// OpenAI API 提供者实现
/// </summary>
public class OpenAIProvider : ILLMProvider
{
    private readonly ChatClient _chatClient;
    private readonly string _model;
    private readonly ILogger<OpenAIProvider> _logger;

    public string Name => "OpenAI";

    /// <summary>
    /// 创建 OpenAI 提供者
    /// </summary>
    /// <param name="apiKey">OpenAI API Key</param>
    /// <param name="model">模型名称，默认 gpt-4o</param>
    /// <param name="logger">日志记录器</param>
    public OpenAIProvider(
        string apiKey,
        string model = "gpt-4o",
        ILogger<OpenAIProvider>? logger = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var client = new OpenAIClient(apiKey);
        _chatClient = client.GetChatClient(model);
        _model = model;
        _logger = logger ?? NullLogger<OpenAIProvider>.Instance;
        _logger.LogDebug("OpenAIProvider 已创建，模型: {Model}", model);
    }

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
            "OpenAI ChatAsync 开始，模型: {Model}，消息数: {Count}",
            _model,
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
                "OpenAI ChatAsync 完成，输入 tokens: {Input}，输出 tokens: {Output}，FinishReason: {Reason}",
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
                "OpenAI API 调用失败，模型: {Model}，状态码: {StatusCode}",
                _model,
                ex.Status
            );
            throw;
        }
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<Abstractions.LLM.ChatMessage> messages,
        Abstractions.LLM.ChatCompletionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        options ??= new Abstractions.LLM.ChatCompletionOptions();

        var chatMessages = BuildMessages(messages, options.SystemPrompt);
        var requestOptions = BuildRequestOptions(options);

        _logger.LogDebug(
            "OpenAI ChatStreamAsync 开始，模型: {Model}，消息数: {Count}",
            _model,
            chatMessages.Count
        );

        await foreach (
            var update in _chatClient.CompleteChatStreamingAsync(
                chatMessages,
                requestOptions,
                cancellationToken
            )
        )
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    yield return part.Text;
                }
            }
        }
    }

    private static List<OpenAIChatMessage> BuildMessages(
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
                    "assistant" when msg.HasToolCalls
                        => CreateAssistantWithToolCalls(msg),
                    "assistant" => new AssistantChatMessage(msg.Content),
                    "system" => new SystemChatMessage(msg.Content),
                    "tool" => new ToolChatMessage(
                        msg.ToolCallId ?? throw new ArgumentException(
                            "Tool 消息必须包含 ToolCallId"
                        ),
                        msg.Content
                    ),
                    _ => throw new ArgumentException($"未知角色: {msg.Role}"),
                }
            );
        }

        return result;
    }

    private static AssistantChatMessage CreateAssistantWithToolCalls(
        Abstractions.LLM.ChatMessage msg
    )
    {
        var toolCalls = msg
            .ToolCalls!.Select(tc => ChatToolCall.CreateFunctionToolCall(
                tc.Id,
                tc.FunctionName,
                BinaryData.FromString(tc.Arguments ?? "{}")
            ))
            .ToList();

        return new AssistantChatMessage(toolCalls);
    }

    private static OpenAIChatOptions BuildRequestOptions(
        Abstractions.LLM.ChatCompletionOptions options
    )
    {
        var requestOptions = new OpenAIChatOptions
        {
            Temperature = options.Temperature,
            MaxOutputTokenCount = options.MaxTokens,
        };

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
