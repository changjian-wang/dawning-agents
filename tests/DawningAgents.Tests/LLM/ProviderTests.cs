using DawningAgents.Abstractions.LLM;
using DawningAgents.Azure;
using DawningAgents.Core.LLM;
using DawningAgents.OpenAI;
using FluentAssertions;

namespace DawningAgents.Tests.LLM;

public class OpenAIProviderTests
{
    [Fact]
    public void Constructor_WithNullApiKey_ThrowsArgumentException()
    {
        var act = () => new OpenAIProvider(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyApiKey_ThrowsArgumentException()
    {
        var act = () => new OpenAIProvider("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Name_ReturnsOpenAI()
    {
        var provider = new OpenAIProvider("fake-key");
        provider.Name.Should().Be("OpenAI");
    }
}

public class AzureOpenAIProviderTests
{
    [Fact]
    public void Constructor_WithNullEndpoint_ThrowsArgumentException()
    {
        var act = () => new AzureOpenAIProvider(null!, "key", "deployment");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullApiKey_ThrowsArgumentException()
    {
        string? nullApiKey = null;
        var act = () => new AzureOpenAIProvider("https://test.openai.azure.com", nullApiKey!, "deployment");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullDeployment_ThrowsArgumentException()
    {
        var act = () => new AzureOpenAIProvider("https://test.openai.azure.com", "key", null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Name_ReturnsAzureOpenAI()
    {
        var provider = new AzureOpenAIProvider("https://test.openai.azure.com", "fake-key", "gpt-4o");
        provider.Name.Should().Be("AzureOpenAI");
    }
}

public class LLMProviderFactoryTests
{
    [Fact]
    public void Create_WithOllamaConfig_ReturnsOllamaProvider()
    {
        var config = new LLMConfiguration
        {
            ProviderType = LLMProviderType.Ollama,
            Model = "test-model",
            Endpoint = "http://localhost:11434"
        };

        var provider = LLMProviderFactory.Create(config);

        provider.Should().BeOfType<OllamaProvider>();
        provider.Name.Should().Be("Ollama");
    }

    [Fact]
    public void Create_WithOpenAIConfig_ReturnsOpenAIProvider()
    {
        var config = new LLMConfiguration
        {
            ProviderType = LLMProviderType.OpenAI,
            Model = "gpt-4o",
            ApiKey = "fake-key"
        };

        var provider = LLMProviderFactory.Create(config);

        provider.Should().BeOfType<OpenAIProvider>();
        provider.Name.Should().Be("OpenAI");
    }

    [Fact]
    public void Create_WithAzureOpenAIConfig_ReturnsAzureOpenAIProvider()
    {
        var config = new LLMConfiguration
        {
            ProviderType = LLMProviderType.AzureOpenAI,
            Model = "gpt-4o",
            ApiKey = "fake-key",
            Endpoint = "https://test.openai.azure.com"
        };

        var provider = LLMProviderFactory.Create(config);

        provider.Should().BeOfType<AzureOpenAIProvider>();
        provider.Name.Should().Be("AzureOpenAI");
    }

    [Fact]
    public void Create_WithOpenAIConfigMissingApiKey_ThrowsInvalidOperationException()
    {
        var config = new LLMConfiguration
        {
            ProviderType = LLMProviderType.OpenAI,
            Model = "gpt-4o"
            // Missing ApiKey
        };

        var act = () => LLMProviderFactory.Create(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*API Key*");
    }

    [Fact]
    public void Create_WithAzureConfigMissingEndpoint_ThrowsInvalidOperationException()
    {
        var config = new LLMConfiguration
        {
            ProviderType = LLMProviderType.AzureOpenAI,
            Model = "gpt-4o",
            ApiKey = "fake-key"
            // Missing Endpoint
        };

        var act = () => LLMProviderFactory.Create(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*endpoint*");
    }
}

public class LLMConfigurationTests
{
    [Fact]
    public void Default_Configuration_UsesOllama()
    {
        var config = new LLMConfiguration();

        config.ProviderType.Should().Be(LLMProviderType.Ollama);
        config.Model.Should().Be("deepseek-coder:6.7b");
    }

    [Fact]
    public void Configuration_SupportsWithExpression()
    {
        var original = new LLMConfiguration
        {
            ProviderType = LLMProviderType.Ollama,
            Model = "model1"
        };

        var modified = original with { Model = "model2" };

        original.Model.Should().Be("model1");
        modified.Model.Should().Be("model2");
        modified.ProviderType.Should().Be(LLMProviderType.Ollama);
    }
}
