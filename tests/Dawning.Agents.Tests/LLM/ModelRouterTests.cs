using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Core.ModelManagement;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Dawning.Agents.Tests.LLM;

public class CostOptimizedRouterTests
{
    private readonly Mock<ILLMProvider> _cheapProvider;
    private readonly Mock<ILLMProvider> _expensiveProvider;
    private readonly IOptions<ModelRouterOptions> _defaultOptions;

    public CostOptimizedRouterTests()
    {
        _cheapProvider = new Mock<ILLMProvider>();
        _cheapProvider.Setup(p => p.Name).Returns("gpt-4o-mini");

        _expensiveProvider = new Mock<ILLMProvider>();
        _expensiveProvider.Setup(p => p.Name).Returns("gpt-4o");

        _defaultOptions = Options.Create(new ModelRouterOptions());
    }

    [Fact]
    public void Name_ReturnsCostOptimized()
    {
        var router = new CostOptimizedRouter(
            [_cheapProvider.Object],
            _defaultOptions,
            NullLogger<CostOptimizedRouter>.Instance
        );

        router.Name.Should().Be("CostOptimized");
    }

    [Fact]
    public async Task SelectProviderAsync_SelectsCheapestProvider()
    {
        var router = new CostOptimizedRouter(
            [_cheapProvider.Object, _expensiveProvider.Object],
            _defaultOptions,
            NullLogger<CostOptimizedRouter>.Instance
        );

        var context = new ModelRoutingContext
        {
            EstimatedInputTokens = 1000,
            EstimatedOutputTokens = 500,
        };

        var selected = await router.SelectProviderAsync(context);

        // gpt-4o-mini is cheaper than gpt-4o
        selected.Name.Should().Be("gpt-4o-mini");
    }

    [Fact]
    public async Task SelectProviderAsync_WithPreferredModel_SelectsPreferred()
    {
        var router = new CostOptimizedRouter(
            [_cheapProvider.Object, _expensiveProvider.Object],
            _defaultOptions,
            NullLogger<CostOptimizedRouter>.Instance
        );

        var context = new ModelRoutingContext
        {
            EstimatedInputTokens = 1000,
            EstimatedOutputTokens = 500,
            PreferredModel = "gpt-4o",
        };

        var selected = await router.SelectProviderAsync(context);

        selected.Name.Should().Be("gpt-4o");
    }

    [Fact]
    public async Task SelectProviderAsync_NoHealthyProviders_ThrowsException()
    {
        var unhealthyProvider = new Mock<ILLMProvider>();
        unhealthyProvider.Setup(p => p.Name).Returns("unhealthy");

        var router = new CostOptimizedRouter(
            [unhealthyProvider.Object],
            _defaultOptions,
            NullLogger<CostOptimizedRouter>.Instance
        );

        // Report multiple failures to make the provider unhealthy
        // UnhealthyThreshold defaults to 3, so 3 consecutive failures needed
        for (int i = 0; i < 5; i++)
        {
            router.ReportResult(unhealthyProvider.Object, ModelCallResult.Failed("error", 0));
        }

        var context = new ModelRoutingContext
        {
            EstimatedInputTokens = 100,
            EstimatedOutputTokens = 100,
        };

        // All providers are unhealthy, should throw exception
        var act = () => router.SelectProviderAsync(context);
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*No healthy providers*");
    }

    [Fact]
    public void GetAvailableProviders_ReturnsAllProviders()
    {
        var router = new CostOptimizedRouter(
            [_cheapProvider.Object, _expensiveProvider.Object],
            _defaultOptions,
            NullLogger<CostOptimizedRouter>.Instance
        );

        router.GetAvailableProviders().Should().HaveCount(2);
    }

