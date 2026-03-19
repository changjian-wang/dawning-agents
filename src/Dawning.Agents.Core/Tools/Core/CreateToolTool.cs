using System.Text.Json;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Tools.Core;

/// <summary>
/// 动态工具创建器 — Agent 用来创建新的可复用脚本工具
/// </summary>
/// <remarks>
/// <para>Risk: High — 创建的工具可执行任意脚本</para>
/// <para>scope=session: 无需确认；scope=user/global: 需要审批</para>
/// </remarks>
public sealed class CreateToolTool : ITool
{
    private readonly IToolSession _session;
    private readonly ILogger<CreateToolTool> _logger;

    /// <summary>
    /// 创建 CreateToolTool
    /// </summary>
    public CreateToolTool(IToolSession session, ILogger<CreateToolTool>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _logger = logger ?? NullLogger<CreateToolTool>.Instance;
    }

    /// <inheritdoc />
    public string Name => "create_tool";

    /// <inheritdoc />
    public string Description =>
        "Create a new reusable script tool that can be called later. "
        + "Define the tool's name, description, parameters, and bash script. "
        + "Use $param_name in the script to reference parameters. "
        + "Scope determines persistence: session (memory only), user (cross-project), global (project-level, can be committed to git).";

    /// <inheritdoc />
    public string ParametersSchema =>
        """
            {
                "type": "object",
                "properties": {
                    "name": {
                        "type": "string",
                        "description": "Tool name in snake_case (e.g. count_lines, find_todos)"
                    },
                    "description": {
                        "type": "string",
                        "description": "What the tool does (for LLM to understand when to use it)"
                    },
                    "script": {
                        "type": "string",
                        "description": "Bash script content. Use $param_name for parameter substitution."
                    },
                    "parameters": {
                        "type": "array",
                        "description": "Tool parameters (optional)",
                        "items": {
                            "type": "object",
                            "properties": {
                                "name": { "type": "string", "description": "Parameter name" },
                                "description": { "type": "string", "description": "Parameter description" },
                                "type": { "type": "string", "description": "Parameter type (string, int, bool)", "default": "string" },
                                "required": { "type": "boolean", "description": "Whether the parameter is required", "default": true }
                            },
                            "required": ["name", "description"]
                        }
                    },
                    "scope": {
                        "type": "string",
                        "enum": ["session", "user", "global"],
                        "description": "Persistence scope (default: session)"
                    }
                },
                "required": ["name", "description", "script"]
            }
            """;

    /// <inheritdoc />
    public bool RequiresConfirmation => false;

    /// <inheritdoc />
    public ToolRiskLevel RiskLevel => ToolRiskLevel.High;

    /// <inheritdoc />
    public string? Category => "Core";

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        EphemeralToolDefinition definition;

        try
        {
            definition = ParseDefinition(input);
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Invalid tool definition: {ex.Message}");
        }

        // Validate name format (snake_case)
        if (!IsValidToolName(definition.Name))
        {
            return ToolResult.Fail(
                "Tool name must be snake_case (lowercase letters, numbers, underscores). "
                    + $"Got: '{definition.Name}'"
            );
        }

        _logger.LogInformation(
            "Creating ephemeral tool: {Name} (scope={Scope})",
            definition.Name,
            definition.Scope
        );

        try
        {
            var tool = _session.CreateTool(definition);

            // If scope is User or Global, promote immediately
            if (definition.Scope != ToolScope.Session)
            {
                await _session
                    .PromoteToolAsync(definition.Name, definition.Scope, cancellationToken)
                    .ConfigureAwait(false);
            }

            var scopeText = definition.Scope switch
            {
                ToolScope.Session => "session (memory only)",
                ToolScope.User => "user (~/.dawning/tools/)",
                ToolScope.Global => "global ({project}/.dawning/tools/)",
                _ => definition.Scope.ToString(),
            };

            var paramList =
                definition.Parameters.Count > 0
                    ? string.Join(", ", definition.Parameters.Select(p => p.Name))
                    : "none";

            return ToolResult.Ok(
                $"Created tool '{definition.Name}' (scope: {scopeText}, parameters: {paramList})\n"
                    + $"The tool is now available for use."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create tool: {Name}", definition.Name);
            return ToolResult.Fail($"Failed to create tool: {ex.Message}");
        }
    }

    private static EphemeralToolDefinition ParseDefinition(string input)
    {
        using var doc = JsonDocument.Parse(input);
        var root = doc.RootElement;

        if (
            !root.TryGetProperty("name", out var nameProp)
            || string.IsNullOrWhiteSpace(nameProp.GetString())
        )
        {
            throw new ArgumentException("name is required");
        }

        if (
            !root.TryGetProperty("description", out var descriptionProp)
            || string.IsNullOrWhiteSpace(descriptionProp.GetString())
        )
        {
            throw new ArgumentException("description is required");
        }

        if (
            !root.TryGetProperty("script", out var scriptProp)
            || string.IsNullOrWhiteSpace(scriptProp.GetString())
        )
        {
            throw new ArgumentException("script is required");
        }

        var definition = new EphemeralToolDefinition
        {
            Name = nameProp.GetString()!,
            Description = descriptionProp.GetString()!,
            Script = scriptProp.GetString()!,
        };

        if (root.TryGetProperty("scope", out var scopeProp))
        {
            var scopeStr = scopeProp.GetString() ?? "session";
            definition.Scope = scopeStr.ToLowerInvariant() switch
            {
                "user" => ToolScope.User,
                "global" => ToolScope.Global,
                _ => ToolScope.Session,
            };
        }

        if (root.TryGetProperty("parameters", out var paramsProp))
        {
            if (paramsProp.ValueKind != JsonValueKind.Array)
            {
                throw new ArgumentException("parameters must be an array");
            }

            foreach (var paramElement in paramsProp.EnumerateArray())
            {
                if (
                    !paramElement.TryGetProperty("name", out var paramNameProp)
                    || string.IsNullOrWhiteSpace(paramNameProp.GetString())
                )
                {
                    throw new ArgumentException("parameter name is required");
                }

                if (
                    !paramElement.TryGetProperty("description", out var paramDescriptionProp)
                    || string.IsNullOrWhiteSpace(paramDescriptionProp.GetString())
                )
                {
                    throw new ArgumentException("parameter description is required");
                }

                var param = new ScriptParameter
                {
                    Name = paramNameProp.GetString()!,
                    Description = paramDescriptionProp.GetString()!,
                    Type = paramElement.TryGetProperty("type", out var typeProp)
                        ? typeProp.GetString() ?? "string"
                        : "string",
                    Required =
                        !paramElement.TryGetProperty("required", out var reqProp)
                        || reqProp.ValueKind switch
                        {
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => throw new ArgumentException(
                                "parameter required must be a boolean"
                            ),
                        },
                };

                definition.Parameters.Add(param);
            }
        }

        return definition;
    }

    private static bool IsValidToolName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 64)
        {
            return false;
        }

        foreach (var c in name)
        {
            if (c != '_' && !char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return !name.StartsWith('_') && !name.EndsWith('_');
    }
}
