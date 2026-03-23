using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dawning.Agents.Abstractions.Multimodal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Multimodal;

/// <summary>
/// OpenAI Whisper 音频转录提供者
/// </summary>
/// <remarks>
/// 使用 OpenAI Whisper API 将音频转录为文本。
/// 支持 mp3, mp4, mpeg, mpga, m4a, wav, webm 格式。
/// </remarks>
public class OpenAIWhisperProvider : IAudioTranscriptionProvider
{
    private readonly HttpClient _httpClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _defaultModel;
    private readonly ILogger<OpenAIWhisperProvider> _logger;

    private static readonly string[] s_supportedFormats =
    [
        "mp3",
        "mp4",
        "mpeg",
        "mpga",
        "m4a",
        "wav",
        "webm",
        "flac",
        "ogg",
    ];

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <inheritdoc />
    public string Name => "OpenAI-Whisper";

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedFormats => s_supportedFormats;

    /// <inheritdoc />
    public long MaxFileSize => 25 * 1024 * 1024; // 25 MB

    /// <inheritdoc />
    public int MaxDurationSeconds => 7200; // 2 hours (with chunking)

    public OpenAIWhisperProvider(
        HttpClient httpClient,
        IHttpClientFactory httpClientFactory,
        string apiKey,
        string baseUrl = "https://api.openai.com/v1",
        string defaultModel = "whisper-1",
        ILogger<OpenAIWhisperProvider>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        _httpClient = httpClient;
        _httpClientFactory = httpClientFactory;
        _apiKey = apiKey;
        _baseUrl = baseUrl.TrimEnd('/');
        _defaultModel = defaultModel;
        _logger = logger ?? NullLogger<OpenAIWhisperProvider>.Instance;
    }

    /// <inheritdoc />
    public async Task<TranscriptionResult> TranscribeAsync(
        AudioContent audio,
        TranscriptionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(audio.Base64Data) && string.IsNullOrEmpty(audio.Url))
        {
            return TranscriptionResult.Failed("音频内容为空");
        }

        byte[] audioData;
        string fileName;

