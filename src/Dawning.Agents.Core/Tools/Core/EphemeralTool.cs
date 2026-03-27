using System.Globalization;
using System.Text;
using System.Text.Json;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Tools.Core;

/// <summary>
/// Dynamic script tool — wraps an <see cref="EphemeralToolDefinition"/> as an executable <see cref="ITool"/>.
/// </summary>
/// <remarks>
/// <para>Executes scripts through <see cref="IToolSandbox"/> with parameters passed via environment variables.</para>
/// </remarks>
public sealed class EphemeralTool : ITool
{
    private readonly EphemeralToolDefinition _definition;
    private readonly IToolSandbox _sandbox;
    private readonly ToolSandboxOptions _defaultOptions;
    private readonly ILogger _logger;

    /// <summary>
    /// Creates an <see cref="EphemeralTool"/>.
    /// </summary>
    public EphemeralTool(
        EphemeralToolDefinition definition,
        IToolSandbox sandbox,
        ToolSandboxOptions? defaultOptions = null,
        ILogger? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(sandbox);
        _definition = definition;
        _sandbox = sandbox;
        _defaultOptions = defaultOptions ?? new ToolSandboxOptions();
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Gets the tool definition.
    /// </summary>
    public EphemeralToolDefinition Definition => _definition;

    /// <inheritdoc />
    public string Name => _definition.Name;

    /// <inheritdoc />
    public string Description => _definition.Description;

    /// <inheritdoc />
    public string ParametersSchema => BuildParametersSchema();

    /// <inheritdoc />
    public bool RequiresConfirmation => true;

    /// <inheritdoc />
    public ToolRiskLevel RiskLevel => ToolRiskLevel.High;

    /// <inheritdoc />
    public string? Category => "Ephemeral";

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Executing ephemeral tool: {Name}", Name);

        // Parse parameters from input
        var parameters = ParseParameters(input);

        // Validate required parameters
        var validationError = ValidateParameters(parameters);
        if (validationError != null)
        {
            return ToolResult.Fail(validationError);
        }

        // Build the script with parameters substituted
        var script = SubstituteParameters(_definition.Script, parameters);

        // Execute via sandbox
        var options = new ToolSandboxOptions
        {
            WorkingDirectory = _defaultOptions.WorkingDirectory,
            Timeout = _defaultOptions.Timeout,
            Mode = _defaultOptions.Mode,
            Runtime = _definition.Runtime,
            Environment = new Dictionary<string, string>(_defaultOptions.Environment),
        };

        // Also pass parameters as environment variables (TOOL_PARAM_xxx)
        foreach (var param in parameters)
        {
            options.Environment[$"TOOL_PARAM_{param.Key.ToUpperInvariant()}"] = param.Value;
        }

        var result = await _sandbox
            .ExecuteAsync(script, options, cancellationToken)
            .ConfigureAwait(false);

        if (result.TimedOut)
        {
            return ToolResult.Fail(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Script timed out after {result.Duration.TotalSeconds:F1}s"
                )
            );
        }

        if (!result.IsSuccess)
        {
            var error = new StringBuilder();
            error.Append($"Exit code {result.ExitCode}");
            if (!string.IsNullOrWhiteSpace(result.Stderr))
            {
                error.AppendLine();
                error.Append(result.Stderr);
            }

            return ToolResult.Fail(error.ToString());
        }

        return ToolResult.Ok(
            string.IsNullOrWhiteSpace(result.Stdout) ? "(no output)" : result.Stdout
        );
    }

    private Dictionary<string, string> ParseParameters(string input)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(input);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                result[prop.Name] =
                    prop.Value.ValueKind == JsonValueKind.String
                        ? prop.Value.GetString() ?? ""
                        : prop.Value.GetRawText();
            }
        }
        catch (JsonException)
        {
            // If single parameter, use the input as-is for the first parameter
            if (_definition.Parameters.Count == 1)
            {
                result[_definition.Parameters[0].Name] = input.Trim();
            }
        }

        return result;
    }

    private string? ValidateParameters(Dictionary<string, string> parameters)
    {
        foreach (var param in _definition.Parameters.Where(p => p.Required))
        {
            if (
                !parameters.TryGetValue(param.Name, out var value)
                || string.IsNullOrWhiteSpace(value)
            )
            {
                if (param.DefaultValue != null)
                {
                    parameters[param.Name] = param.DefaultValue;
                }
                else
                {
                    return $"Required parameter '{param.Name}' is missing";
                }
            }
        }

        return null;
    }

    private static string SubstituteParameters(string script, Dictionary<string, string> parameters)
    {
        var result = script;
        // Sort by key length descending to prevent prefix collision
        // e.g. $path_prefix must be replaced before $path
        foreach (var param in parameters.OrderByDescending(p => p.Key.Length))
        {
            // Replace $param_name with the value (shell-escaped and quoted)
            result = result.Replace($"${param.Key}", $"'{EscapeForShell(param.Value)}'");
        }

        return result;
    }

    private static string EscapeForShell(string value)
    {
        // Escape single quotes for POSIX shell safety
        return value.Replace("'", "'\\''");
    }

    private string BuildParametersSchema()
    {
        if (_definition.Parameters.Count == 0)
        {
            return """{"type": "object", "properties": {}}""";
        }

        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var param in _definition.Parameters)
        {
            var prop = new Dictionary<string, string>
            {
                ["type"] = param.Type,
                ["description"] = param.Description,
            };

            properties[param.Name] = prop;

            if (param.Required)
            {
                required.Add(param.Name);
            }
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
        };

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        return JsonSerializer.Serialize(schema);
    }
}
