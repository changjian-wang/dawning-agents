using DawningAgents.Abstractions.LLM;
using DawningAgents.Core.LLM;
using FluentAssertions;

namespace DawningAgents.Tests.LLM;

public class OllamaProviderTests
{
    [Fact]
    public void Constructor_WithNullModel_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => new OllamaProvider(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyModel_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => new OllamaProvider("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullBaseUrl_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => new OllamaProvider("test-model", null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Name_ReturnsOllama()
    {
        // Arrange
        var provider = new OllamaProvider("test-model");

        // Act & Assert
        provider.Name.Should().Be("Ollama");
    }

    [Fact]
    public void ChatCompletionOptions_HasDefaultValues()
    {
        // Arrange
        var options = new ChatCompletionOptions();

        // Assert
        options.Temperature.Should().Be(0.7f);
        options.MaxTokens.Should().Be(1000);
        options.SystemPrompt.Should().BeNull();
    }

    [Fact]
    public void ChatMessage_RecordEquality()
    {
        // Arrange
        var msg1 = new ChatMessage("user", "Hello");
        var msg2 = new ChatMessage("user", "Hello");

        // Assert
        msg1.Should().Be(msg2);
    }

    [Fact]
    public void ChatCompletionResponse_CalculatesTotalTokens()
    {
        // Arrange
        var response = new ChatCompletionResponse
        {
            Content = "测试",
            PromptTokens = 10,
            CompletionTokens = 20
        };

        // Assert
        response.TotalTokens.Should().Be(30);
    }

    [Fact]
    public void ChatMessage_Deconstruction()
    {
        // Arrange
        var msg = new ChatMessage("user", "Hello World");

        // Act
        var (role, content) = msg;

        // Assert
        role.Should().Be("user");
        content.Should().Be("Hello World");
    }
}
