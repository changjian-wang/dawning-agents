using System.Net;
using System.Text;
using System.Text.Json;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Core.LLM;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace Dawning.Agents.Tests.LLM;

/// <summary>
/// ResponseFormat / Structured Output 相关测试
/// </summary>
public class StructuredOutputTests
{
    #region ResponseFormat 模型测试

    [Fact]
    public void ResponseFormat_Text_ShouldHaveCorrectType()
    {
        var format = ResponseFormat.Text;
        format.Type.Should().Be(ResponseFormatType.Text);
        format.SchemaName.Should().BeNull();
        format.Schema.Should().BeNull();
    }

    [Fact]
    public void ResponseFormat_JsonObject_ShouldHaveCorrectType()
    {
        var format = ResponseFormat.JsonObject;
        format.Type.Should().Be(ResponseFormatType.JsonObject);
        format.SchemaName.Should().BeNull();
        format.Schema.Should().BeNull();
    }

    [Fact]
    public void ResponseFormat_JsonSchema_ShouldSetAllProperties()
    {
        var schema = """{"type":"object","properties":{"name":{"type":"string"}}}""";
        var format = ResponseFormat.JsonSchema("PersonSchema", schema, strict: true);

        format.Type.Should().Be(ResponseFormatType.JsonSchema);
        format.SchemaName.Should().Be("PersonSchema");
        format.Schema.Should().Be(schema);
        format.Strict.Should().BeTrue();
    }

    [Fact]
    public void ResponseFormat_JsonSchema_DefaultsStrictToTrue()
    {
        var format = ResponseFormat.JsonSchema("Test", "{}");
        format.Strict.Should().BeTrue();
    }

    [Fact]
    public void ChatCompletionOptions_ResponseFormat_DefaultsToNull()
    {
        var options = new ChatCompletionOptions();
        options.ResponseFormat.Should().BeNull();
    }

    [Fact]
    public void ChatCompletionOptions_CanSetResponseFormat()
    {
        var options = new ChatCompletionOptions { ResponseFormat = ResponseFormat.JsonObject };

        options.ResponseFormat.Should().NotBeNull();
        options.ResponseFormat!.Type.Should().Be(ResponseFormatType.JsonObject);
    }

    #endregion

    #region OllamaProvider ResponseFormat 测试

