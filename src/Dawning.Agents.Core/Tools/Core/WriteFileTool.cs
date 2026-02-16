using System.Text.Json;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Tools.Core;

/// <summary>
/// 文件写入工具 — 创建文件或覆盖文件内容
/// </summary>
/// <remarks>
/// <para>Risk: Medium — 创建/覆盖文件</para>
/// <para>自动创建不存在的父目录</para>
/// </remarks>
public sealed class WriteFileTool : ITool
{
    private readonly ILogger<WriteFileTool> _logger;

    /// <summary>
    /// 创建 WriteFileTool
    /// </summary>
    public WriteFileTool(ILogger<WriteFileTool>? logger = null)
    {
        _logger = logger ?? NullLogger<WriteFileTool>.Instance;
    }

    /// <inheritdoc />
    public string Name => "write_file";

    /// <inheritdoc />
    public string Description =>
        "Create a new file or overwrite an existing file with the specified content. "
        + "Parent directories are created automatically if they don't exist. "
        + "Use edit_file instead if you want to make targeted changes to an existing file.";

    /// <inheritdoc />
    public string ParametersSchema =>
        """
            {
                "type": "object",
                "properties": {
                    "path": {
                        "type": "string",
                        "description": "Absolute or relative path to the file to write"
                    },
                    "content": {
                        "type": "string",
                        "description": "The content to write to the file"
                    }
                },
                "required": ["path", "content"]
            }
            """;

    /// <inheritdoc />
    public bool RequiresConfirmation => false;

    /// <inheritdoc />
    public ToolRiskLevel RiskLevel => ToolRiskLevel.Medium;

    /// <inheritdoc />
    public string? Category => "Core";

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        string path;
        string content;

        try
        {
            using var doc = JsonDocument.Parse(input);
            var root = doc.RootElement;

            path =
                root.GetProperty("path").GetString()
                ?? throw new ArgumentException("path is required");

            content =
                root.GetProperty("content").GetString()
                ?? throw new ArgumentException("content is required");
        }
        catch (JsonException ex)
        {
            return Task.FromResult(ToolResult.Fail($"Invalid input JSON: {ex.Message}"));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(ToolResult.Fail("Path cannot be empty"));
        }

        var fullPath = Path.GetFullPath(path);

        _logger.LogDebug("Writing file: {Path}", fullPath);

        try
        {
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                _logger.LogDebug("Created directory: {Dir}", dir);
            }

            var isNew = !File.Exists(fullPath);
            File.WriteAllText(fullPath, content);

            var lineCount = content.Split('\n').Length;
            var action = isNew ? "Created" : "Overwritten";

            return Task.FromResult(ToolResult.Ok($"{action} {fullPath} ({lineCount} lines)"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write file: {Path}", fullPath);
            return Task.FromResult(ToolResult.Fail($"Failed to write file: {ex.Message}"));
        }
    }
}
