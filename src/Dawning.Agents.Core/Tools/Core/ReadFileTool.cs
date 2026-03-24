using System.Text;
using System.Text.Json;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Tools.Core;

/// <summary>
/// 文件读取工具 — 读取文件内容并显示行号
/// </summary>
/// <remarks>
/// <para>Risk: Low — 只读操作</para>
/// <para>支持分段读取（offset + limit），适用于大文件</para>
/// </remarks>
public sealed class ReadFileTool : ITool
{
    private const int _defaultLimit = 2000;
    private const int _maxLimit = 10000;
    private readonly ILogger<ReadFileTool> _logger;
    private readonly string? _workingDirectory;

    /// <summary>
    /// 创建 ReadFileTool
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="workingDirectory">工作目录（沙箱根目录），设置后将禁止访问此目录外的路径</param>
    public ReadFileTool(ILogger<ReadFileTool>? logger = null, string? workingDirectory = null)
    {
        _logger = logger ?? NullLogger<ReadFileTool>.Instance;
        _workingDirectory = workingDirectory is not null
            ? Path.GetFullPath(workingDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar
            : null;
    }

    /// <inheritdoc />
    public string Name => "read_file";

    /// <inheritdoc />
    public string Description =>
        "Read the contents of a file with line numbers. "
        + "Supports reading specific line ranges using offset and limit parameters. "
        + "Line numbers are 1-indexed.";

    /// <inheritdoc />
    public string ParametersSchema =>
        """
            {
                "type": "object",
                "properties": {
                    "path": {
                        "type": "string",
                        "description": "Absolute or relative path to the file to read"
                    },
                    "offset": {
                        "type": "integer",
                        "description": "1-based line number to start reading from (default: 1)"
                    },
                    "limit": {
                        "type": "integer",
                        "description": "Maximum number of lines to read (default: 2000, max: 10000)"
                    }
                },
                "required": ["path"]
            }
            """;

    /// <inheritdoc />
    public bool RequiresConfirmation => false;

    /// <inheritdoc />
    public ToolRiskLevel RiskLevel => ToolRiskLevel.Low;

    /// <inheritdoc />
    public string? Category => "Core";

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        string path;
        int offset = 1;
        int limit = _defaultLimit;

        try
        {
            using var doc = JsonDocument.Parse(input);
            var root = doc.RootElement;

            if (
                !root.TryGetProperty("path", out var pathProp)
                || string.IsNullOrWhiteSpace(pathProp.GetString())
            )
            {
                throw new ArgumentException("Path cannot be empty");
            }

            path = pathProp.GetString()!;

            if (
                root.TryGetProperty("offset", out var offsetProp)
                && offsetProp.ValueKind == JsonValueKind.Number
            )
            {
                offset = Math.Max(1, offsetProp.GetInt32());
            }

            if (
                root.TryGetProperty("limit", out var limitProp)
                && limitProp.ValueKind == JsonValueKind.Number
            )
            {
                limit = Math.Clamp(limitProp.GetInt32(), 1, _maxLimit);
            }
        }
        catch (JsonException)
        {
            path = input.Trim();
        }
        catch (ArgumentException ex)
        {
            return Task.FromResult(ToolResult.Fail(ex.Message));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(ToolResult.Fail("Path cannot be empty"));
        }

        var fullPath = _workingDirectory is not null
            ? Path.GetFullPath(Path.Combine(_workingDirectory, path))
            : Path.GetFullPath(path);

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (_workingDirectory is not null && !fullPath.StartsWith(_workingDirectory, comparison))
        {
            _logger.LogWarning("Path traversal attempt blocked: {Path}", path);
            return Task.FromResult(
                ToolResult.Fail($"Access denied: path is outside the allowed directory")
            );
        }

        if (!File.Exists(fullPath))
        {
            return Task.FromResult(ToolResult.Fail($"File not found: {fullPath}"));
        }

        _logger.LogDebug(
            "Reading file: {Path} (offset={Offset}, limit={Limit})",
            fullPath,
            offset,
            limit
        );

        try
        {
            var lines = File.ReadAllLines(fullPath);
            var totalLines = lines.Length;

            if (offset > totalLines)
            {
                return Task.FromResult(
                    ToolResult.Ok(
                        $"File has {totalLines} lines, offset {offset} is beyond end of file."
                    )
                );
            }

            var startIndex = offset - 1; // Convert to 0-based
            var count = Math.Min(limit, totalLines - startIndex);
            var lineNumberWidth = (startIndex + count).ToString().Length;

            var sb = new StringBuilder();

            for (var i = 0; i < count; i++)
            {
                var lineNum = startIndex + i + 1; // 1-based
                sb.Append(lineNum.ToString().PadLeft(lineNumberWidth));
                sb.Append(" | ");
                sb.AppendLine(lines[startIndex + i]);
            }

            if (startIndex + count < totalLines)
            {
                sb.AppendLine();
                sb.Append(
                    $"[Showing lines {offset}-{startIndex + count} of {totalLines}. "
                        + $"Use offset={startIndex + count + 1} to continue reading.]"
                );
            }

            return Task.FromResult(ToolResult.Ok(sb.ToString()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read file: {Path}", fullPath);
            return Task.FromResult(ToolResult.Fail($"Failed to read file: {ex.Message}"));
        }
    }
}
