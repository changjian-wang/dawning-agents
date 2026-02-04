using Dawning.Agents.Abstractions.Workflow;
using Dawning.Agents.Core.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core;

/// <summary>
/// 工作流服务 DI 扩展
/// </summary>
public static class WorkflowServiceCollectionExtensions
{
    /// <summary>
    /// 添加工作流引擎
    /// </summary>
    public static IServiceCollection AddWorkflowEngine(this IServiceCollection services)
    {
        services.TryAddSingleton<IWorkflowEngine, WorkflowEngine>();
        services.TryAddSingleton<IWorkflowSerializer, WorkflowSerializer>();
        services.TryAddSingleton<IWorkflowVisualizer, WorkflowVisualizer>();

        return services;
    }

    /// <summary>
    /// 添加工作流序列化器
    /// </summary>
    public static IServiceCollection AddWorkflowSerializer(this IServiceCollection services)
    {
        services.TryAddSingleton<IWorkflowSerializer, WorkflowSerializer>();
        return services;
    }

    /// <summary>
    /// 添加工作流可视化器
    /// </summary>
    public static IServiceCollection AddWorkflowVisualizer(this IServiceCollection services)
    {
        services.TryAddSingleton<IWorkflowVisualizer, WorkflowVisualizer>();
        return services;
    }

    /// <summary>
    /// 注册预定义工作流
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
    /// 注册工作流（使用构建器）
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
