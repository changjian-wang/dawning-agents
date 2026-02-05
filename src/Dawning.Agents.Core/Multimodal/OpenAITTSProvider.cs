using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dawning.Agents.Abstractions.Multimodal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Multimodal;

/// <summary>
/// OpenAI TTS 文本转语音提供者
/// </summary>
/// <remarks>
/// 使用 OpenAI TTS API 将文本转换为语音。
/// 支持 tts-1 和 tts-1-hd 模型。
/// </remarks>
public class OpenAITTSProvider : ITextToSpeechProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _defaultModel;
    private readonly ILogger<OpenAITTSProvider> _logger;

    private static readonly VoiceInfo[] _availableVoices =
    [
        new VoiceInfo
        {
            Id = "alloy",
            Name = "Alloy",
            Gender = VoiceGender.Neutral,
            Languages = ["en", "zh", "ja", "ko", "de", "fr", "es", "it", "pt", "ru"],
            Description = "中性、平衡的声音",
        },
        new VoiceInfo
        {
            Id = "echo",
            Name = "Echo",
            Gender = VoiceGender.Male,
            Languages = ["en", "zh", "ja", "ko", "de", "fr", "es", "it", "pt", "ru"],
            Description = "深沉、有力的男声",
        },
        new VoiceInfo
        {
            Id = "fable",
            Name = "Fable",
            Gender = VoiceGender.Neutral,
            Languages = ["en", "zh", "ja", "ko", "de", "fr", "es", "it", "pt", "ru"],
            Description = "温暖、叙述性的声音",
        },
        new VoiceInfo
        {
            Id = "onyx",
            Name = "Onyx",
            Gender = VoiceGender.Male,
            Languages = ["en", "zh", "ja", "ko", "de", "fr", "es", "it", "pt", "ru"],
            Description = "深沉、权威的男声",
        },
        new VoiceInfo
        {
            Id = "nova",
            Name = "Nova",
            Gender = VoiceGender.Female,
            Languages = ["en", "zh", "ja", "ko", "de", "fr", "es", "it", "pt", "ru"],
            Description = "友好、活泼的女声",
        },
        new VoiceInfo
        {
            Id = "shimmer",
            Name = "Shimmer",
            Gender = VoiceGender.Female,
            Languages = ["en", "zh", "ja", "ko", "de", "fr", "es", "it", "pt", "ru"],
            Description = "清晰、专业的女声",
        },
    ];

    private static readonly string[] _supportedFormats = ["mp3", "opus", "aac", "flac", "wav", "pcm"];

    private static readonly JsonSerializerOptions _jsonOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

    /// <inheritdoc />
    public string Name => "OpenAI-TTS";

    /// <inheritdoc />
    public IReadOnlyList<VoiceInfo> AvailableVoices => _availableVoices;

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedOutputFormats => _supportedFormats;

    /// <inheritdoc />
    public int MaxTextLength => 4096;

    public OpenAITTSProvider(
        HttpClient httpClient,
        string apiKey,
        string baseUrl = "https://api.openai.com/v1",
        string defaultModel = "tts-1",
        ILogger<OpenAITTSProvider>? logger = null
    )
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _baseUrl = baseUrl.TrimEnd('/');
        _defaultModel = defaultModel;
        _logger = logger ?? NullLogger<OpenAITTSProvider>.Instance;
    }

    /// <inheritdoc />
    public async Task<SpeechResult> SynthesizeAsync(
        string text,
        SpeechOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(text))
        {
            return SpeechResult.Failed("文本内容为空");
        }

        if (text.Length > MaxTextLength)
        {
            return SpeechResult.Failed($"文本过长: {text.Length} 字符 (最大 {MaxTextLength} 字符)");
        }

        options ??= new SpeechOptions();
        var model = options.Model ?? _defaultModel;
        var voice = options.Voice;
        var format = GetFormatString(options.OutputFormat);

        _logger.LogDebug(
            "开始语音合成，模型: {Model}，声音: {Voice}，格式: {Format}，文本长度: {Length}",
            model,
            voice,
            format,
            text.Length
        );

        var requestBody = new
        {
            model,
            input = text,
            voice,
            response_format = format,
            speed = options.Speed,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/audio/speech");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = JsonContent.Create(requestBody, options: _jsonOptions);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "TTS API 返回错误: {StatusCode} - {Response}",
                    response.StatusCode,
                    errorText
                );
                return SpeechResult.Failed($"API 错误: {response.StatusCode} - {errorText}");
            }

            var audioData = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            _logger.LogDebug("语音合成成功，音频大小: {Size} 字节", audioData.Length);

            var mimeType = GetMimeType(options.OutputFormat);

            return new SpeechResult
            {
                Success = true,
                AudioData = audioData,
                Audio = AudioContent.FromBase64(Convert.ToBase64String(audioData), mimeType),
                CharacterCount = text.Length,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "语音合成请求失败");
            return SpeechResult.Failed(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<SpeechResult> SynthesizeToFileAsync(
        string text,
        string outputPath,
        SpeechOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var result = await SynthesizeAsync(text, options, cancellationToken);

        if (!result.Success || result.AudioData == null)
        {
            return result;
        }

        try
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(outputPath, result.AudioData, cancellationToken);

            _logger.LogDebug("音频已保存到: {OutputPath}", outputPath);

            return new SpeechResult
            {
                Success = true,
                AudioData = result.AudioData,
                Audio = result.Audio,
                OutputPath = outputPath,
                CharacterCount = text.Length,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存音频文件失败: {OutputPath}", outputPath);
            return SpeechResult.Failed($"保存文件失败: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<byte[]> SynthesizeStreamAsync(
        string text,
        SpeechOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        options ??= new SpeechOptions();
        var model = options.Model ?? _defaultModel;
        var voice = options.Voice;
        var format = GetFormatString(options.OutputFormat);

        var requestBody = new
        {
            model,
            input = text,
            voice,
            response_format = format,
            speed = options.Speed,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/audio/speech");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = JsonContent.Create(requestBody, options: _jsonOptions);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"API 错误: {response.StatusCode} - {error}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var buffer = new byte[8192];
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0
            && !cancellationToken.IsCancellationRequested)
        {
            var chunk = new byte[bytesRead];
            Array.Copy(buffer, chunk, bytesRead);
            yield return chunk;
        }
    }

    private static string GetFormatString(SpeechOutputFormat format)
    {
        return format switch
        {
            SpeechOutputFormat.Mp3 => "mp3",
            SpeechOutputFormat.Opus => "opus",
            SpeechOutputFormat.Aac => "aac",
            SpeechOutputFormat.Flac => "flac",
            SpeechOutputFormat.Wav => "wav",
            SpeechOutputFormat.Pcm => "pcm",
            _ => "mp3",
        };
    }

    private static string GetMimeType(SpeechOutputFormat format)
    {
        return format switch
        {
            SpeechOutputFormat.Mp3 => "audio/mpeg",
            SpeechOutputFormat.Opus => "audio/opus",
            SpeechOutputFormat.Aac => "audio/aac",
            SpeechOutputFormat.Flac => "audio/flac",
            SpeechOutputFormat.Wav => "audio/wav",
            SpeechOutputFormat.Pcm => "audio/pcm",
            _ => "audio/mpeg",
        };
    }
}
