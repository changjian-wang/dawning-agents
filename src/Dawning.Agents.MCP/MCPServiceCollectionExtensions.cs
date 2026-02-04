namespace Dawning.Agents.MCP;

using Dawning.Agents.MCP.Client;
using Dawning.Agents.MCP.Server;
using Dawning.Agents.MCP.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// MCP 服务注册扩展方法
/// </summary>
public static class MCPServiceCollectionExtensions
{
    #region MCP Server

    /// <summary>
    /// 注册 MCP Server
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddMCPServer(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<MCPServerOptions>(configuration.GetSection(MCPServerOptions.SectionName));
        services.TryAddSingleton<IMCPTransport, StdioTransport>();
        services.TryAddSingleton<MCPServer>();
        return services;
    }

    /// <summary>
    /// 注册 MCP Server
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddMCPServer(
        this IServiceCollection services,
        Action<MCPServerOptions> configureOptions
    )
    {
        services.Configure(configureOptions);
        services.TryAddSingleton<IMCPTransport, StdioTransport>();
        services.TryAddSingleton<MCPServer>();
        return services;
    }

    /// <summary>
    /// 注册 MCP Server（使用默认配置）
    /// </summary>
    public static IServiceCollection AddMCPServer(this IServiceCollection services)
    {
        services.Configure<MCPServerOptions>(_ => { });
        services.TryAddSingleton<IMCPTransport, StdioTransport>();
        services.TryAddSingleton<MCPServer>();
        return services;
    }

    /// <summary>
    /// 使用 Stdio 传输
    /// </summary>
    public static IServiceCollection UseMCPStdioTransport(this IServiceCollection services)
    {
        services.RemoveAll<IMCPTransport>();
        services.AddSingleton<IMCPTransport, StdioTransport>();
        return services;
    }

    /// <summary>
    /// 注册 MCP 资源提供者
    /// </summary>
    public static IServiceCollection AddMCPResourceProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IMCPResourceProvider
    {
        services.AddSingleton<IMCPResourceProvider, TProvider>();
        return services;
    }

    /// <summary>
    /// 注册 MCP 提示词提供者
    /// </summary>
    public static IServiceCollection AddMCPPromptProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IMCPPromptProvider
    {
        services.AddSingleton<IMCPPromptProvider, TProvider>();
        return services;
    }

    #endregion

    #region MCP Client

    /// <summary>
    /// 注册 MCP Client
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddMCPClient(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<MCPClientOptions>(configuration.GetSection(MCPClientOptions.SectionName));
        services.TryAddSingleton<MCPClient>();
        return services;
    }

    /// <summary>
    /// 注册 MCP Client
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddMCPClient(
        this IServiceCollection services,
        Action<MCPClientOptions> configureOptions
    )
    {
        services.Configure(configureOptions);
        services.TryAddSingleton<MCPClient>();
        return services;
    }

    /// <summary>
    /// 注册 MCP Client（使用默认配置）
    /// </summary>
    public static IServiceCollection AddMCPClient(this IServiceCollection services)
    {
        services.Configure<MCPClientOptions>(_ => { });
        services.TryAddSingleton<MCPClient>();
        return services;
    }

    #endregion
}
