using System.Reflection;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core.Tools;

/// <summary>
/// Tools 系统的 DI 扩展方法
/// </summary>
public static class ToolServiceCollectionExtensions
{
    /// <summary>
    /// 添加工具注册表（单例）
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
    /// 注册 6 个核心工具 + 工具基础设施（IToolSandbox, IToolSession, IToolStore）
    /// </summary>
    /// <remarks>
    /// <para>核心工具: read_file, write_file, edit_file, search, bash, create_tool</para>
    /// <para>基础设施: ToolSandbox, ToolSession, FileToolStore</para>
    /// </remarks>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">沙箱选项配置（可选）</param>
    public static IServiceCollection AddCoreTools(
        this IServiceCollection services,
        Action<ToolSandboxOptions>? configureOptions = null
    )
    {
        services.AddToolRegistry();

        // 配置沙箱选项
        var sandboxOptions = new ToolSandboxOptions();
        configureOptions?.Invoke(sandboxOptions);
        services.AddSingleton(sandboxOptions);

        // 基础设施
        services.TryAddSingleton<IToolSandbox, ToolSandbox>();
        services.TryAddSingleton<IToolStore, FileToolStore>();
        services.TryAddScoped<IToolSession, ToolSession>();

        // 核心工具注册
        services.AddSingleton<IToolRegistration>(sp =>
        {
            var registry = sp.GetRequiredService<IToolRegistry>();
            var sandbox = sp.GetRequiredService<IToolSandbox>();
            var options = sp.GetRequiredService<ToolSandboxOptions>();

            // 1. read_file
            registry.Register(
                new ReadFileTool(
                    sp.GetService<Microsoft.Extensions.Logging.ILogger<ReadFileTool>>()
                )
            );

            // 2. write_file
            registry.Register(
                new WriteFileTool(
                    sp.GetService<Microsoft.Extensions.Logging.ILogger<WriteFileTool>>()
                )
            );

            // 3. edit_file
            registry.Register(
                new EditFileTool(
                    sp.GetService<Microsoft.Extensions.Logging.ILogger<EditFileTool>>()
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
    /// 注册单个工具实例
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="tool">工具实例</param>
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
    /// 注册包含 [FunctionTool] 方法的工具类
    /// </summary>
    /// <typeparam name="T">工具类类型</typeparam>
    public static IServiceCollection AddToolsFrom<T>(this IServiceCollection services)
        where T : class
    {
        services.AddToolRegistry();

        // 注册工具类本身
        services.TryAddSingleton<T>();

        // 扫描并注册工具
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
    /// 从程序集扫描并注册所有 [FunctionTool] 标记的方法
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="assembly">要扫描的程序集</param>
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
    /// 添加工具审批处理器（默认实现）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="strategy">审批策略</param>
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
    /// 确保所有工具已注册（在构建 Host 后调用）
    /// </summary>
    public static IServiceProvider EnsureToolsRegistered(this IServiceProvider serviceProvider)
    {
        // 触发所有 IToolRegistration 的创建，从而触发工具注册
        _ = serviceProvider.GetServices<IToolRegistration>().ToList();
        return serviceProvider;
    }
}

/// <summary>
/// 工具注册标记接口（用于 DI 触发注册）
/// </summary>
public interface IToolRegistration
{
    string Source { get; }
}

/// <summary>
/// 工具注册实现
/// </summary>
internal class ToolRegistration : IToolRegistration
{
    public string Source { get; }

    public ToolRegistration(string source)
    {
        Source = source;
    }
}
