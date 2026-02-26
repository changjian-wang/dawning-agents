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
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private IMCPTransport? _transport;
    private Process? _serverProcess;
    private long _requestId;
    private bool _initialized;
    private MCPServerInfo? _serverInfo;
    private MCPServerCapabilities? _serverCapabilities;
    private readonly Dictionary<long, TaskCompletionSource<MCPResponse>> _pendingRequests = new();

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
        _logger.LogInformation(
            "Connecting to MCP Server: {Command} {Arguments}",
            command,
            arguments
        );

        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments ?? string.Empty,
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

        await _transport.StartAsync(cancellationToken);

        // 启动响应监听
        _ = ListenForResponsesAsync(cancellationToken);

        // 发送初始化请求
        await InitializeAsync(cancellationToken);

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
        _transport = transport;
        await _transport.StartAsync(cancellationToken);

        // 启动响应监听
        _ = ListenForResponsesAsync(cancellationToken);

        // 发送初始化请求
        await InitializeAsync(cancellationToken);
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
        );

        _serverInfo = response.ServerInfo;
        _serverCapabilities = response.Capabilities;
        _initialized = true;

        // 发送 initialized 通知
        await SendNotificationAsync(MCPMethods.Initialized, null, cancellationToken);
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
        );

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
        );
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
        );

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
        );
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
        );

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
        );
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
            await _transport!.SendAsync(json, cancellationToken);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            var response = await tcs.Task.WaitAsync(cts.Token);

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
        await _transport!.SendAsync(json, cancellationToken);
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

                var message = await transport.ReceiveAsync(cancellationToken);
                if (message == null)
                {
                    continue;
                }

                var response = JsonSerializer.Deserialize<MCPResponse>(message);
                if (response?.Id == null)
                {
                    continue;
                }

                var id = Convert.ToInt64(response.Id);

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
        if (_transport != null)
        {
            await _transport.DisposeAsync();
        }

        if (_serverProcess != null)
        {
            if (!_serverProcess.HasExited)
            {
                _serverProcess.Kill();
            }
            _serverProcess.Dispose();
        }

        _requestLock.Dispose();
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
