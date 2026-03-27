using System.Reflection;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core.Tools;

/// <summary>
/// DI extension methods for the tools system.
/// </summary>
public static class ToolServiceCollectionExtensions
{
    /// <summary>
    /// Adds the tool registry (singleton).
    /// </summary>
    public static IServiceCollection AddToolRegistry(this IServiceCollection services)
    {
        services.TryAddSingleton<ToolRegistry>();
        services.TryAddSingleton<IToolRegistry>(sp => sp.GetRequiredService<ToolRegistry>());
        services.TryAddSingleton<IToolReader>(sp => sp.GetRequiredService<ToolRegistry>());
        services.TryAddSingleton<IToolRegistrar>(sp => sp.GetRequiredService<ToolRegistry>());
        services.TryAddSingleton<ToolScanner>();
        return services;
    }

    /// <summary>
    /// Registers 6 core tools + tool infrastructure (IToolSandbox, IToolSession, IToolStore).
    /// </summary>
    /// <remarks>
    /// <para>Core tools: read_file, write_file, edit_file, search, bash, create_tool</para>
    /// <para>Infrastructure: ToolSandbox, ToolSession, FileToolStore</para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Sandbox options configuration (optional).</param>
    public static IServiceCollection AddCoreTools(
        this IServiceCollection services,
        Action<ToolSandboxOptions>? configureOptions = null
    )
    {
        services.AddToolRegistry();

        // Configure sandbox options
        var sandboxOptions = new ToolSandboxOptions();
        configureOptions?.Invoke(sandboxOptions);
        services.AddSingleton(sandboxOptions);

        // Infrastructure
        services.TryAddSingleton<IToolSandbox, ToolSandbox>();
        services.TryAddSingleton<IToolStore, FileToolStore>();
        services.TryAddScoped<IToolSession, ToolSession>();

        // Core tool registration
        services.AddSingleton<IToolRegistration>(sp =>
        {
            var registry = sp.GetRequiredService<IToolRegistry>();
            var sandbox = sp.GetRequiredService<IToolSandbox>();
            var options = sp.GetRequiredService<ToolSandboxOptions>();

            // 1. read_file
            registry.Register(
                new ReadFileTool(
                    sp.GetService<Microsoft.Extensions.Logging.ILogger<ReadFileTool>>(),
                    options.WorkingDirectory
                )
            );

            // 2. write_file
            registry.Register(
                new WriteFileTool(
                    sp.GetService<Microsoft.Extensions.Logging.ILogger<WriteFileTool>>(),
                    options.WorkingDirectory
                )
            );

            // 3. edit_file
            registry.Register(
                new EditFileTool(
                    sp.GetService<Microsoft.Extensions.Logging.ILogger<EditFileTool>>(),
                    options.WorkingDirectory
                )
            );

            // 4. search
            registry.Register(
                new SearchTool(sp.GetService<Microsoft.Extensions.Logging.ILogger<SearchTool>>())
            );

            // 5. bash
            registry.Register(
                new BashTool(
                    sandbox,
                    options,
                    sp.GetService<CommandAnalyzer>(),
                    sp.GetService<Microsoft.Extensions.Logging.ILogger<BashTool>>()
                )
            );

            // Note: create_tool is NOT registered in the global registry. It depends on
            // IToolSession (scoped) and is created by FunctionCallingAgent per scope.
            // Session ephemeral tools are also resolved via IToolSession, not the registry.

            return new ToolRegistration("CoreTools");
        });

        return services;
    }

    /// <summary>
    /// Registers a single tool instance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="tool">The tool instance.</param>
    public static IServiceCollection AddTool(this IServiceCollection services, ITool tool)
    {
        services.AddToolRegistry();

        services.AddSingleton<IToolRegistration>(sp =>
        {
            var registry = sp.GetRequiredService<IToolRegistry>();
            registry.Register(tool);
            return new ToolRegistration(tool.Name);
        });

        return services;
    }

