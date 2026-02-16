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

public class OllamaProviderFunctionCallingTests
{
    private static HttpClient CreateMockHttpClient(
        string responseContent,
        HttpStatusCode statusCode = HttpStatusCode.OK
    )
    {
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(responseContent, Encoding.UTF8, "application/json"),
                }
            );

        return new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost:11434") };
    }

    private static HttpClient CreateMockHttpClientCapture(
        string responseContent,
        out Mock<HttpMessageHandler> handlerMock,
        HttpStatusCode statusCode = HttpStatusCode.OK
    )
    {
        handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(responseContent, Encoding.UTF8, "application/json"),
                }
            );

        return new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:11434"),
        };
    }

    [Fact]
    public async Task ChatAsync_WithToolCalls_ShouldReturnToolCalls()
    {
        // Ollama 返回 tool_calls 的 JSON 格式
        var responseJson = """
            {
                "model": "llama3",
                "message": {
                    "role": "assistant",
                    "content": "",
                    "tool_calls": [
                        {
                            "function": {
                                "name": "get_weather",
                                "arguments": {
                                    "location": "Beijing"
                                }
                            }
                        }
                    ]
                },
                "done": true,
                "done_reason": "stop",
                "prompt_eval_count": 50,
                "eval_count": 30
            }
            """;

        var httpClient = CreateMockHttpClient(responseJson);
        var provider = new OllamaProvider(
            httpClient,
            "llama3",
            NullLogger<OllamaProvider>.Instance
        );

        var tools = new List<ToolDefinition>
        {
            new()
            {
                Name = "get_weather",
                Description = "Get weather for a location",
                ParametersSchema =
                    """{"type":"object","properties":{"location":{"type":"string"}}}""",
            },
        };

        var response = await provider.ChatAsync(
            [ChatMessage.User("What's the weather in Beijing?")],
            new ChatCompletionOptions { Tools = tools }
        );

        response.HasToolCalls.Should().BeTrue();
        response.ToolCalls.Should().HaveCount(1);
        response.ToolCalls![0].FunctionName.Should().Be("get_weather");
        response.ToolCalls[0].Arguments.Should().Contain("Beijing");
        response.FinishReason.Should().Be("tool_calls");
    }

    [Fact]
    public async Task ChatAsync_WithoutToolCalls_ShouldReturnNormalResponse()
    {
        var responseJson = """
            {
                "model": "llama3",
                "message": {
                    "role": "assistant",
                    "content": "Hello! How can I help?"
                },
                "done": true,
                "done_reason": "stop",
                "prompt_eval_count": 10,
                "eval_count": 8
            }
            """;

        var httpClient = CreateMockHttpClient(responseJson);
        var provider = new OllamaProvider(
            httpClient,
            "llama3",
            NullLogger<OllamaProvider>.Instance
        );

        var response = await provider.ChatAsync(
            [ChatMessage.User("Hello")],
            new ChatCompletionOptions()
        );

        response.HasToolCalls.Should().BeFalse();
        response.Content.Should().Be("Hello! How can I help?");
        response.FinishReason.Should().Be("stop");
    }

    [Fact]
    public async Task ChatAsync_MultipleToolCalls_ShouldReturnAll()
    {
        var responseJson = """
            {
                "model": "llama3",
                "message": {
                    "role": "assistant",
                    "content": "",
                    "tool_calls": [
                        {
                            "function": {
                                "name": "search",
                                "arguments": { "query": "test" }
                            }
                        },
                        {
                            "function": {
                                "name": "calculate",
                                "arguments": { "expr": "1+1" }
                            }
                        }
                    ]
                },
                "done": true,
                "prompt_eval_count": 50,
                "eval_count": 30
            }
            """;

        var httpClient = CreateMockHttpClient(responseJson);
        var provider = new OllamaProvider(
            httpClient,
            "llama3",
            NullLogger<OllamaProvider>.Instance
        );

        var response = await provider.ChatAsync([ChatMessage.User("Search and calculate")]);

        response.HasToolCalls.Should().BeTrue();
        response.ToolCalls.Should().HaveCount(2);
        response.ToolCalls![0].FunctionName.Should().Be("search");
        response.ToolCalls[1].FunctionName.Should().Be("calculate");
    }

    [Fact]
    public async Task ChatAsync_ShouldSendToolDefinitions()
    {
        var responseJson = """
            {
                "model": "llama3",
                "message": {
                    "role": "assistant",
                    "content": "Ok"
                },
                "done": true,
                "prompt_eval_count": 10,
                "eval_count": 5
            }
            """;

        var httpClient = CreateMockHttpClientCapture(responseJson, out var handlerMock);
        var provider = new OllamaProvider(
            httpClient,
            "llama3",
            NullLogger<OllamaProvider>.Instance
        );

        var tools = new List<ToolDefinition>
        {
            new()
            {
                Name = "get_weather",
                Description = "Get weather",
                ParametersSchema =
                    """{"type":"object","properties":{"location":{"type":"string"}}}""",
            },
        };

        await provider.ChatAsync(
            [ChatMessage.User("Hello")],
            new ChatCompletionOptions { Tools = tools }
        );

        // 验证发送了请求
        handlerMock
            .Protected()
            .Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post && req.RequestUri!.PathAndQuery == "/api/chat"
                ),
                ItExpr.IsAny<CancellationToken>()
            );
    }

    [Fact]
    public async Task ChatAsync_ToolMessages_ShouldBeSentCorrectly()
    {
        var responseJson = """
            {
                "model": "llama3",
                "message": {
                    "role": "assistant",
                    "content": "The weather is sunny"
                },
                "done": true,
                "prompt_eval_count": 10,
                "eval_count": 5
            }
            """;

        string? capturedBody = null;
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>(
                async (req, _) =>
                {
                    capturedBody = await req.Content!.ReadAsStringAsync();
                }
            )
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
                }
            );

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("http://localhost:11434"),
        };

        var provider = new OllamaProvider(
            httpClient,
            "llama3",
            NullLogger<OllamaProvider>.Instance
        );

        // 模拟 Function Calling 的第二轮对话：包含 assistant+tool_calls 和 tool 消息
        var messages = new List<ChatMessage>
        {
            ChatMessage.User("What's the weather?"),
            ChatMessage.AssistantWithToolCalls([
                new ToolCall("call_1", "get_weather", """{"location":"Beijing"}"""),
            ]),
            ChatMessage.ToolResult("call_1", "Sunny, 25°C"),
        };

        await provider.ChatAsync(messages);

        capturedBody.Should().NotBeNull();

        // 验证请求 body 包含正确的 tool 消息结构
        var bodyJson = JsonDocument.Parse(capturedBody!);
        var messagesArray = bodyJson.RootElement.GetProperty("messages");
        messagesArray.GetArrayLength().Should().Be(3);

        // 验证 tool role 消息
        var toolMsg = messagesArray[2];
        toolMsg.GetProperty("role").GetString().Should().Be("tool");
        toolMsg.GetProperty("content").GetString().Should().Be("Sunny, 25°C");
    }

    [Fact]
    public async Task ChatAsync_ToolCallIds_ShouldBeSequential()
    {
        var responseJson = """
            {
                "model": "llama3",
                "message": {
                    "role": "assistant",
                    "content": "",
                    "tool_calls": [
                        { "function": { "name": "tool_a", "arguments": {} } },
                        { "function": { "name": "tool_b", "arguments": {} } },
                        { "function": { "name": "tool_c", "arguments": {} } }
                    ]
                },
                "done": true,
                "prompt_eval_count": 10,
                "eval_count": 5
            }
            """;

        var httpClient = CreateMockHttpClient(responseJson);
        var provider = new OllamaProvider(
            httpClient,
            "llama3",
            NullLogger<OllamaProvider>.Instance
        );

        var response = await provider.ChatAsync([ChatMessage.User("test")]);

        response.ToolCalls.Should().HaveCount(3);
        response.ToolCalls![0].Id.Should().Be("call_0");
        response.ToolCalls[1].Id.Should().Be("call_1");
        response.ToolCalls[2].Id.Should().Be("call_2");
    }
}
