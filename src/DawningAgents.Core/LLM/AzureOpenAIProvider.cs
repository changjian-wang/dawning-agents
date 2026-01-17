using System.ClientModel;
using System.Runtime.CompilerServices;
using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using OpenAI.Chat;

namespace DawningAgents.Core.LLM;

/// <summary>
/// Azure OpenAI / Azure AI Foundry 提供者实现
/// 支持 Azure OpenAI Service 和 Azure AI Foundry 部署的模型
/// </summary>
public class AzureOpenAIProvider : ILLMProvider
{
    private readonly ChatClient _chatClient;
    private readonly string _deploymentName;

    public string Name => "AzureOpenAI";

    /// <summary>
    /// 创建 Azure OpenAI 提供者
    /// </summary>
    /// <param name="endpoint">Azure OpenAI 端点，如 https://your-resource.openai.azure.com/</param>
    /// <param name="apiKey">Azure OpenAI API Key</param>
    /// <param name="deploymentName">模型部署名称</param>
    public AzureOpenAIProvider(string endpoint, string apiKey, string deploymentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentName);

        var client = new AzureOpenAIClient(
            new Uri(endpoint),
            new AzureKeyCredential(apiKey));

        _chatClient = client.GetChatClient(deploymentName);
        _deploymentName = deploymentName;
    }

    /// <summary>
    /// 使用 Azure AD 身份验证创建 Azure OpenAI 提供者
    /// </summary>
    /// <param name="endpoint">Azure OpenAI 端点</param>
    /// <param name="credential">Azure 凭据（如 DefaultAzureCredential）</param>
    /// <param name="deploymentName">模型部署名称</param>
    public AzureOpenAIProvider(string endpoint, TokenCredential credential, string deploymentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentName);

        var client = new AzureOpenAIClient(new Uri(endpoint), credential);
        _chatClient = client.GetChatClient(deploymentName);
        _deploymentName = deploymentName;
    }

    public async Task<ChatCompletionResponse> ChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ChatCompletionOptions();

        var chatMessages = BuildMessages(messages, options.SystemPrompt);
        var requestOptions = BuildRequestOptions(options);

        var response = await _chatClient.CompleteChatAsync(
            chatMessages,
            requestOptions,
            cancellationToken);

        var completion = response.Value;

        return new ChatCompletionResponse
        {
            Content = completion.Content[0].Text ?? string.Empty,
            PromptTokens = completion.Usage.InputTokenCount,
            CompletionTokens = completion.Usage.OutputTokenCount,
            FinishReason = completion.FinishReason.ToString()
        };
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new ChatCompletionOptions();

        var chatMessages = BuildMessages(messages, options.SystemPrompt);
        var requestOptions = BuildRequestOptions(options);

        await foreach (var update in _chatClient.CompleteChatStreamingAsync(
            chatMessages,
            requestOptions,
            cancellationToken))
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

    private static List<OpenAI.Chat.ChatMessage> BuildMessages(
        IEnumerable<ChatMessage> messages,
        string? systemPrompt)
    {
        var result = new List<OpenAI.Chat.ChatMessage>();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            result.Add(new SystemChatMessage(systemPrompt));
        }

        foreach (var msg in messages)
        {
            result.Add(msg.Role.ToLowerInvariant() switch
            {
                "user" => new UserChatMessage(msg.Content),
                "assistant" => new AssistantChatMessage(msg.Content),
                "system" => new SystemChatMessage(msg.Content),
                _ => throw new ArgumentException($"未知角色: {msg.Role}")
            });
        }

        return result;
    }

    private static OpenAI.Chat.ChatCompletionOptions BuildRequestOptions(ChatCompletionOptions options)
    {
        return new OpenAI.Chat.ChatCompletionOptions
        {
            Temperature = options.Temperature,
            MaxOutputTokenCount = options.MaxTokens
        };
    }
}
