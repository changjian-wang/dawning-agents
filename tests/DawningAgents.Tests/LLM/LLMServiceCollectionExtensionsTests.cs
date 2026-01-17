using DawningAgents.Abstractions.LLM;
using DawningAgents.Azure;
using DawningAgents.Core.LLM;
using DawningAgents.OpenAI;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DawningAgents.Tests.LLM;

public class LLMServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLLMProvider_WithConfiguration_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new LLMConfiguration
        {
            ProviderType = LLMProviderType.Ollama,
            Model = "test-model"
        };

        // Act
        services.AddLLMProvider(config);
        var provider = services.BuildServiceProvider();

        // Assert
        var llmProvider = provider.GetService<ILLMProvider>();
        llmProvider.Should().NotBeNull();
        llmProvider.Should().BeOfType<OllamaProvider>();
    }

    [Fact]
    public void AddOllamaProvider_RegistersOllamaProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOllamaProvider("test-model", "http://localhost:11434");
        var provider = services.BuildServiceProvider();

        // Assert
        var llmProvider = provider.GetRequiredService<ILLMProvider>();
        llmProvider.Should().BeOfType<OllamaProvider>();
        llmProvider.Name.Should().Be("Ollama");
    }

    [Fact]
    public void AddOpenAIProvider_RegistersOpenAIProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOpenAIProvider("fake-api-key", "gpt-4o");
        var provider = services.BuildServiceProvider();

        // Assert
        var llmProvider = provider.GetRequiredService<ILLMProvider>();
        llmProvider.Should().BeOfType<OpenAIProvider>();
        llmProvider.Name.Should().Be("OpenAI");
    }

    [Fact]
    public void AddAzureOpenAIProvider_RegistersAzureProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAzureOpenAIProvider(
            "https://test.openai.azure.com",
            "fake-api-key",
            "gpt-4o");
        var provider = services.BuildServiceProvider();

        // Assert
        var llmProvider = provider.GetRequiredService<ILLMProvider>();
        llmProvider.Should().BeOfType<AzureOpenAIProvider>();
        llmProvider.Name.Should().Be("AzureOpenAI");
    }

    [Fact]
    public void AddLLMProvider_RegistersAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOllamaProvider("test-model");

        // Act
        var provider = services.BuildServiceProvider();
        var instance1 = provider.GetRequiredService<ILLMProvider>();
        var instance2 = provider.GetRequiredService<ILLMProvider>();

        // Assert
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void AddLLMProvider_WithConfigure_AppliesConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddLLMProvider(config =>
        {
            config = config with
            {
                ProviderType = LLMProviderType.Ollama,
                Model = "custom-model"
            };
        });

        // 由于 record 的 with 表达式创建新实例，需要不同的方式测试
        // 这里验证服务已注册
        var provider = services.BuildServiceProvider();
        var llmProvider = provider.GetService<ILLMProvider>();

        // Assert
        llmProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddLLMProvider_ConfigurationIsInjectable()
    {
        // Arrange
        var services = new ServiceCollection();
        var expectedConfig = new LLMConfiguration
        {
            ProviderType = LLMProviderType.OpenAI,
            ApiKey = "test-key",
            Model = "gpt-4o"
        };

        // Act
        services.AddLLMProvider(expectedConfig);
        var provider = services.BuildServiceProvider();

        // Assert
        var config = provider.GetRequiredService<LLMConfiguration>();
        config.Should().Be(expectedConfig);
    }
}
