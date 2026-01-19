using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Azure;
using Dawning.Agents.Core.LLM;
using Dawning.Agents.OpenAI;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Tests.LLM;

public class LLMServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLLMProvider_WithOptions_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddLLMProvider(options =>
        {
            options.ProviderType = LLMProviderType.Ollama;
            options.Model = "test-model";
            options.Endpoint = "http://localhost:11434";
        });
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
        services.AddAzureOpenAIProvider("https://test.openai.azure.com", "fake-api-key", "gpt-4o");
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
        services.AddLLMProvider(options =>
        {
            options.ProviderType = LLMProviderType.Ollama;
            options.Model = "custom-model";
            options.Endpoint = "http://localhost:11434";
        });

        var provider = services.BuildServiceProvider();
        var llmProvider = provider.GetService<ILLMProvider>();

        // Assert
        llmProvider.Should().NotBeNull();
        llmProvider.Should().BeOfType<OllamaProvider>();
    }

    [Fact]
    public void AddLLMProvider_OptionsAreInjectable()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddLLMProvider(options =>
        {
            options.ProviderType = LLMProviderType.OpenAI;
            options.ApiKey = "test-key";
            options.Model = "gpt-4o";
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetRequiredService<IOptions<LLMOptions>>().Value;
        options.ProviderType.Should().Be(LLMProviderType.OpenAI);
        options.ApiKey.Should().Be("test-key");
        options.Model.Should().Be("gpt-4o");
    }
}
