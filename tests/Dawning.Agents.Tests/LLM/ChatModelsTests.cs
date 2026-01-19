using Dawning.Agents.Abstractions.LLM;
using FluentAssertions;

namespace Dawning.Agents.Tests.LLM;

/// <summary>
/// Tests for Chat data models
/// </summary>
public class ChatModelsTests
{
    #region ChatMessage Tests

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

    [Theory]
    [InlineData("user", "Hello")]
    [InlineData("assistant", "Hi there!")]
    [InlineData("system", "You are helpful")]
    public void ChatMessage_SupportsAllRoles(string role, string content)
    {
        // Act
        var msg = new ChatMessage(role, content);

        // Assert
        msg.Role.Should().Be(role);
        msg.Content.Should().Be(content);
    }

    #endregion

    #region ChatCompletionOptions Tests

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
    public void ChatCompletionOptions_CanSetCustomValues()
    {
        // Arrange & Act
        var options = new ChatCompletionOptions
        {
            Temperature = 0.5f,
            MaxTokens = 2000,
            SystemPrompt = "You are a helpful assistant.",
        };

        // Assert
        options.Temperature.Should().Be(0.5f);
        options.MaxTokens.Should().Be(2000);
        options.SystemPrompt.Should().Be("You are a helpful assistant.");
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    [InlineData(2.0f)]
    public void ChatCompletionOptions_AcceptsVariousTemperatures(float temperature)
    {
        // Act
        var options = new ChatCompletionOptions { Temperature = temperature };

        // Assert
        options.Temperature.Should().Be(temperature);
    }

    #endregion

    #region ChatCompletionResponse Tests

    [Fact]
    public void ChatCompletionResponse_CalculatesTotalTokens()
    {
        // Arrange
        var response = new ChatCompletionResponse
        {
            Content = "测试",
            PromptTokens = 10,
            CompletionTokens = 20,
        };

        // Assert
        response.TotalTokens.Should().Be(30);
    }

    [Fact]
    public void ChatCompletionResponse_TotalTokensIsComputed()
    {
        // Arrange
        var response = new ChatCompletionResponse
        {
            Content = "Hello",
            PromptTokens = 100,
            CompletionTokens = 50,
        };

        // Assert - TotalTokens should always equal PromptTokens + CompletionTokens
        response.TotalTokens.Should().Be(response.PromptTokens + response.CompletionTokens);
    }

    [Fact]
    public void ChatCompletionResponse_WithZeroTokens()
    {
        // Arrange
        var response = new ChatCompletionResponse
        {
            Content = "",
            PromptTokens = 0,
            CompletionTokens = 0,
        };

        // Assert
        response.TotalTokens.Should().Be(0);
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("length")]
    [InlineData("content_filter")]
    public void ChatCompletionResponse_SupportsFinishReasons(string reason)
    {
        // Act
        var response = new ChatCompletionResponse { Content = "Test", FinishReason = reason };

        // Assert
        response.FinishReason.Should().Be(reason);
    }

    #endregion
}
