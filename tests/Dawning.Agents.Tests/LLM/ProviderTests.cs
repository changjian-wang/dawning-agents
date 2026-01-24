using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Azure;
using Dawning.Agents.Core.LLM;
using Dawning.Agents.OpenAI;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Dawning.Agents.Tests.LLM;

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
        var act = () =>
            new AzureOpenAIProvider("https://test.openai.azure.com", nullApiKey!, "deployment");
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
        var provider = new AzureOpenAIProvider(
            "https://test.openai.azure.com",
            "fake-key",
            "gpt-4o"
        );
        provider.Name.Should().Be("AzureOpenAI");
    }
}

public class OllamaProviderTests
{
    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        var act = () => new OllamaProvider(null!, "model");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullModel_ThrowsArgumentException()
    {
        using var httpClient = new HttpClient();
        var act = () => new OllamaProvider(httpClient, null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Name_ReturnsOllama()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:11434") };
        var provider = new OllamaProvider(httpClient, "test-model");
        provider.Name.Should().Be("Ollama");
    }
}

public class LLMProviderDITests
{
    [Fact]
    public void AddLLMProvider_WithOllamaOptions_ReturnsOllamaProvider()
    {
        var services = new ServiceCollection();
        services.AddLLMProvider(options =>
        {
            options.ProviderType = LLMProviderType.Ollama;
            options.Model = "test-model";
            options.Endpoint = "http://localhost:11434";
        });

        var provider = services.BuildServiceProvider().GetRequiredService<ILLMProvider>();

        provider.Should().BeOfType<OllamaProvider>();
        provider.Name.Should().Be("Ollama");
    }

    [Fact]
    public void AddOpenAIProvider_ReturnsOpenAIProvider()
    {
        var services = new ServiceCollection();
        services.AddOpenAIProvider("fake-key", "gpt-4o");

        var provider = services.BuildServiceProvider().GetRequiredService<ILLMProvider>();

        provider.Should().BeOfType<OpenAIProvider>();
        provider.Name.Should().Be("OpenAI");
    }

    [Fact]
    public void AddAzureOpenAIProvider_ReturnsAzureOpenAIProvider()
    {
        var services = new ServiceCollection();
        services.AddAzureOpenAIProvider(
            "https://test.openai.azure.com",
            "fake-key",
            "gpt-4o"
        );

        var provider = services.BuildServiceProvider().GetRequiredService<ILLMProvider>();

        provider.Should().BeOfType<AzureOpenAIProvider>();
        provider.Name.Should().Be("AzureOpenAI");
    }

    [Fact]
    public void AddLLMProvider_WithNonOllamaType_ThrowsOnResolve()
    {
        var services = new ServiceCollection();
        services.AddLLMProvider(options =>
        {
            options.ProviderType = LLMProviderType.OpenAI;
            options.Model = "gpt-4o";
            options.ApiKey = "fake-key";
        });

        var act = () => services.BuildServiceProvider().GetRequiredService<ILLMProvider>();

        act.Should().Throw<InvalidOperationException>().WithMessage("*仅支持 Ollama*");
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
        var options = new LLMOptions { ProviderType = LLMProviderType.OpenAI, Model = "gpt-4o" };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*ApiKey*");
    }

    [Fact]
    public void Validate_AzureOpenAI_WithoutEndpoint_Throws()
    {
        var options = new LLMOptions
        {
            ProviderType = LLMProviderType.AzureOpenAI,
            Model = "gpt-4o",
            ApiKey = "fake-key",
        };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Endpoint*");
    }

    [Fact]
    public void Validate_Ollama_SetsDefaultEndpoint()
    {
        var options = new LLMOptions
        {
            ProviderType = LLMProviderType.Ollama,
            Model = "test-model",
        };

        options.Validate();

        options.Endpoint.Should().Be("http://localhost:11434");
    }
}
