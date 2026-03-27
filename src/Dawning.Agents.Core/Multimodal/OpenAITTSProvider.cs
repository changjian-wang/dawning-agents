using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dawning.Agents.Abstractions.Multimodal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Multimodal;

/// <summary>
/// OpenAI TTS text-to-speech provider.
/// </summary>
/// <remarks>
/// Uses the OpenAI TTS API to convert text to speech.
/// Supports tts-1 and tts-1-hd models.
/// </remarks>
public sealed class OpenAITTSProvider : ITextToSpeechProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _defaultModel;
    private readonly ILogger<OpenAITTSProvider> _logger;

    private static readonly VoiceInfo[] s_availableVoices =
    [
        new VoiceInfo
        {
            Id = "alloy",
            Name = "Alloy",
            Gender = VoiceGender.Neutral,
            Languages = ["en", "zh", "ja", "ko", "de", "fr", "es", "it", "pt", "ru"],
            Description = "Neutral, balanced voice",
        },
        new VoiceInfo
        {
            Id = "echo",
            Name = "Echo",
            Gender = VoiceGender.Male,
            Languages = ["en", "zh", "ja", "ko", "de", "fr", "es", "it", "pt", "ru"],
            Description = "Deep, powerful male voice",
        },
        new VoiceInfo
        {
            Id = "fable",
            Name = "Fable",
            Gender = VoiceGender.Neutral,
            Languages = ["en", "zh", "ja", "ko", "de", "fr", "es", "it", "pt", "ru"],
            Description = "Warm, narrative voice",
        },
        new VoiceInfo
        {
            Id = "onyx",
            Name = "Onyx",
            Gender = VoiceGender.Male,
            Languages = ["en", "zh", "ja", "ko", "de", "fr", "es", "it", "pt", "ru"],
            Description = "Deep, authoritative male voice",
        },
        new VoiceInfo
        {
            Id = "nova",
            Name = "Nova",
            Gender = VoiceGender.Female,
            Languages = ["en", "zh", "ja", "ko", "de", "fr", "es", "it", "pt", "ru"],
            Description = "Friendly, lively female voice",
        },
        new VoiceInfo
        {
            Id = "shimmer",
            Name = "Shimmer",
            Gender = VoiceGender.Female,
            Languages = ["en", "zh", "ja", "ko", "de", "fr", "es", "it", "pt", "ru"],
            Description = "Clear, professional female voice",
        },
    ];

    private static readonly string[] s_supportedFormats =
    [
        "mp3",
        "opus",
        "aac",
        "flac",
        "wav",
        "pcm",
    ];

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <inheritdoc />
    public string Name => "OpenAI-TTS";

    /// <inheritdoc />
    public IReadOnlyList<VoiceInfo> AvailableVoices => s_availableVoices;

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedOutputFormats => s_supportedFormats;

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
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

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
            return SpeechResult.Failed("Text content is empty");
        }

        if (text.Length > MaxTextLength)
        {
            return SpeechResult.Failed($"Text too long: {text.Length} characters (max {MaxTextLength} characters)");
        }

        options ??= new SpeechOptions();
        var model = options.Model ?? _defaultModel;
        var voice = options.Voice;
        var format = GetFormatString(options.OutputFormat);

        _logger.LogDebug(
            "Starting speech synthesis, model: {Model}, voice: {Voice}, format: {Format}, text length: {Length}",
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
        request.Content = JsonContent.Create(requestBody, options: s_jsonOptions);

        try
        {
            using var response = await _httpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response
                    .Content.ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);
                _logger.LogWarning(
                    "TTS API returned error: {StatusCode} - {Response}",
                    response.StatusCode,
                    errorText
                );
                return SpeechResult.Failed($"API error: {response.StatusCode} - {errorText}");
            }

            var audioData = await response
                .Content.ReadAsByteArrayAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.LogDebug("Speech synthesis succeeded, audio size: {Size} bytes", audioData.Length);

            var mimeType = GetMimeType(options.OutputFormat);

            return new SpeechResult
            {
                Success = true,
                AudioData = audioData,
                Audio = AudioContent.FromBase64(Convert.ToBase64String(audioData), mimeType),
                CharacterCount = text.Length,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Speech synthesis request failed");
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
        var result = await SynthesizeAsync(text, options, cancellationToken).ConfigureAwait(false);

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

            await File.WriteAllBytesAsync(outputPath, result.AudioData, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogDebug("Audio saved to: {OutputPath}", outputPath);

            return new SpeechResult
            {
                Success = true,
                AudioData = result.AudioData,
                Audio = result.Audio,
                OutputPath = outputPath,
                CharacterCount = text.Length,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save audio file: {OutputPath}", outputPath);
            return SpeechResult.Failed($"Failed to save file: {ex.Message}");
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
        request.Content = JsonContent.Create(requestBody, options: s_jsonOptions);

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response
                .Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            throw new InvalidOperationException($"API error: {response.StatusCode} - {error}");
        }

        await using var stream = await response
            .Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        var buffer = new byte[8192];
        int bytesRead;

        while (
            (bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false))
                > 0
            && !cancellationToken.IsCancellationRequested
        )
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