    [Fact]
    public void ReportResult_UpdatesStatistics()
    {
        var router = new CostOptimizedRouter(
            [_cheapProvider.Object],
            _defaultOptions,
            NullLogger<CostOptimizedRouter>.Instance
        );

        router.ReportResult(
            _cheapProvider.Object,
            ModelCallResult.Succeeded(100, 500, 200, 0.001m)
        );

        router.ReportResult(
            _cheapProvider.Object,
            ModelCallResult.Succeeded(200, 600, 300, 0.002m)
        );

        // Verify statistics updated (indirectly via internal GetHealthyProviders method)
        router.GetAvailableProviders().Should().Contain(_cheapProvider.Object);
    }
}

public class LatencyOptimizedRouterTests
{
    private readonly Mock<ILLMProvider> _fastProvider;
    private readonly Mock<ILLMProvider> _slowProvider;
    private readonly IOptions<ModelRouterOptions> _defaultOptions;

    public LatencyOptimizedRouterTests()
    {
        _fastProvider = new Mock<ILLMProvider>();
        _fastProvider.Setup(p => p.Name).Returns("ollama-fast");

        _slowProvider = new Mock<ILLMProvider>();
        _slowProvider.Setup(p => p.Name).Returns("gpt-4-slow");

        _defaultOptions = Options.Create(new ModelRouterOptions());
    }

    [Fact]
    public void Name_ReturnsLatencyOptimized()
    {
        var router = new LatencyOptimizedRouter(
            [_fastProvider.Object],
            _defaultOptions,
            NullLogger<LatencyOptimizedRouter>.Instance
        );

        router.Name.Should().Be("LatencyOptimized");
    }

    [Fact]
    public async Task SelectProviderAsync_SelectsFastestByDefault()
    {
        var router = new LatencyOptimizedRouter(
            [_fastProvider.Object, _slowProvider.Object],
            _defaultOptions,
            NullLogger<LatencyOptimizedRouter>.Instance
        );

        var context = new ModelRoutingContext
        {
            EstimatedInputTokens = 1000,
            EstimatedOutputTokens = 500,
        };

        var selected = await router.SelectProviderAsync(context);

        // Ollama local model has lowest estimated latency by default
        selected.Name.Should().Contain("ollama");
    }

    [Fact]
    public async Task SelectProviderAsync_UsesHistoricalLatency()
    {
        var router = new LatencyOptimizedRouter(
            [_fastProvider.Object, _slowProvider.Object],
            _defaultOptions,
            NullLogger<LatencyOptimizedRouter>.Instance
        );

        // Report that slow provider is actually faster
        for (int i = 0; i < 5; i++)
        {
            router.ReportResult(
                _slowProvider.Object,
                ModelCallResult.Succeeded(50, 100, 100, 0.001m)
            );
            router.ReportResult(
                _fastProvider.Object,
                ModelCallResult.Succeeded(500, 100, 100, 0.001m)
            );
        }

        var context = new ModelRoutingContext
        {
            EstimatedInputTokens = 1000,
            EstimatedOutputTokens = 500,
        };

        var selected = await router.SelectProviderAsync(context);

        // Should select the actually faster one based on historical data
        selected.Name.Should().Be("gpt-4-slow");
    }

    [Fact]
    public async Task SelectProviderAsync_WithMaxLatency_FiltersProviders()
    {
        var router = new LatencyOptimizedRouter(
            [_fastProvider.Object, _slowProvider.Object],
            _defaultOptions,
            NullLogger<LatencyOptimizedRouter>.Instance
        );

        // Report latency
        router.ReportResult(_fastProvider.Object, ModelCallResult.Succeeded(100, 100, 100, 0.001m));
        router.ReportResult(
            _slowProvider.Object,
            ModelCallResult.Succeeded(2000, 100, 100, 0.001m)
        );

        var context = new ModelRoutingContext
        {
            EstimatedInputTokens = 100,
            EstimatedOutputTokens = 100,
            MaxLatencyMs = 500,
        };

        var selected = await router.SelectProviderAsync(context);

        selected.Name.Should().Be("ollama-fast");
    }
}

