namespace Dawning.Agents.MCP.Server;

using System.Text.Json;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.MCP.Protocol;
using Dawning.Agents.MCP.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// MCP Server 实现
/// </summary>
/// <remarks>
/// 实现 Anthropic Model Context Protocol，使 Dawning.Agents 的工具、资源和提示词
/// 可以被 Claude Desktop、Cursor 等 MCP 兼容工具调用。
/// </remarks>
public sealed class MCPServer : IAsyncDisposable
{
    private readonly MCPServerOptions _options;
    private readonly IToolReader _toolRegistry;
    private readonly IMCPTransport _transport;
    private readonly ILogger<MCPServer> _logger;
    private readonly List<IMCPResourceProvider> _resourceProviders = [];
    private readonly List<IMCPPromptProvider> _promptProviders = [];
    private readonly SemaphoreSlim _requestSemaphore;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _inflightTasks = [];
    private readonly Lock _inflightLock = new();
    private bool _initialized;
    private MCPClientInfo? _clientInfo;

    public MCPServer(
        IOptions<MCPServerOptions> options,
        IToolReader toolRegistry,
        IMCPTransport transport,
        ILogger<MCPServer>? logger = null
    )
    {
        _options = options.Value;
        _toolRegistry = toolRegistry;
        _transport = transport;
        _logger =
            logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MCPServer>.Instance;
        _requestSemaphore = new SemaphoreSlim(_options.MaxConcurrentRequests);
    }

    /// <summary>
    /// 是否已初始化
    /// </summary>
    public bool IsInitialized => _initialized;

    /// <summary>
    /// 客户端信息
    /// </summary>
    public MCPClientInfo? ClientInfo => _clientInfo;

    /// <summary>
    /// 注册资源提供者
    /// </summary>
    public void RegisterResourceProvider(IMCPResourceProvider provider)
    {
        _resourceProviders.Add(provider);
        _logger.LogDebug("Registered resource provider: {Provider}", provider.GetType().Name);
    }

    /// <summary>
    /// 注册提示词提供者
    /// </summary>
    public void RegisterPromptProvider(IMCPPromptProvider provider)
    {
        _promptProviders.Add(provider);
        _logger.LogDebug("Registered prompt provider: {Provider}", provider.GetType().Name);
    }

