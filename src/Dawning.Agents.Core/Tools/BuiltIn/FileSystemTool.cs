using System.Text;
using Dawning.Agents.Abstractions.Tools;

namespace Dawning.Agents.Core.Tools.BuiltIn;

/// <summary>
/// 文件系统工具 - 提供文件和目录操作能力
/// </summary>
/// <remarks>
/// <para>包含读取、写入、搜索、删除等文件操作</para>
/// <para>危险操作（写入、删除）需要用户确认</para>
/// </remarks>
public class FileSystemTool
{
    /// <summary>
    /// 读取文件内容
    /// </summary>
    /// <param name="filePath">文件的绝对路径</param>
    /// <param name="startLine">起始行号（1-based，可选）</param>
    /// <param name="endLine">结束行号（1-based，可选）</param>
    /// <returns>文件内容</returns>
    [FunctionTool("读取文件内容。可指定行范围读取部分内容。", Category = "FileSystem")]
    public async Task<ToolResult> ReadFile(
        [ToolParameter("文件的绝对路径")] string filePath,
        [ToolParameter("起始行号（1-based，可选）")] int startLine = 0,
        [ToolParameter("结束行号（1-based，可选）")] int endLine = 0,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return ToolResult.Fail("文件路径不能为空");
            }

            if (!File.Exists(filePath))
            {
                return ToolResult.Fail($"文件不存在: {filePath}");
            }

            var content = await File.ReadAllTextAsync(filePath, cancellationToken);

            // 如果指定了行范围
            if (startLine > 0 || endLine > 0)
            {
                var lines = content.Split('\n');
                var start = Math.Max(1, startLine) - 1;
                var end = endLine > 0 ? Math.Min(endLine, lines.Length) : lines.Length;

                if (start >= lines.Length)
                {
                    return ToolResult.Fail($"起始行 {startLine} 超出文件总行数 {lines.Length}");
                }

                content = string.Join('\n', lines.Skip(start).Take(end - start));
            }

