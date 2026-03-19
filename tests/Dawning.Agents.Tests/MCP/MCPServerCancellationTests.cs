namespace Dawning.Agents.Tests.MCP;

using System.Reflection;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.MCP.Protocol;
using Dawning.Agents.MCP.Server;
using Dawning.Agents.MCP.Transport;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

public class MCPServerCancellationTests
{
    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        var act = () => new MCPServer(null!, new StubToolReader([]), new NoopTransport());
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public void Constructor_NullToolRegistry_ThrowsArgumentNullException()
    {
        var options = Options.Create(new MCPServerOptions());
        var act = () => new MCPServer(options, null!, new NoopTransport());
        act.Should().Throw<ArgumentNullException>().WithParameterName("toolRegistry");
    }

    [Fact]
    public void Constructor_NullTransport_ThrowsArgumentNullException()
    {
        var options = Options.Create(new MCPServerOptions());
        var act = () => new MCPServer(options, new StubToolReader([]), null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("transport");
    }

    [Fact]
    public async Task HandleToolsCallAsync_Should_Return_Cancelled_When_External_Cancellation_Triggered()
    {
        var server = CreateServer(new DelayedTool(), toolTimeoutSeconds: 30);
        var request = CreateToolCallRequest("delayed");

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var response = await InvokeHandleToolsCallAsync(server, request, cts.Token);

        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(MCPErrorCodes.ToolExecutionFailed);
        response.Error.Message.Should().Be("Tool execution cancelled");
    }

    [Fact]
    public async Task HandleToolsCallAsync_Should_Return_TimedOut_When_Tool_Timeout_Triggered()
    {
        var server = CreateServer(new DelayedTool(), toolTimeoutSeconds: 1);
        var request = CreateToolCallRequest("delayed");

        var response = await InvokeHandleToolsCallAsync(server, request, CancellationToken.None);

        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(MCPErrorCodes.ToolExecutionFailed);
        response.Error.Message.Should().Be("Tool execution timed out");
    }

    private static MCPServer CreateServer(ITool tool, int toolTimeoutSeconds)
    {
        var options = Options.Create(
            new MCPServerOptions
            {
                EnableTools = true,
                ToolTimeoutSeconds = toolTimeoutSeconds,
                MaxConcurrentRequests = 1,
            }
        );

        var toolReader = new StubToolReader([tool]);
        return new MCPServer(options, toolReader, new NoopTransport());
    }

    private static MCPRequest CreateToolCallRequest(string toolName)
    {
        return new MCPRequest
        {
            Id = 1,
            Method = MCPMethods.ToolsCall,
            Params = new CallToolParams
            {
                Name = toolName,
                Arguments = new Dictionary<string, object?>(),
            },
        };
    }

    private static async Task<MCPResponse> InvokeHandleToolsCallAsync(
        MCPServer server,
        MCPRequest request,
        CancellationToken cancellationToken
    )
    {
        var method = typeof(MCPServer).GetMethod(
            "HandleToolsCallAsync",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        method.Should().NotBeNull();

        var task = method!.Invoke(server, [request, cancellationToken]) as Task<MCPResponse>;
        task.Should().NotBeNull();

        return await task!;
    }

    private sealed class DelayedTool : ITool
    {
        public string Name => "delayed";

        public string Description => "Delayed tool for cancellation tests";

        public string ParametersSchema => "{\"type\":\"object\"}";

        public bool RequiresConfirmation => false;

        public ToolRiskLevel RiskLevel => ToolRiskLevel.Low;

        public string? Category => "test";

        public async Task<ToolResult> ExecuteAsync(
            string input,
            CancellationToken cancellationToken = default
        )
        {
            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            return ToolResult.Ok("ok");
        }
    }

    private sealed class StubToolReader : IToolReader
    {
        private readonly Dictionary<string, ITool> _tools;

        public StubToolReader(IEnumerable<ITool> tools)
        {
            _tools = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        }

        public int Count => _tools.Count;

        public ITool? GetTool(string name)
        {
            return _tools.TryGetValue(name, out var tool) ? tool : null;
        }

        public IReadOnlyList<ITool> GetAllTools()
        {
            return _tools.Values.ToList();
        }

        public bool HasTool(string name)
        {
            return _tools.ContainsKey(name);
        }

        public IReadOnlyList<ITool> GetToolsByCategory(string category)
        {
            return _tools
                .Values.Where(t =>
                    string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase)
                )
                .ToList();
        }

        public IReadOnlyList<string> GetCategories()
        {
            return _tools
                .Values.Select(t => t.Category)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    private sealed class NoopTransport : IMCPTransport
    {
        public bool IsConnected => false;

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public Task<string?> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }

        public Task SendAsync(string message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