    /// <summary>
    /// Registers a tool class containing <see cref="FunctionToolAttribute"/> methods.
    /// </summary>
    /// <typeparam name="T">The tool class type.</typeparam>
    public static IServiceCollection AddToolsFrom<T>(this IServiceCollection services)
        where T : class
    {
        services.AddToolRegistry();

        // Register the tool class itself
        services.TryAddSingleton<T>();

        // Scan and register tools
        services.AddSingleton<IToolRegistration>(sp =>
        {
            var instance = sp.GetRequiredService<T>();
            var scanner = sp.GetRequiredService<ToolScanner>();
            var registry = sp.GetRequiredService<IToolRegistry>();

            foreach (var tool in scanner.ScanInstance(instance))
            {
                registry.Register(tool);
            }

            return new ToolRegistration(typeof(T).Name);
        });

        return services;
    }

    /// <summary>
    /// Scans and registers all methods marked with <see cref="FunctionToolAttribute"/> from an assembly.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly to scan.</param>
    public static IServiceCollection AddToolsFromAssembly(
        this IServiceCollection services,
        Assembly assembly
    )
    {
        services.AddToolRegistry();

        services.AddSingleton<IToolRegistration>(sp =>
        {
            var scanner = sp.GetRequiredService<ToolScanner>();
            var registry = sp.GetRequiredService<IToolRegistry>();

            foreach (var tool in scanner.ScanAssembly(assembly, sp))
            {
                registry.Register(tool);
            }

            return new ToolRegistration(assembly.GetName().Name ?? "Unknown");
        });

        return services;
    }

    /// <summary>
    /// Adds the tool approval handler (default implementation).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="strategy">The approval strategy.</param>
    public static IServiceCollection AddToolApprovalHandler(
        this IServiceCollection services,
        ApprovalStrategy strategy = ApprovalStrategy.RiskBased
    )
    {
        services.TryAddSingleton<IToolApprovalHandler>(sp => new DefaultToolApprovalHandler(
            strategy,
            sp.GetService<Microsoft.Extensions.Logging.ILogger<DefaultToolApprovalHandler>>()
        ));
        return services;
    }

    /// <summary>
    /// Ensures all tools are registered (call after building the Host).
    /// </summary>
    public static IServiceProvider EnsureToolsRegistered(this IServiceProvider serviceProvider)
    {
        // Trigger creation of all IToolRegistration instances, which triggers tool registration
        _ = serviceProvider.GetServices<IToolRegistration>().ToList();
        return serviceProvider;
    }

    /// <summary>
    /// Adds the tool usage tracker (in-memory, singleton).
    /// </summary>
    public static IServiceCollection AddToolUsageTracking(this IServiceCollection services)
    {
        services.TryAddSingleton<IToolUsageTracker, InMemoryToolUsageTracker>();
        return services;
    }

    /// <summary>
    /// Adds the semantic skill router.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Router configuration (optional).</param>
    public static IServiceCollection AddSkillRouter(
        this IServiceCollection services,
        Action<SkillRouterOptions>? configure = null
    )
    {
        if (configure != null)
        {
            services.AddValidatedOptions(configure);
        }
        else
        {
            services.AddValidatedOptions<SkillRouterOptions>(_ => { });
        }

        services.TryAddSingleton<ISkillRouter, SemanticSkillRouter>();
        return services;
    }

    /// <summary>
    /// Adds the skill evolution policy (default implementation).
    /// </summary>
    public static IServiceCollection AddSkillEvolution(this IServiceCollection services)
    {
        services.AddToolUsageTracking();
        services.TryAddSingleton<ISkillEvolutionPolicy, DefaultSkillEvolutionPolicy>();
        return services;
    }
}

/// <summary>
/// Marker interface for tool registration (used to trigger DI registration).
/// </summary>
public interface IToolRegistration
{
    string Source { get; }
}

/// <summary>
/// Tool registration implementation.
/// </summary>
internal class ToolRegistration : IToolRegistration
{
    public string Source { get; }

    public ToolRegistration(string source)
    {
        Source = source;
    }
}
