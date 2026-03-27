namespace Dawning.Agents.MCP.Client;

using System.Text.Json;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.MCP.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Wraps a remote MCP Server tool as an <see cref="ITool"/>.
/// </summary>
/// <remarks>
/// Wraps a remote MCP Server tool as a Dawning.Agents ITool interface,
/// enabling direct invocation by agents.
/// </remarks>
public sealed class MCPToolProxy : ITool
{
    private readonly MCPClient _client;
    private readonly MCPToolDefinition _definition;
    private readonly ILogger<MCPToolProxy> _logger;

    public MCPToolProxy(
        MCPClient client,
        MCPToolDefinition definition,
        ILogger<MCPToolProxy>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(definition);
        _client = client;
        _definition = definition;
        _logger = logger ?? NullLogger<MCPToolProxy>.Instance;
    }

    private string? _cachedParametersSchema;

    public string Name => _definition.Name;

    public string Description => _definition.Description ?? string.Empty;

    public string ParametersSchema =>
        _cachedParametersSchema ??= JsonSerializer.Serialize(_definition.InputSchema);

    public bool RequiresConfirmation => false;

    public ToolRiskLevel RiskLevel => ToolRiskLevel.Medium; // Remote tools default to medium risk

    public string? Category => "MCP";

    public async Task<ToolResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            Dictionary<string, object?>? arguments = null;

            if (!string.IsNullOrWhiteSpace(input))
            {
                arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(input);
            }

            var result = await _client
                .CallToolAsync(_definition.Name, arguments, cancellationToken)
                .ConfigureAwait(false);

            // Extract text content
            var output = string.Join(
                "\n",
                result.Content.Where(c => c.Type == "text" && c.Text != null).Select(c => c.Text)
            );

            return result.IsError ? ToolResult.Fail(output) : ToolResult.Ok(output);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (MCPException ex)
        {
            _logger.LogError(ex, "MCP tool call failed: {Tool}", _definition.Name);
            return ToolResult.Fail($"MCP tool call failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Remote tool execution failed: {Tool}", _definition.Name);
            return ToolResult.Fail($"Remote tool execution failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Provides extension methods for registering MCP tools.
/// </summary>
public static class MCPToolRegistryExtensions
{
    /// <summary>
    /// Registers all remote tools from an MCP Client.
    /// </summary>
    public static async Task RegisterMCPToolsAsync(
        this IToolRegistrar registry,
        MCPClient client,
        CancellationToken cancellationToken = default
    )
    {
        var tools = await client.ListToolsAsync(cancellationToken).ConfigureAwait(false);

        foreach (var tool in tools)
        {
            var proxy = new MCPToolProxy(client, tool);
            registry.Register(proxy);
        }
    }
}
