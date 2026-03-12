namespace Dawning.Agents.MCP.Client;

using System.Diagnostics;
using System.Text.Json;
using Dawning.Agents.MCP.Protocol;
using Dawning.Agents.MCP.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// MCP Client 实现
/// </summary>
/// <remarks>
/// 连接远程 MCP Server，调用远程工具和资源。
/// 支持 stdio 和 HTTP 传输方式。
/// </remarks>
public sealed class MCPClient : IAsyncDisposable
{
    private readonly MCPClientOptions _options;
    private readonly ILogger<MCPClient> _logger;
    private IMCPTransport? _transport;
    private Process? _serverProcess;
    private long _requestId;
    private volatile bool _initialized;
    private MCPServerInfo? _serverInfo;
    private MCPServerCapabilities? _serverCapabilities;
    private readonly Dictionary<long, TaskCompletionSource<MCPResponse>> _pendingRequests = new();
    private CancellationTokenSource? _listenerCts;
    private Task? _listenerTask;
    private volatile bool _disposed;

    public MCPClient(IOptions<MCPClientOptions> options, ILogger<MCPClient>? logger = null)
    {
        _options = options.Value;
        _logger =
            logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MCPClient>.Instance;
    }

    /// <summary>
    /// 是否已连接
    /// </summary>
    public bool IsConnected => _transport?.IsConnected ?? false;

    /// <summary>
    /// 是否已初始化
    /// </summary>
    public bool IsInitialized => _initialized;

    /// <summary>
    /// 服务器信息
    /// </summary>
    public MCPServerInfo? ServerInfo => _serverInfo;

    /// <summary>
    /// 服务器能力
    /// </summary>
    public MCPServerCapabilities? ServerCapabilities => _serverCapabilities;

