namespace Dawning.Agents.MCP.Providers;

using Dawning.Agents.MCP.Protocol;
using Dawning.Agents.MCP.Server;
using Microsoft.Extensions.Logging;

/// <summary>
/// A file system resource provider.
/// </summary>
/// <remarks>
/// Exposes files under a specified directory as MCP resources.
/// URI format: file:///{path}
/// </remarks>
public sealed class FileSystemResourceProvider : IMCPResourceProvider
{
    private readonly string _rootPath;
    private readonly string[] _allowedExtensions;
    private readonly ILogger<FileSystemResourceProvider> _logger;

    public FileSystemResourceProvider(
        string rootPath,
        string[]? allowedExtensions = null,
        ILogger<FileSystemResourceProvider>? logger = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _rootPath = Path.GetFullPath(rootPath);
        // Ensure path ends with directory separator to prevent prefix-matching sibling directories
        if (!_rootPath.EndsWith(Path.DirectorySeparatorChar))
        {
            _rootPath += Path.DirectorySeparatorChar;
        }

        _allowedExtensions =
            allowedExtensions
            ?? [".txt", ".md", ".json", ".xml", ".yaml", ".yml", ".cs", ".py", ".js", ".ts"];
        _logger =
            logger
            ?? Microsoft
                .Extensions
                .Logging
                .Abstractions
                .NullLogger<FileSystemResourceProvider>
                .Instance;
    }

    public IEnumerable<MCPResource> GetResources()
    {
        if (!Directory.Exists(_rootPath))
        {
            _logger.LogWarning("Root path does not exist: {Path}", _rootPath);
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(_rootPath, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (!_allowedExtensions.Contains(ext))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(_rootPath, file).Replace('\\', '/');
            var uri = $"file:///{relativePath}";

            yield return new MCPResource
            {
                Uri = uri,
                Name = Path.GetFileName(file),
                Description = $"File: {relativePath}",
                MimeType = GetMimeType(ext),
            };
        }
    }

    public IEnumerable<MCPResourceTemplate> GetResourceTemplates()
    {
        yield return new MCPResourceTemplate
        {
            UriTemplate = "file:///{path}",
            Name = "File",
            Description = "Read a file from the workspace",
        };
    }

    public async Task<ResourceContent?> ReadResourceAsync(
        string uri,
        CancellationToken cancellationToken = default
    )
    {
        if (!SupportsUri(uri))
        {
            return null;
        }

        var path = GetPathFromUri(uri);
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, path));

        // Security check: ensure path is within the root directory
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!fullPath.StartsWith(_rootPath, comparison))
        {
            _logger.LogWarning("Path traversal attempt: {Uri}", uri);
            return null;
        }

        if (!File.Exists(fullPath))
        {
            _logger.LogDebug("File not found: {Path}", fullPath);
            return null;
        }

        try
        {
            var content = await File.ReadAllTextAsync(fullPath, cancellationToken)
                .ConfigureAwait(false);
            var ext = Path.GetExtension(fullPath).ToLowerInvariant();

            return new ResourceContent
            {
                Uri = uri,
                Text = content,
                MimeType = GetMimeType(ext),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file: {Path}", fullPath);
            return null;
        }
    }

    public bool SupportsUri(string uri) =>
        uri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase);

    private static string GetPathFromUri(string uri)
    {
        if (uri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
        {
            return Uri.UnescapeDataString(uri[8..]);
        }
        return uri;
    }

    private static string GetMimeType(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".yaml" or ".yml" => "application/yaml",
            ".cs" => "text/x-csharp",
            ".py" => "text/x-python",
            ".js" => "text/javascript",
            ".ts" => "text/typescript",
            ".html" => "text/html",
            ".css" => "text/css",
            _ => "text/plain",
        };
}
