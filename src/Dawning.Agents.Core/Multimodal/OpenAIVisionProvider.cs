using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dawning.Agents.Abstractions.Multimodal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Multimodal;

/// <summary>
/// OpenAI Vision 提供者
/// </summary>
public sealed class OpenAIVisionProvider : IVisionProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _defaultModel;
    private readonly ILogger<OpenAIVisionProvider> _logger;

    private static readonly string[] s_supportedFormats =
    [
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/webp",
    ];

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public OpenAIVisionProvider(
        HttpClient httpClient,
        string apiKey,
        string baseUrl = "https://api.openai.com/v1",
        string defaultModel = "gpt-4o",
        ILogger<OpenAIVisionProvider>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        _httpClient = httpClient;
        _apiKey = apiKey;
        _baseUrl = baseUrl.TrimEnd('/');
        _defaultModel = defaultModel;
        _logger = logger ?? NullLogger<OpenAIVisionProvider>.Instance;
    }

    /// <inheritdoc />
    public bool SupportsVision => true;

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedImageFormats => s_supportedFormats;

    /// <inheritdoc />
    public long MaxImageSize => 20 * 1024 * 1024; // 20 MB

    /// <inheritdoc />
    public async Task<VisionAnalysisResult> AnalyzeImageAsync(
        ImageContent image,
        string prompt,
        VisionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var messages = new List<MultimodalMessage>
        {
            MultimodalMessage.User(TextContent.Create(prompt), image),
        };

        var response = await ChatWithVisionAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);

        if (response.Success)
        {
            return new VisionAnalysisResult
            {
                Success = true,
                Description = response.Content,
                TokenUsage = new TokenUsage
                {
                    PromptTokens = response.Usage?.PromptTokens ?? 0,
                    CompletionTokens = response.Usage?.CompletionTokens ?? 0,
                },
            };
        }

        return VisionAnalysisResult.Fail(response.Error ?? "分析失败");
    }

    /// <inheritdoc />
    public async Task<VisionChatResponse> ChatWithVisionAsync(
        IReadOnlyList<MultimodalMessage> messages,
        VisionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= new VisionOptions();
        var model = options.Model ?? _defaultModel;

        _logger.LogDebug(
            "开始视觉聊天，模型: {Model}，消息数: {MessageCount}",
            model,
            messages.Count
        );

        var requestMessages = new List<object>();

        // 添加系统消息
        if (!string.IsNullOrEmpty(options.SystemPrompt))
        {
            requestMessages.Add(new { role = "system", content = options.SystemPrompt });
        }

        // 转换多模态消息
        foreach (var message in messages)
        {
            var content = ConvertContent(message.Content, options.Detail);
            requestMessages.Add(new { role = message.Role, content });
        }

        var requestBody = new
        {
            model,
            messages = requestMessages,
            max_tokens = options.MaxTokens,
            temperature = options.Temperature,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = JsonContent.Create(requestBody, options: s_jsonOptions);

        try
        {
            using var response = await _httpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);
            var responseJson = await response
                .Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OpenAI API 返回错误: {StatusCode} - {Response}",
                    response.StatusCode,
                    responseJson
                );
                return VisionChatResponse.Failed($"API 错误: {response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            var content = root.GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            var usage = root.GetProperty("usage");
            var promptTokens = usage.GetProperty("prompt_tokens").GetInt32();
            var completionTokens = usage.GetProperty("completion_tokens").GetInt32();

            return new VisionChatResponse
            {
                Success = true,
                Content = content ?? "",
                Usage = new TokenUsage
                {
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                },
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "视觉聊天请求失败");
            return VisionChatResponse.Failed(ex.Message);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ChatWithVisionStreamAsync(
        IReadOnlyList<MultimodalMessage> messages,
        VisionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        options ??= new VisionOptions();
        var model = options.Model ?? _defaultModel;

        var requestMessages = new List<object>();

        if (!string.IsNullOrEmpty(options.SystemPrompt))
        {
            requestMessages.Add(new { role = "system", content = options.SystemPrompt });
        }

        foreach (var message in messages)
        {
            var content = ConvertContent(message.Content, options.Detail);
            requestMessages.Add(new { role = message.Role, content });
        }

        var requestBody = new
        {
            model,
            messages = requestMessages,
            max_tokens = options.MaxTokens,
            temperature = options.Temperature,
            stream = true,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = JsonContent.Create(requestBody, options: s_jsonOptions);

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response
                .Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            throw new InvalidOperationException($"API 错误: {response.StatusCode} - {error}");
        }

        using var stream = await response
            .Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        string? line;
        while (
            (line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null
            && !cancellationToken.IsCancellationRequested
        )
        {
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line["data: ".Length..];
            if (data == "[DONE]")
            {
                break;
            }

            using var doc = JsonDocument.Parse(data);
            var delta = doc.RootElement.GetProperty("choices")[0].GetProperty("delta");

            if (delta.TryGetProperty("content", out var contentElement))
            {
                var content = contentElement.GetString();
                if (!string.IsNullOrEmpty(content))
                {
                    yield return content;
                }
            }
        }
    }

    private static object ConvertContent(List<ContentItem> content, ImageDetail defaultDetail)
    {
        if (content.Count == 1 && content[0] is TextContent textOnly)
        {
            return textOnly.Text;
        }

        var parts = new List<object>();
        foreach (var item in content)
        {
            switch (item)
            {
                case TextContent text:
                    parts.Add(new { type = "text", text = text.Text });
                    break;

                case ImageContent image:
                    if (!string.IsNullOrEmpty(image.Url))
                    {
                        parts.Add(
                            new
                            {
                                type = "image_url",
                                image_url = new
                                {
                                    url = image.Url,
                                    detail = (
                                        image.Detail == ImageDetail.Auto
                                            ? defaultDetail
                                            : image.Detail
                                    )
                                        .ToString()
                                        .ToLowerInvariant(),
                                },
                            }
                        );
                    }
                    else if (!string.IsNullOrEmpty(image.Base64Data))
                    {
                        var dataUrl = $"data:{image.MimeType};base64,{image.Base64Data}";
                        parts.Add(
                            new
                            {
                                type = "image_url",
                                image_url = new
                                {
                                    url = dataUrl,
                                    detail = (
                                        image.Detail == ImageDetail.Auto
                                            ? defaultDetail
                                            : image.Detail
                                    )
                                        .ToString()
                                        .ToLowerInvariant(),
                                },
                            }
                        );
                    }
                    break;
            }
        }

        return parts;
    }
}
