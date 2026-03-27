using Dawning.Agents.Abstractions.Workflow;
using Dawning.Agents.Core.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core;

/// <summary>
/// Dependency injection extension methods for workflow services.
/// </summary>
public static class WorkflowServiceCollectionExtensions
{
    /// <summary>
    /// Adds the workflow engine and related services.
    /// </summary>
    public static IServiceCollection AddWorkflowEngine(this IServiceCollection services)
    {
        services.TryAddSingleton<IWorkflowEngine, WorkflowEngine>();
        services.TryAddSingleton<IWorkflowSerializer, WorkflowSerializer>();
        services.TryAddSingleton<IWorkflowVisualizer, WorkflowVisualizer>();

        return services;
    }

    /// <summary>
    /// Adds the workflow serializer.
    /// </summary>
    public static IServiceCollection AddWorkflowSerializer(this IServiceCollection services)
    {
        services.TryAddSingleton<IWorkflowSerializer, WorkflowSerializer>();
        return services;
    }

    /// <summary>
    /// Adds the workflow visualizer.
    /// </summary>
    public static IServiceCollection AddWorkflowVisualizer(this IServiceCollection services)
    {
        services.TryAddSingleton<IWorkflowVisualizer, WorkflowVisualizer>();
        return services;
    }

    /// <summary>
    /// Registers a predefined workflow definition.
    /// </summary>
    public static IServiceCollection AddWorkflow(
        this IServiceCollection services,
        WorkflowDefinition definition
    )
    {
        services.AddSingleton(definition);
        return services;
    }

    /// <summary>
    /// Registers a workflow definition using a builder.
    /// </summary>
    public static IServiceCollection AddWorkflow(
        this IServiceCollection services,
        string id,
        string name,
        Action<WorkflowBuilder> configure
    )
    {
        var builder = WorkflowBuilder.Create(id, name);
        configure(builder);
        var definition = builder.Build();
        services.AddSingleton(definition);
        return services;
    }
}