    /// <summary>
    /// 启动 MCP Server
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _cts.Token
        );

        _logger.LogInformation(
            "Starting MCP Server: {Name} v{Version}",
            _options.Name,
            _options.Version
        );

        await _transport.StartAsync(linkedCts.Token).ConfigureAwait(false);

        // 主消息循环
        while (!linkedCts.Token.IsCancellationRequested)
        {
            try
            {
                var message = await _transport.ReceiveAsync(linkedCts.Token).ConfigureAwait(false);
                if (message == null)
                {
                    continue;
                }

                // 限制并发请求
                await _requestSemaphore.WaitAsync(linkedCts.Token).ConfigureAwait(false);
                var task = ProcessAndReleaseAsync(message, linkedCts.Token);
                lock (_inflightLock)
                {
                    _inflightTasks.Add(task);
                }

                _ = task.ContinueWith(
                    _ =>
                    {
                        lock (_inflightLock)
                        {
                            _inflightTasks.Remove(task);
                        }
                    },
                    TaskScheduler.Default
                );
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving message");
            }
        }

        _logger.LogInformation("MCP Server stopped");
    }

    /// <summary>
    /// 处理消息并释放信号量
    /// </summary>
    private async Task ProcessAndReleaseAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            await ProcessMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error processing message");
        }
        finally
        {
            try
            {
                _requestSemaphore.Release();
            }
            catch (ObjectDisposedException) { }
        }
    }

    /// <summary>
    /// 处理单个消息
    /// </summary>
    private async Task ProcessMessageAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            var request = JsonSerializer.Deserialize<MCPRequest>(message);
            if (request == null)
            {
                await SendErrorAsync(
                        null,
                        MCPErrorCodes.ParseError,
                        "Invalid JSON",
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                return;
            }

            _logger.LogDebug("Processing request: {Method}", request.Method);

            var response = await HandleRequestAsync(request, cancellationToken)
                .ConfigureAwait(false);
            if (response != null)
            {
                await SendResponseAsync(response, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "JSON parse error");
            await SendErrorAsync(
                    null,
                    MCPErrorCodes.ParseError,
                    "Invalid JSON format",
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            await SendErrorAsync(
                    null,
                    MCPErrorCodes.InternalError,
                    "Internal server error",
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 处理请求并返回响应
    /// </summary>
    private async Task<MCPResponse?> HandleRequestAsync(
        MCPRequest request,
        CancellationToken cancellationToken
    )
    {
        return request.Method switch
        {
            MCPMethods.Initialize => await HandleInitializeAsync(request, cancellationToken)
                .ConfigureAwait(false),
            MCPMethods.Shutdown => HandleShutdown(request),
            MCPMethods.ToolsList => HandleToolsList(request),
            MCPMethods.ToolsCall => await HandleToolsCallAsync(request, cancellationToken)
                .ConfigureAwait(false),
            MCPMethods.ResourcesList => HandleResourcesList(request),
            MCPMethods.ResourcesRead => await HandleResourcesReadAsync(request, cancellationToken)
                .ConfigureAwait(false),
            MCPMethods.PromptsList => HandlePromptsList(request),
            MCPMethods.PromptsGet => await HandlePromptsGetAsync(request, cancellationToken)
                .ConfigureAwait(false),

            // 通知方法不需要响应
            MCPMethods.Initialized => null,

            _ => MCPResponse.Failure(
                request.Id,
                MCPErrorCodes.MethodNotFound,
                $"Unknown method: {request.Method}"
            ),
        };
    }

    /// <summary>
    /// 处理初始化请求
    /// </summary>
    private Task<MCPResponse> HandleInitializeAsync(
        MCPRequest request,
        CancellationToken cancellationToken
    )
    {
        var paramsJson = JsonSerializer.Serialize(request.Params);
        var initParams = JsonSerializer.Deserialize<InitializeParams>(paramsJson);

        if (initParams == null)
        {
            return Task.FromResult(
                MCPResponse.Failure(
                    request.Id,
                    MCPErrorCodes.InvalidParams,
                    "Invalid initialize params"
                )
            );
        }

        _clientInfo = initParams.ClientInfo;
        _initialized = true;

        _logger.LogInformation(
            "Client connected: {Name} v{Version}",
            _clientInfo.Name,
            _clientInfo.Version
        );

        var result = new InitializeResult
        {
            ProtocolVersion = MCPProtocolVersion.Latest,
            Capabilities = BuildServerCapabilities(),
            ServerInfo = new MCPServerInfo { Name = _options.Name, Version = _options.Version },
        };

        return Task.FromResult(MCPResponse.Success(request.Id, result));
    }

    /// <summary>
    /// 处理关闭请求
    /// </summary>
    private MCPResponse HandleShutdown(MCPRequest request)
    {
        _logger.LogInformation("Shutdown requested");
        _cts.Cancel();
        return MCPResponse.Success(request.Id, null);
    }

    /// <summary>
    /// 处理工具列表请求
    /// </summary>
    private MCPResponse HandleToolsList(MCPRequest request)
    {
        if (!_options.EnableTools)
        {
            return MCPResponse.Success(request.Id, new ListToolsResult { Tools = [] });
        }

        var tools = _toolRegistry
            .GetAllTools()
            .Select(t => new MCPToolDefinition
            {
                Name = t.Name,
                Description = t.Description,
                InputSchema = ParseJsonSchema(t.ParametersSchema),
            })
            .ToList();

        return MCPResponse.Success(request.Id, new ListToolsResult { Tools = tools });
    }

    /// <summary>
    /// 处理工具调用请求
    /// </summary>
    private async Task<MCPResponse> HandleToolsCallAsync(
        MCPRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!_options.EnableTools)
        {
            return MCPResponse.Failure(
                request.Id,
                MCPErrorCodes.MethodNotFound,
                "Tools not enabled"
            );
        }

        var paramsJson = JsonSerializer.Serialize(request.Params);
        var callParams = JsonSerializer.Deserialize<CallToolParams>(paramsJson);

        if (callParams == null)
        {
            return MCPResponse.Failure(
                request.Id,
                MCPErrorCodes.InvalidParams,
                "Invalid call params"
            );
        }

        var tool = _toolRegistry.GetTool(callParams.Name);
        if (tool == null)
        {
            return MCPResponse.Failure(
                request.Id,
                MCPErrorCodes.ToolNotFound,
                $"Tool not found: {callParams.Name}"
            );
        }

        try
        {
            using var timeoutCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(_options.ToolTimeoutSeconds)
            );
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCts.Token
            );

            var inputJson =
                callParams.Arguments != null
                    ? JsonSerializer.Serialize(callParams.Arguments)
                    : "{}";

            var result = await tool.ExecuteAsync(inputJson, linkedCts.Token).ConfigureAwait(false);

            var callResult = new CallToolResult
            {
                Content = [MCPContent.TextContent(result.Output)],
                IsError = !result.Success,
            };

            return MCPResponse.Success(request.Id, callResult);
        }
        catch (OperationCanceledException)
        {
            return MCPResponse.Failure(
                request.Id,
                MCPErrorCodes.ToolExecutionFailed,
                "Tool execution timed out"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool execution failed: {Tool}", callParams.Name);
            return MCPResponse.Failure(
                request.Id,
                MCPErrorCodes.ToolExecutionFailed,
                "Tool execution failed"
            );
        }
    }

    /// <summary>
    /// 处理资源列表请求
    /// </summary>
    private MCPResponse HandleResourcesList(MCPRequest request)
    {
        if (!_options.EnableResources)
        {
            return MCPResponse.Success(request.Id, new ListResourcesResult { Resources = [] });
        }

        var resources = _resourceProviders.SelectMany(p => p.GetResources()).ToList();

        return MCPResponse.Success(request.Id, new ListResourcesResult { Resources = resources });
    }

    /// <summary>
    /// 处理资源读取请求
    /// </summary>
    private async Task<MCPResponse> HandleResourcesReadAsync(
        MCPRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!_options.EnableResources)
        {
            return MCPResponse.Failure(
                request.Id,
                MCPErrorCodes.MethodNotFound,
                "Resources not enabled"
            );
        }

        var paramsJson = JsonSerializer.Serialize(request.Params);
        var readParams = JsonSerializer.Deserialize<ReadResourceParams>(paramsJson);

        if (readParams == null || string.IsNullOrEmpty(readParams.Uri))
        {
            return MCPResponse.Failure(
                request.Id,
                MCPErrorCodes.InvalidParams,
                "Invalid resource URI"
            );
        }

        foreach (var provider in _resourceProviders)
        {
            var content = await provider
                .ReadResourceAsync(readParams.Uri, cancellationToken)
                .ConfigureAwait(false);
            if (content != null)
            {
                return MCPResponse.Success(
                    request.Id,
                    new ReadResourceResult { Contents = [content] }
                );
            }
        }

        return MCPResponse.Failure(
            request.Id,
            MCPErrorCodes.ResourceNotFound,
            $"Resource not found: {readParams.Uri}"
        );
    }

    /// <summary>
    /// 处理提示词列表请求
    /// </summary>
    private MCPResponse HandlePromptsList(MCPRequest request)
    {
        if (!_options.EnablePrompts)
        {
            return MCPResponse.Success(request.Id, new ListPromptsResult { Prompts = [] });
        }

        var prompts = _promptProviders.SelectMany(p => p.GetPrompts()).ToList();

        return MCPResponse.Success(request.Id, new ListPromptsResult { Prompts = prompts });
    }

    /// <summary>
    /// 处理获取提示词请求
    /// </summary>
    private async Task<MCPResponse> HandlePromptsGetAsync(
        MCPRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!_options.EnablePrompts)
        {
            return MCPResponse.Failure(
                request.Id,
                MCPErrorCodes.MethodNotFound,
                "Prompts not enabled"
            );
        }

        var paramsJson = JsonSerializer.Serialize(request.Params);
        var getParams = JsonSerializer.Deserialize<GetPromptParams>(paramsJson);

        if (getParams == null || string.IsNullOrEmpty(getParams.Name))
        {
            return MCPResponse.Failure(
                request.Id,
                MCPErrorCodes.InvalidParams,
                "Invalid prompt name"
            );
        }

        foreach (var provider in _promptProviders)
        {
            var result = await provider
                .GetPromptAsync(getParams.Name, getParams.Arguments, cancellationToken)
                .ConfigureAwait(false);

            if (result != null)
            {
                return MCPResponse.Success(request.Id, result);
            }
        }

        return MCPResponse.Failure(
            request.Id,
            MCPErrorCodes.ResourceNotFound,
            $"Prompt not found: {getParams.Name}"
        );
    }

    /// <summary>
    /// 构建服务器能力声明
    /// </summary>
    private MCPServerCapabilities BuildServerCapabilities()
    {
        return new MCPServerCapabilities
        {
            Tools = _options.EnableTools ? new ToolsCapability { ListChanged = true } : null,
            Resources = _options.EnableResources
                ? new ResourcesCapability { Subscribe = true, ListChanged = true }
                : null,
            Prompts = _options.EnablePrompts ? new PromptsCapability { ListChanged = true } : null,
            Logging = _options.EnableLogging ? new LoggingCapability() : null,
        };
    }

    /// <summary>
    /// 解析 JSON Schema 字符串
    /// </summary>
    private MCPInputSchema ParseJsonSchema(string schemaJson)
    {
        try
        {
            return JsonSerializer.Deserialize<MCPInputSchema>(schemaJson) ?? new MCPInputSchema();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse tool JSON schema");
            return new MCPInputSchema();
        }
    }

    /// <summary>
    /// 发送响应
    /// </summary>
    private async Task SendResponseAsync(MCPResponse response, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(response);
        await _transport.SendAsync(json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 发送错误响应
    /// </summary>
    private async Task SendErrorAsync(
        object? id,
        int code,
        string message,
        CancellationToken cancellationToken
    )
    {
        var response = MCPResponse.Failure(id, code, message);
        await SendResponseAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 发送通知（无需响应）
    /// </summary>
    public async Task SendNotificationAsync(
        string method,
        object? @params = null,
        CancellationToken cancellationToken = default
    )
    {
        var notification = new MCPNotification { Method = method, Params = @params };
        var json = JsonSerializer.Serialize(notification);
        await _transport.SendAsync(json, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();

        // Await all in-flight request tasks
        Task[] snapshot;
        lock (_inflightLock)
        {
            snapshot = [.. _inflightTasks];
        }

        if (snapshot.Length > 0)
        {
            try
            {
                await Task.WhenAll(snapshot).ConfigureAwait(false);
            }
            catch
            {
                // Errors already logged in ProcessAndReleaseAsync
            }
        }

        _requestSemaphore.Dispose();
        _cts.Dispose();
        await _transport.DisposeAsync().ConfigureAwait(false);
    }
}
