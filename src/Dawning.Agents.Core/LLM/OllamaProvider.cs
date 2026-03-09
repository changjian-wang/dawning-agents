using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Dawning.Agents.Abstractions.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.LLM;

/// <summary>
/// Ollama 本地模型提供者实现（支持 Native Function Calling）
/// </summary>
public class OllamaProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<OllamaProvider> _logger;

    public string Name => "Ollama";

    public OllamaProvider(
        HttpClient httpClient,
        string model,
        ILogger<OllamaProvider>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        _httpClient = httpClient;
        _model = model;
        _logger = logger ?? NullLogger<OllamaProvider>.Instance;

        _logger.LogDebug("OllamaProvider 已创建，模型: {Model}", model);
    }

    public async Task<ChatCompletionResponse> ChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= new ChatCompletionOptions();

        var request = BuildRequest(messages, options, stream: false);
        var json = JsonSerializer.Serialize(request, JsonOptions.Default);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogDebug("发送聊天请求到 Ollama，模型: {Model}", _model);

        using var response = await _httpClient
            .PostAsync("/api/chat", content, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var result = await response
            .Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions.Default, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug(
            "Ollama 响应完成，Token: {Prompt}/{Completion}",
            result?.PromptEvalCount,
            result?.EvalCount
        );

        // 提取 tool calls（如有）
        IReadOnlyList<ToolCall>? toolCalls = null;
        var finishReason = result?.DoneReason ?? "stop";

        if (result?.Message?.ToolCalls is { Count: > 0 } ollamaToolCalls)
        {
            var callIdCounter = 0;
            toolCalls = ollamaToolCalls
                .Where(tc => tc.Function != null)
                .Select(tc => new ToolCall(
                    $"call_{callIdCounter++}",
                    tc.Function!.Name,
                    tc.Function.Arguments is not null
                        ? JsonSerializer.Serialize(tc.Function.Arguments, JsonOptions.Default)
                        : "{}"
                ))
                .ToList();

            finishReason = "tool_calls";
            _logger.LogDebug("收到 {Count} 个 tool calls", toolCalls.Count);
        }

        return new ChatCompletionResponse
        {
            Content = result?.Message?.Content ?? string.Empty,
            PromptTokens = result?.PromptEvalCount ?? 0,
            CompletionTokens = result?.EvalCount ?? 0,
            FinishReason = finishReason,
            ToolCalls = toolCalls,
        };
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        options ??= new ChatCompletionOptions();

        var request = BuildRequest(messages, options, stream: true);
        var json = JsonSerializer.Serialize(request, JsonOptions.Default);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = content,
        };

        using var response = await _httpClient
            .SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break; // End of stream
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var chunk = TryDeserializeChunk(line);
            if (!string.IsNullOrEmpty(chunk?.Message?.Content))
            {
                yield return chunk.Message.Content;
            }
        }
    }

    public async IAsyncEnumerable<StreamingChatEvent> ChatStreamEventsAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        options ??= new ChatCompletionOptions();

        var request = BuildRequest(messages, options, stream: true);
        var json = JsonSerializer.Serialize(request, JsonOptions.Default);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = content,
        };

        using var response = await _httpClient
            .SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? finishReason = null;
        int promptTokens = 0;
        int completionTokens = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var chunk = TryDeserializeChunk(line);
            if (chunk is null)
            {
                continue;
            }

            // Content delta
            if (!string.IsNullOrEmpty(chunk.Message?.Content))
            {
                yield return StreamingChatEvent.Content(chunk.Message.Content);
            }

            // Tool call delta (Ollama sends complete tool calls per chunk)
            if (chunk.Message?.ToolCalls is { Count: > 0 } toolCalls)
            {
                for (var i = 0; i < toolCalls.Count; i++)
                {
                    var tc = toolCalls[i];
                    if (tc.Function != null)
                    {
                        yield return StreamingChatEvent.ToolCall(
                            new ToolCallDelta
                            {
                                Index = i,
                                Id = $"call_{i}",
                                FunctionName = tc.Function.Name,
                                ArgumentsDelta = tc.Function.Arguments is not null
                                    ? JsonSerializer.Serialize(
                                        tc.Function.Arguments,
                                        JsonOptions.Default
                                    )
                                    : "{}",
                            }
                        );
                    }
                }
            }

            // Final chunk
            if (chunk.Done)
            {
                finishReason = chunk.DoneReason ?? "stop";
                promptTokens = chunk.PromptEvalCount;
                completionTokens = chunk.EvalCount;
            }
        }

        yield return StreamingChatEvent.Done(
            finishReason ?? "stop",
            new StreamingTokenUsage
            {
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
            }
        );
    }

    private OllamaChatResponse? TryDeserializeChunk(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<OllamaChatResponse>(line, JsonOptions.Default);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Ollama streaming response chunk");
            return null;
        }
    }

    private OllamaChatRequest BuildRequest(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions options,
        bool stream
    )
    {
        var ollamaMessages = new List<OllamaMessage>();

        if (!string.IsNullOrWhiteSpace(options.SystemPrompt))
        {
            ollamaMessages.Add(
                new OllamaMessage { Role = "system", Content = options.SystemPrompt }
            );
        }

        foreach (var msg in messages)
        {
            var ollamaMsg = new OllamaMessage
            {
                Role = msg.Role.ToLowerInvariant(),
                Content = msg.Content,
            };

            // assistant 消息携带 tool_calls
            if (msg.HasToolCalls)
            {
                ollamaMsg.ToolCalls = msg.ToolCalls!.Select(tc => new OllamaToolCall
                    {
                        Function = new OllamaFunctionCall
                        {
                            Name = tc.FunctionName,
                            Arguments = string.IsNullOrWhiteSpace(tc.Arguments)
                                ? null
                                : JsonSerializer.Deserialize<JsonObject>(tc.Arguments),
                        },
                    })
                    .ToList();
            }

            ollamaMessages.Add(ollamaMsg);
        }

        // 构建 tools 列表
        List<OllamaToolDefinition>? tools = null;
        if (options.Tools is { Count: > 0 })
        {
            tools = options
                .Tools.Select(t => new OllamaToolDefinition
                {
                    Type = "function",
                    Function = new OllamaFunctionDefinition
                    {
                        Name = t.Name,
                        Description = t.Description,
                        Parameters = string.IsNullOrWhiteSpace(t.ParametersSchema)
                            ? null
                            : JsonSerializer.Deserialize<JsonObject>(t.ParametersSchema),
                    },
                })
                .ToList();

            _logger.LogDebug("传递 {Count} 个工具定义到 Ollama", tools.Count);
        }

        // 构建 format（Ollama 原生支持 json 格式）
        string? format = null;
        if (options.ResponseFormat is { } responseFormat)
        {
            format = responseFormat.Type switch
            {
                ResponseFormatType.JsonObject => "json",
                ResponseFormatType.JsonSchema => "json",
                _ => null,
            };

            if (format != null)
            {
                _logger.LogDebug("Ollama 响应格式设置为: {Format}", format);
            }
        }

        return new OllamaChatRequest
        {
            Model = _model,
            Messages = ollamaMessages,
            Stream = stream,
            Tools = tools,
            Format = format,
            Options = new OllamaOptions
            {
                Temperature = options.Temperature,
                NumPredict = options.MaxTokens,
            },
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

        [JsonPropertyName("tools")]
        public List<OllamaToolDefinition>? Tools { get; init; }

        [JsonPropertyName("format")]
        public string? Format { get; init; }

        [JsonPropertyName("options")]
        public OllamaOptions? Options { get; init; }
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; init; }

        [JsonPropertyName("content")]
        public string? Content { get; init; }

        [JsonPropertyName("tool_calls")]
        public List<OllamaToolCall>? ToolCalls { get; set; }
    }

    private sealed class OllamaToolCall
    {
        [JsonPropertyName("function")]
        public OllamaFunctionCall? Function { get; init; }
    }

    private sealed class OllamaFunctionCall
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("arguments")]
        public JsonObject? Arguments { get; init; }
    }

    private sealed class OllamaToolDefinition
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "function";

        [JsonPropertyName("function")]
        public required OllamaFunctionDefinition Function { get; init; }
    }

    private sealed class OllamaFunctionDefinition
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("description")]
        public required string Description { get; init; }

        [JsonPropertyName("parameters")]
        public JsonObject? Parameters { get; init; }
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
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
    }
}
