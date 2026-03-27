using System.Text.Json;
using System.Text.Json.Serialization;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Tools.Core;

/// <summary>
/// File system-based persistent tool store.
/// </summary>
/// <remarks>
/// <para>User tools are stored in the ~/.dawning/tools/ directory.</para>
/// <para>Global tools are stored in the {project}/.dawning/tools/ directory.</para>
/// <para>Tool file format: {name}.tool.json</para>
/// </remarks>
public sealed class FileToolStore : IToolStore
{
    private const string _toolFileExtension = ".tool.json";
    private readonly string _userToolsPath;
    private readonly string _globalToolsPath;
    private readonly ILogger<FileToolStore> _logger;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    /// Creates a <see cref="FileToolStore"/>.
    /// </summary>
    /// <param name="globalToolsBasePath">Project root directory (base path for Global tools).</param>
    /// <param name="logger">The logger.</param>
    public FileToolStore(string? globalToolsBasePath = null, ILogger<FileToolStore>? logger = null)
    {
        _userToolsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".dawning",
            "tools"
        );

        _globalToolsPath = Path.Combine(
            globalToolsBasePath ?? Directory.GetCurrentDirectory(),
            ".dawning",
            "tools"
        );

        _logger = logger ?? NullLogger<FileToolStore>.Instance;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<EphemeralToolDefinition>> LoadToolsAsync(
        ToolScope scope,
        CancellationToken cancellationToken = default
    )
    {
        ValidateScope(scope);
        var dir = GetDirectoryForScope(scope);

        if (!Directory.Exists(dir))
        {
            return Task.FromResult<IReadOnlyList<EphemeralToolDefinition>>(
                Array.Empty<EphemeralToolDefinition>()
            );
        }

        var tools = new List<EphemeralToolDefinition>();

        foreach (var file in Directory.EnumerateFiles(dir, $"*{_toolFileExtension}"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var definition = JsonSerializer.Deserialize<EphemeralToolDefinition>(
                    json,
                    s_jsonOptions
                );

                if (definition != null)
                {
                    definition.Scope = scope;
                    tools.Add(definition);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load tool from {File}", file);
            }
        }

        _logger.LogDebug("Loaded {Count} tools from {Scope}", tools.Count, scope);
        return Task.FromResult<IReadOnlyList<EphemeralToolDefinition>>(tools.AsReadOnly());
    }

    /// <inheritdoc />
    public Task SaveToolAsync(
        EphemeralToolDefinition definition,
        ToolScope scope,
        CancellationToken cancellationToken = default
    )
    {
        ValidateScope(scope);
        ArgumentNullException.ThrowIfNull(definition);

        var dir = GetDirectoryForScope(scope);
        Directory.CreateDirectory(dir);

        var filePath = GetToolFilePath(dir, definition.Name);
        var json = JsonSerializer.Serialize(definition, s_jsonOptions);
        File.WriteAllText(filePath, json);

        _logger.LogInformation(
            "Saved tool '{Name}' to {Scope} ({Path})",
            definition.Name,
            scope,
            filePath
        );

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteToolAsync(
        string name,
        ToolScope scope,
        CancellationToken cancellationToken = default
    )
    {
        ValidateScope(scope);

        var dir = GetDirectoryForScope(scope);
        var filePath = GetToolFilePath(dir, name);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("Deleted tool '{Name}' from {Scope}", name, scope);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(
        string name,
        ToolScope scope,
        CancellationToken cancellationToken = default
    )
    {
        ValidateScope(scope);

        var dir = GetDirectoryForScope(scope);
        var filePath = GetToolFilePath(dir, name);

        return Task.FromResult(File.Exists(filePath));
    }

    /// <inheritdoc />
    public async Task UpdateToolAsync(
        EphemeralToolDefinition definition,
        ToolScope scope,
        CancellationToken cancellationToken = default
    )
    {
        ValidateScope(scope);
        ArgumentNullException.ThrowIfNull(definition);

        var dir = GetDirectoryForScope(scope);
        var filePath = GetToolFilePath(dir, definition.Name);

        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException(
                $"Tool '{definition.Name}' does not exist in {scope} scope"
            );
        }

        // Auto-increment version metadata
        definition.Metadata.RevisionCount++;
        definition.Metadata.Version++;
        definition.Metadata.LastRevisedAt = DateTimeOffset.UtcNow;

        await SaveToolAsync(definition, scope, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Updated tool '{Name}' in {Scope} (v{Version})",
            definition.Name,
            scope,
            definition.Metadata.Version
        );
    }

    private string GetDirectoryForScope(ToolScope scope)
    {
        return scope switch
        {
            ToolScope.User => _userToolsPath,
            ToolScope.Global => _globalToolsPath,
            _ => throw new ArgumentException($"Unsupported scope: {scope}", nameof(scope)),
        };
    }

    private static string GetToolFilePath(string directory, string name)
    {
        var fullDir = Path.GetFullPath(directory);
        var filePath = Path.GetFullPath(Path.Combine(directory, $"{name}{_toolFileExtension}"));
        if (!filePath.StartsWith(fullDir, StringComparison.Ordinal))
        {
            throw new ArgumentException("Invalid tool name: path traversal detected", nameof(name));
        }

        return filePath;
    }

    private static void ValidateScope(ToolScope scope)
    {
        if (scope == ToolScope.Session)
        {
            throw new ArgumentException(
                "Session scope tools are stored in memory, not on disk",
                nameof(scope)
            );
        }
    }
}