public class LoadBalancedRouterTests
{
    private readonly Mock<ILLMProvider> _provider1;
    private readonly Mock<ILLMProvider> _provider2;
    private readonly Mock<ILLMProvider> _provider3;

    public LoadBalancedRouterTests()
    {
        _provider1 = new Mock<ILLMProvider>();
        _provider1.Setup(p => p.Name).Returns("provider-1");

        _provider2 = new Mock<ILLMProvider>();
        _provider2.Setup(p => p.Name).Returns("provider-2");

        _provider3 = new Mock<ILLMProvider>();
        _provider3.Setup(p => p.Name).Returns("provider-3");
    }

    [Fact]
    public void Name_IncludesStrategy()
    {
        var options = Options.Create(
            new ModelRouterOptions { Strategy = ModelRoutingStrategy.RoundRobin }
        );

        var router = new LoadBalancedRouter(
            [_provider1.Object],
            options,
            NullLogger<LoadBalancedRouter>.Instance
        );

        router.Name.Should().Contain("RoundRobin");
    }

    [Fact]
    public async Task SelectProviderAsync_RoundRobin_DistributesEvenly()
    {
        var options = Options.Create(
            new ModelRouterOptions { Strategy = ModelRoutingStrategy.RoundRobin }
        );

        var router = new LoadBalancedRouter(
            [_provider1.Object, _provider2.Object, _provider3.Object],
            options,
            NullLogger<LoadBalancedRouter>.Instance
        );

        var context = new ModelRoutingContext();
        var selections = new List<string>();

        // Select 9 times
        for (int i = 0; i < 9; i++)
        {
            var selected = await router.SelectProviderAsync(context);
            selections.Add(selected.Name);
        }

        // Should be evenly distributed
        selections.Count(s => s == "provider-1").Should().Be(3);
        selections.Count(s => s == "provider-2").Should().Be(3);
        selections.Count(s => s == "provider-3").Should().Be(3);
    }

    [Fact]
    public async Task SelectProviderAsync_SingleProvider_ReturnsSameProvider()
    {
        var options = Options.Create(
            new ModelRouterOptions { Strategy = ModelRoutingStrategy.RoundRobin }
        );

        var router = new LoadBalancedRouter(
            [_provider1.Object],
            options,
            NullLogger<LoadBalancedRouter>.Instance
        );

        var context = new ModelRoutingContext();
        var selected1 = await router.SelectProviderAsync(context);
        var selected2 = await router.SelectProviderAsync(context);
        var selected3 = await router.SelectProviderAsync(context);

        selected1.Name.Should().Be("provider-1");
        selected2.Name.Should().Be("provider-1");
        selected3.Name.Should().Be("provider-1");
    }

    [Fact]
    public async Task SelectProviderAsync_Random_SelectsFromAllProviders()
    {
        var options = Options.Create(
            new ModelRouterOptions { Strategy = ModelRoutingStrategy.Random }
        );

        var router = new LoadBalancedRouter(
            [_provider1.Object, _provider2.Object, _provider3.Object],
            options,
            NullLogger<LoadBalancedRouter>.Instance
        );

        var context = new ModelRoutingContext();
        var selections = new HashSet<string>();

        // Select enough times to cover all providers
        for (int i = 0; i < 100; i++)
        {
            var selected = await router.SelectProviderAsync(context);
            selections.Add(selected.Name);
        }

        // Random strategy should eventually select all providers
        selections.Should().Contain("provider-1");
        selections.Should().Contain("provider-2");
        selections.Should().Contain("provider-3");
    }
}