            return ToolResult.Ok(content);
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"读取文件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 写入文件（需要确认）
    /// </summary>
    [FunctionTool(
        "创建或覆盖写入文件内容。这是一个破坏性操作，需要用户确认。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.Medium,
        Category = "FileSystem"
    )]
    public async Task<ToolResult> WriteFile(
        [ToolParameter("文件的绝对路径")] string filePath,
        [ToolParameter("要写入的内容")] string content,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return ToolResult.Fail("文件路径不能为空");
            }

            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(filePath, content, cancellationToken);
            return ToolResult.Ok($"文件已写入: {filePath}");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"写入文件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 追加内容到文件（需要确认）
    /// </summary>
    [FunctionTool(
        "追加内容到文件末尾。如果文件不存在则创建。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.Medium,
        Category = "FileSystem"
    )]
    public async Task<ToolResult> AppendFile(
        [ToolParameter("文件的绝对路径")] string filePath,
        [ToolParameter("要追加的内容")] string content,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return ToolResult.Fail("文件路径不能为空");
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.AppendAllTextAsync(filePath, content, cancellationToken);
            return ToolResult.Ok($"内容已追加到文件: {filePath}");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"追加文件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除文件（高风险，需要确认）
    /// </summary>
    [FunctionTool(
        "删除指定的文件。这是一个不可逆操作，需要用户确认。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.High,
        Category = "FileSystem"
    )]
    public Task<ToolResult> DeleteFile([ToolParameter("文件的绝对路径")] string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return Task.FromResult(ToolResult.Fail("文件路径不能为空"));
            }

            if (!File.Exists(filePath))
            {
                return Task.FromResult(ToolResult.Fail($"文件不存在: {filePath}"));
            }

            File.Delete(filePath);
            return Task.FromResult(ToolResult.Ok($"文件已删除: {filePath}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail($"删除文件失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 列出目录内容
    /// </summary>
    [FunctionTool("列出目录中的文件和子目录。", Category = "FileSystem")]
    public Task<ToolResult> ListDirectory(
        [ToolParameter("目录的绝对路径")] string directoryPath,
        [ToolParameter("是否递归列出子目录")] bool recursive = false
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return Task.FromResult(ToolResult.Fail("目录路径不能为空"));
            }

            if (!Directory.Exists(directoryPath))
            {
                return Task.FromResult(ToolResult.Fail($"目录不存在: {directoryPath}"));
            }

            var searchOption = recursive
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;
            var entries = new List<string>();

            foreach (var dir in Directory.GetDirectories(directoryPath, "*", searchOption))
            {
                var relativePath = Path.GetRelativePath(directoryPath, dir);
                entries.Add($"[DIR]  {relativePath}/");
            }

            foreach (var file in Directory.GetFiles(directoryPath, "*", searchOption))
            {
                var relativePath = Path.GetRelativePath(directoryPath, file);
                var fileInfo = new FileInfo(file);
                entries.Add($"[FILE] {relativePath} ({FormatFileSize(fileInfo.Length)})");
            }

            if (entries.Count == 0)
            {
                return Task.FromResult(ToolResult.Ok("目录为空"));
            }

            return Task.FromResult(ToolResult.Ok(string.Join('\n', entries)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail($"列出目录失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 搜索文件
    /// </summary>
    [FunctionTool("使用通配符模式搜索文件。", Category = "FileSystem")]
    public Task<ToolResult> SearchFiles(
        [ToolParameter("搜索的根目录")] string directoryPath,
        [ToolParameter("搜索模式（如 *.cs, *.txt）")] string pattern,
        [ToolParameter("是否递归搜索子目录")] bool recursive = true
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return Task.FromResult(ToolResult.Fail("目录路径不能为空"));
            }

            if (!Directory.Exists(directoryPath))
            {
                return Task.FromResult(ToolResult.Fail($"目录不存在: {directoryPath}"));
            }

            var searchOption = recursive
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(directoryPath, pattern, searchOption);

            if (files.Length == 0)
            {
                return Task.FromResult(ToolResult.Ok($"未找到匹配 '{pattern}' 的文件"));
            }

            var results = files.Select(f => Path.GetRelativePath(directoryPath, f));
            return Task.FromResult(
                ToolResult.Ok($"找到 {files.Length} 个文件:\n{string.Join('\n', results)}")
            );
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail($"搜索文件失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 在文件中搜索文本
    /// </summary>
    [FunctionTool("在文件中搜索包含指定文本的行。", Category = "FileSystem")]
    public async Task<ToolResult> GrepFile(
        [ToolParameter("文件的绝对路径")] string filePath,
        [ToolParameter("要搜索的文本")] string searchText,
        [ToolParameter("是否区分大小写")] bool caseSensitive = false,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return ToolResult.Fail("文件路径不能为空");
            }

            if (!File.Exists(filePath))
            {
                return ToolResult.Fail($"文件不存在: {filePath}");
            }

            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
            var comparison = caseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            var matches = lines
                .Select((line, index) => new { Line = line, Number = index + 1 })
                .Where(x => x.Line.Contains(searchText, comparison))
                .Select(x => $"{x.Number}: {x.Line}")
                .ToList();

            if (matches.Count == 0)
            {
                return ToolResult.Ok($"未找到包含 '{searchText}' 的内容");
            }

            return ToolResult.Ok($"找到 {matches.Count} 处匹配:\n{string.Join('\n', matches)}");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"搜索文件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取文件信息
    /// </summary>
    [FunctionTool("获取文件的详细信息（大小、创建时间、修改时间等）。", Category = "FileSystem")]
    public Task<ToolResult> GetFileInfo([ToolParameter("文件的绝对路径")] string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return Task.FromResult(ToolResult.Fail("文件路径不能为空"));
            }

            if (!File.Exists(filePath))
            {
                return Task.FromResult(ToolResult.Fail($"文件不存在: {filePath}"));
            }

            var info = new FileInfo(filePath);
            var sb = new StringBuilder();
            sb.AppendLine($"文件名: {info.Name}");
            sb.AppendLine($"完整路径: {info.FullName}");
            sb.AppendLine($"大小: {FormatFileSize(info.Length)}");
            sb.AppendLine($"创建时间: {info.CreationTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"修改时间: {info.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"访问时间: {info.LastAccessTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"只读: {info.IsReadOnly}");
            sb.AppendLine($"扩展名: {info.Extension}");

            return Task.FromResult(ToolResult.Ok(sb.ToString().TrimEnd()));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail($"获取文件信息失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 复制文件（需要确认）
    /// </summary>
    [FunctionTool(
        "复制文件到新位置。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.Medium,
        Category = "FileSystem"
    )]
    public Task<ToolResult> CopyFile(
        [ToolParameter("源文件路径")] string sourcePath,
        [ToolParameter("目标文件路径")] string destinationPath,
        [ToolParameter("如果目标存在是否覆盖")] bool overwrite = false
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return Task.FromResult(ToolResult.Fail("源文件路径不能为空"));
            }

            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                return Task.FromResult(ToolResult.Fail("目标文件路径不能为空"));
            }

            if (!File.Exists(sourcePath))
            {
                return Task.FromResult(ToolResult.Fail($"源文件不存在: {sourcePath}"));
            }

            if (File.Exists(destinationPath) && !overwrite)
            {
                return Task.FromResult(ToolResult.Fail($"目标文件已存在: {destinationPath}"));
            }

            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(sourcePath, destinationPath, overwrite);
            return Task.FromResult(ToolResult.Ok($"文件已复制: {sourcePath} -> {destinationPath}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail($"复制文件失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 移动/重命名文件（需要确认）
    /// </summary>
    [FunctionTool(
        "移动或重命名文件。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.Medium,
        Category = "FileSystem"
    )]
    public Task<ToolResult> MoveFile(
        [ToolParameter("源文件路径")] string sourcePath,
        [ToolParameter("目标文件路径")] string destinationPath,
        [ToolParameter("如果目标存在是否覆盖")] bool overwrite = false
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return Task.FromResult(ToolResult.Fail("源文件路径不能为空"));
            }

            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                return Task.FromResult(ToolResult.Fail("目标文件路径不能为空"));
            }

            if (!File.Exists(sourcePath))
            {
                return Task.FromResult(ToolResult.Fail($"源文件不存在: {sourcePath}"));
            }

            if (File.Exists(destinationPath) && !overwrite)
            {
                return Task.FromResult(ToolResult.Fail($"目标文件已存在: {destinationPath}"));
            }

            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Move(sourcePath, destinationPath, overwrite);
            return Task.FromResult(ToolResult.Ok($"文件已移动: {sourcePath} -> {destinationPath}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail($"移动文件失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 创建目录（需要确认）
    /// </summary>
    [FunctionTool(
        "创建目录（包括所有必要的父目录）。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.Low,
        Category = "FileSystem"
    )]
    public Task<ToolResult> CreateDirectory([ToolParameter("目录的绝对路径")] string directoryPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return Task.FromResult(ToolResult.Fail("目录路径不能为空"));
            }

            if (Directory.Exists(directoryPath))
            {
                return Task.FromResult(ToolResult.Ok($"目录已存在: {directoryPath}"));
            }

            Directory.CreateDirectory(directoryPath);
            return Task.FromResult(ToolResult.Ok($"目录已创建: {directoryPath}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail($"创建目录失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 删除目录（高风险，需要确认）
    /// </summary>
    [FunctionTool(
        "删除目录及其所有内容。这是一个不可逆操作，需要用户确认。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.High,
        Category = "FileSystem"
    )]
    public Task<ToolResult> DeleteDirectory(
        [ToolParameter("目录的绝对路径")] string directoryPath,
        [ToolParameter("是否递归删除所有内容")] bool recursive = false
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return Task.FromResult(ToolResult.Fail("目录路径不能为空"));
            }

            if (!Directory.Exists(directoryPath))
            {
                return Task.FromResult(ToolResult.Fail($"目录不存在: {directoryPath}"));
            }

            Directory.Delete(directoryPath, recursive);
            return Task.FromResult(ToolResult.Ok($"目录已删除: {directoryPath}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail($"删除目录失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 检查文件或目录是否存在
    /// </summary>
    [FunctionTool("检查文件或目录是否存在。", Category = "FileSystem")]
    public Task<ToolResult> Exists([ToolParameter("文件或目录的绝对路径")] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(ToolResult.Fail("路径不能为空"));
        }

        if (File.Exists(path))
        {
            return Task.FromResult(ToolResult.Ok($"文件存在: {path}"));
        }

        if (Directory.Exists(path))
        {
            return Task.FromResult(ToolResult.Ok($"目录存在: {path}"));
        }

        return Task.FromResult(ToolResult.Ok($"路径不存在: {path}"));
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