        if (!string.IsNullOrEmpty(audio.Base64Data))
        {
            audioData = Convert.FromBase64String(audio.Base64Data);
            fileName = $"audio.{GetExtensionFromMimeType(audio.MimeType)}";
        }
        else
        {
            // 从 URL 下载（验证 URL 方案防止 SSRF）
            if (
                !Uri.TryCreate(audio.Url, UriKind.Absolute, out var audioUri)
                || (audioUri.Scheme != Uri.UriSchemeHttps && audioUri.Scheme != Uri.UriSchemeHttp)
            )
            {
                return TranscriptionResult.Failed("不支持的音频 URL 方案，仅允许 http/https");
            }

            try
            {
                using var downloadClient = _httpClientFactory.CreateClient("WhisperDownload");
                audioData = await downloadClient
                    .GetByteArrayAsync(audioUri, cancellationToken)
                    .ConfigureAwait(false);
                fileName = Path.GetFileName(new Uri(audio.Url!).LocalPath);
                if (string.IsNullOrEmpty(Path.GetExtension(fileName)))
                {
                    fileName = "audio.mp3";
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从 URL 下载音频失败: {Url}", audio.Url);
                return TranscriptionResult.Failed($"下载音频失败: {ex.Message}");
            }
        }

        using var stream = new MemoryStream(audioData);
        return await TranscribeStreamAsync(stream, fileName, options, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TranscriptionResult> TranscribeFileAsync(
        string filePath,
        TranscriptionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!File.Exists(filePath))
        {
            return TranscriptionResult.Failed($"文件不存在: {filePath}");
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > MaxFileSize)
        {
            return TranscriptionResult.Failed(
                $"文件过大: {fileInfo.Length} 字节 (最大 {MaxFileSize} 字节)"
            );
        }

        var extension = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
        if (!s_supportedFormats.Contains(extension))
        {
            return TranscriptionResult.Failed($"不支持的格式: {extension}");
        }

        _logger.LogDebug("开始转录文件: {FilePath}", filePath);

        await using var stream = File.OpenRead(filePath);
        return await TranscribeStreamAsync(
                stream,
                Path.GetFileName(filePath),
                options,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TranscriptionResult> TranscribeStreamAsync(
        Stream audioStream,
        string fileName,
        TranscriptionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= new TranscriptionOptions();
        var model = options.Model ?? _defaultModel;

        _logger.LogDebug(
            "开始转录，模型: {Model}，文件: {FileName}，语言: {Language}",
            model,
            fileName,
            options.Language ?? "auto"
        );

        using var content = new MultipartFormDataContent();

        // 添加音频文件
        var fileContent = new StreamContent(audioStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            GetMimeTypeFromExtension(Path.GetExtension(fileName))
        );
        content.Add(fileContent, "file", fileName);

        // 添加模型
        content.Add(new StringContent(model), "model");

        // 添加可选参数
        if (!string.IsNullOrEmpty(options.Language))
        {
            content.Add(new StringContent(options.Language), "language");
        }

        if (!string.IsNullOrEmpty(options.Prompt))
        {
            content.Add(new StringContent(options.Prompt), "prompt");
        }

        // 响应格式
        var responseFormat = options.ResponseFormat switch
        {
            TranscriptionFormat.Json => "json",
            TranscriptionFormat.VerboseJson => "verbose_json",
            TranscriptionFormat.Srt => "srt",
            TranscriptionFormat.Vtt => "vtt",
            _ => "text",
        };
        content.Add(new StringContent(responseFormat), "response_format");

        if (options.Temperature > 0)
        {
            content.Add(
                new StringContent(options.Temperature.ToString("F2", CultureInfo.InvariantCulture)),
                "temperature"
            );
        }

        // 时间戳粒度
        if (options.IncludeTimestamps && options.ResponseFormat == TranscriptionFormat.VerboseJson)
        {
            var granularity =
                options.TimestampGranularity == TimestampGranularity.Word ? "word" : "segment";
            content.Add(new StringContent(granularity), "timestamp_granularities[]");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_baseUrl}/audio/transcriptions"
        );
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = content;

        try
        {
            using var response = await _httpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);
            var responseText = await response
                .Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Whisper API 返回错误: {StatusCode} - {Response}",
                    response.StatusCode,
                    responseText
                );
                return TranscriptionResult.Failed(
                    $"API 错误: {response.StatusCode} - {responseText}"
                );
            }

            return ParseTranscriptionResponse(responseText, options.ResponseFormat);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "转录请求失败");
            return TranscriptionResult.Failed(ex.Message);
        }
    }

    private TranscriptionResult ParseTranscriptionResponse(
        string responseText,
        TranscriptionFormat format
    )
    {
        if (
            format == TranscriptionFormat.Text
            || format == TranscriptionFormat.Srt
            || format == TranscriptionFormat.Vtt
        )
        {
            return TranscriptionResult.Ok(responseText);
        }

        try
        {
            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;

            var text = root.GetProperty("text").GetString() ?? "";

            string? language = null;
            if (root.TryGetProperty("language", out var langElement))
            {
                language = langElement.GetString();
            }

            double? duration = null;
            if (root.TryGetProperty("duration", out var durationElement))
            {
                duration = durationElement.GetDouble();
            }

            List<TranscriptionSegment>? segments = null;
            if (root.TryGetProperty("segments", out var segmentsElement))
            {
                segments = [];
                foreach (var seg in segmentsElement.EnumerateArray())
                {
                    segments.Add(
                        new TranscriptionSegment
                        {
                            Id = seg.GetProperty("id").GetInt32(),
                            Start = seg.GetProperty("start").GetDouble(),
                            End = seg.GetProperty("end").GetDouble(),
                            Text = seg.GetProperty("text").GetString() ?? "",
                            Confidence = seg.TryGetProperty("avg_logprob", out var prob)
                                ? Math.Exp(prob.GetDouble()) // Convert log probability to probability
                                : null,
                        }
                    );
                }
            }

            List<TranscriptionWord>? words = null;
            if (root.TryGetProperty("words", out var wordsElement))
            {
                words = [];
                foreach (var word in wordsElement.EnumerateArray())
                {
                    words.Add(
                        new TranscriptionWord
                        {
                            Word = word.GetProperty("word").GetString() ?? "",
                            Start = word.GetProperty("start").GetDouble(),
                            End = word.GetProperty("end").GetDouble(),
                        }
                    );
                }
            }

            return new TranscriptionResult
            {
                Success = true,
                Text = text,
                DetectedLanguage = language,
                DurationSeconds = duration,
                Segments = segments,
                Words = words,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析转录响应失败");
            return TranscriptionResult.Ok(responseText);
        }
    }

    private static string GetExtensionFromMimeType(string mimeType)
    {
        return mimeType.ToLowerInvariant() switch
        {
            "audio/mpeg" or "audio/mp3" => "mp3",
            "audio/mp4" => "mp4",
            "audio/m4a" or "audio/x-m4a" => "m4a",
            "audio/wav" or "audio/x-wav" => "wav",
            "audio/webm" => "webm",
            "audio/flac" => "flac",
            "audio/ogg" => "ogg",
            _ => "mp3",
        };
    }

    private static string GetMimeTypeFromExtension(string extension)
    {
        return extension.TrimStart('.').ToLowerInvariant() switch
        {
            "mp3" => "audio/mpeg",
            "mp4" => "audio/mp4",
            "m4a" => "audio/m4a",
            "wav" => "audio/wav",
            "webm" => "audio/webm",
            "flac" => "audio/flac",
            "ogg" => "audio/ogg",
            "mpeg" or "mpga" => "audio/mpeg",
            _ => "application/octet-stream",
        };
    }
}