public class ModelCallResultTests
{
    [Fact]
    public void Succeeded_CreatesSuccessResult()
    {
        var result = ModelCallResult.Succeeded(100, 500, 200, 0.001m);

        result.Success.Should().BeTrue();
        result.LatencyMs.Should().Be(100);
        result.InputTokens.Should().Be(500);
        result.OutputTokens.Should().Be(200);
        result.Cost.Should().Be(0.001m);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failed_CreatesFailureResult()
    {
        var result = ModelCallResult.Failed("Connection timeout", 5000);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Connection timeout");
        result.LatencyMs.Should().Be(5000);
    }
}

public class ModelRoutingContextTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var context = new ModelRoutingContext();

        context.EstimatedInputTokens.Should().Be(0);
        context.EstimatedOutputTokens.Should().Be(0);
        context.Priority.Should().Be(RequestPriority.Normal);
        context.RequiresStreaming.Should().BeFalse();
        context.MaxLatencyMs.Should().Be(0);
        context.MaxCost.Should().Be(0);
        context.PreferredModel.Should().BeNull();
        context.ExcludedProviders.Should().BeEmpty();
    }

    [Fact]
    public void With_CreatesNewInstance()
    {
        var original = new ModelRoutingContext
        {
            EstimatedInputTokens = 100,
            Priority = RequestPriority.Normal,
        };

        var modified = original with
        {
            Priority = RequestPriority.High,
            ExcludedProviders = ["provider-1"],
        };

        // Original unchanged
        original.Priority.Should().Be(RequestPriority.Normal);
        original.ExcludedProviders.Should().BeEmpty();

        // New instance has modifications
        modified.EstimatedInputTokens.Should().Be(100);
        modified.Priority.Should().Be(RequestPriority.High);
        modified.ExcludedProviders.Should().Contain("provider-1");
    }
}

public class ModelPricingTests
{
    [Fact]
    public void CalculateCost_ReturnsCorrectValue()
    {
        var pricing = new ModelPricing
        {
            Model = "test-model",
            InputPricePerKToken = 0.0025m,
            OutputPricePerKToken = 0.01m,
        };

        // 1M input + 1M output with per-K pricing
        // 1M / 1K * $0.0025 = $2.50 + 1M / 1K * $0.01 = $10.00 = $12.50
        var cost = pricing.CalculateCost(1_000_000, 1_000_000);

        cost.Should().Be(12.50m);
    }

    [Fact]
    public void CalculateCost_WithSmallTokens_ReturnsSmallCost()
    {
        var pricing = new ModelPricing
        {
            Model = "test-model",
            InputPricePerKToken = 0.0025m,
            OutputPricePerKToken = 0.01m,
        };

        // 1000 input + 500 output
        // (1000 * 0.0025 / 1000) + (500 * 0.01 / 1000) = 0.0025 + 0.005 = 0.0075
        var cost = pricing.CalculateCost(1000, 500);

        cost.Should().Be(0.0075m);
    }

    [Fact]
    public void KnownPricing_ReturnsCorrectPricingForGPT4o()
    {
        var pricing = ModelPricing.KnownPricing.GetPricing("gpt-4o");

        pricing.Should().NotBeNull();
        pricing.InputPricePerKToken.Should().Be(0.0025m);
        pricing.OutputPricePerKToken.Should().Be(0.01m);
    }

    [Fact]
    public void KnownPricing_ReturnsDefaultForUnknown()
    {
        var pricing = ModelPricing.KnownPricing.GetPricing("unknown-model");

        pricing.Should().NotBeNull();
        pricing.InputPricePerKToken.Should().Be(0.001m);
        pricing.OutputPricePerKToken.Should().Be(0.002m);
    }
}

public class ModelRouterDITests
{
    private sealed class TestProvider(string name = "test-provider") : ILLMProvider
    {
        public string Name => name;

