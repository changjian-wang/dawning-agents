namespace Dawning.Agents.MCP.Client;

using System.Text.Json;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.MCP.Protocol;

/// <summary>
/// MCP 工具代理
/// </summary>
/// <remarks>
/// 将远程 MCP Server 的工具包装为 Dawning.Agents 的 ITool 接口，
/// 使其可以被 Agent 直接调用。
/// </remarks>
public sealed class MCPToolProxy : ITool
{
    private readonly MCPClient _client;
    private readonly MCPToolDefinition _definition;

    public MCPToolProxy(MCPClient client, MCPToolDefinition definition)
    {
        _client = client;
        _definition = definition;
    }

    public string Name => _definition.Name;

    public string Description => _definition.Description ?? string.Empty;

    public string ParametersSchema => JsonSerializer.Serialize(_definition.InputSchema);

    public bool RequiresConfirmation => false;

    public ToolRiskLevel RiskLevel => ToolRiskLevel.Medium; // 远程工具默认中等风险

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

            // 提取文本内容
            var output = string.Join(
                "\n",
                result.Content.Where(c => c.Type == "text" && c.Text != null).Select(c => c.Text)
            );

            return result.IsError ? ToolResult.Fail(output) : ToolResult.Ok(output);
        }
        catch (MCPException ex)
        {
            return ToolResult.Fail($"MCP Error ({ex.ErrorCode}): {ex.Message}");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Error calling remote tool: {ex.Message}");
        }
    }
}

/// <summary>
/// MCP 工具注册表扩展
/// </summary>
public static class MCPToolRegistryExtensions
{
    /// <summary>
    /// 从 MCP Client 注册所有远程工具
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
