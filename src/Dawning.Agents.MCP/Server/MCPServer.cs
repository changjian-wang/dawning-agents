namespace Dawning.Agents.MCP.Server;

using System.Text.Json;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.MCP.Protocol;
using Dawning.Agents.MCP.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// MCP Server implementation.
/// </summary>
/// <remarks>
/// Implements the Anthropic Model Context Protocol, exposing Dawning.Agents tools, resources,
/// and prompts for invocation by MCP-compatible clients such as Claude Desktop and Cursor.
/// </remarks>
public sealed class MCPServer : IAsyncDisposable
{
    private readonly MCPServerOptions _options;
    private readonly IToolReader _toolRegistry;
    private readonly IMCPTransport _transport;
    private readonly ILogger<MCPServer> _logger;
    private readonly List<IMCPResourceProvider> _resourceProviders = [];
    private readonly List<IMCPPromptProvider> _promptProviders = [];
    private readonly Lock _providerLock = new();
    private IMCPResourceProvider[] _frozenResourceProviders = [];
    private IMCPPromptProvider[] _frozenPromptProviders = [];
    private readonly SemaphoreSlim _requestSemaphore;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _inflightTasks = [];
    private readonly Lock _inflightLock = new();
    private volatile bool _initialized;
    private MCPClientInfo? _clientInfo;

    public MCPServer(
        IOptions<MCPServerOptions> options,
        IToolReader toolRegistry,
        IMCPTransport transport,
        ILogger<MCPServer>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(toolRegistry);
        ArgumentNullException.ThrowIfNull(transport);
        _options = options.Value;
        _toolRegistry = toolRegistry;
        _transport = transport;
        _logger =
            logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MCPServer>.Instance;
        _requestSemaphore = new SemaphoreSlim(_options.MaxConcurrentRequests);
    }

    /// <summary>
    /// Gets a value indicating whether the server has been initialized.
    /// </summary>
    public bool IsInitialized => _initialized;

    /// <summary>
    /// Gets the connected client information.
    /// </summary>
    public MCPClientInfo? ClientInfo => _clientInfo;

    /// <summary>
    /// Registers a resource provider.
    /// </summary>
    public void RegisterResourceProvider(IMCPResourceProvider provider)
    {
        lock (_providerLock)
        {
            _resourceProviders.Add(provider);
        }

        _logger.LogDebug("Registered resource provider: {Provider}", provider.GetType().Name);
    }

    /// <summary>
    /// Registers a prompt provider.
    /// </summary>
    public void RegisterPromptProvider(IMCPPromptProvider provider)
    {
        lock (_providerLock)
        {
            _promptProviders.Add(provider);
        }

        _logger.LogDebug("Registered prompt provider: {Provider}", provider.GetType().Name);
    }

    /// <summary>
    /// Starts the MCP Server.
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

        // Freeze provider lists for thread-safe access during request processing
        lock (_providerLock)
        {
            _frozenResourceProviders = [.. _resourceProviders];
            _frozenPromptProviders = [.. _promptProviders];
        }

