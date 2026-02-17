using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.Multimodal;

/// <summary>
/// 音频转录提供者接口 (Speech-to-Text)
/// </summary>
/// <remarks>
/// 支持将音频文件转录为文本，类似 OpenAI Whisper。
/// </remarks>
public interface IAudioTranscriptionProvider
{
    /// <summary>
    /// 提供者名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 支持的音频格式
    /// </summary>
    IReadOnlyList<string> SupportedFormats { get; }

    /// <summary>
    /// 最大音频文件大小（字节）
    /// </summary>
    long MaxFileSize { get; }

    /// <summary>
    /// 最大音频时长（秒）
    /// </summary>
    int MaxDurationSeconds { get; }

    /// <summary>
    /// 转录音频内容
    /// </summary>
    Task<TranscriptionResult> TranscribeAsync(
        AudioContent audio,
        TranscriptionOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 转录音频文件
    /// </summary>
    Task<TranscriptionResult> TranscribeFileAsync(
        string filePath,
        TranscriptionOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 转录音频流
    /// </summary>
    Task<TranscriptionResult> TranscribeStreamAsync(
        Stream audioStream,
        string fileName,
        TranscriptionOptions? options = null,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// 文本转语音提供者接口 (Text-to-Speech)
/// </summary>
public interface ITextToSpeechProvider
{
    /// <summary>
    /// 提供者名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 可用的声音列表
    /// </summary>
    IReadOnlyList<VoiceInfo> AvailableVoices { get; }

    /// <summary>
    /// 支持的输出格式
    /// </summary>
    IReadOnlyList<string> SupportedOutputFormats { get; }

    /// <summary>
    /// 最大文本长度
    /// </summary>
    int MaxTextLength { get; }

    /// <summary>
    /// 将文本转换为语音
    /// </summary>
    Task<SpeechResult> SynthesizeAsync(
        string text,
        SpeechOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 将文本转换为语音并保存到文件
    /// </summary>
    Task<SpeechResult> SynthesizeToFileAsync(
        string text,
        string outputPath,
        SpeechOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 流式文本转语音
    /// </summary>
    IAsyncEnumerable<byte[]> SynthesizeStreamAsync(
        string text,
        SpeechOptions? options = null,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// 转录配置选项
/// </summary>
public class TranscriptionOptions : IValidatableOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Audio:Transcription";

    /// <summary>
    /// 模型名称
    /// </summary>
    /// <remarks>
    /// OpenAI: whisper-1
    /// Azure: whisper
    /// </remarks>
    public string? Model { get; set; }

    /// <summary>
    /// 源语言（ISO 639-1 代码）
    /// </summary>
    /// <remarks>
    /// 设置源语言可以提高准确性和速度。
    /// 例如："zh"（中文）,"en"（英文）,"ja"（日文）
    /// </remarks>
    public string? Language { get; set; }

    /// <summary>
    /// 提示词（帮助模型理解上下文）
    /// </summary>
    public string? Prompt { get; set; }

    /// <summary>
    /// 响应格式
    /// </summary>
    public TranscriptionFormat ResponseFormat { get; set; } = TranscriptionFormat.Text;

    /// <summary>
    /// 温度参数（0-1，较低值更确定）
    /// </summary>
    public double Temperature { get; set; } = 0;

    /// <summary>
    /// 是否包含时间戳
    /// </summary>
    public bool IncludeTimestamps { get; set; }

    /// <summary>
    /// 时间戳粒度
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
/// 转录响应格式
/// </summary>
public enum TranscriptionFormat
{
    /// <summary>
    /// 纯文本
    /// </summary>
    Text,

    /// <summary>
    /// JSON 格式
    /// </summary>
    Json,

    /// <summary>
    /// 详细 JSON（包含时间戳等）
    /// </summary>
    VerboseJson,

    /// <summary>
    /// SRT 字幕格式
    /// </summary>
    Srt,

    /// <summary>
    /// VTT 字幕格式
    /// </summary>
    Vtt,
}

/// <summary>
/// 时间戳粒度
/// </summary>
public enum TimestampGranularity
{
    /// <summary>
    /// 按段落
    /// </summary>
    Segment,

    /// <summary>
    /// 按单词
    /// </summary>
    Word,
}

/// <summary>
/// 转录结果
/// </summary>
public record TranscriptionResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// 转录文本
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// 检测到的语言
    /// </summary>
    public string? DetectedLanguage { get; init; }

    /// <summary>
    /// 音频时长（秒）
    /// </summary>
    public double? DurationSeconds { get; init; }

    /// <summary>
    /// 分段信息（带时间戳）
    /// </summary>
    public IReadOnlyList<TranscriptionSegment>? Segments { get; init; }

    /// <summary>
    /// 单词信息（带时间戳）
    /// </summary>
    public IReadOnlyList<TranscriptionWord>? Words { get; init; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 创建成功结果
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
    /// 创建失败结果
    /// </summary>
    public static TranscriptionResult Failed(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>
/// 转录分段
/// </summary>
public record TranscriptionSegment
{
    /// <summary>
    /// 分段 ID
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// 开始时间（秒）
    /// </summary>
    public double Start { get; init; }

    /// <summary>
    /// 结束时间（秒）
    /// </summary>
    public double End { get; init; }

    /// <summary>
    /// 分段文本
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// 置信度（0-1）
    /// </summary>
    public double? Confidence { get; init; }
}

/// <summary>
/// 转录单词
/// </summary>
public record TranscriptionWord
{
    /// <summary>
    /// 单词
    /// </summary>
    public required string Word { get; init; }

    /// <summary>
    /// 开始时间（秒）
    /// </summary>
    public double Start { get; init; }

    /// <summary>
    /// 结束时间（秒）
    /// </summary>
    public double End { get; init; }

    /// <summary>
    /// 置信度（0-1）
    /// </summary>
    public double? Confidence { get; init; }
}

/// <summary>
/// 语音合成配置选项
/// </summary>
public class SpeechOptions : IValidatableOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Audio:Speech";

    /// <summary>
    /// 模型名称
    /// </summary>
    /// <remarks>
    /// OpenAI: tts-1, tts-1-hd
    /// Azure: neural voices
    /// </remarks>
    public string? Model { get; set; }

    /// <summary>
    /// 声音名称
    /// </summary>
    /// <remarks>
    /// OpenAI: alloy, echo, fable, onyx, nova, shimmer
    /// Azure: zh-CN-XiaoxiaoNeural, en-US-JennyNeural 等
    /// </remarks>
    public string Voice { get; set; } = "alloy";

    /// <summary>
    /// 输出格式
    /// </summary>
    public SpeechOutputFormat OutputFormat { get; set; } = SpeechOutputFormat.Mp3;

    /// <summary>
    /// 语速（0.25-4.0，1.0 为正常速度）
    /// </summary>
    public double Speed { get; set; } = 1.0;

    /// <summary>
    /// 音调调整（仅部分提供者支持）
    /// </summary>
    public double? Pitch { get; set; }

    /// <summary>
    /// 音量调整（仅部分提供者支持）
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
/// 语音输出格式
/// </summary>
public enum SpeechOutputFormat
{
    /// <summary>
    /// MP3 格式
    /// </summary>
    Mp3,

    /// <summary>
    /// Opus 格式（低延迟）
    /// </summary>
    Opus,

    /// <summary>
    /// AAC 格式
    /// </summary>
    Aac,

    /// <summary>
    /// FLAC 格式（无损）
    /// </summary>
    Flac,

    /// <summary>
    /// WAV 格式
    /// </summary>
    Wav,

    /// <summary>
    /// PCM 格式
    /// </summary>
    Pcm,
}

/// <summary>
/// 语音信息
/// </summary>
public record VoiceInfo
{
    /// <summary>
    /// 声音 ID
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 声音名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 支持的语言
    /// </summary>
    public IReadOnlyList<string> Languages { get; init; } = [];

    /// <summary>
    /// 性别
    /// </summary>
    public VoiceGender Gender { get; init; } = VoiceGender.Neutral;

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 预览 URL
    /// </summary>
    public string? PreviewUrl { get; init; }
}

/// <summary>
/// 声音性别
/// </summary>
public enum VoiceGender
{
    /// <summary>
    /// 中性
    /// </summary>
    Neutral,

    /// <summary>
    /// 男性
    /// </summary>
    Male,

    /// <summary>
    /// 女性
    /// </summary>
    Female,
}

/// <summary>
/// 语音合成结果
/// </summary>
public record SpeechResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// 音频数据
    /// </summary>
    public byte[]? AudioData { get; init; }

    /// <summary>
    /// 音频内容（包装为 AudioContent）
    /// </summary>
    public AudioContent? Audio { get; init; }

    /// <summary>
    /// 输出文件路径（如果保存到文件）
    /// </summary>
    public string? OutputPath { get; init; }

    /// <summary>
    /// 音频时长（秒）
    /// </summary>
    public double? DurationSeconds { get; init; }

    /// <summary>
    /// 字符数
    /// </summary>
    public int? CharacterCount { get; init; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 创建成功结果
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
    /// 创建文件保存成功结果
    /// </summary>
    public static SpeechResult OkFile(string outputPath, double? duration = null) =>
        new()
        {
            Success = true,
            OutputPath = outputPath,
            DurationSeconds = duration,
        };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static SpeechResult Failed(string error) => new() { Success = false, Error = error };
}
