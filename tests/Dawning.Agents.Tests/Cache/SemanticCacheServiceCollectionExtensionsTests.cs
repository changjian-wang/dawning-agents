using Dawning.Agents.Abstractions.Cache;
using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Core.Cache;
using Dawning.Agents.Core.RAG;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Dawning.Agents.Tests.Cache;

/// <summary>
/// SemanticCache DI extension tests
/// </summary>
public class SemanticCacheServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSemanticCache_WithConfiguration_RegistersService()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["SemanticCache:Enabled"] = "true",
                    ["SemanticCache:SimilarityThreshold"] = "0.95",
                    ["SemanticCache:MaxEntries"] = "5000",
                }
            )
            .Build();

        // Register dependencies first
        services.AddSingleton<IVectorStore>(new InMemoryVectorStore());
        services.AddSingleton(new Mock<IEmbeddingProvider>().Object);
        services.AddSemanticCache(config);

        var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<ISemanticCache>();

        cache.Should().NotBeNull();
        cache.Should().BeOfType<SemanticCache>();
    }

    [Fact]
    public void AddSemanticCache_WithAction_RegistersService()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IVectorStore>(new InMemoryVectorStore());
        services.AddSingleton(new Mock<IEmbeddingProvider>().Object);
        services.AddSemanticCache(options =>
        {
            options.SimilarityThreshold = 0.9f;
            options.MaxEntries = 1000;
        });

        var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<ISemanticCache>();

        cache.Should().NotBeNull();
    }

    [Fact]
    public void AddSemanticCache_WithParams_RegistersService()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IVectorStore>(new InMemoryVectorStore());
        services.AddSingleton(new Mock<IEmbeddingProvider>().Object);
        services.AddSemanticCache(
            similarityThreshold: 0.9f,
            maxEntries: 2000,
            expirationMinutes: 720
        );

        var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<ISemanticCache>();

        cache.Should().NotBeNull();
    }

    [Fact]
    public void AddSemanticCache_IsSingleton()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IVectorStore>(new InMemoryVectorStore());
        services.AddSingleton(new Mock<IEmbeddingProvider>().Object);
        services.AddSemanticCache();

        var provider = services.BuildServiceProvider();
        var cache1 = provider.GetRequiredService<ISemanticCache>();
        var cache2 = provider.GetRequiredService<ISemanticCache>();

        cache1.Should().BeSameAs(cache2);
    }
}