    [Fact]
    public async Task OllamaProvider_ChatAsync_WithJsonObjectFormat_ShouldSendFormatJson()
    {
        // Arrange
        var ollamaResponse = """
            {
                "message": {
                    "role": "assistant",
                    "content": "{\"name\": \"Alice\", \"age\": 30}"
                },
                "done": true,
                "done_reason": "stop",
                "prompt_eval_count": 10,
                "eval_count": 20
            }
            """;

        HttpRequestMessage? capturedRequest = null;
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(ollamaResponse, Encoding.UTF8, "application/json"),
                }
            );

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("http://localhost:11434"),
        };

        var provider = new OllamaProvider(
            httpClient,
            "qwen2.5:0.5b",
            NullLogger<OllamaProvider>.Instance
        );

        var messages = new[] { ChatMessage.User("Return a JSON with name and age") };
        var options = new ChatCompletionOptions { ResponseFormat = ResponseFormat.JsonObject };

        // Act
        var result = await provider.ChatAsync(messages, options);

        // Assert
        result.Content.Should().Contain("Alice");

        // Verify the request body contains format: "json"
        capturedRequest.Should().NotBeNull();
        var requestBody = await capturedRequest!.Content!.ReadAsStringAsync();
        requestBody.Should().Contain("\"format\"");
        requestBody.Should().Contain("\"json\"");
    }

    [Fact]
    public async Task OllamaProvider_ChatAsync_WithJsonSchemaFormat_ShouldSendFormatJson()
    {
        // Arrange - Ollama treats JsonSchema same as JsonObject (format: "json")
        var ollamaResponse = """
            {
                "message": {
                    "role": "assistant",
                    "content": "{\"name\": \"Bob\"}"
                },
                "done": true,
                "done_reason": "stop",
                "prompt_eval_count": 10,
                "eval_count": 15
            }
            """;

        HttpRequestMessage? capturedRequest = null;
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(ollamaResponse, Encoding.UTF8, "application/json"),
                }
            );

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("http://localhost:11434"),
        };

        var provider = new OllamaProvider(
            httpClient,
            "qwen2.5:0.5b",
            NullLogger<OllamaProvider>.Instance
        );

        var schema =
            """{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}""";
        var messages = new[] { ChatMessage.User("Return a person") };
        var options = new ChatCompletionOptions
        {
            ResponseFormat = ResponseFormat.JsonSchema("Person", schema),
        };

        // Act
        var result = await provider.ChatAsync(messages, options);

        // Assert
        result.Content.Should().Contain("Bob");

        var requestBody = await capturedRequest!.Content!.ReadAsStringAsync();
        requestBody.Should().Contain("\"format\"");
        requestBody.Should().Contain("\"json\"");
    }

    [Fact]
    public async Task OllamaProvider_ChatAsync_WithTextFormat_ShouldNotSendFormatField()
    {
        // Arrange
        var ollamaResponse = """
            {
                "message": {
                    "role": "assistant",
                    "content": "Hello, this is plain text."
                },
                "done": true,
                "done_reason": "stop",
                "prompt_eval_count": 5,
                "eval_count": 8
            }
            """;

        HttpRequestMessage? capturedRequest = null;
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(ollamaResponse, Encoding.UTF8, "application/json"),
                }
            );

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("http://localhost:11434"),
        };

        var provider = new OllamaProvider(
            httpClient,
            "qwen2.5:0.5b",
            NullLogger<OllamaProvider>.Instance
        );

        var messages = new[] { ChatMessage.User("Hello") };
        var options = new ChatCompletionOptions { ResponseFormat = ResponseFormat.Text };

        // Act
        var result = await provider.ChatAsync(messages, options);

        // Assert
        result.Content.Should().Contain("plain text");

        // format field should NOT be present in request (null is omitted by JsonIgnoreCondition.WhenWritingNull)
        var requestBody = await capturedRequest!.Content!.ReadAsStringAsync();
        requestBody.Should().NotContain("\"format\"");
    }

    [Fact]
    public async Task OllamaProvider_ChatAsync_WithoutResponseFormat_ShouldNotSendFormatField()
    {
        // Arrange
        var ollamaResponse = """
            {
                "message": {
                    "role": "assistant",
                    "content": "No format specified."
                },
                "done": true,
                "done_reason": "stop",
                "prompt_eval_count": 5,
                "eval_count": 5
            }
            """;

        HttpRequestMessage? capturedRequest = null;
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(ollamaResponse, Encoding.UTF8, "application/json"),
                }
            );

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("http://localhost:11434"),
        };

        var provider = new OllamaProvider(
            httpClient,
            "qwen2.5:0.5b",
            NullLogger<OllamaProvider>.Instance
        );

        var messages = new[] { ChatMessage.User("Hello") };

        // Act - No ResponseFormat set (defaults to null)
        var result = await provider.ChatAsync(messages);

        // Assert
        var requestBody = await capturedRequest!.Content!.ReadAsStringAsync();
        requestBody.Should().NotContain("\"format\"");
    }

    #endregion

    #region ResponseFormatType 枚举测试

    [Theory]
    [InlineData(ResponseFormatType.Text, 0)]
    [InlineData(ResponseFormatType.JsonObject, 1)]
    [InlineData(ResponseFormatType.JsonSchema, 2)]
    public void ResponseFormatType_ShouldHaveExpectedValues(
        ResponseFormatType type,
        int expectedValue
    )
    {
        ((int)type).Should().Be(expectedValue);
    }

    #endregion
}