        // Main message loop
        while (!linkedCts.Token.IsCancellationRequested)
        {
            try
            {
                var message = await _transport.ReceiveAsync(linkedCts.Token).ConfigureAwait(false);
                if (message == null)
                {
                    if (!_transport.IsConnected)
                    {
                        _logger.LogInformation("Transport disconnected, stopping server");
                        break;
                    }

                    continue;
                }

                // Throttle concurrent requests
                await _requestSemaphore.WaitAsync(linkedCts.Token).ConfigureAwait(false);
                // Use _cts.Token (not linkedCts.Token) for inflight tasks:
                // linkedCts is using-scoped and will be disposed when StartAsync exits,
                // but inflight tasks may still be running and creating linked CTS from this token.
                var task = ProcessAndReleaseAsync(message, _cts.Token);
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
    /// Processes a message and releases the semaphore.
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
    /// Processes a single message.
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
    /// Handles a request and returns a response.
    /// </summary>
    private async Task<MCPResponse?> HandleRequestAsync(
        MCPRequest request,
        CancellationToken cancellationToken
    )
    {
        // Per MCP spec: reject all requests except Initialize before initialization
        if (
            !_initialized
            && request.Method != MCPMethods.Initialize
            && request.Method != MCPMethods.Initialized
        )
        {
            return MCPResponse.Failure(
                request.Id,
                MCPErrorCodes.InvalidRequest,
                "Server not initialized. Send 'initialize' first."
            );
        }

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

            // Notification methods do not require a response
            MCPMethods.Initialized => null,

            _ => MCPResponse.Failure(request.Id, MCPErrorCodes.MethodNotFound, "Unknown method"),
        };
    }

    /// <summary>
    /// Handles the initialization request.
    /// </summary>
    private Task<MCPResponse> HandleInitializeAsync(
        MCPRequest request,
        CancellationToken cancellationToken
    )
    {
        var initParams = DeserializeParams<InitializeParams>(request.Params);

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

        Volatile.Write(ref _clientInfo, initParams.ClientInfo);
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
    /// Handles the shutdown request.
    /// </summary>
    private MCPResponse HandleShutdown(MCPRequest request)
    {
        _logger.LogInformation("Shutdown requested");
        _cts.Cancel();
        return MCPResponse.Success(request.Id, null);
    }

    /// <summary>
    /// Handles the tools list request.
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
    /// Handles a tool call request.
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

        var callParams = DeserializeParams<CallToolParams>(request.Params);

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
            return MCPResponse.Failure(request.Id, MCPErrorCodes.ToolNotFound, "Tool not found");
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return MCPResponse.Failure(
                request.Id,
                MCPErrorCodes.ToolExecutionFailed,
                "Tool execution cancelled"
            );
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
    /// Handles the resources list request.
    /// </summary>
    private MCPResponse HandleResourcesList(MCPRequest request)
    {
        if (!_options.EnableResources)
        {
            return MCPResponse.Success(request.Id, new ListResourcesResult { Resources = [] });
        }

        var resources = _frozenResourceProviders.SelectMany(p => p.GetResources()).ToList();

        return MCPResponse.Success(request.Id, new ListResourcesResult { Resources = resources });
    }

    /// <summary>
    /// Handles a resource read request.
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

        var readParams = DeserializeParams<ReadResourceParams>(request.Params);

        if (readParams == null || string.IsNullOrEmpty(readParams.Uri))
        {
            return MCPResponse.Failure(
                request.Id,
                MCPErrorCodes.InvalidParams,
                "Invalid resource URI"
            );
        }

        foreach (var provider in _frozenResourceProviders)
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
            "Resource not found"
        );
    }

    /// <summary>
    /// Handles the prompts list request.
    /// </summary>
    private MCPResponse HandlePromptsList(MCPRequest request)
    {
        if (!_options.EnablePrompts)
        {
            return MCPResponse.Success(request.Id, new ListPromptsResult { Prompts = [] });
        }

        var prompts = _frozenPromptProviders.SelectMany(p => p.GetPrompts()).ToList();

        return MCPResponse.Success(request.Id, new ListPromptsResult { Prompts = prompts });
    }

    /// <summary>
    /// Handles a get prompt request.
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

        var getParams = DeserializeParams<GetPromptParams>(request.Params);

        if (getParams == null || string.IsNullOrEmpty(getParams.Name))
        {
            return MCPResponse.Failure(
                request.Id,
                MCPErrorCodes.InvalidParams,
                "Invalid prompt name"
            );
        }

        foreach (var provider in _frozenPromptProviders)
        {
            var result = await provider
                .GetPromptAsync(getParams.Name, getParams.Arguments, cancellationToken)
                .ConfigureAwait(false);

            if (result != null)
            {
                return MCPResponse.Success(request.Id, result);
            }
        }

        return MCPResponse.Failure(request.Id, MCPErrorCodes.ResourceNotFound, "Prompt not found");
    }

    /// <summary>
    /// Builds the server capabilities declaration.
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
    /// Parses a JSON Schema string.
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
    /// Sends a response.
    /// </summary>
    private async Task SendResponseAsync(MCPResponse response, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(response);
        await _transport.SendAsync(json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends an error response.
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
    /// Sends a notification (no response expected).
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

    private volatile bool _disposed;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

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
                await Task.WhenAll(snapshot)
                    .WaitAsync(TimeSpan.FromSeconds(10))
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Timeout or errors — already logged in ProcessAndReleaseAsync
            }
        }

        _requestSemaphore.Dispose();
        _cts.Dispose();
        await _transport.DisposeAsync().ConfigureAwait(false);
    }

    private static T? DeserializeParams<T>(object? @params)
        where T : class
    {
        return @params switch
        {
            JsonElement element => element.Deserialize<T>(),
            T typed => typed,
            _ => null,
        };
    }
}
