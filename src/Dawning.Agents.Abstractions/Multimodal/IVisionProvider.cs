using Dawning.Agents.Abstractions.LLM;

namespace Dawning.Agents.Abstractions.Multimodal;

/// <summary>
/// Vision processing provider interface.
/// </summary>
public interface IVisionProvider
{
    /// <summary>
    /// Whether vision capabilities are supported.
    /// </summary>
    bool SupportsVision { get; }

    /// <summary>
    /// Supported image formats.
    /// </summary>
    IReadOnlyList<string> SupportedImageFormats { get; }

    /// <summary>
    /// Maximum image size (bytes).
    /// </summary>
    long MaxImageSize { get; }

    /// <summary>
    /// Analyzes an image.
    /// </summary>
    Task<VisionAnalysisResult> AnalyzeImageAsync(
        ImageContent image,
        string prompt,
        VisionOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Multimodal chat.
    /// </summary>
    Task<VisionChatResponse> ChatWithVisionAsync(
        IReadOnlyList<MultimodalMessage> messages,
        VisionOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Multimodal chat (streaming).
    /// </summary>
    IAsyncEnumerable<string> ChatWithVisionStreamAsync(
        IReadOnlyList<MultimodalMessage> messages,
        VisionOptions? options = null,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Vision chat response.
/// </summary>
public record VisionChatResponse
{
    /// <summary>
    /// Whether the request succeeded.
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// Response content.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Error message.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Usage statistics.
    /// </summary>
    public TokenUsage? Usage { get; init; }

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    public static VisionChatResponse Ok(string content, TokenUsage? usage = null) =>
        new()
        {
            Success = true,
            Content = content,
            Usage = usage,
        };

    /// <summary>
    /// Creates a failure response.
    /// </summary>
    public static VisionChatResponse Failed(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>
/// Vision analysis configuration options.
/// </summary>
public class VisionOptions : IValidatableOptions
{
    /// <summary>
    /// Model name (defaults to a vision-capable model).
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Image detail level.
    /// </summary>
    public ImageDetail Detail { get; set; } = ImageDetail.Auto;

    /// <summary>
    /// Maximum output token count.
    /// </summary>
    public int MaxTokens { get; set; } = 1024;

    /// <summary>
    /// Temperature parameter.
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// System prompt.
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <inheritdoc />
    public void Validate()
    {
        if (MaxTokens <= 0)
        {
            throw new InvalidOperationException("VisionOptions.MaxTokens must be greater than 0.");
        }

        if (Temperature < 0.0 || Temperature > 2.0)
        {
            throw new InvalidOperationException(
                "VisionOptions.Temperature must be between 0.0 and 2.0."
            );
        }
    }
}

/// <summary>
/// Vision analysis result.
/// </summary>
public record VisionAnalysisResult
{
    /// <summary>
    /// Whether the analysis succeeded.
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// Analysis description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Detected objects.
    /// </summary>
    public List<DetectedObject>? Objects { get; init; }

    /// <summary>
    /// Detected text (OCR).
    /// </summary>
    public string? DetectedText { get; init; }

    /// <summary>
    /// Tags.
    /// </summary>
    public List<string>? Tags { get; init; }

    /// <summary>
    /// Confidence (0–1).
    /// </summary>
    public double? Confidence { get; init; }

    /// <summary>
    /// Error message.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Token usage statistics.
    /// </summary>
    public TokenUsage? TokenUsage { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static VisionAnalysisResult Ok(string description) =>
        new() { Success = true, Description = description };

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static VisionAnalysisResult Fail(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>
/// Detected object.
/// </summary>
public record DetectedObject
{
    /// <summary>
    /// Object name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Confidence (0–1).
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Bounding box (normalized coordinates).
    /// </summary>
    public BoundingBox? BoundingBox { get; init; }
}

/// <summary>
/// Bounding box.
/// </summary>
public record BoundingBox
{
    /// <summary>
    /// Top-left X (0–1).
    /// </summary>
    public double X { get; init; }

    /// <summary>
    /// Top-left Y (0–1).
    /// </summary>
    public double Y { get; init; }

    /// <summary>
    /// Width (0–1).
    /// </summary>
    public double Width { get; init; }

    /// <summary>
    /// Height (0–1).
    /// </summary>
    public double Height { get; init; }
}

/// <summary>
/// Token usage statistics.
/// </summary>
public record TokenUsage
{
    /// <summary>
    /// Prompt token count.
    /// </summary>
    public int PromptTokens { get; init; }

    /// <summary>
    /// Completion token count.
    /// </summary>
    public int CompletionTokens { get; init; }

    /// <summary>
    /// Total token count.
    /// </summary>
    public int TotalTokens => PromptTokens + CompletionTokens;
}
