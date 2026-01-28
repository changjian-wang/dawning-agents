using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Azure;
using Dawning.Agents.Core.RAG;
using Dawning.Agents.OpenAI;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dawning.Agents.Tests.RAG;

public class EmbeddingProviderDITests
{
    [Fact]
    public void AddOpenAIEmbedding_RegistersOpenAIProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOpenAIEmbedding("sk-test-key", "text-embedding-3-small");
        var provider = services.BuildServiceProvider().GetRequiredService<IEmbeddingProvider>();

        // Assert
        provider.Should().BeOfType<OpenAIEmbeddingProvider>();
        provider.Name.Should().Be("OpenAI");
        provider.Dimensions.Should().Be(1536);
    }

    [Fact]
    public void AddAzureOpenAIEmbedding_RegistersAzureProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAzureOpenAIEmbedding(
            "https://test.openai.azure.com",
            "test-key",
            "embedding-deployment",
            dimensions: 1536
        );
        var provider = services.BuildServiceProvider().GetRequiredService<IEmbeddingProvider>();

        // Assert
        provider.Should().BeOfType<AzureOpenAIEmbeddingProvider>();
        provider.Name.Should().Be("AzureOpenAI");
    }

    [Fact]
    public void AddOllamaEmbedding_RegistersOllamaProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOllamaEmbedding("nomic-embed-text", "http://localhost:11434");
        var provider = services.BuildServiceProvider().GetRequiredService<IEmbeddingProvider>();

        // Assert
        provider.Should().BeOfType<OllamaEmbeddingProvider>();
        provider.Name.Should().Be("Ollama");
        provider.Dimensions.Should().Be(768);
    }

    [Fact]
    public void AddSimpleEmbedding_RegistersSimpleProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddSimpleEmbedding(dimensions: 512);
        var provider = services.BuildServiceProvider().GetRequiredService<IEmbeddingProvider>();

        // Assert
        provider.Should().BeOfType<SimpleEmbeddingProvider>();
        provider.Name.Should().Be("SimpleEmbedding");
        provider.Dimensions.Should().Be(512);
    }

    [Fact]
    public void AddEmbeddingProvider_WithOpenAIConfig_RegistersOpenAIProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["LLM:ProviderType"] = "OpenAI",
                    ["LLM:ApiKey"] = "sk-test-key",
                    ["RAG:EmbeddingModel"] = "text-embedding-3-small",
                }
            )
            .Build();

        // Act
        services.AddEmbeddingProvider(configuration);
        var provider = services.BuildServiceProvider().GetRequiredService<IEmbeddingProvider>();

        // Assert
        provider.Should().BeOfType<OpenAIEmbeddingProvider>();
        provider.Name.Should().Be("OpenAI");
    }

    [Fact]
    public void AddEmbeddingProvider_WithAzureConfig_RegistersAzureProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["LLM:ProviderType"] = "AzureOpenAI",
                    ["LLM:Endpoint"] = "https://test.openai.azure.com",
                    ["LLM:ApiKey"] = "test-key",
                    ["RAG:EmbeddingModel"] = "embedding-deployment",
                }
            )
            .Build();

        // Act
        services.AddEmbeddingProvider(configuration);
        var provider = services.BuildServiceProvider().GetRequiredService<IEmbeddingProvider>();

        // Assert
        provider.Should().BeOfType<AzureOpenAIEmbeddingProvider>();
        provider.Name.Should().Be("AzureOpenAI");
    }

    [Fact]
    public void AddEmbeddingProvider_WithOllamaConfig_RegistersOllamaProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["LLM:ProviderType"] = "Ollama",
                    ["LLM:Endpoint"] = "http://localhost:11434",
                    ["RAG:EmbeddingModel"] = "nomic-embed-text",
                }
            )
            .Build();

        // Act
        services.AddEmbeddingProvider(configuration);
        var provider = services.BuildServiceProvider().GetRequiredService<IEmbeddingProvider>();

        // Assert
        provider.Should().BeOfType<OllamaEmbeddingProvider>();
        provider.Name.Should().Be("Ollama");
    }

    [Fact]
    public void AddRAG_RegistersDefaultSimpleEmbeddingProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddRAG();
        var provider = services.BuildServiceProvider().GetRequiredService<IEmbeddingProvider>();

        // Assert
        provider.Should().BeOfType<SimpleEmbeddingProvider>();
    }

    [Fact]
    public void AddEmbeddingProvider_ThenAddRAG_UsesExistingProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - 先注册 OpenAI，再注册 RAG
        services.AddOpenAIEmbedding("sk-test-key");
        services.AddRAG();
        var provider = services.BuildServiceProvider().GetRequiredService<IEmbeddingProvider>();

        // Assert - 应该使用先注册的 OpenAI
        provider.Should().BeOfType<OpenAIEmbeddingProvider>();
    }
}