        public Task<ChatCompletionResponse> ChatAsync(
            IEnumerable<ChatMessage> messages,
            ChatCompletionOptions? options = null,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(new ChatCompletionResponse { Content = "ok" });

        public async IAsyncEnumerable<string> ChatStreamAsync(
            IEnumerable<ChatMessage> messages,
            ChatCompletionOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
                CancellationToken cancellationToken = default
        )
        {
            yield return "ok";
            await Task.CompletedTask;
        }

        public async IAsyncEnumerable<StreamingChatEvent> ChatStreamEventsAsync(
            IEnumerable<ChatMessage> messages,
            ChatCompletionOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
                CancellationToken cancellationToken = default
        )
        {
            yield return new StreamingChatEvent { ContentDelta = "ok" };
            await Task.CompletedTask;
        }
    }

    private static Mock<ILLMProvider> CreateMockProvider(string name = "test-provider")
    {
        var mock = new Mock<ILLMProvider>();
        mock.Setup(p => p.Name).Returns(name);
        return mock;
    }

    [Fact]
    public void AddModelRouter_RegistersIModelRouter()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Add a provider
        services.AddSingleton<ILLMProvider>(CreateMockProvider().Object);

        services.AddModelRouter(ModelRoutingStrategy.CostOptimized);

        var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IModelRouter>();

        router.Should().BeOfType<CostOptimizedRouter>();
        router.Name.Should().Be("CostOptimized");
    }

    [Fact]
    public void AddLatencyOptimizedRouter_RegistersLatencyRouter()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSingleton<ILLMProvider>(CreateMockProvider().Object);
        services.AddLatencyOptimizedRouter();

        var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IModelRouter>();

        router.Should().BeOfType<LatencyOptimizedRouter>();
    }

    [Fact]
    public void AddLoadBalancedRouter_RegistersLoadBalancer()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSingleton<ILLMProvider>(CreateMockProvider().Object);
        services.AddLoadBalancedRouter(ModelRoutingStrategy.RoundRobin);

        var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IModelRouter>();

        router.Should().BeOfType<LoadBalancedRouter>();
    }

    [Fact]
    public void AddModelRouter_WithConfiguration_BindsOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILLMProvider>(CreateMockProvider().Object);

        services.AddModelRouter(options =>
        {
            options.Strategy = ModelRoutingStrategy.LatencyOptimized;
            options.EnableFailover = true;
            options.MaxFailoverRetries = 5;
        });

        var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IModelRouter>();

        router.Should().BeOfType<LatencyOptimizedRouter>();
    }

    [Fact]
    public void UseRoutingLLMProvider_WithImplementationTypeRegistration_UsesRoutingProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddSingleton<ILLMProvider, TestProvider>();
        services.AddModelRouter(ModelRoutingStrategy.CostOptimized);

        var act = () => services.UseRoutingLLMProvider();

        act.Should().NotThrow();

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ILLMProvider>().Should().BeOfType<RoutingLLMProvider>();
        provider.GetServices<ILLMProvider>().Should().Contain(p => p is TestProvider);
    }

    [Fact]
    public void UseRoutingLLMProvider_WithImplementationFactoryRegistration_UsesRoutingProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddSingleton<ILLMProvider>(_ => new TestProvider("factory-provider"));
        services.AddModelRouter(ModelRoutingStrategy.CostOptimized);

        var act = () => services.UseRoutingLLMProvider();

        act.Should().NotThrow();

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ILLMProvider>().Should().BeOfType<RoutingLLMProvider>();
        provider.GetServices<ILLMProvider>().Should().Contain(p => p.Name == "factory-provider");
    }
}

public class ModelRouterOptionsTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var options = new ModelRouterOptions();

        options.Strategy.Should().Be(ModelRoutingStrategy.CostOptimized);
        options.EnableFailover.Should().BeTrue();
        options.MaxFailoverRetries.Should().Be(2);
        options.HealthCheckIntervalSeconds.Should().Be(30);
        options.UnhealthyThreshold.Should().Be(3);
        options.RecoveryThreshold.Should().Be(2);
    }

    [Fact]
    public void SectionName_IsCorrect()
    {
        ModelRouterOptions.SectionName.Should().Be("ModelRouter");
    }
}
