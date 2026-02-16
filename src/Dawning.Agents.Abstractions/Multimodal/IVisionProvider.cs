using Dawning.Agents.Abstractions.LLM;

namespace Dawning.Agents.Abstractions.Multimodal;

/// <summary>
/// 视觉处理提供者接口
/// </summary>
public interface IVisionProvider
{
    /// <summary>
    /// 是否支持视觉功能
    /// </summary>
    bool SupportsVision { get; }

    /// <summary>
    /// 支持的图像格式
    /// </summary>
    IReadOnlyList<string> SupportedImageFormats { get; }

    /// <summary>
    /// 最大图像大小（字节）
    /// </summary>
    long MaxImageSize { get; }

    /// <summary>
    /// 分析图像
    /// </summary>
    Task<VisionAnalysisResult> AnalyzeImageAsync(
        ImageContent image,
        string prompt,
        VisionOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 多模态聊天
    /// </summary>
    Task<VisionChatResponse> ChatWithVisionAsync(
        IReadOnlyList<MultimodalMessage> messages,
        VisionOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 多模态聊天（流式）
    /// </summary>
    IAsyncEnumerable<string> ChatWithVisionStreamAsync(
        IReadOnlyList<MultimodalMessage> messages,
        VisionOptions? options = null,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// 视觉聊天响应
/// </summary>
public record VisionChatResponse
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// 响应内容
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 使用统计
    /// </summary>
    public TokenUsage? Usage { get; init; }

    /// <summary>
    /// 创建成功响应
    /// </summary>
    public static VisionChatResponse Ok(string content, TokenUsage? usage = null) =>
        new()
        {
            Success = true,
            Content = content,
            Usage = usage,
        };

    /// <summary>
    /// 创建失败响应
    /// </summary>
    public static VisionChatResponse Failed(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>
/// 视觉分析配置选项
/// </summary>
public class VisionOptions
{
    /// <summary>
    /// 模型名称（默认使用支持视觉的模型）
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// 图像详情级别
    /// </summary>
    public ImageDetail Detail { get; set; } = ImageDetail.Auto;

    /// <summary>
    /// 最大输出 Token 数
    /// </summary>
    public int MaxTokens { get; set; } = 1024;

    /// <summary>
    /// 温度参数
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// 系统提示词
    /// </summary>
    public string? SystemPrompt { get; set; }
}

/// <summary>
/// 视觉分析结果
/// </summary>
public record VisionAnalysisResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// 分析描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 检测到的对象
    /// </summary>
    public List<DetectedObject>? Objects { get; init; }

    /// <summary>
    /// 检测到的文本（OCR）
    /// </summary>
    public string? DetectedText { get; init; }

    /// <summary>
    /// 标签
    /// </summary>
    public List<string>? Tags { get; init; }

    /// <summary>
    /// 置信度（0-1）
    /// </summary>
    public double? Confidence { get; init; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Token 使用统计
    /// </summary>
    public TokenUsage? TokenUsage { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static VisionAnalysisResult Ok(string description) =>
        new() { Success = true, Description = description };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static VisionAnalysisResult Fail(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>
/// 检测到的对象
/// </summary>
public record DetectedObject
{
    /// <summary>
    /// 对象名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 置信度（0-1）
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// 边界框（归一化坐标）
    /// </summary>
    public BoundingBox? BoundingBox { get; init; }
}

/// <summary>
/// 边界框
/// </summary>
public record BoundingBox
{
    /// <summary>
    /// 左上角 X（0-1）
    /// </summary>
    public double X { get; init; }

    /// <summary>
    /// 左上角 Y（0-1）
    /// </summary>
    public double Y { get; init; }

    /// <summary>
    /// 宽度（0-1）
    /// </summary>
    public double Width { get; init; }

    /// <summary>
    /// 高度（0-1）
    /// </summary>
    public double Height { get; init; }
}

/// <summary>
/// Token 使用统计
/// </summary>
public record TokenUsage
{
    /// <summary>
    /// 提示 Token 数
    /// </summary>
    public int PromptTokens { get; init; }

    /// <summary>
    /// 完成 Token 数
    /// </summary>
    public int CompletionTokens { get; init; }

    /// <summary>
    /// 总 Token 数
    /// </summary>
    public int TotalTokens => PromptTokens + CompletionTokens;
}
