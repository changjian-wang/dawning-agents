using System.Reflection;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Tools;

/// <summary>
/// Tool scanner — scans and registers methods marked with <see cref="FunctionToolAttribute"/> from types or assemblies.
/// </summary>
public sealed class ToolScanner
{
    private readonly ILogger<ToolScanner> _logger;

    public ToolScanner(ILogger<ToolScanner>? logger = null)
    {
        _logger = logger ?? NullLogger<ToolScanner>.Instance;
    }

    /// <summary>
    /// Scans tools from an object instance.
    /// </summary>
    /// <param name="instance">The object instance containing <see cref="FunctionToolAttribute"/> methods.</param>
    /// <returns>The list of discovered tools.</returns>
    public IEnumerable<ITool> ScanInstance(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var type = instance.GetType();
        _logger.LogDebug("Scanning tools in type {Type}", type.Name);

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = method.GetCustomAttribute<FunctionToolAttribute>();
            if (attr == null)
            {
                continue;
            }

            _logger.LogDebug("Discovered tool method: {Method}", method.Name);
            yield return new MethodTool(method, instance, attr);
        }
    }

    /// <summary>
    /// Scans static tool methods from a type.
    /// </summary>
    /// <param name="type">The type containing <see cref="FunctionToolAttribute"/> static methods.</param>
    /// <returns>The list of discovered tools.</returns>
    public IEnumerable<ITool> ScanType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        _logger.LogDebug("Scanning static tools in type {Type}", type.Name);

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            var attr = method.GetCustomAttribute<FunctionToolAttribute>();
            if (attr == null)
            {
                continue;
            }

            _logger.LogDebug("Discovered static tool method: {Method}", method.Name);
            yield return new MethodTool(method, null, attr);
        }
    }

    /// <summary>
    /// Scans all tools from an assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="serviceProvider">The service provider (used for creating instances).</param>
    /// <returns>The list of discovered tools.</returns>
    public IEnumerable<ITool> ScanAssembly(
        Assembly assembly,
        IServiceProvider? serviceProvider = null
    )
    {
        ArgumentNullException.ThrowIfNull(assembly);

        _logger.LogDebug("Scanning tools in assembly {Assembly}", assembly.GetName().Name);

        foreach (var type in assembly.GetExportedTypes())
        {
            // Scan static methods
            foreach (var tool in ScanType(type))
            {
                yield return tool;
            }

            // Scan instance methods (requires instantiable types)
            if (!type.IsAbstract && !type.IsInterface && HasInstanceToolMethods(type))
            {
                object? instance = null;

                // Try to resolve from the DI container
                if (serviceProvider != null)
                {
                    instance = serviceProvider.GetService(type);
                }

                // Try to create a parameterless instance
                if (instance == null && type.GetConstructor(Type.EmptyTypes) != null)
                {
                    try
                    {
                        instance = Activator.CreateInstance(type);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            "Failed to create instance of type {Type}: {Error}",
                            type.Name,
                            ex.Message
                        );
                    }
                }

                if (instance != null)
                {
                    foreach (var tool in ScanInstance(instance))
                    {
                        yield return tool;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks whether a type contains instance tool methods.
    /// </summary>
    private static bool HasInstanceToolMethods(Type type)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Any(m => m.GetCustomAttribute<FunctionToolAttribute>() != null);
    }
}
