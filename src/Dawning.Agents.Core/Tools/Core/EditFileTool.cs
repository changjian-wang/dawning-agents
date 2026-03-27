using System.Text;
using System.Text.Json;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Tools.Core;

/// <summary>
/// File editing tool — search and replace using exact string matching.
/// </summary>
/// <remarks>
/// <para>Risk: Medium — modifies existing file contents.</para>
/// <para>Algorithm: exact string match (same as Copilot), requires oldString to match exactly once in the file.</para>
/// </remarks>
public sealed class EditFileTool : ITool
{
    private readonly ILogger<EditFileTool> _logger;
    private readonly string? _workingDirectory;

    /// <summary>
    /// Creates an <see cref="EditFileTool"/>.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="workingDirectory">Working directory (sandbox root); when set, access to paths outside this directory is denied.</param>
    public EditFileTool(ILogger<EditFileTool>? logger = null, string? workingDirectory = null)
    {
        _logger = logger ?? NullLogger<EditFileTool>.Instance;
        _workingDirectory = workingDirectory is not null
            ? Path.GetFullPath(workingDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar
            : null;
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

            if (
                !root.TryGetProperty("path", out var pathProp)
                || string.IsNullOrWhiteSpace(pathProp.GetString())
            )
            {
                throw new ArgumentException("Path cannot be empty");
            }

            if (
                !root.TryGetProperty("oldString", out var oldStringProp)
                || oldStringProp.GetString() is null
            )
            {
                throw new ArgumentException("oldString is required");
            }

            path = pathProp.GetString()!;
            oldString = oldStringProp.GetString()!;
            newString = root.TryGetProperty("newString", out var newStringProp)
                ? newStringProp.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException ex)
        {
            return Task.FromResult(ToolResult.Fail($"Invalid input JSON: {ex.Message}"));
        }
        catch (ArgumentException ex)
        {
            return Task.FromResult(ToolResult.Fail(ex.Message));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(ToolResult.Fail("Path cannot be empty"));
        }

        if (string.IsNullOrEmpty(oldString))
        {
            return Task.FromResult(ToolResult.Fail("oldString cannot be empty"));
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
    /// Attempts to find similar context to help the user locate the issue.
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
