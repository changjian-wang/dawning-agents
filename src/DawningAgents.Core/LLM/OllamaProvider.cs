using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DawningAgents.Abstractions.LLM;

namespace DawningAgents.Core.LLM;

/// <summary>
/// Ollama 本地模型提供者实现
/// </summary>
public class OllamaProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly string _baseUrl;

    public string Name => "Ollama";

    public OllamaProvider(string model = "deepseek-coder:1.3B", string baseUrl = "http://localhost:11434")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        _model = model;
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromMinutes(5) // 本地模型可能较慢
        };
    }

    public async Task<ChatCompletionResponse> ChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ChatCompletionOptions();

        var request = BuildRequest(messages, options, stream: false);
        var json = JsonSerializer.Serialize(request, JsonOptions.Default);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/api/chat", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions.Default, cancellationToken);

        return new ChatCompletionResponse
        {
            Content = result?.Message?.Content ?? string.Empty,
            PromptTokens = result?.PromptEvalCount ?? 0,
            CompletionTokens = result?.EvalCount ?? 0,
            FinishReason = result?.DoneReason ?? "stop"
        };
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new ChatCompletionOptions();

        var request = BuildRequest(messages, options, stream: true);
        var json = JsonSerializer.Serialize(request, JsonOptions.Default);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = content
        };

        var response = await _httpClient.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break; // End of stream
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var chunk = JsonSerializer.Deserialize<OllamaChatResponse>(line, JsonOptions.Default);
            if (!string.IsNullOrEmpty(chunk?.Message?.Content))
            {
                yield return chunk.Message.Content;
            }
        }
    }

    private OllamaChatRequest BuildRequest(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions options,
        bool stream)
    {
        var ollamaMessages = new List<OllamaMessage>();

        if (!string.IsNullOrWhiteSpace(options.SystemPrompt))
        {
            ollamaMessages.Add(new OllamaMessage { Role = "system", Content = options.SystemPrompt });
        }

        foreach (var msg in messages)
        {
            ollamaMessages.Add(new OllamaMessage
            {
                Role = msg.Role.ToLowerInvariant(),
                Content = msg.Content
            });
        }

        return new OllamaChatRequest
        {
            Model = _model,
            Messages = ollamaMessages,
            Stream = stream,
            Options = new OllamaOptions
            {
                Temperature = options.Temperature,
                NumPredict = options.MaxTokens
            }
        };
    }

    #region Ollama API Models

    private sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("messages")]
        public required List<OllamaMessage> Messages { get; init; }

        [JsonPropertyName("stream")]
        public bool Stream { get; init; }

        [JsonPropertyName("options")]
        public OllamaOptions? Options { get; init; }
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; init; }

        [JsonPropertyName("content")]
        public required string Content { get; init; }
    }

    private sealed class OllamaOptions
    {
        [JsonPropertyName("temperature")]
        public float Temperature { get; init; }

        [JsonPropertyName("num_predict")]
        public int NumPredict { get; init; }
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; init; }

        [JsonPropertyName("done")]
        public bool Done { get; init; }

        [JsonPropertyName("done_reason")]
        public string? DoneReason { get; init; }

        [JsonPropertyName("prompt_eval_count")]
        public int PromptEvalCount { get; init; }

        [JsonPropertyName("eval_count")]
        public int EvalCount { get; init; }
    }

    #endregion

    private static class JsonOptions
    {
        public static readonly JsonSerializerOptions Default = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}
