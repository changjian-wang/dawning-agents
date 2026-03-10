using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Tools.Core;

/// <summary>
/// 搜索工具 — 支持文本搜索（grep）和文件搜索（glob）两种模式
/// </summary>
/// <remarks>
/// <para>Risk: Low — 只读操作</para>
/// <para>grep 模式: 在文件内容中搜索文本或正则表达式</para>
/// <para>glob 模式: 搜索匹配模式的文件路径</para>
/// </remarks>
public sealed class SearchTool : ITool
{
    private const int DefaultMaxResults = 50;
    private const int MaxResultsLimit = 500;
    private const int ContextLines = 0;
    private readonly ILogger<SearchTool> _logger;

    /// <summary>
    /// 创建 SearchTool
    /// </summary>
    public SearchTool(ILogger<SearchTool>? logger = null)
    {
        _logger = logger ?? NullLogger<SearchTool>.Instance;
    }

    /// <inheritdoc />
    public string Name => "search";

    /// <inheritdoc />
    public string Description =>
        "Search for text in files (grep mode) or search for files by name (glob mode). "
        + "In grep mode, searches file contents using plain text or regex patterns. "
        + "In glob mode, finds files matching a glob pattern (e.g. **/*.cs). "
        + "Results include file paths and line numbers.";

    /// <inheritdoc />
    public string ParametersSchema =>
        """
            {
                "type": "object",
                "properties": {
                    "pattern": {
                        "type": "string",
                        "description": "Search pattern: text/regex for grep mode, glob pattern (e.g. **/*.cs) for glob mode"
                    },
                    "mode": {
                        "type": "string",
                        "enum": ["grep", "glob"],
                        "description": "Search mode (default: grep)"
                    },
                    "path": {
                        "type": "string",
                        "description": "Directory to search in (default: current directory)"
                    },
                    "isRegex": {
                        "type": "boolean",
                        "description": "Whether the pattern is a regex (grep mode only, default: false)"
                    },
                    "includePattern": {
                        "type": "string",
                        "description": "Only search in files matching this glob (grep mode only, e.g. *.cs)"
                    },
                    "maxResults": {
                        "type": "integer",
                        "description": "Maximum number of results to return (default: 50, max: 500)"
                    }
                },
                "required": ["pattern"]
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
        string pattern;
        var mode = "grep";
        var searchPath = ".";
        var isRegex = false;
        string? includePattern = null;
        var maxResults = DefaultMaxResults;

        try
        {
            using var doc = JsonDocument.Parse(input);
            var root = doc.RootElement;

            pattern =
                root.GetProperty("pattern").GetString()
                ?? throw new ArgumentException("pattern is required");

            if (root.TryGetProperty("mode", out var modeProp))
            {
                mode = modeProp.GetString() ?? "grep";
            }

            if (root.TryGetProperty("path", out var pathProp))
            {
                searchPath = pathProp.GetString() ?? ".";
            }

            if (root.TryGetProperty("isRegex", out var regexProp))
            {
                isRegex = regexProp.GetBoolean();
            }

            if (root.TryGetProperty("includePattern", out var includeProp))
            {
                includePattern = includeProp.GetString();
            }

            if (
                root.TryGetProperty("maxResults", out var maxProp)
                && maxProp.ValueKind == JsonValueKind.Number
            )
            {
                maxResults = Math.Clamp(maxProp.GetInt32(), 1, MaxResultsLimit);
            }
        }
        catch (JsonException)
        {
            pattern = input.Trim();
        }

        if (string.IsNullOrWhiteSpace(pattern))
        {
            return Task.FromResult(ToolResult.Fail("Pattern cannot be empty"));
        }

        var fullPath = Path.GetFullPath(searchPath);
        if (!Directory.Exists(fullPath))
        {
            return Task.FromResult(ToolResult.Fail($"Directory not found: {fullPath}"));
        }

        try
        {
            return mode.Equals("glob", StringComparison.OrdinalIgnoreCase)
                ? Task.FromResult(GlobSearch(fullPath, pattern, maxResults))
                : Task.FromResult(
                    GrepSearch(fullPath, pattern, isRegex, includePattern, maxResults)
                );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed: {Pattern}", pattern);
            return Task.FromResult(ToolResult.Fail($"Search failed: {ex.Message}"));
        }
    }

    private ToolResult GrepSearch(
        string directory,
        string pattern,
        bool isRegex,
        string? includePattern,
        int maxResults
    )
    {
        _logger.LogDebug(
            "Grep search: pattern={Pattern}, isRegex={IsRegex}, dir={Dir}",
            pattern,
            isRegex,
            directory
        );

        Regex? regex = null;
        if (isRegex)
        {
            try
            {
                regex = new Regex(
                    pattern,
                    RegexOptions.Compiled | RegexOptions.IgnoreCase,
                    TimeSpan.FromSeconds(2)
                );
            }
            catch (ArgumentException ex)
            {
                return ToolResult.Fail($"Invalid regex pattern: {ex.Message}");
            }
        }

        var results = new List<GrepResult>();
        var searchPattern = includePattern ?? "*";

        var files = Directory
            .EnumerateFiles(
                directory,
                searchPattern,
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    MatchCasing = MatchCasing.CaseInsensitive,
                }
            )
            .Where(f => !IsIgnoredPath(f));

        foreach (var file in files)
        {
            if (results.Count >= maxResults)
            {
                break;
            }

            try
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var isMatch =
                        regex != null
                            ? regex.IsMatch(lines[i])
                            : lines[i].Contains(pattern, StringComparison.OrdinalIgnoreCase);

                    if (isMatch)
                    {
                        results.Add(
                            new GrepResult
                            {
                                FilePath = Path.GetRelativePath(directory, file),
                                LineNumber = i + 1,
                                LineContent = lines[i].TrimEnd(),
                            }
                        );

                        if (results.Count >= maxResults)
                        {
                            break;
                        }
                    }
                }
            }
            catch
            {
                // Skip files that can't be read (binary, locked, etc.)
            }
        }

        if (results.Count == 0)
        {
            return ToolResult.Ok("No matches found.");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Found {results.Count} match(es):");
        sb.AppendLine();

        string? lastFile = null;
        foreach (var result in results)
        {
            if (result.FilePath != lastFile)
            {
                if (lastFile != null)
                {
                    sb.AppendLine();
                }

                sb.AppendLine($"--- {result.FilePath} ---");
                lastFile = result.FilePath;
            }

            sb.AppendLine($"  {result.LineNumber}: {result.LineContent}");
        }

        if (results.Count >= maxResults)
        {
            sb.AppendLine();
            sb.Append($"[Results truncated at {maxResults}. Use maxResults to see more.]");
        }

        return ToolResult.Ok(sb.ToString());
    }

    private ToolResult GlobSearch(string directory, string pattern, int maxResults)
    {
        _logger.LogDebug("Glob search: pattern={Pattern}, dir={Dir}", pattern, directory);

        var files = Directory
            .EnumerateFiles(
                directory,
                pattern,
                new EnumerationOptions
                {
                    RecurseSubdirectories = pattern.Contains("**"),
                    IgnoreInaccessible = true,
                    MatchCasing = MatchCasing.CaseInsensitive,
                }
            )
            .Where(f => !IsIgnoredPath(f))
            .Take(maxResults)
            .Select(f => Path.GetRelativePath(directory, f))
            .OrderBy(f => f)
            .ToList();

        if (files.Count == 0)
        {
            return ToolResult.Ok("No files found matching the pattern.");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Found {files.Count} file(s):");

        foreach (var file in files)
        {
            sb.AppendLine($"  {file}");
        }

        if (files.Count >= maxResults)
        {
            sb.AppendLine();
            sb.Append($"[Results truncated at {maxResults}. Use maxResults to see more.]");
        }

        return ToolResult.Ok(sb.ToString());
    }

    private static bool IsIgnoredPath(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Skip common non-project directories
        return segments.Any(s =>
            s
                is ".git"
                    or "node_modules"
                    or "bin"
                    or "obj"
                    or ".vs"
                    or ".idea"
                    or "__pycache__"
                    or ".mypy_cache"
        );
    }

    private class GrepResult
    {
        public required string FilePath { get; init; }
        public required int LineNumber { get; init; }
        public required string LineContent { get; init; }
    }
}
