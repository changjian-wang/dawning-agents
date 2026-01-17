using System.ClientModel;
using System.Runtime.CompilerServices;
using OpenAI;
using OpenAI.Chat;

namespace DawningAgents.Core.LLM;

/// <summary>
/// OpenAI API 提供者实现
/// </summary>
public class OpenAIProvider : ILLMProvider
{
    private readonly ChatClient _chatClient;
    private readonly string _model;

    public string Name => "OpenAI";

    /// <summary>
    /// 创建 OpenAI 提供者
    /// </summary>
    /// <param name="apiKey">OpenAI API Key</param>
    /// <param name="model">模型名称，默认 gpt-4o</param>
    public OpenAIProvider(string apiKey, string model = "gpt-4o")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var client = new OpenAIClient(apiKey);
        _chatClient = client.GetChatClient(model);
        _model = model;
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
