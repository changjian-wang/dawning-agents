using Dawning.Agents.Abstractions.Multimodal;

namespace Dawning.Agents.Core.Multimodal;

/// <summary>
/// <see cref="ImageContent"/> 的文件 I/O 扩展方法
/// </summary>
public static class ImageContentExtensions
{
    /// <summary>
    /// 从文件创建图像内容
    /// </summary>
    public static async Task<ImageContent> FromFileAsync(
        string filePath,
        CancellationToken cancellationToken = default
    )
    {
        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        var base64 = Convert.ToBase64String(bytes);
        var mimeType = GetMimeTypeFromExtension(Path.GetExtension(filePath));
        return new ImageContent { Base64Data = base64, MimeType = mimeType };
    }

    private static string GetMimeTypeFromExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream",
        };
    }
}
