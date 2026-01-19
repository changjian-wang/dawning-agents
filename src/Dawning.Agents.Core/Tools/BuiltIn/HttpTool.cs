using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Dawning.Agents.Abstractions.Tools;

namespace Dawning.Agents.Core.Tools.BuiltIn;

/// <summary>
/// HTTP/Web 工具 - 提供 HTTP 请求能力
/// </summary>
/// <remarks>
/// <para>支持 GET、POST 等 HTTP 方法</para>
/// <para>网络请求可能有外部影响，属于中等风险操作</para>
/// </remarks>
public class HttpTool
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// 创建 HTTP 工具
    /// </summary>
    /// <param name="httpClient">HTTP 客户端（通过 DI 注入）</param>
    public HttpTool(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// 发送 HTTP GET 请求
    /// </summary>
    [FunctionTool(
        "发送 HTTP GET 请求并返回响应内容。",
        RiskLevel = ToolRiskLevel.Medium,
        Category = "Http"
    )]
    public async Task<ToolResult> HttpGet(
        [ToolParameter("请求的 URL")] string url,
        [ToolParameter("请求头（JSON 格式，可选）")] string? headers = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return ToolResult.Fail("URL 不能为空");
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return ToolResult.Fail($"无效的 URL: {url}");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            AddHeaders(request, headers);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            var result = new StringBuilder();
            result.AppendLine($"状态码: {(int)response.StatusCode} {response.StatusCode}");
            result.AppendLine($"内容类型: {response.Content.Headers.ContentType}");
            result.AppendLine($"内容长度: {content.Length} 字符");
            result.AppendLine();
            result.AppendLine("--- 响应内容 ---");
            result.Append(TruncateContent(content));

            return ToolResult.Ok(result.ToString());
        }
        catch (HttpRequestException ex)
        {
            return ToolResult.Fail($"HTTP 请求失败: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return ToolResult.Fail("请求超时");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"请求失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 发送 HTTP POST 请求（需要确认）
    /// </summary>
    [FunctionTool(
        "发送 HTTP POST 请求。这可能会修改服务器数据，需要谨慎使用。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.Medium,
        Category = "Http"
    )]
    public async Task<ToolResult> HttpPost(
        [ToolParameter("请求的 URL")] string url,
        [ToolParameter("请求体内容")] string body,
        [ToolParameter("内容类型（默认 application/json）")]
            string contentType = "application/json",
        [ToolParameter("请求头（JSON 格式，可选）")] string? headers = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return ToolResult.Fail("URL 不能为空");
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return ToolResult.Fail($"无效的 URL: {url}");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Content = new StringContent(body ?? string.Empty, Encoding.UTF8, contentType);
            AddHeaders(request, headers);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            var result = new StringBuilder();
            result.AppendLine($"状态码: {(int)response.StatusCode} {response.StatusCode}");
            result.AppendLine($"内容类型: {response.Content.Headers.ContentType}");
            result.AppendLine();
            result.AppendLine("--- 响应内容 ---");
            result.Append(TruncateContent(content));

            return ToolResult.Ok(result.ToString());
        }
        catch (HttpRequestException ex)
        {
            return ToolResult.Fail($"HTTP 请求失败: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return ToolResult.Fail("请求超时");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"请求失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 发送 HTTP PUT 请求（需要确认）
    /// </summary>
    [FunctionTool(
        "发送 HTTP PUT 请求。这可能会修改服务器数据。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.Medium,
        Category = "Http"
    )]
    public async Task<ToolResult> HttpPut(
        [ToolParameter("请求的 URL")] string url,
        [ToolParameter("请求体内容")] string body,
        [ToolParameter("内容类型（默认 application/json）")]
            string contentType = "application/json",
        [ToolParameter("请求头（JSON 格式，可选）")] string? headers = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return ToolResult.Fail("URL 不能为空");
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return ToolResult.Fail($"无效的 URL: {url}");
            }

            using var request = new HttpRequestMessage(HttpMethod.Put, uri);
            request.Content = new StringContent(body ?? string.Empty, Encoding.UTF8, contentType);
            AddHeaders(request, headers);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            var result = new StringBuilder();
            result.AppendLine($"状态码: {(int)response.StatusCode} {response.StatusCode}");
            result.AppendLine($"内容类型: {response.Content.Headers.ContentType}");
            result.AppendLine();
            result.AppendLine("--- 响应内容 ---");
            result.Append(TruncateContent(content));

            return ToolResult.Ok(result.ToString());
        }
        catch (HttpRequestException ex)
        {
            return ToolResult.Fail($"HTTP 请求失败: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return ToolResult.Fail("请求超时");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"请求失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 发送 HTTP DELETE 请求（高风险，需要确认）
    /// </summary>
    [FunctionTool(
        "发送 HTTP DELETE 请求。这可能会删除服务器数据，是高风险操作。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.High,
        Category = "Http"
    )]
    public async Task<ToolResult> HttpDelete(
        [ToolParameter("请求的 URL")] string url,
        [ToolParameter("请求头（JSON 格式，可选）")] string? headers = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return ToolResult.Fail("URL 不能为空");
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return ToolResult.Fail($"无效的 URL: {url}");
            }

            using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
            AddHeaders(request, headers);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            var result = new StringBuilder();
            result.AppendLine($"状态码: {(int)response.StatusCode} {response.StatusCode}");
            result.AppendLine();
            result.AppendLine("--- 响应内容 ---");
            result.Append(TruncateContent(content));

            return ToolResult.Ok(result.ToString());
        }
        catch (HttpRequestException ex)
        {
            return ToolResult.Fail($"HTTP 请求失败: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return ToolResult.Fail("请求超时");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"请求失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取 URL 的 HTTP 头信息
    /// </summary>
    [FunctionTool("获取 URL 的 HTTP 响应头信息（HEAD 请求）。", Category = "Http")]
    public async Task<ToolResult> HttpHead(
        [ToolParameter("请求的 URL")] string url,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return ToolResult.Fail("URL 不能为空");
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return ToolResult.Fail($"无效的 URL: {url}");
            }

            using var request = new HttpRequestMessage(HttpMethod.Head, uri);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            var result = new StringBuilder();
            result.AppendLine($"状态码: {(int)response.StatusCode} {response.StatusCode}");
            result.AppendLine();
            result.AppendLine("--- 响应头 ---");

            foreach (var header in response.Headers)
            {
                result.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
            }

            foreach (var header in response.Content.Headers)
            {
                result.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
            }

            return ToolResult.Ok(result.ToString().TrimEnd());
        }
        catch (HttpRequestException ex)
        {
            return ToolResult.Fail($"HTTP 请求失败: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return ToolResult.Fail("请求超时");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"请求失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 下载文件（需要确认）
    /// </summary>
    [FunctionTool(
        "下载文件到本地路径。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.Medium,
        Category = "Http"
    )]
    public async Task<ToolResult> DownloadFile(
        [ToolParameter("下载 URL")] string url,
        [ToolParameter("保存的本地路径")] string savePath,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return ToolResult.Fail("URL 不能为空");
            }

            if (string.IsNullOrWhiteSpace(savePath))
            {
                return ToolResult.Fail("保存路径不能为空");
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return ToolResult.Fail($"无效的 URL: {url}");
            }

            // 确保目录存在
            var directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var response = await _httpClient.GetAsync(uri, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var fileStream = File.Create(savePath);
            await response.Content.CopyToAsync(fileStream, cancellationToken);

            var fileInfo = new FileInfo(savePath);
            return ToolResult.Ok(
                $"文件已下载: {savePath}\n大小: {FormatFileSize(fileInfo.Length)}"
            );
        }
        catch (HttpRequestException ex)
        {
            return ToolResult.Fail($"下载失败: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return ToolResult.Fail("下载超时");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"下载失败: {ex.Message}");
        }
    }

    private static void AddHeaders(HttpRequestMessage request, string? headersJson)
    {
        if (string.IsNullOrWhiteSpace(headersJson))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(headersJson);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                request.Headers.TryAddWithoutValidation(prop.Name, prop.Value.GetString());
            }
        }
        catch
        {
            // 忽略无效的 headers JSON
        }
    }

    private static string TruncateContent(string content, int maxLength = 10000)
    {
        if (content.Length <= maxLength)
        {
            return content;
        }

        return content[..maxLength] + $"\n\n... (截断，总长度 {content.Length} 字符)";
    }

    private static string FormatFileSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int index = 0;
        double size = bytes;

        while (size >= 1024 && index < suffixes.Length - 1)
        {
            size /= 1024;
            index++;
        }

        return $"{size:0.##} {suffixes[index]}";
    }
}
