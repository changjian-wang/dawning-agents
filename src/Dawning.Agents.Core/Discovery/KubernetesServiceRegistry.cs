using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Dawning.Agents.Abstractions.Discovery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Discovery;

/// <summary>
/// Kubernetes 服务发现 (通过 Endpoints API)
/// </summary>
public sealed class KubernetesServiceRegistry : IServiceRegistry
{
    private readonly HttpClient _httpClient;
    private readonly KubernetesOptions _options;
    private readonly ILogger<KubernetesServiceRegistry> _logger;

    public KubernetesServiceRegistry(
        HttpClient httpClient,
        IOptions<KubernetesOptions>? options = null,
        ILogger<KubernetesServiceRegistry>? logger = null)
    {
        _httpClient = httpClient;
        _options = options?.Value ?? new KubernetesOptions();
        _logger = logger ?? NullLogger<KubernetesServiceRegistry>.Instance;
    }

    public Task RegisterAsync(ServiceInstance instance, CancellationToken cancellationToken = default)
    {
        // Kubernetes 通过 Pod 自动注册，此处为空实现
        _logger.LogDebug("Kubernetes 模式下服务由 K8s 自动管理，跳过手动注册");
        return Task.CompletedTask;
    }

    public Task DeregisterAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        // Kubernetes 通过 Pod 自动注销
        _logger.LogDebug("Kubernetes 模式下服务由 K8s 自动管理，跳过手动注销");
        return Task.CompletedTask;
    }

    public Task HeartbeatAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        // Kubernetes 使用 liveness/readiness probe
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ServiceInstance>> GetInstancesAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_options.ApiServerUrl}/api/v1/namespaces/{_options.Namespace}/endpoints/{serviceName}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("获取 Endpoints 失败: {StatusCode}", response.StatusCode);
                return Array.Empty<ServiceInstance>();
            }

            var endpoints = await response.Content.ReadFromJsonAsync<KubernetesEndpoints>(
                cancellationToken: cancellationToken);

            if (endpoints?.Subsets == null)
            {
                return Array.Empty<ServiceInstance>();
            }

            var instances = new List<ServiceInstance>();
            foreach (var subset in endpoints.Subsets)
            {
                if (subset.Addresses == null)
                {
                    continue;
                }

                var port = subset.Ports?.FirstOrDefault()?.Port ?? 80;
                foreach (var address in subset.Addresses)
                {
                    instances.Add(new ServiceInstance
                    {
                        Id = $"{serviceName}-{address.Ip}:{port}",
                        ServiceName = serviceName,
                        Host = address.Ip,
                        Port = port,
                        IsHealthy = true
                    });
                }
            }

            _logger.LogDebug("发现 {Count} 个 {ServiceName} 实例", instances.Count, serviceName);
            return instances;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Kubernetes Endpoints 失败: {ServiceName}", serviceName);
            return Array.Empty<ServiceInstance>();
        }
    }

    public async Task<IReadOnlyList<string>> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_options.ApiServerUrl}/api/v1/namespaces/{_options.Namespace}/services";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<string>();
            }

            var services = await response.Content.ReadFromJsonAsync<KubernetesServiceList>(
                cancellationToken: cancellationToken);

            return services?.Items?.Select(s => s.Metadata?.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList()
                   ?? (IReadOnlyList<string>)Array.Empty<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Kubernetes Services 列表失败");
            return Array.Empty<string>();
        }
    }

    public async IAsyncEnumerable<ServiceInstance[]> WatchAsync(
        string serviceName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 简化实现: 轮询模式
        while (!cancellationToken.IsCancellationRequested)
        {
            var instances = await GetInstancesAsync(serviceName, cancellationToken);
            yield return instances.ToArray();
            await Task.Delay(TimeSpan.FromSeconds(_options.WatchIntervalSeconds), cancellationToken);
        }
    }

    #region Kubernetes API Models

    private sealed class KubernetesEndpoints
    {
        [JsonPropertyName("subsets")]
        public List<EndpointSubset>? Subsets { get; set; }
    }

    private sealed class EndpointSubset
    {
        [JsonPropertyName("addresses")]
        public List<EndpointAddress>? Addresses { get; set; }

        [JsonPropertyName("ports")]
        public List<EndpointPort>? Ports { get; set; }
    }

    private sealed class EndpointAddress
    {
        [JsonPropertyName("ip")]
        public string Ip { get; set; } = "";
    }

    private sealed class EndpointPort
    {
        [JsonPropertyName("port")]
        public int Port { get; set; }
    }

    private sealed class KubernetesServiceList
    {
        [JsonPropertyName("items")]
        public List<KubernetesService>? Items { get; set; }
    }

    private sealed class KubernetesService
    {
        [JsonPropertyName("metadata")]
        public KubernetesMetadata? Metadata { get; set; }
    }

    private sealed class KubernetesMetadata
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    #endregion
}

/// <summary>
/// Kubernetes 配置选项
/// </summary>
public sealed class KubernetesOptions
{
    public const string SectionName = "Kubernetes";

    /// <summary>
    /// Kubernetes API Server 地址
    /// </summary>
    public string ApiServerUrl { get; set; } = "https://kubernetes.default.svc";

    /// <summary>
    /// 命名空间
    /// </summary>
    public string Namespace { get; set; } = "default";

    /// <summary>
    /// Watch 轮询间隔 (秒)
    /// </summary>
    public int WatchIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// 是否启用 (Pod 内自动检测)
    /// </summary>
    public bool Enabled { get; set; } = false;
}
