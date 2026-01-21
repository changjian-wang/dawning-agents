using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Core.RAG;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dawning.Agents.Tests.RAG;

/// <summary>
/// RAG DI 扩展方法单元测试
/// </summary>
public class RAGServiceCollectionExtensionsTests
{
    #region AddRAG Tests

    [Fact]
    public void AddRAG_ShouldRegisterAllServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddRAG();
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<IEmbeddingProvider>().Should().NotBeNull();
        provider.GetService<IVectorStore>().Should().NotBeNull();
        provider.GetService<DocumentChunker>().Should().NotBeNull();
        provider.GetService<IRetriever>().Should().NotBeNull();
        provider.GetService<KnowledgeBase>().Should().NotBeNull();
    }

    [Fact]
    public void AddRAG_ShouldRegisterAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRAG();
        var provider = services.BuildServiceProvider();

        // Act
        var vectorStore1 = provider.GetService<IVectorStore>();
        var vectorStore2 = provider.GetService<IVectorStore>();

        // Assert
        vectorStore1.Should().BeSameAs(vectorStore2);
    }

    #endregion

    #region AddRAG with Configuration Tests

    [Fact]
    public void AddRAG_WithConfiguration_ShouldBindOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["RAG:ChunkSize"] = "200",
                    ["RAG:ChunkOverlap"] = "30",
                    ["RAG:TopK"] = "10",
                    ["RAG:MinScore"] = "0.7",
                }
            )
            .Build();

        // Act
        services.AddRAG(config);
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<RAGOptions>>()
            .Value;
        options.ChunkSize.Should().Be(200);
        options.ChunkOverlap.Should().Be(30);
        options.TopK.Should().Be(10);
        options.MinScore.Should().Be(0.7f);
    }

    #endregion

    #region AddRAG with Action Tests

    [Fact]
    public void AddRAG_WithAction_ShouldConfigureOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddRAG(options =>
        {
            options.ChunkSize = 300;
            options.TopK = 15;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<RAGOptions>>()
            .Value;
        options.ChunkSize.Should().Be(300);
        options.TopK.Should().Be(15);
    }

    #endregion

    #region AddInMemoryVectorStore Tests

    [Fact]
    public void AddInMemoryVectorStore_ShouldRegisterVectorStore()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddInMemoryVectorStore();
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<IVectorStore>().Should().BeOfType<InMemoryVectorStore>();
    }

    #endregion

    #region AddSimpleEmbedding Tests

    [Fact]
    public void AddSimpleEmbedding_ShouldRegisterEmbeddingProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSimpleEmbedding(dimensions: 256);
        var provider = services.BuildServiceProvider();

        // Assert
        var embeddingProvider = provider.GetService<IEmbeddingProvider>();
        embeddingProvider.Should().BeOfType<SimpleEmbeddingProvider>();
        embeddingProvider!.Dimensions.Should().Be(256);
    }

    [Fact]
    public void AddSimpleEmbedding_DefaultDimensions_ShouldBe384()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSimpleEmbedding();
        var provider = services.BuildServiceProvider();

        // Assert
        var embeddingProvider = provider.GetService<IEmbeddingProvider>();
        embeddingProvider!.Dimensions.Should().Be(384);
    }

    #endregion

    #region AddKnowledgeBase Tests

    [Fact]
    public void AddKnowledgeBase_ShouldRegisterKnowledgeBaseAndChunker()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRAG(); // Need full RAG for KnowledgeBase dependencies

        // Act
        services.AddKnowledgeBase();
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<DocumentChunker>().Should().NotBeNull();
        provider.GetService<KnowledgeBase>().Should().NotBeNull();
    }

    #endregion
}
