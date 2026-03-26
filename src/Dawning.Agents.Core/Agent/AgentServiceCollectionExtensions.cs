using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Core.Agent;
using Dawning.Agents.Core.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core;

/// <summary>
/// Agent service registration extensions.
/// </summary>
public static class AgentServiceCollectionExtensions
{
    /// <summary>
    /// Adds ReAct Agent services (text parsing-based agent).
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddReActAgent(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<AgentOptions>(configuration, AgentOptions.SectionName);

        // Ensure ToolRegistry is registered (optional agent dependency)
        services.AddToolRegistry();

        services.TryAddScoped<IAgent, ReActAgent>();

        return services;
    }

    /// <summary>
    /// Adds ReAct Agent services (using a configuration delegate).
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Configuration delegate.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddReActAgent(
        this IServiceCollection services,
        Action<AgentOptions> configure
    )
    {
        services.AddValidatedOptions(configure);

        // Ensure ToolRegistry is registered (optional agent dependency)
        services.AddToolRegistry();

        services.TryAddScoped<IAgent, ReActAgent>();

        return services;
    }

    /// <summary>
    /// Adds Function Calling Agent services (LLM native tool calling-based agent).
    /// </summary>
    /// <remarks>
    /// <para>Uses LLM native Function Calling, which is more reliable than ReActAgent's text parsing.</para>
    /// <para>Requires an LLM Provider that supports Function Calling (OpenAI, Azure OpenAI, Ollama, etc.).</para>
    /// </remarks>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddFunctionCallingAgent(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<AgentOptions>(configuration, AgentOptions.SectionName);

        services.AddToolRegistry();

        services.TryAddScoped<IAgent, FunctionCallingAgent>();

        return services;
    }

    /// <summary>
    /// Adds Function Calling Agent services (using a configuration delegate).
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Configuration delegate.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddFunctionCallingAgent(
        this IServiceCollection services,
        Action<AgentOptions> configure
    )
    {
        services.AddValidatedOptions(configure);

        services.AddToolRegistry();

        services.TryAddScoped<IAgent, FunctionCallingAgent>();

        return services;
    }

    /// <summary>
    /// Adds the reflection engine (LLM-based tool failure diagnosis and repair).
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Reflection configuration (optional).</param>
    public static IServiceCollection AddReflectionEngine(
        this IServiceCollection services,
        Action<ReflectionOptions>? configure = null
    )
    {
        if (configure != null)
        {
            services.AddValidatedOptions(configure);
        }
        else
        {
            services.AddValidatedOptions<ReflectionOptions>(o => o.Enabled = true);
        }

        services.TryAddSingleton<IReflectionEngine, LLMReflectionEngine>();
        return services;
    }

    /// <summary>
    /// Adds the agent checkpoint service (in-memory implementation, suitable for development and testing).
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAgentCheckpoint(this IServiceCollection services)
    {
        services.TryAddSingleton<IAgentCheckpoint, InMemoryAgentCheckpoint>();
        return services;
    }
}
