using Microsoft.Extensions.DependencyInjection;

namespace Dawning.Agents.Core.Tools.BuiltIn;

/// <summary>
/// 内置工具的 DI 扩展方法
/// </summary>
public static class BuiltInToolExtensions
{
    /// <summary>
    /// 添加所有内置工具（不含高风险工具）
    /// </summary>
    /// <remarks>
    /// 包含以下工具:
    /// - DateTimeTool: 日期时间操作
    /// - MathTool: 数学计算
    /// - JsonTool: JSON 处理
    /// - UtilityTool: 通用工具（GUID、随机数、哈希、编码等）
    /// </remarks>
    public static IServiceCollection AddBuiltInTools(this IServiceCollection services)
    {
        services.AddToolsFrom<DateTimeTool>();
        services.AddToolsFrom<MathTool>();
        services.AddToolsFrom<JsonTool>();
        services.AddToolsFrom<UtilityTool>();

        return services;
    }

    /// <summary>
    /// 添加所有内置工具（包含高风险工具）
    /// </summary>
    /// <remarks>
    /// <para>除了基础工具外，还包含:</para>
    /// <list type="bullet">
    /// <item>FileSystemTool: 文件系统操作（读写、删除文件）</item>
    /// <item>HttpTool: HTTP 请求（需要 HttpClient）</item>
    /// <item>ProcessTool: 进程/命令执行</item>
    /// <item>GitTool: Git 版本控制</item>
    /// </list>
    /// <para>⚠️ 高风险操作会标记 RequiresConfirmation = true</para>
    /// </remarks>
    public static IServiceCollection AddAllBuiltInTools(this IServiceCollection services)
    {
        services.AddBuiltInTools();
        services.AddFileSystemTools();
        services.AddProcessTools();
        services.AddGitTools();

        return services;
    }

    /// <summary>
    /// 添加日期时间工具
    /// </summary>
    public static IServiceCollection AddDateTimeTools(this IServiceCollection services)
    {
        services.AddToolsFrom<DateTimeTool>();
        return services;
    }

    /// <summary>
    /// 添加数学计算工具
    /// </summary>
    public static IServiceCollection AddMathTools(this IServiceCollection services)
    {
        services.AddToolsFrom<MathTool>();
        return services;
    }

    /// <summary>
    /// 添加 JSON 处理工具
    /// </summary>
    public static IServiceCollection AddJsonTools(this IServiceCollection services)
    {
        services.AddToolsFrom<JsonTool>();
        return services;
    }

    /// <summary>
    /// 添加通用工具
    /// </summary>
    public static IServiceCollection AddUtilityTools(this IServiceCollection services)
    {
        services.AddToolsFrom<UtilityTool>();
        return services;
    }

    /// <summary>
    /// 添加文件系统工具
    /// </summary>
    /// <remarks>
    /// 包含文件读写、目录操作、文件搜索等功能。
    /// 写入和删除操作标记为需要确认。
    /// </remarks>
    public static IServiceCollection AddFileSystemTools(this IServiceCollection services)
    {
        services.AddToolsFrom<FileSystemTool>();
        return services;
    }

    /// <summary>
    /// 添加 HTTP 工具
    /// </summary>
    /// <remarks>
    /// 需要先注册 HttpClient（推荐使用 IHttpClientFactory）。
    /// POST/PUT/DELETE 操作标记为需要确认。
    /// </remarks>
    public static IServiceCollection AddHttpTools(this IServiceCollection services)
    {
        services.AddHttpClient<HttpTool>();
        services.AddToolsFrom<HttpTool>();
        return services;
    }

    /// <summary>
    /// 添加进程/命令执行工具
    /// </summary>
    /// <remarks>
    /// ⚠️ 高风险工具：所有命令执行都需要用户确认。
    /// </remarks>
    public static IServiceCollection AddProcessTools(this IServiceCollection services)
    {
        services.AddSingleton<ProcessToolOptions>();
        services.AddToolsFrom<ProcessTool>();
        return services;
    }

    /// <summary>
    /// 添加进程/命令执行工具（带配置）
    /// </summary>
    public static IServiceCollection AddProcessTools(
        this IServiceCollection services,
        Action<ProcessToolOptions> configure
    )
    {
        var options = new ProcessToolOptions();
        configure(options);
        services.AddSingleton(options);
        services.AddToolsFrom<ProcessTool>();
        return services;
    }

    /// <summary>
    /// 添加 Git 版本控制工具
    /// </summary>
    /// <remarks>
    /// 只读操作无需确认，修改操作（commit、push、checkout 等）需要确认。
    /// </remarks>
    public static IServiceCollection AddGitTools(this IServiceCollection services)
    {
        services.AddToolsFrom<GitTool>();
        return services;
    }
}
