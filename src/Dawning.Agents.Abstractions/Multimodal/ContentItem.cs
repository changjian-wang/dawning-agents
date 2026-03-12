namespace Dawning.Agents.Abstractions.Multimodal;

/// <summary>
/// 多模态内容类型
/// </summary>
public enum ContentType
{
    /// <summary>
    /// 纯文本
    /// </summary>
    Text,

    /// <summary>
    /// 图像
    /// </summary>
    Image,

    /// <summary>
    /// 音频
    /// </summary>
    Audio,

    /// <summary>
    /// 视频
    /// </summary>
    Video,

    /// <summary>
    /// 文档
    /// </summary>
    Document,
}

/// <summary>
/// 多模态内容项
/// </summary>
public abstract record ContentItem
{
    /// <summary>
    /// 内容类型
    /// </summary>
    public abstract ContentType Type { get; }
}

/// <summary>
/// 文本内容
/// </summary>
public record TextContent : ContentItem
{
    /// <inheritdoc />
    public override ContentType Type => ContentType.Text;

    /// <summary>
    /// 文本内容
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// 创建文本内容
    /// </summary>
    public static TextContent Create(string text) => new() { Text = text };
}

/// <summary>
/// 图像内容
/// </summary>
public record ImageContent : ContentItem
{
    /// <inheritdoc />
    public override ContentType Type => ContentType.Image;

    /// <summary>
    /// 图像数据（Base64 编码）
    /// </summary>
    public string? Base64Data { get; init; }

    /// <summary>
    /// 图像 URL
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// 图像 MIME 类型
    /// </summary>
    public string MimeType { get; init; } = "image/png";

    /// <summary>
    /// 图像详情级别（用于 GPT-4V）
    /// </summary>
    public ImageDetail Detail { get; init; } = ImageDetail.Auto;

    /// <summary>
    /// 图像描述/Alt 文本
    /// </summary>
    public string? AltText { get; init; }

    /// <summary>
    /// 从 Base64 数据创建
    /// </summary>
    public static ImageContent FromBase64(string base64Data, string mimeType = "image/png") =>
        new() { Base64Data = base64Data, MimeType = mimeType };

    /// <summary>
    /// 从 URL 创建
    /// </summary>
    public static ImageContent FromUrl(string url, ImageDetail detail = ImageDetail.Auto) =>
        new() { Url = url, Detail = detail };
}

/// <summary>
/// 图像详情级别
/// </summary>
public enum ImageDetail
{
    /// <summary>
    /// 自动选择
    /// </summary>
    Auto,

    /// <summary>
    /// 低分辨率（更快、更便宜）
    /// </summary>
    Low,

    /// <summary>
    /// 高分辨率（更详细）
    /// </summary>
    High,
}

/// <summary>
/// 音频内容
/// </summary>
public record AudioContent : ContentItem
{
    /// <inheritdoc />
    public override ContentType Type => ContentType.Audio;

    /// <summary>
    /// 音频数据（Base64 编码）
    /// </summary>
    public string? Base64Data { get; init; }

    /// <summary>
    /// 音频 URL
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// 音频 MIME 类型
    /// </summary>
    public string MimeType { get; init; } = "audio/mp3";

    /// <summary>
    /// 时长（秒）
    /// </summary>
    public double? DurationSeconds { get; init; }

    /// <summary>
    /// 转录文本（如果已转录）
    /// </summary>
    public string? Transcript { get; init; }

    /// <summary>
    /// 从 Base64 数据创建
    /// </summary>
    public static AudioContent FromBase64(string base64Data, string mimeType = "audio/mp3") =>
        new() { Base64Data = base64Data, MimeType = mimeType };

    /// <summary>
    /// 从 URL 创建
    /// </summary>
    public static AudioContent FromUrl(string url) => new() { Url = url };
}

/// <summary>
/// 多模态消息（包含多个内容项）
/// </summary>
public class MultimodalMessage
{
    /// <summary>
    /// 角色
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// 内容项列表
    /// </summary>
    public List<ContentItem> Content { get; init; } = [];

    /// <summary>
    /// 创建用户消息
    /// </summary>
    public static MultimodalMessage User(params ContentItem[] content) =>
        new() { Role = "user", Content = [.. content] };

    /// <summary>
    /// 创建助手消息
    /// </summary>
    public static MultimodalMessage Assistant(string text) =>
        new() { Role = "assistant", Content = [TextContent.Create(text)] };

    /// <summary>
    /// 创建系统消息
    /// </summary>
    public static MultimodalMessage System(string text) =>
        new() { Role = "system", Content = [TextContent.Create(text)] };

    /// <summary>
    /// 添加文本
    /// </summary>
    public MultimodalMessage AddText(string text)
    {
        Content.Add(TextContent.Create(text));
        return this;
    }

    /// <summary>
    /// 添加图像（从 URL）
    /// </summary>
    public MultimodalMessage AddImageUrl(string url, ImageDetail detail = ImageDetail.Auto)
    {
        Content.Add(ImageContent.FromUrl(url, detail));
        return this;
    }

    /// <summary>
    /// 添加图像（从 Base64）
    /// </summary>
    public MultimodalMessage AddImageBase64(string base64Data, string mimeType = "image/png")
    {
        Content.Add(ImageContent.FromBase64(base64Data, mimeType));
        return this;
    }
}
