namespace Dawning.Agents.MCP;

using Dawning.Agents.MCP.Client;
using Dawning.Agents.MCP.Server;
using Dawning.Agents.MCP.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Provides extension methods for registering MCP services.
/// </summary>
public static class MCPServiceCollectionExtensions
{
    #region MCP Server

    /// <summary>
    /// Registers the MCP Server with configuration binding.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddMCPServer(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<MCPServerOptions>()
            .Bind(configuration.GetSection(MCPServerOptions.SectionName))
            .Validate(
                options =>
                {
                    options.Validate();
                    return true;
                },
                $"Invalid {nameof(MCPServerOptions)} configuration"
            )
            .ValidateOnStart();
        services.TryAddSingleton<IMCPTransport, StdioTransport>();
        services.TryAddSingleton<MCPServer>();
        return services;
    }

    /// <summary>
    /// Registers the MCP Server with a configuration delegate.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configureOptions">A delegate to configure <see cref="MCPServerOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddMCPServer(
        this IServiceCollection services,
        Action<MCPServerOptions> configureOptions
    )
    {
        services
            .AddOptions<MCPServerOptions>()
            .Configure(configureOptions)
            .Validate(
                options =>
                {
                    options.Validate();
                    return true;
                },
                $"Invalid {nameof(MCPServerOptions)} configuration"
            )
            .ValidateOnStart();
        services.TryAddSingleton<IMCPTransport, StdioTransport>();
        services.TryAddSingleton<MCPServer>();
        return services;
    }

    /// <summary>
    /// Registers the MCP Server with default options.
    /// </summary>
    public static IServiceCollection AddMCPServer(this IServiceCollection services)
    {
        services
            .AddOptions<MCPServerOptions>()
            .Configure(_ => { })
            .Validate(
                options =>
                {
                    options.Validate();
                    return true;
                },
                $"Invalid {nameof(MCPServerOptions)} configuration"
            )
            .ValidateOnStart();
        services.TryAddSingleton<IMCPTransport, StdioTransport>();
        services.TryAddSingleton<MCPServer>();
        return services;
    }

    /// <summary>
    /// Configures the MCP Server to use stdio transport.
    /// </summary>
    public static IServiceCollection UseMCPStdioTransport(this IServiceCollection services)
    {
        services.RemoveAll<IMCPTransport>();
        services.AddSingleton<IMCPTransport, StdioTransport>();
        return services;
    }

    /// <summary>
    /// Registers an MCP resource provider.
    /// </summary>
    public static IServiceCollection AddMCPResourceProvider<TProvider>(
        this IServiceCollection services
    )
        where TProvider : class, IMCPResourceProvider
    {
        services.AddSingleton<IMCPResourceProvider, TProvider>();
        return services;
    }

    /// <summary>
    /// Registers an MCP prompt provider.
    /// </summary>
    public static IServiceCollection AddMCPPromptProvider<TProvider>(
        this IServiceCollection services
    )
        where TProvider : class, IMCPPromptProvider
    {
        services.AddSingleton<IMCPPromptProvider, TProvider>();
        return services;
    }

    #endregion

    #region MCP Client

    /// <summary>
    /// Registers the MCP Client with configuration binding.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddMCPClient(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<MCPClientOptions>()
            .Bind(configuration.GetSection(MCPClientOptions.SectionName))
            .Validate(
                options =>
                {
                    options.Validate();
                    return true;
                },
                $"Invalid {nameof(MCPClientOptions)} configuration"
            )
            .ValidateOnStart();
        services.TryAddSingleton<MCPClient>();
        return services;
    }

    /// <summary>
    /// Registers the MCP Client with a configuration delegate.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configureOptions">A delegate to configure <see cref="MCPClientOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddMCPClient(
        this IServiceCollection services,
        Action<MCPClientOptions> configureOptions
    )
    {
        services
            .AddOptions<MCPClientOptions>()
            .Configure(configureOptions)
            .Validate(
                options =>
                {
                    options.Validate();
                    return true;
                },
                $"Invalid {nameof(MCPClientOptions)} configuration"
            )
            .ValidateOnStart();
        services.TryAddSingleton<MCPClient>();
        return services;
    }

    /// <summary>
    /// Registers the MCP Client with default options.
    /// </summary>
    public static IServiceCollection AddMCPClient(this IServiceCollection services)
    {
        services
            .AddOptions<MCPClientOptions>()
            .Configure(_ => { })
            .Validate(
                options =>
                {
                    options.Validate();
                    return true;
                },
                $"Invalid {nameof(MCPClientOptions)} configuration"
            )
            .ValidateOnStart();
        services.TryAddSingleton<MCPClient>();
        return services;
    }

    #endregion
}
