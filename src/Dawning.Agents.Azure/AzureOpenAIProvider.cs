using System.ClientModel;
using System.Runtime.CompilerServices;
using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Dawning.Agents.Abstractions.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI.Chat;

namespace Dawning.Agents.Azure;

// 类型别名消除歧义
using OpenAIChatMessage = global::OpenAI.Chat.ChatMessage;
using OpenAIChatOptions = global::OpenAI.Chat.ChatCompletionOptions;

/// <summary>
/// Azure OpenAI / Azure AI Foundry 提供者实现
/// 支持 Azure OpenAI Service 和 Azure AI Foundry 部署的模型
/// </summary>
public class AzureOpenAIProvider : ILLMProvider
{
    private readonly ChatClient _chatClient;
    private readonly string _deploymentName;
    private readonly ILogger<AzureOpenAIProvider> _logger;

    public string Name => "AzureOpenAI";

    /// <summary>
    /// 创建 Azure OpenAI 提供者
    /// </summary>
    /// <param name="endpoint">Azure OpenAI 端点，如 https://your-resource.openai.azure.com/</param>
    /// <param name="apiKey">Azure OpenAI API Key</param>
    /// <param name="deploymentName">模型部署名称</param>
    /// <param name="logger">日志记录器</param>
    public AzureOpenAIProvider(
        string endpoint,
        string apiKey,
        string deploymentName,
        ILogger<AzureOpenAIProvider>? logger = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentName);

        var client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));

        _chatClient = client.GetChatClient(deploymentName);
        _deploymentName = deploymentName;
        _logger = logger ?? NullLogger<AzureOpenAIProvider>.Instance;
        _logger.LogDebug(
            "AzureOpenAIProvider 已创建，端点: {Endpoint}，部署: {Deployment}",
            endpoint,
            deploymentName
        );
    }

    /// <summary>
    /// 使用 Azure AD 身份验证创建 Azure OpenAI 提供者
    /// </summary>
    /// <param name="endpoint">Azure OpenAI 端点</param>
    /// <param name="credential">Azure 凭据（如 DefaultAzureCredential）</param>
    /// <param name="deploymentName">模型部署名称</param>
    /// <param name="logger">日志记录器</param>
    public AzureOpenAIProvider(
        string endpoint,
        TokenCredential credential,
        string deploymentName,
        ILogger<AzureOpenAIProvider>? logger = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentName);

        var client = new AzureOpenAIClient(new Uri(endpoint), credential);
        _chatClient = client.GetChatClient(deploymentName);
        _deploymentName = deploymentName;
        _logger = logger ?? NullLogger<AzureOpenAIProvider>.Instance;
        _logger.LogDebug(
            "AzureOpenAIProvider 已创建（Azure AD 认证），端点: {Endpoint}，部署: {Deployment}",
            endpoint,
            deploymentName
        );
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
            "AzureOpenAI ChatAsync 开始，部署: {Deployment}，消息数: {Count}",
            _deploymentName,
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
                "AzureOpenAI ChatAsync 完成，输入 tokens: {Input}，输出 tokens: {Output}，FinishReason: {Reason}",
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
                "AzureOpenAI API 调用失败，部署: {Deployment}，状态码: {StatusCode}",
                _deploymentName,
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
            "AzureOpenAI ChatStreamAsync 开始，部署: {Deployment}，消息数: {Count}",
            _deploymentName,
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