    /// <summary>
    /// 连接到 MCP Server（通过启动进程）
    /// </summary>
    /// <param name="command">服务器命令（如 "python mcp_server.py"）</param>
    /// <param name="arguments">命令参数</param>
    /// <param name="workingDirectory">工作目录</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task ConnectAsync(
        string command,
        string? arguments = null,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default
    )
    {
        if (IsConnected)
        {
            throw new InvalidOperationException(
                "Already connected to MCP Server. Dispose first before reconnecting."
            );
        }

        _logger.LogInformation(
            "Connecting to MCP Server: {Command} {Arguments}",
            command,
            arguments
        );

        var (fileName, processArguments) = ResolveProcessCommand(command, arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = processArguments,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        _serverProcess = new Process { StartInfo = startInfo };

        try
        {
            _serverProcess.Start();

            // Drain stderr to prevent pipe deadlock
            _serverProcess.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    _logger.LogDebug("MCP Server stderr: {Line}", e.Data);
                }
            };
            _serverProcess.BeginErrorReadLine();
        }
        catch
        {
            _serverProcess.Dispose();
            _serverProcess = null;
            throw;
        }

        _transport = new StdioTransport(
            _serverProcess.StandardOutput.BaseStream,
            _serverProcess.StandardInput.BaseStream,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<StdioTransport>.Instance
        );

        try
        {
            await _transport.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            try
            {
                _serverProcess.Kill();
            }
            catch
            {
                // Best effort - process may have already exited
            }

            _serverProcess.Dispose();
            _serverProcess = null;
            _transport = null;
            throw;
        }

        // 启动响应监听
        _listenerCts?.Dispose();
        _listenerCts = new CancellationTokenSource();
        _listenerTask = ListenForResponsesAsync(_listenerCts.Token);

        // 发送初始化请求
        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }

        _logger.LogInformation(
            "Connected to MCP Server: {Name} v{Version}",
            _serverInfo?.Name,
            _serverInfo?.Version
        );
    }

    /// <summary>
    /// 连接到已有的传输层
    /// </summary>
    public async Task ConnectAsync(
        IMCPTransport transport,
        CancellationToken cancellationToken = default
    )
    {
        if (IsConnected)
        {
            throw new InvalidOperationException(
                "Already connected to MCP Server. Dispose first before reconnecting."
            );
        }

        _transport = transport;
        await _transport.StartAsync(cancellationToken).ConfigureAwait(false);

        // 启动响应监听
        _listenerCts?.Dispose();
        _listenerCts = new CancellationTokenSource();
        _listenerTask = ListenForResponsesAsync(_listenerCts.Token);

        // 发送初始化请求
        try
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static (string FileName, string Arguments) ResolveProcessCommand(
        string command,
        string? arguments
    )
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Command cannot be null or whitespace", nameof(command));
        }

        if (!string.IsNullOrWhiteSpace(arguments))
        {
            return (command.Trim(), arguments);
        }

        var trimmed = command.Trim();

        if (trimmed.StartsWith('"'))
        {
            var closingQuote = trimmed.IndexOf('"', 1);
            if (closingQuote > 1)
            {
                var fileName = trimmed[1..closingQuote];
                var remainingArgs = trimmed[(closingQuote + 1)..].TrimStart();
                return (fileName, remainingArgs);
            }
        }

        var separatorIndex = trimmed.IndexOfAny([' ', '\t']);
        if (separatorIndex < 0)
        {
            return (trimmed, string.Empty);
        }

        return (trimmed[..separatorIndex], trimmed[(separatorIndex + 1)..].TrimStart());
    }

    /// <summary>
    /// 发送初始化请求
    /// </summary>
    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var initParams = new InitializeParams
        {
            ProtocolVersion = MCPProtocolVersion.Latest,
            Capabilities = new MCPClientCapabilities
            {
                Roots = new RootsCapability { ListChanged = true },
            },
            ClientInfo = new MCPClientInfo { Name = _options.Name, Version = _options.Version },
        };

        var response = await SendRequestAsync<InitializeResult>(
                MCPMethods.Initialize,
                initParams,
                TimeSpan.FromSeconds(_options.ConnectionTimeoutSeconds),
                cancellationToken
            )
            .ConfigureAwait(false);

        _serverInfo = response.ServerInfo;
        _serverCapabilities = response.Capabilities;
        _initialized = true;

        // 发送 initialized 通知
        await SendNotificationAsync(MCPMethods.Initialized, null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 获取可用工具列表
    /// </summary>
    public async Task<List<MCPToolDefinition>> ListToolsAsync(
        CancellationToken cancellationToken = default
    )
    {
        EnsureInitialized();

        var result = await SendRequestAsync<ListToolsResult>(
                MCPMethods.ToolsList,
                new ListToolsParams(),
                TimeSpan.FromSeconds(_options.RequestTimeoutSeconds),
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.Tools;
    }

    /// <summary>
    /// 调用工具
    /// </summary>
    public async Task<CallToolResult> CallToolAsync(
        string toolName,
        Dictionary<string, object?>? arguments = null,
        CancellationToken cancellationToken = default
    )
    {
        EnsureInitialized();

        var callParams = new CallToolParams { Name = toolName, Arguments = arguments };

        return await SendRequestAsync<CallToolResult>(
                MCPMethods.ToolsCall,
                callParams,
                TimeSpan.FromSeconds(_options.ToolCallTimeoutSeconds),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 获取资源列表
    /// </summary>
    public async Task<List<MCPResource>> ListResourcesAsync(
        CancellationToken cancellationToken = default
    )
    {
        EnsureInitialized();

        var result = await SendRequestAsync<ListResourcesResult>(
                MCPMethods.ResourcesList,
                new ListResourcesParams(),
                TimeSpan.FromSeconds(_options.RequestTimeoutSeconds),
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.Resources;
    }

    /// <summary>
    /// 读取资源
    /// </summary>
    public async Task<ReadResourceResult> ReadResourceAsync(
        string uri,
        CancellationToken cancellationToken = default
    )
    {
        EnsureInitialized();

        var readParams = new ReadResourceParams { Uri = uri };

        return await SendRequestAsync<ReadResourceResult>(
                MCPMethods.ResourcesRead,
                readParams,
                TimeSpan.FromSeconds(_options.RequestTimeoutSeconds),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 获取提示词列表
    /// </summary>
    public async Task<List<MCPPrompt>> ListPromptsAsync(
        CancellationToken cancellationToken = default
    )
    {
        EnsureInitialized();

        var result = await SendRequestAsync<ListPromptsResult>(
                MCPMethods.PromptsList,
                new ListPromptsParams(),
                TimeSpan.FromSeconds(_options.RequestTimeoutSeconds),
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.Prompts;
    }

    /// <summary>
    /// 获取提示词
    /// </summary>
    public async Task<GetPromptResult> GetPromptAsync(
        string name,
        Dictionary<string, string>? arguments = null,
        CancellationToken cancellationToken = default
    )
    {
        EnsureInitialized();

        var getParams = new GetPromptParams { Name = name, Arguments = arguments };

        return await SendRequestAsync<GetPromptResult>(
                MCPMethods.PromptsGet,
                getParams,
                TimeSpan.FromSeconds(_options.RequestTimeoutSeconds),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 发送请求并等待响应
    /// </summary>
    private async Task<TResult> SendRequestAsync<TResult>(
        string method,
        object? @params,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        EnsureConnected();

        var id = Interlocked.Increment(ref _requestId);
        var request = new MCPRequest
        {
            Id = id,
            Method = method,
            Params = @params,
        };

        var tcs = new TaskCompletionSource<MCPResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        lock (_pendingRequests)
        {
            _pendingRequests[id] = tcs;
        }

        try
        {
            var json = JsonSerializer.Serialize(request);
            await _transport!.SendAsync(json, cancellationToken).ConfigureAwait(false);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            MCPResponse response;
            try
            {
                response = await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(MCPClient),
                    "MCP client was disposed while awaiting response"
                );
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new MCPException(
                    MCPErrorCodes.InternalError,
                    $"Request '{method}' timed out after {timeout.TotalSeconds}s"
                );
            }

            if (response.Error != null)
            {
                throw new MCPException(response.Error.Code, response.Error.Message);
            }

            var resultJson = JsonSerializer.Serialize(response.Result);
            return JsonSerializer.Deserialize<TResult>(resultJson)
                ?? throw new MCPException(
                    MCPErrorCodes.InternalError,
                    "Failed to deserialize result"
                );
        }
        finally
        {
            lock (_pendingRequests)
            {
                _pendingRequests.Remove(id);
            }
        }
    }

    /// <summary>
    /// 发送通知（不等待响应）
    /// </summary>
    private async Task SendNotificationAsync(
        string method,
        object? @params,
        CancellationToken cancellationToken
    )
    {
        EnsureConnected();

        var notification = new MCPNotification { Method = method, Params = @params };

        var json = JsonSerializer.Serialize(notification);
        await _transport!.SendAsync(json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 监听响应
    /// </summary>
    private async Task ListenForResponsesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && (_transport?.IsConnected ?? false))
        {
            try
            {
                var transport = _transport;
                if (transport == null || !transport.IsConnected)
                {
                    break;
                }

                var message = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                if (message == null)
                {
                    continue;
                }

                var response = JsonSerializer.Deserialize<MCPResponse>(message);
                if (response?.Id == null)
                {
                    continue;
                }

                if (!long.TryParse(response.Id.ToString(), out var id))
                {
                    _logger.LogWarning("Received response with non-numeric ID: {Id}", response.Id);
                    continue;
                }

                TaskCompletionSource<MCPResponse>? tcs;
                lock (_pendingRequests)
                {
                    _pendingRequests.TryGetValue(id, out tcs);
                }

                tcs?.TrySetResult(response);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving response");
            }
        }
    }

    private void EnsureConnected()
    {
        if (_transport == null || !_transport.IsConnected)
        {
            throw new InvalidOperationException("Not connected to MCP Server");
        }
    }

    private void EnsureInitialized()
    {
        EnsureConnected();
        if (!_initialized)
        {
            throw new InvalidOperationException("MCP Client not initialized");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_listenerCts != null)
        {
            await _listenerCts.CancelAsync().ConfigureAwait(false);
        }

        if (_listenerTask != null)
        {
            try
            {
                await _listenerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }

        if (_transport != null)
        {
            await _transport.DisposeAsync().ConfigureAwait(false);
        }

        // Cancel all in-flight requests so callers are not blocked until their timeouts expire
        lock (_pendingRequests)
        {
            foreach (var tcs in _pendingRequests.Values)
            {
                tcs.TrySetCanceled();
            }
            _pendingRequests.Clear();
        }

        if (_serverProcess != null)
        {
            try
            {
                if (!_serverProcess.HasExited)
                {
                    _serverProcess.Kill();
                }
            }
            catch (InvalidOperationException)
            {
                // Process already exited between HasExited check and Kill()
            }
            _serverProcess.Dispose();
        }

        _listenerCts?.Dispose();
    }
}

/// <summary>
/// MCP 异常
/// </summary>
public class MCPException : Exception
{
    public int ErrorCode { get; }

    public MCPException(int errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
