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
    public void Create_WithOllamaOptions_ReturnsOllamaProvider()
    {
        var options = new LLMOptions
        {
            ProviderType = LLMProviderType.Ollama,
            Model = "test-model",
            Endpoint = "http://localhost:11434"
        };

        var provider = LLMProviderFactory.Create(options);

        provider.Should().BeOfType<OllamaProvider>();
        provider.Name.Should().Be("Ollama");
    }

    [Fact]
    public void Create_WithOpenAIOptions_ReturnsOpenAIProvider()
    {
        var options = new LLMOptions
        {
            ProviderType = LLMProviderType.OpenAI,
            Model = "gpt-4o",
            ApiKey = "fake-key"
        };

        var provider = LLMProviderFactory.Create(options);

        provider.Should().BeOfType<OpenAIProvider>();
        provider.Name.Should().Be("OpenAI");
    }

    [Fact]
    public void Create_WithAzureOpenAIOptions_ReturnsAzureOpenAIProvider()
    {
        var options = new LLMOptions
        {
            ProviderType = LLMProviderType.AzureOpenAI,
            Model = "gpt-4o",
            ApiKey = "fake-key",
            Endpoint = "https://test.openai.azure.com"
        };

        var provider = LLMProviderFactory.Create(options);

        provider.Should().BeOfType<AzureOpenAIProvider>();
        provider.Name.Should().Be("AzureOpenAI");
    }

    [Fact]
    public void Create_WithOpenAIOptionsMissingApiKey_ThrowsInvalidOperationException()
    {
        var options = new LLMOptions
        {
            ProviderType = LLMProviderType.OpenAI,
            Model = "gpt-4o"
            // Missing ApiKey
        };

        var act = () => LLMProviderFactory.Create(options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*API Key*");
    }

    [Fact]
    public void Create_WithAzureOptionsMissingEndpoint_ThrowsInvalidOperationException()
    {
        var options = new LLMOptions
        {
            ProviderType = LLMProviderType.AzureOpenAI,
            Model = "gpt-4o",
            ApiKey = "fake-key"
            // Missing Endpoint
        };

        var act = () => LLMProviderFactory.Create(options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*endpoint*");
    }
}

public class LLMOptionsTests
{
    [Fact]
    public void Default_Options_UsesOllama()
    {
        var options = new LLMOptions();

        options.ProviderType.Should().Be(LLMProviderType.Ollama);
        options.Model.Should().Be("deepseek-coder:1.3b");
    }

    [Fact]
    public void Validate_OpenAI_WithoutApiKey_Throws()
    {
        var options = new LLMOptions
        {
            ProviderType = LLMProviderType.OpenAI,
            Model = "gpt-4o"
        };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ApiKey*");
    }

    [Fact]
    public void Validate_AzureOpenAI_WithoutEndpoint_Throws()
    {
        var options = new LLMOptions
        {
            ProviderType = LLMProviderType.AzureOpenAI,
            Model = "gpt-4o",
            ApiKey = "fake-key"
        };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Endpoint*");
    }

    [Fact]
    public void Validate_Ollama_SetsDefaultEndpoint()
    {
        var options = new LLMOptions
        {
            ProviderType = LLMProviderType.Ollama,
            Model = "test-model"
        };

        options.Validate();

        options.Endpoint.Should().Be("http://localhost:11434");
    }
}
