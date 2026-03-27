using Dawning.Agents.Abstractions.Discovery;
using Dawning.Agents.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core.Discovery;

public static class DiscoveryServiceCollectionExtensions
{
    /// <summary>
    /// Adds service discovery (automatically selects implementation based on environment).
    /// </summary>
    public static IServiceCollection AddServiceDiscovery(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<ServiceRegistryOptions>(
            configuration,
            ServiceRegistryOptions.SectionName
        );
        services.Configure<KubernetesOptions>(
            configuration.GetSection(KubernetesOptions.SectionName)
        );

        var k8sOptions = configuration
            .GetSection(KubernetesOptions.SectionName)
            .Get<KubernetesOptions>();

        if (k8sOptions?.Enabled == true || IsRunningInKubernetes())
        {
            services.AddHttpClient<IServiceRegistry, KubernetesServiceRegistry>(client =>
            {
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                // Use the ServiceAccount token when running inside a Pod
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
    /// Adds the in-memory service registry (development/testing).
    /// </summary>
    public static IServiceCollection AddInMemoryServiceRegistry(this IServiceCollection services)
    {
        services.TryAddSingleton<IServiceRegistry, InMemoryServiceRegistry>();
        return services;
    }

    /// <summary>
    /// Adds Kubernetes service discovery.
    /// </summary>
    public static IServiceCollection AddKubernetesServiceDiscovery(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<KubernetesOptions>(
            configuration.GetSection(KubernetesOptions.SectionName)
        );

        services.AddHttpClient<IServiceRegistry, KubernetesServiceRegistry>(client =>
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        return services;
    }

    private static bool IsRunningInKubernetes()
    {
        // Detect whether running inside a Kubernetes Pod
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"));
    }
}
