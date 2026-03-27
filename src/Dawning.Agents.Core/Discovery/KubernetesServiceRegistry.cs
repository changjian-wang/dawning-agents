using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dawning.Agents.Abstractions.Discovery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Discovery;

/// <summary>
/// Kubernetes service discovery (via the Endpoints API).
/// </summary>
public sealed class KubernetesServiceRegistry : IServiceRegistry
{
    private readonly HttpClient _httpClient;
    private readonly KubernetesOptions _options;
    private readonly ILogger<KubernetesServiceRegistry> _logger;

    public KubernetesServiceRegistry(
        HttpClient httpClient,
        IOptions<KubernetesOptions>? options = null,
        ILogger<KubernetesServiceRegistry>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _options = options?.Value ?? new KubernetesOptions();
        _logger = logger ?? NullLogger<KubernetesServiceRegistry>.Instance;
    }

    public Task RegisterAsync(
        ServiceInstance instance,
        CancellationToken cancellationToken = default
    )
    {
        // Kubernetes auto-registers via Pods; this is a no-op
        _logger.LogDebug("In Kubernetes mode, services are managed by K8s; skipping manual registration");
        return Task.CompletedTask;
    }

    public Task DeregisterAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        // Kubernetes auto-deregisters via Pods
        _logger.LogDebug("In Kubernetes mode, services are managed by K8s; skipping manual deregistration");
        return Task.CompletedTask;
    }

    public Task HeartbeatAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        // Kubernetes uses liveness/readiness probes
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ServiceInstance>> GetInstancesAsync(
        string serviceName,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var url =
                $"{_options.ApiServerUrl}/api/v1/namespaces/{Uri.EscapeDataString(_options.Namespace)}/endpoints/{Uri.EscapeDataString(serviceName)}";
            using var response = await _httpClient
                .GetAsync(url, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get Endpoints: {StatusCode}", response.StatusCode);
                return Array.Empty<ServiceInstance>();
            }

            var endpoints = await response
                .Content.ReadFromJsonAsync<KubernetesEndpoints>(
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);

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
                    instances.Add(
                        new ServiceInstance
                        {
                            Id = $"{serviceName}-{address.Ip}:{port}",
                            ServiceName = serviceName,
                            Host = address.Ip,
                            Port = port,
                            IsHealthy = true,
                        }
                    );
                }
            }

            _logger.LogDebug("Discovered {Count} {ServiceName} instances", instances.Count, serviceName);
            return instances;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Kubernetes Endpoints: {ServiceName}", serviceName);
            return Array.Empty<ServiceInstance>();
        }
    }

    public async Task<IReadOnlyList<string>> GetServicesAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var url =
                $"{_options.ApiServerUrl}/api/v1/namespaces/{Uri.EscapeDataString(_options.Namespace)}/services";
            using var response = await _httpClient
                .GetAsync(url, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<string>();
            }

            var services = await response
                .Content.ReadFromJsonAsync<KubernetesServiceList>(
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);

            return services
                    ?.Items?.Select(s => s.Metadata?.Name ?? "")
                    .Where(n => !string.IsNullOrEmpty(n))
                    .ToList()
                ?? (IReadOnlyList<string>)Array.Empty<string>();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Kubernetes Services list");
            return Array.Empty<string>();
        }
    }

    public async IAsyncEnumerable<ServiceInstance[]> WatchAsync(
        string serviceName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        // Simplified implementation: polling mode
        while (!cancellationToken.IsCancellationRequested)
        {
            ServiceInstance[] snapshot;
            try
            {
                var instances = await GetInstancesAsync(serviceName, cancellationToken)
                    .ConfigureAwait(false);
                snapshot = instances.ToArray();
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Watch error for {ServiceName}, retrying...", serviceName);
                await Task.Delay(
                        TimeSpan.FromSeconds(_options.WatchIntervalSeconds * 2),
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                continue;
            }

            yield return snapshot;
            await Task.Delay(TimeSpan.FromSeconds(_options.WatchIntervalSeconds), cancellationToken)
                .ConfigureAwait(false);
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
/// Kubernetes configuration options.
/// </summary>
public sealed class KubernetesOptions
{
    public const string SectionName = "Kubernetes";

    /// <summary>
    /// Gets or sets the Kubernetes API server URL.
    /// </summary>
    public string ApiServerUrl { get; set; } = "https://kubernetes.default.svc";

    /// <summary>
    /// Gets or sets the namespace.
    /// </summary>
    public string Namespace { get; set; } = "default";

    /// <summary>
    /// Gets or sets the watch polling interval in seconds.
    /// </summary>
    public int WatchIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets a value indicating whether Kubernetes integration is enabled (auto-detected inside a Pod).
    /// </summary>
    public bool Enabled { get; set; } = false;
}
