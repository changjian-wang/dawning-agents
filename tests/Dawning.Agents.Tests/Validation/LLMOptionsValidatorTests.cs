using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Core.Validation;
using FluentAssertions;

namespace Dawning.Agents.Tests.Validation;

public class LLMOptionsValidatorTests
{
    private readonly LLMOptionsValidator _validator = new();

    [Fact]
    public void Validate_WithValidOllamaOptions_ShouldPass()
    {
        // Arrange
        var options = new LLMOptions
        {
            ProviderType = LLMProviderType.Ollama,
            Model = "qwen2.5:0.5b",
            Endpoint = "http://localhost:11434",
        };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithValidOpenAIOptions_ShouldPass()
    {
        // Arrange
        var options = new LLMOptions
        {
            ProviderType = LLMProviderType.OpenAI,
            Model = "gpt-4o",
            ApiKey = "sk-test-key",
        };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithValidAzureOpenAIOptions_ShouldPass()
    {
        // Arrange
        var options = new LLMOptions
        {
            ProviderType = LLMProviderType.AzureOpenAI,
            Model = "gpt-4o",
            ApiKey = "test-key",
            Endpoint = "https://myservice.openai.azure.com",
        };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyModel_ShouldFail()
    {
        // Arrange
        var options = new LLMOptions
        {
            ProviderType = LLMProviderType.Ollama,
            Model = "",
        };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Model");
    }

    [Fact]
    public void Validate_OpenAI_WithoutApiKey_ShouldFail()
    {
        // Arrange
        var options = new LLMOptions
        {
            ProviderType = LLMProviderType.OpenAI,
            Model = "gpt-4o",
            ApiKey = null,
        };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ApiKey");
    }

    [Fact]
    public void Validate_AzureOpenAI_WithoutEndpoint_ShouldFail()
    {
        // Arrange
        var options = new LLMOptions
        {
            ProviderType = LLMProviderType.AzureOpenAI,
            Model = "gpt-4o",
            ApiKey = "test-key",
            Endpoint = null,
        };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Endpoint");
    }

    [Fact]
    public void Validate_AzureOpenAI_WithInvalidEndpoint_ShouldFail()
    {
        // Arrange
        var options = new LLMOptions
        {
            ProviderType = LLMProviderType.AzureOpenAI,
            Model = "gpt-4o",
            ApiKey = "test-key",
            Endpoint = "not-a-url",
        };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Endpoint");
    }

    [Fact]
    public void Validate_Ollama_WithEmptyEndpoint_ShouldPass()
    {
        // Arrange - Ollama 默认使用 localhost
        var options = new LLMOptions
        {
            ProviderType = LLMProviderType.Ollama,
            Model = "qwen2.5:0.5b",
            Endpoint = null,
        };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
