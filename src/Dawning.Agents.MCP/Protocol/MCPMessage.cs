namespace Dawning.Agents.MCP.Protocol;

using System.Text.Json.Serialization;

/// <summary>
/// MCP JSON-RPC 请求消息
/// </summary>
public sealed class MCPRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public object? Id { get; set; }

    [JsonPropertyName("method")]
    public required string Method { get; set; }

    [JsonPropertyName("params")]
    public object? Params { get; set; }
}

/// <summary>
/// MCP JSON-RPC 响应消息
/// </summary>
public sealed class MCPResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public object? Id { get; set; }

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    public MCPError? Error { get; set; }

    public static MCPResponse Success(object? id, object? result) =>
        new() { Id = id, Result = result };

    public static MCPResponse Failure(object? id, int code, string message) =>
        new()
        {
            Id = id,
            Error = new MCPError { Code = code, Message = message },
        };
}

/// <summary>
/// MCP 错误对象
/// </summary>
public sealed class MCPError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public required string Message { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }
}

/// <summary>
/// MCP JSON-RPC 通知消息（无需响应）
/// </summary>
public sealed class MCPNotification
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("method")]
    public required string Method { get; set; }

    [JsonPropertyName("params")]
    public object? Params { get; set; }
}

/// <summary>
/// MCP 错误代码
/// </summary>
public static class MCPErrorCodes
{
    // JSON-RPC 标准错误
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;

    // MCP 自定义错误
    public const int ResourceNotFound = -32001;
    public const int ToolNotFound = -32002;
    public const int ToolExecutionFailed = -32003;
}
