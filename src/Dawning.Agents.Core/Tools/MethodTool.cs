using System.Reflection;
using System.Text.Json;
using Dawning.Agents.Abstractions.Tools;

namespace Dawning.Agents.Core.Tools;

/// <summary>
/// Reflection-based method tool implementation.
/// </summary>
/// <remarks>
/// Wraps methods marked with <see cref="FunctionToolAttribute"/> as <see cref="ITool"/> instances.
/// </remarks>
public sealed class MethodTool : ITool
{
    private readonly MethodInfo _method;
    private readonly object? _instance;
    private readonly ParameterInfo[] _parameters;

    /// <summary>
    /// Gets the tool name (unique identifier).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the tool description (for LLM to understand the tool's purpose).
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the parameter JSON Schema (for LLM to understand parameter format).
    /// </summary>
    public string ParametersSchema { get; }

    /// <summary>
    /// Gets a value indicating whether user confirmation is required before execution.
    /// </summary>
    public bool RequiresConfirmation { get; }

    /// <summary>
    /// Gets the risk level of the tool.
    /// </summary>
    public ToolRiskLevel RiskLevel { get; }

    /// <summary>
    /// Gets the tool category.
    /// </summary>
    public string? Category { get; }

    /// <summary>
    /// Creates a method tool.
    /// </summary>
    /// <param name="method">The method info.</param>
    /// <param name="instance">The instance the method belongs to (<see langword="null"/> for static methods).</param>
    /// <param name="attribute">The <see cref="FunctionToolAttribute"/>.</param>
    public MethodTool(MethodInfo method, object? instance, FunctionToolAttribute attribute)
    {
        _method = method ?? throw new ArgumentNullException(nameof(method));
        _instance = instance;

        Name = attribute.Name ?? method.Name;
        Description = attribute.Description;
        RequiresConfirmation = attribute.RequiresConfirmation;
        RiskLevel = attribute.RiskLevel;
        Category = attribute.Category;

        // Filter out CancellationToken parameters
        _parameters = method
            .GetParameters()
            .Where(p => p.ParameterType != typeof(CancellationToken))
            .ToArray();

        ParametersSchema = BuildParametersSchema();
    }

    /// <summary>
    /// Executes the tool.
    /// </summary>
    /// <param name="input">Input parameters (string or JSON).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The execution result.</returns>
    public async Task<ToolResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var args = ParseArguments(input, cancellationToken);
            var result = _method.Invoke(_instance, args);

            // Handle async methods
            if (result is Task task)
            {
                await task.ConfigureAwait(false);

                // Use type check to get the result of generic Task<T>
                var taskType = task.GetType();
                if (taskType.IsGenericType)
                {
                    var resultProperty = taskType.GetProperty("Result");
                    result = resultProperty?.GetValue(task);
                }
                else
                {
                    result = null;
                }
            }
            else if (result is not null && result.GetType() is { IsValueType: true } valueType)
            {
                // Handle ValueTask / ValueTask<T>
                if (
                    valueType.IsGenericType
                    && valueType.GetGenericTypeDefinition() == typeof(ValueTask<>)
                )
                {
                    var asTask = valueType.GetMethod("AsTask")!.Invoke(result, null) as Task;
                    await asTask!.ConfigureAwait(false);
                    var resultProperty = asTask.GetType().GetProperty("Result");
                    result = resultProperty?.GetValue(asTask);
                }
                else if (valueType == typeof(ValueTask))
                {
                    await ((ValueTask)result).ConfigureAwait(false);
                    result = null;
                }
            }

