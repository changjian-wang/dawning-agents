using System.Text;
using System.Text.Json;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Tools.Core;

/// <summary>
/// 文件编辑工具 — 使用精确字符串匹配进行搜索替换
/// </summary>
/// <remarks>
/// <para>Risk: Medium — 修改现有文件内容</para>
/// <para>算法: exact string match（同 Copilot 方式），要求 oldString 在文件中唯一匹配</para>
/// </remarks>
public sealed class EditFileTool : ITool
{
    private readonly ILogger<EditFileTool> _logger;

    /// <summary>
    /// 创建 EditFileTool
    /// </summary>
    public EditFileTool(ILogger<EditFileTool>? logger = null)
    {
        _logger = logger ?? NullLogger<EditFileTool>.Instance;
    }

    /// <inheritdoc />
    public string Name => "edit_file";

    /// <inheritdoc />
    public string Description =>
        "Make targeted edits to an existing file using exact string matching. "
        + "Provide the exact text to find (oldString) and the replacement text (newString). "
        + "The oldString must match exactly one location in the file. "
        + "Include enough context (at least 3 lines before and after) to ensure a unique match. "
        + "Use write_file instead if you want to create a new file or overwrite entirely.";

    /// <inheritdoc />
    public string ParametersSchema =>
        """
            {
                "type": "object",
                "properties": {
                    "path": {
                        "type": "string",
                        "description": "Absolute or relative path to the file to edit"
                    },
                    "oldString": {
                        "type": "string",
                        "description": "The exact literal text to find and replace (must match exactly once)"
                    },
                    "newString": {
                        "type": "string",
                        "description": "The replacement text"
                    }
                },
                "required": ["path", "oldString", "newString"]
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
        string oldString;
        string newString;

        try
        {
            using var doc = JsonDocument.Parse(input);
            var root = doc.RootElement;

            path =
                root.GetProperty("path").GetString()
                ?? throw new ArgumentException("path is required");

            oldString =
                root.GetProperty("oldString").GetString()
                ?? throw new ArgumentException("oldString is required");

            newString = root.GetProperty("newString").GetString() ?? string.Empty;
        }
        catch (JsonException ex)
        {
            return Task.FromResult(ToolResult.Fail($"Invalid input JSON: {ex.Message}"));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(ToolResult.Fail("Path cannot be empty"));
        }

        if (string.IsNullOrEmpty(oldString))
        {
            return Task.FromResult(ToolResult.Fail("oldString cannot be empty"));
        }

        var fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult(ToolResult.Fail($"File not found: {fullPath}"));
        }

        _logger.LogDebug("Editing file: {Path}", fullPath);

        try
        {
            var content = File.ReadAllText(fullPath);

            // Count occurrences
            var matchCount = CountOccurrences(content, oldString);

            if (matchCount == 0)
            {
                // Try to provide helpful context
                var suggestion = FindSimilarContext(content, oldString);
                var message = $"oldString not found in {fullPath}";
                if (suggestion != null)
                {
                    message += $"\n\nDid you mean:\n{suggestion}";
                }

                return Task.FromResult(ToolResult.Fail(message));
            }

            if (matchCount > 1)
            {
                return Task.FromResult(
                    ToolResult.Fail(
                        $"oldString matches {matchCount} locations in {fullPath}. "
                            + "Include more context to ensure a unique match."
                    )
                );
            }

            // Exactly one match — perform the replacement
            var newContent = content.Replace(oldString, newString);
            File.WriteAllText(fullPath, newContent);

            // Calculate affected line range for reporting
            var startIndex = content.IndexOf(oldString, StringComparison.Ordinal);
            var lineNumber = content[..startIndex].Count(c => c == '\n') + 1;
            var oldLineCount = oldString.Count(c => c == '\n') + 1;
            var newLineCount = newString.Count(c => c == '\n') + 1;

            var sb = new StringBuilder();
            sb.Append($"Edited {fullPath}");
            sb.Append($" (line {lineNumber}");
            if (oldLineCount > 1)
            {
                sb.Append($"-{lineNumber + oldLineCount - 1}");
            }

            sb.Append($": {oldLineCount} lines → {newLineCount} lines)");

            return Task.FromResult(ToolResult.Ok(sb.ToString()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to edit file: {Path}", fullPath);
            return Task.FromResult(ToolResult.Fail($"Failed to edit file: {ex.Message}"));
        }
    }

    private static int CountOccurrences(string text, string search)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(search, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += search.Length;
        }

        return count;
    }

    /// <summary>
    /// 尝试找到相似的上下文，帮助用户定位问题
    /// </summary>
    private static string? FindSimilarContext(string content, string search)
    {
        // Try trimmed version
        var trimmed = search.Trim();
        if (
            !string.IsNullOrEmpty(trimmed)
            && trimmed != search
            && content.Contains(trimmed, StringComparison.Ordinal)
        )
        {
            return "The text exists but whitespace/indentation differs. Check leading/trailing whitespace.";
        }

        // Try first line only
        var firstLine = search.Split('\n')[0].Trim();
        if (
            !string.IsNullOrEmpty(firstLine)
            && firstLine.Length > 10
            && content.Contains(firstLine, StringComparison.Ordinal)
        )
        {
            return $"First line found: \"{Truncate(firstLine, 80)}\". "
                + "The rest may have whitespace or content differences.";
        }

        return null;
    }

    private static string Truncate(string text, int maxLength)
    {
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }
}
