using System.Collections.Concurrent;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Tools;

/// <summary>
/// Thread-safe tool registry implementation.
/// </summary>
public sealed class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, ITool> _tools = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly ILogger<ToolRegistry> _logger;

    // Cache; invalidated on tool register/remove
    private volatile IReadOnlyList<ITool>? _cachedAllTools;
    private volatile IReadOnlyList<string>? _cachedCategories;
    private readonly Lock _cacheLock = new();

    public ToolRegistry(ILogger<ToolRegistry>? logger = null)
    {
        _logger = logger ?? NullLogger<ToolRegistry>.Instance;
    }

    /// <summary>
    /// Registers a tool.
    /// </summary>
    /// <param name="tool">The tool to register.</param>
    public void Register(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        if (_tools.TryGetValue(tool.Name, out var existing))
        {
            _logger.LogWarning("Tool '{Name}' already exists and will be overwritten", tool.Name);
        }

        _tools[tool.Name] = tool;
        InvalidateCache();
        _logger.LogDebug("Registered tool: {Name} - {Description}", tool.Name, tool.Description);
    }

    /// <summary>
    /// Gets a tool by name.
    /// </summary>
    /// <param name="name">The tool name (case-insensitive).</param>
    /// <returns>The tool instance, or <see langword="null"/> if not found.</returns>
    public ITool? GetTool(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _tools.GetValueOrDefault(name);
    }

    /// <summary>
    /// Gets all registered tools (with caching).
    /// </summary>
    public IReadOnlyList<ITool> GetAllTools()
    {
        var cached = _cachedAllTools;
        if (cached is not null)
        {
            return cached;
        }

        lock (_cacheLock)
        {
            return _cachedAllTools ??= _tools.Values.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Checks whether a tool is registered.
    /// </summary>
    /// <param name="name">The tool name.</param>
    public bool HasTool(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _tools.ContainsKey(name);
    }

    /// <summary>
    /// Gets the number of registered tools.
    /// </summary>
    public int Count => _tools.Count;

    /// <summary>
    /// Gets tools by category.
    /// </summary>
    /// <param name="category">The tool category.</param>
    /// <returns>All tools in the specified category.</returns>
    public IReadOnlyList<ITool> GetToolsByCategory(string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        return _tools
            .Values.Where(t =>
                string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase)
            )
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets all tool categories (with caching).
    /// </summary>
    /// <returns>A list of category names.</returns>
    public IReadOnlyList<string> GetCategories()
    {
        var cached = _cachedCategories;
        if (cached is not null)
        {
            return cached;
        }

        lock (_cacheLock)
        {
            return _cachedCategories ??= _tools
                .Values.Where(t => !string.IsNullOrWhiteSpace(t.Category))
                .Select(t => t.Category!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Scans and registers all methods marked with <see cref="FunctionToolAttribute"/> from a type.
    /// </summary>
    /// <typeparam name="T">The tool class type.</typeparam>
    public void RegisterToolsFromType<T>()
        where T : class, new()
    {
        var scanner = new ToolScanner();
        var instance = new T();
        foreach (var tool in scanner.ScanInstance(instance))
        {
            Register(tool);
        }
    }

    /// <summary>
    /// Scans and registers all methods marked with <see cref="FunctionToolAttribute"/> from an instance.
    /// </summary>
    /// <param name="instance">The tool class instance.</param>
    public void RegisterToolsFromInstance(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var scanner = new ToolScanner();
        foreach (var tool in scanner.ScanInstance(instance))
        {
            Register(tool);
        }
    }

    /// <summary>
    /// Invalidates the cache.
    /// </summary>
    private void InvalidateCache()
    {
        _cachedAllTools = null;
        _cachedCategories = null;
    }
}
