using System.Reflection;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Tools;

/// <summary>
/// 工具扫描器 - 从类型或程序集中扫描并注册 [FunctionTool] 标记的方法
/// </summary>
public class ToolScanner
{
    private readonly ILogger<ToolScanner> _logger;

    public ToolScanner(ILogger<ToolScanner>? logger = null)
    {
        _logger = logger ?? NullLogger<ToolScanner>.Instance;
    }

    /// <summary>
    /// 从实例对象扫描工具
    /// </summary>
    /// <param name="instance">包含 [FunctionTool] 方法的对象实例</param>
    /// <returns>扫描到的工具列表</returns>
    public IEnumerable<ITool> ScanInstance(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var type = instance.GetType();
        _logger.LogDebug("扫描类型 {Type} 中的工具", type.Name);

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = method.GetCustomAttribute<FunctionToolAttribute>();
            if (attr == null)
            {
                continue;
            }

            _logger.LogDebug("发现工具方法: {Method}", method.Name);
            yield return new MethodTool(method, instance, attr);
        }
    }

    /// <summary>
    /// 从类型扫描静态工具方法
    /// </summary>
    /// <param name="type">包含 [FunctionTool] 静态方法的类型</param>
    /// <returns>扫描到的工具列表</returns>
    public IEnumerable<ITool> ScanType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        _logger.LogDebug("扫描类型 {Type} 中的静态工具", type.Name);

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            var attr = method.GetCustomAttribute<FunctionToolAttribute>();
            if (attr == null)
            {
                continue;
            }

            _logger.LogDebug("发现静态工具方法: {Method}", method.Name);
            yield return new MethodTool(method, null, attr);
        }
    }

    /// <summary>
    /// 从程序集扫描所有工具
    /// </summary>
    /// <param name="assembly">要扫描的程序集</param>
    /// <param name="serviceProvider">服务提供者（用于创建实例）</param>
    /// <returns>扫描到的工具列表</returns>
    public IEnumerable<ITool> ScanAssembly(
        Assembly assembly,
        IServiceProvider? serviceProvider = null
    )
    {
        ArgumentNullException.ThrowIfNull(assembly);

        _logger.LogDebug("扫描程序集 {Assembly} 中的工具", assembly.GetName().Name);

        foreach (var type in assembly.GetExportedTypes())
        {
            // 扫描静态方法
            foreach (var tool in ScanType(type))
            {
                yield return tool;
            }

            // 扫描实例方法（需要能创建实例的类型）
            if (!type.IsAbstract && !type.IsInterface && HasInstanceToolMethods(type))
            {
                object? instance = null;

                // 尝试从 DI 容器获取
                if (serviceProvider != null)
                {
                    instance = serviceProvider.GetService(type);
                }

                // 尝试创建无参实例
                if (instance == null && type.GetConstructor(Type.EmptyTypes) != null)
                {
                    try
                    {
                        instance = Activator.CreateInstance(type);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            "无法创建类型 {Type} 的实例: {Error}",
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
    /// 检查类型是否包含实例工具方法
    /// </summary>
    private static bool HasInstanceToolMethods(Type type)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Any(m => m.GetCustomAttribute<FunctionToolAttribute>() != null);
    }
}
