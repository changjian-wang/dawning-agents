using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.Multimodal;

/// <summary>
/// Audio transcription provider interface (Speech-to-Text).
/// </summary>
/// <remarks>
/// Supports transcribing audio files to text, similar to OpenAI Whisper.
/// </remarks>
public interface IAudioTranscriptionProvider
{
    /// <summary>
    /// Provider name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Supported audio formats.
    /// </summary>
    IReadOnlyList<string> SupportedFormats { get; }

    /// <summary>
    /// Maximum audio file size (bytes).
    /// </summary>
    long MaxFileSize { get; }

    /// <summary>
    /// Maximum audio duration (seconds).
    /// </summary>
    int MaxDurationSeconds { get; }

    /// <summary>
    /// Transcribes audio content.
    /// </summary>
    Task<TranscriptionResult> TranscribeAsync(
        AudioContent audio,
        TranscriptionOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Transcribes an audio file.
    /// </summary>
    Task<TranscriptionResult> TranscribeFileAsync(
        string filePath,
        TranscriptionOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Transcribes an audio stream.
    /// </summary>
    Task<TranscriptionResult> TranscribeStreamAsync(
        Stream audioStream,
        string fileName,
        TranscriptionOptions? options = null,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Text-to-Speech provider interface.
/// </summary>
public interface ITextToSpeechProvider
{
    /// <summary>
    /// Provider name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Available voices list.
    /// </summary>
    IReadOnlyList<VoiceInfo> AvailableVoices { get; }

    /// <summary>
    /// Supported output formats.
    /// </summary>
    IReadOnlyList<string> SupportedOutputFormats { get; }

    /// <summary>
    /// Maximum text length.
    /// </summary>
    int MaxTextLength { get; }

    /// <summary>
    /// Converts text to speech.
    /// </summary>
    Task<SpeechResult> SynthesizeAsync(
        string text,
        SpeechOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Converts text to speech and saves to a file.
    /// </summary>
    Task<SpeechResult> SynthesizeToFileAsync(
        string text,
        string outputPath,
        SpeechOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Streaming text-to-speech.
    /// </summary>
    IAsyncEnumerable<byte[]> SynthesizeStreamAsync(
        string text,
        SpeechOptions? options = null,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Transcription configuration options.
/// </summary>
public class TranscriptionOptions : IValidatableOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Audio:Transcription";

    /// <summary>
    /// Model name.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Source language (ISO 639-1 code).
    /// </summary>
    /// <remarks>
    /// Setting the source language can improve accuracy and speed.
    /// Examples: "zh" (Chinese), "en" (English), "ja" (Japanese).
    /// </remarks>
    public string? Language { get; set; }

    /// <summary>
    /// Prompt (helps the model understand context).
    /// </summary>
    public string? Prompt { get; set; }

    /// <summary>
    /// Response format.
    /// </summary>
    public TranscriptionFormat ResponseFormat { get; set; } = TranscriptionFormat.Text;

    /// <summary>
    /// Temperature parameter (0–1, lower values are more deterministic).
    /// </summary>
    public double Temperature { get; set; } = 0;

    /// <summary>
    /// Whether to include timestamps.
    /// </summary>
    public bool IncludeTimestamps { get; set; }

    /// <summary>
    /// Timestamp granularity.
    /// </summary>
    public TimestampGranularity TimestampGranularity { get; set; } = TimestampGranularity.Segment;

    /// <inheritdoc />
    public void Validate()
    {
        if (Temperature is < 0 or > 1)
        {
            throw new InvalidOperationException(
                "Transcription Temperature must be between 0 and 1"
            );
        }
    }
}

/// <summary>
/// Transcription response format.
/// </summary>
public enum TranscriptionFormat
{
    /// <summary>
    /// Plain text.
    /// </summary>
    Text,

    /// <summary>
    /// JSON format.
    /// </summary>
    Json,

    /// <summary>
    /// Verbose JSON (includes timestamps, etc.).
    /// </summary>
    VerboseJson,

    /// <summary>
    /// SRT subtitle format.
    /// </summary>
    Srt,

    /// <summary>
    /// VTT subtitle format.
    /// </summary>
    Vtt,
}

/// <summary>
/// Timestamp granularity.
/// </summary>
public enum TimestampGranularity
{
    /// <summary>
    /// By segment.
    /// </summary>
    Segment,

    /// <summary>
    /// By word.
    /// </summary>
    Word,
}

/// <summary>
/// Transcription result.
/// </summary>
public record TranscriptionResult
{
    /// <summary>
    /// Whether the transcription succeeded.
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// Transcribed text.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Detected language.
    /// </summary>
    public string? DetectedLanguage { get; init; }

    /// <summary>
    /// Audio duration (seconds).
    /// </summary>
    public double? DurationSeconds { get; init; }

    /// <summary>
    /// Segment information (with timestamps).
    /// </summary>
    public IReadOnlyList<TranscriptionSegment>? Segments { get; init; }

    /// <summary>
    /// Word information (with timestamps).
    /// </summary>
    public IReadOnlyList<TranscriptionWord>? Words { get; init; }

    /// <summary>
    /// Error message.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static TranscriptionResult Ok(
        string text,
        string? language = null,
        double? duration = null
    ) =>
        new()
        {
            Success = true,
            Text = text,
            DetectedLanguage = language,
            DurationSeconds = duration,
        };

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static TranscriptionResult Failed(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>
/// Transcription segment.
/// </summary>
public record TranscriptionSegment
{
    /// <summary>
    /// Segment ID.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Start time (seconds).
    /// </summary>
    public double Start { get; init; }

    /// <summary>
    /// End time (seconds).
    /// </summary>
    public double End { get; init; }

    /// <summary>
    /// Segment text.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Confidence (0–1).
    /// </summary>
    public double? Confidence { get; init; }
}

/// <summary>
/// Transcription word.
/// </summary>
public record TranscriptionWord
{
    /// <summary>
    /// Word.
    /// </summary>
    public required string Word { get; init; }

    /// <summary>
    /// Start time (seconds).
    /// </summary>
    public double Start { get; init; }

    /// <summary>
    /// End time (seconds).
    /// </summary>
    public double End { get; init; }

    /// <summary>
    /// Confidence (0–1).
    /// </summary>
    public double? Confidence { get; init; }
}

/// <summary>
/// Speech synthesis configuration options.
/// </summary>
public class SpeechOptions : IValidatableOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Audio:Speech";

    /// <summary>
    /// Model name.
    /// </summary>
    /// <remarks>
    /// OpenAI: tts-1, tts-1-hd
    /// Azure: neural voices
    /// </remarks>
    public string? Model { get; set; }

    /// <summary>
    /// Voice name.
    /// </summary>
    /// <remarks>
    /// OpenAI: alloy, echo, fable, onyx, nova, shimmer
    /// Azure: zh-CN-XiaoxiaoNeural, en-US-JennyNeural, etc.
    /// </remarks>
    public string Voice { get; set; } = "alloy";

    /// <summary>
    /// Output format.
    /// </summary>
    public SpeechOutputFormat OutputFormat { get; set; } = SpeechOutputFormat.Mp3;

    /// <summary>
    /// Speech speed (0.25–4.0, 1.0 is normal speed).
    /// </summary>
    public double Speed { get; set; } = 1.0;

    /// <summary>
    /// Pitch adjustment (only supported by some providers).
    /// </summary>
    public double? Pitch { get; set; }

    /// <summary>
    /// Volume adjustment (only supported by some providers).
    /// </summary>
    public double? Volume { get; set; }

    /// <inheritdoc />
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Voice))
        {
            throw new InvalidOperationException("Speech Voice is required");
        }

        if (Speed is < 0.25 or > 4.0)
        {
            throw new InvalidOperationException("Speech Speed must be between 0.25 and 4.0");
        }
    }
}

/// <summary>
/// Speech output format.
/// </summary>
public enum SpeechOutputFormat
{
    /// <summary>
    /// MP3 format.
    /// </summary>
    Mp3,

    /// <summary>
    /// Opus format (low latency).
    /// </summary>
    Opus,

    /// <summary>
    /// AAC format.
    /// </summary>
    Aac,

    /// <summary>
    /// FLAC format (lossless).
    /// </summary>
    Flac,

    /// <summary>
    /// WAV format.
    /// </summary>
    Wav,

    /// <summary>
    /// PCM format.
    /// </summary>
    Pcm,
}

/// <summary>
/// Voice information.
/// </summary>
public record VoiceInfo
{
    /// <summary>
    /// Voice ID.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Voice name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Supported languages.
    /// </summary>
    public IReadOnlyList<string> Languages { get; init; } = [];

    /// <summary>
    /// Gender.
    /// </summary>
    public VoiceGender Gender { get; init; } = VoiceGender.Neutral;

    /// <summary>
    /// Description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Preview URL.
    /// </summary>
    public string? PreviewUrl { get; init; }
}

/// <summary>
/// Voice gender.
/// </summary>
public enum VoiceGender
{
    /// <summary>
    /// Neutral.
    /// </summary>
    Neutral,

    /// <summary>
    /// Male.
    /// </summary>
    Male,

    /// <summary>
    /// Female.
    /// </summary>
    Female,
}

/// <summary>
/// Speech synthesis result.
/// </summary>
public record SpeechResult
{
    /// <summary>
    /// Whether the synthesis succeeded.
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// Audio data.
    /// </summary>
    public byte[]? AudioData { get; init; }

    /// <summary>
    /// Audio content (wrapped as AudioContent).
    /// </summary>
    public AudioContent? Audio { get; init; }

    /// <summary>
    /// Output file path (if saved to file).
    /// </summary>
    public string? OutputPath { get; init; }

    /// <summary>
    /// Audio duration (seconds).
    /// </summary>
    public double? DurationSeconds { get; init; }

    /// <summary>
    /// Character count.
    /// </summary>
    public int? CharacterCount { get; init; }

    /// <summary>
    /// Error message.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static SpeechResult Ok(byte[] audioData, double? duration = null) =>
        new()
        {
            Success = true,
            AudioData = audioData,
            DurationSeconds = duration,
            CharacterCount = null,
        };

    /// <summary>
    /// Creates a successful file-save result.
    /// </summary>
    public static SpeechResult OkFile(string outputPath, double? duration = null) =>
        new()
        {
            Success = true,
            OutputPath = outputPath,
            DurationSeconds = duration,
        };

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static SpeechResult Failed(string error) => new() { Success = false, Error = error };
}
