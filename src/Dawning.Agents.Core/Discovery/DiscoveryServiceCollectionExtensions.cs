using System;
using Dawning.Agents.Abstractions.Discovery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core.Discovery;

public static class DiscoveryServiceCollectionExtensions
{
    /// <summary>
    /// 添加服务发现 (自动根据环境选择实现)
    /// </summary>
    public static IServiceCollection AddServiceDiscovery(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ServiceRegistryOptions>(
            configuration.GetSection(ServiceRegistryOptions.SectionName));
        services.Configure<KubernetesOptions>(
            configuration.GetSection(KubernetesOptions.SectionName));

        var k8sOptions = configuration.GetSection(KubernetesOptions.SectionName).Get<KubernetesOptions>();

        if (k8sOptions?.Enabled == true || IsRunningInKubernetes())
        {
            services.AddHttpClient<IServiceRegistry, KubernetesServiceRegistry>(client =>
            {
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                // 在 Pod 内使用 ServiceAccount Token
                var token = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_TOKEN");
                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                }
            });
        }
        else
        {
            services.TryAddSingleton<IServiceRegistry, InMemoryServiceRegistry>();
        }

        return services;
    }

    /// <summary>
    /// 添加内存服务注册表 (开发/测试)
    /// </summary>
    public static IServiceCollection AddInMemoryServiceRegistry(this IServiceCollection services)
    {
        services.TryAddSingleton<IServiceRegistry, InMemoryServiceRegistry>();
        return services;
    }

    /// <summary>
    /// 添加 Kubernetes 服务发现
    /// </summary>
    public static IServiceCollection AddKubernetesServiceDiscovery(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<KubernetesOptions>(
            configuration.GetSection(KubernetesOptions.SectionName));

        services.AddHttpClient<IServiceRegistry, KubernetesServiceRegistry>(client =>
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        return services;
    }

    private static bool IsRunningInKubernetes()
    {
        // 检测是否在 Kubernetes Pod 内运行
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"));
    }
}