            // Handle return value
            return result switch
            {
                ToolResult toolResult => toolResult,
                string str => ToolResult.Ok(str),
                null => ToolResult.Ok(string.Empty),
                _ => ToolResult.Ok(result.ToString() ?? string.Empty),
            };
        }
        catch (OperationCanceledException)
        {
            // Cancellation is not treated as an error; rethrow
            throw;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is OperationCanceledException)
        {
            // Cancellation from inner method (preserves original stack trace)
            System
                .Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException)
                .Throw();
            throw; // unreachable, satisfies compiler
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            return ToolResult.Fail(ex.InnerException.Message);
        }
        catch (Exception ex)
        {
            return ToolResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Parses input arguments.
    /// </summary>
    private object?[] ParseArguments(string input, CancellationToken cancellationToken)
    {
        var methodParams = _method.GetParameters();
        var args = new object?[methodParams.Length];

        // If there is only one non-CancellationToken parameter of type string, use input directly
        if (_parameters.Length == 1 && _parameters[0].ParameterType == typeof(string))
        {
            for (int i = 0; i < methodParams.Length; i++)
            {
                if (methodParams[i].ParameterType == typeof(CancellationToken))
                {
                    args[i] = cancellationToken;
                }
                else
                {
                    args[i] = input;
                }
            }
            return args;
        }

        // Try to parse JSON input (using ensures disposal)
        using var jsonDoc = TryParseJson(input);

        for (int i = 0; i < methodParams.Length; i++)
        {
            var param = methodParams[i];

            if (param.ParameterType == typeof(CancellationToken))
            {
                args[i] = cancellationToken;
                continue;
            }

            // Try to get value from JSON
            if (jsonDoc?.RootElement.TryGetProperty(param.Name!, out var jsonValue) == true)
            {
                args[i] = ConvertJsonValue(jsonValue, param.ParameterType);
            }
            else if (param.HasDefaultValue)
            {
                args[i] = param.DefaultValue;
            }
            else if (param.ParameterType == typeof(string) && methodParams.Length == 1)
            {
                // Single-parameter string method; use input directly
                args[i] = input;
            }
            else
            {
                args[i] = GetDefaultValue(param.ParameterType);
            }
        }

        return args;
    }

    /// <summary>
    /// Tries to parse JSON; returns <see langword="null"/> on failure.
    /// </summary>
    private static JsonDocument? TryParseJson(string input)
    {
        try
        {
            return JsonDocument.Parse(input);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Converts a JSON value to the target type.
    /// </summary>
    private static object? ConvertJsonValue(JsonElement element, Type targetType)
    {
        // Handle Nullable<T>: extract the underlying type
        var underlyingType = Nullable.GetUnderlyingType(targetType);
        if (underlyingType is not null)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return ConvertJsonValue(element, underlyingType);
        }

        return targetType switch
        {
            _ when targetType == typeof(string) => element.GetString(),
            _ when targetType == typeof(int) => element.GetInt32(),
            _ when targetType == typeof(long) => element.GetInt64(),
            _ when targetType == typeof(double) => element.GetDouble(),
            _ when targetType == typeof(float) => element.GetSingle(),
            _ when targetType == typeof(bool) => element.GetBoolean(),
            _ when targetType == typeof(decimal) => element.GetDecimal(),
            _ => element.Deserialize(targetType),
        };
    }

    /// <summary>
    /// Gets the default value for a type.
    /// </summary>
    private static object? GetDefaultValue(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    /// <summary>
    /// Builds the JSON Schema for parameters.
    /// </summary>
    private string BuildParametersSchema()
    {
        if (_parameters.Length == 0)
        {
            return """{"type":"object","properties":{}}""";
        }

        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var param in _parameters)
        {
            var paramSchema = new Dictionary<string, object>
            {
                ["type"] = GetJsonType(param.ParameterType),
            };

            // Get parameter description
            var descAttr = param.GetCustomAttribute<ToolParameterAttribute>();
            if (descAttr != null)
            {
                paramSchema["description"] = descAttr.Description;
            }

            properties[param.Name!] = paramSchema;

            if (!param.HasDefaultValue)
            {
                required.Add(param.Name!);
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

    /// <summary>
    /// Gets the corresponding JSON Schema type for a .NET type.
    /// </summary>
    private static string GetJsonType(Type type)
    {
        // Unwrap Nullable<T> first to ensure int?/bool? etc. generate the correct schema type
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
        {
            return GetJsonType(underlying);
        }

        return type switch
        {
            _ when type == typeof(string) => "string",
            _ when type == typeof(int) || type == typeof(long) => "integer",
            _ when type == typeof(double) || type == typeof(float) || type == typeof(decimal) =>
                "number",
            _ when type == typeof(bool) => "boolean",
            _ => "string",
        };
    }
}
