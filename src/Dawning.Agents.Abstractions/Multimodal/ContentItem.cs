namespace Dawning.Agents.Abstractions.Multimodal;

/// <summary>
/// Multimodal content type.
/// </summary>
public enum ContentType
{
    /// <summary>
    /// Plain text.
    /// </summary>
    Text,

    /// <summary>
    /// Image.
    /// </summary>
    Image,

    /// <summary>
    /// Audio.
    /// </summary>
    Audio,

    /// <summary>
    /// Video.
    /// </summary>
    Video,

    /// <summary>
    /// Document.
    /// </summary>
    Document,
}

/// <summary>
/// Multimodal content item.
/// </summary>
public abstract record ContentItem
{
    /// <summary>
    /// Content type.
    /// </summary>
    public abstract ContentType Type { get; }
}

/// <summary>
/// Text content.
/// </summary>
public record TextContent : ContentItem
{
    /// <inheritdoc />
    public override ContentType Type => ContentType.Text;

    /// <summary>
    /// Text content.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Creates text content.
    /// </summary>
    public static TextContent Create(string text) => new() { Text = text };
}

/// <summary>
/// Image content.
/// </summary>
public record ImageContent : ContentItem
{
    /// <inheritdoc />
    public override ContentType Type => ContentType.Image;

    /// <summary>
    /// Image data (Base64 encoded).
    /// </summary>
    public string? Base64Data { get; init; }

    /// <summary>
    /// Image URL.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Image MIME type.
    /// </summary>
    public string MimeType { get; init; } = "image/png";

    /// <summary>
    /// Image detail level (for GPT-4V).
    /// </summary>
    public ImageDetail Detail { get; init; } = ImageDetail.Auto;

    /// <summary>
    /// Image description / alt text.
    /// </summary>
    public string? AltText { get; init; }

    /// <summary>
    /// Creates from Base64 data.
    /// </summary>
    public static ImageContent FromBase64(string base64Data, string mimeType = "image/png") =>
        new() { Base64Data = base64Data, MimeType = mimeType };

    /// <summary>
    /// Creates from a URL.
    /// </summary>
    public static ImageContent FromUrl(string url, ImageDetail detail = ImageDetail.Auto) =>
        new() { Url = url, Detail = detail };
}

/// <summary>
/// Image detail level.
/// </summary>
public enum ImageDetail
{
    /// <summary>
    /// Auto select.
    /// </summary>
    Auto,

    /// <summary>
    /// Low resolution (faster, cheaper).
    /// </summary>
    Low,

    /// <summary>
    /// High resolution (more detailed).
    /// </summary>
    High,
}

/// <summary>
/// Audio content.
/// </summary>
public record AudioContent : ContentItem
{
    /// <inheritdoc />
    public override ContentType Type => ContentType.Audio;

    /// <summary>
    /// Audio data (Base64 encoded).
    /// </summary>
    public string? Base64Data { get; init; }

    /// <summary>
    /// Audio URL.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Audio MIME type.
    /// </summary>
    public string MimeType { get; init; } = "audio/mp3";

    /// <summary>
    /// Duration (seconds).
    /// </summary>
    public double? DurationSeconds { get; init; }

    /// <summary>
    /// Transcript text (if already transcribed).
    /// </summary>
    public string? Transcript { get; init; }

    /// <summary>
    /// Creates from Base64 data.
    /// </summary>
    public static AudioContent FromBase64(string base64Data, string mimeType = "audio/mp3") =>
        new() { Base64Data = base64Data, MimeType = mimeType };

    /// <summary>
    /// Creates from a URL.
    /// </summary>
    public static AudioContent FromUrl(string url) => new() { Url = url };
}

/// <summary>
/// Multimodal message (contains multiple content items).
/// </summary>
public class MultimodalMessage
{
    /// <summary>
    /// Role.
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// Content item list.
    /// </summary>
    public List<ContentItem> Content { get; init; } = [];

    /// <summary>
    /// Creates a user message.
    /// </summary>
    public static MultimodalMessage User(params ContentItem[] content) =>
        new() { Role = "user", Content = [.. content] };

    /// <summary>
    /// Creates an assistant message.
    /// </summary>
    public static MultimodalMessage Assistant(string text) =>
        new() { Role = "assistant", Content = [TextContent.Create(text)] };

    /// <summary>
    /// Creates a system message.
    /// </summary>
    public static MultimodalMessage System(string text) =>
        new() { Role = "system", Content = [TextContent.Create(text)] };

    /// <summary>
    /// Adds text.
    /// </summary>
    public MultimodalMessage AddText(string text)
    {
        Content.Add(TextContent.Create(text));
        return this;
    }

    /// <summary>
    /// Adds an image (from URL).
    /// </summary>
    public MultimodalMessage AddImageUrl(string url, ImageDetail detail = ImageDetail.Auto)
    {
        Content.Add(ImageContent.FromUrl(url, detail));
        return this;
    }

    /// <summary>
    /// Adds an image (from Base64).
    /// </summary>
    public MultimodalMessage AddImageBase64(string base64Data, string mimeType = "image/png")
    {
        Content.Add(ImageContent.FromBase64(base64Data, mimeType));
        return this;
    }
}
