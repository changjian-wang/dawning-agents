using System.Reflection;
using System.Text.Json;
using Dawning.Agents.Abstractions.Tools;

namespace Dawning.Agents.Core.Tools;

/// <summary>
/// 基于方法反射的工具实现
/// </summary>
/// <remarks>
/// 将标记了 [FunctionTool] 的方法包装为 ITool 实例
/// </remarks>
public sealed class MethodTool : ITool
{
    private readonly MethodInfo _method;
    private readonly object? _instance;
    private readonly ParameterInfo[] _parameters;

    /// <summary>
    /// 工具名称（唯一标识符）
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 工具描述（供 LLM 理解工具用途）
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 参数的 JSON Schema（供 LLM 理解参数格式）
    /// </summary>
    public string ParametersSchema { get; }

    /// <summary>
    /// 是否需要用户确认才能执行
    /// </summary>
    public bool RequiresConfirmation { get; }

    /// <summary>
    /// 工具的风险等级
    /// </summary>
    public ToolRiskLevel RiskLevel { get; }

    /// <summary>
    /// 工具分类
    /// </summary>
    public string? Category { get; }

    /// <summary>
    /// 创建方法工具
    /// </summary>
    /// <param name="method">方法信息</param>
    /// <param name="instance">方法所属实例（静态方法为 null）</param>
    /// <param name="attribute">FunctionTool 特性</param>
    public MethodTool(MethodInfo method, object? instance, FunctionToolAttribute attribute)
    {
        _method = method ?? throw new ArgumentNullException(nameof(method));
        _instance = instance;

        Name = attribute.Name ?? method.Name;
        Description = attribute.Description;
        RequiresConfirmation = attribute.RequiresConfirmation;
        RiskLevel = attribute.RiskLevel;
        Category = attribute.Category;

        // 过滤掉 CancellationToken 参数
        _parameters = method
            .GetParameters()
            .Where(p => p.ParameterType != typeof(CancellationToken))
            .ToArray();

        ParametersSchema = BuildParametersSchema();
    }

    /// <summary>
    /// 执行工具
    /// </summary>
    /// <param name="input">输入参数（字符串或 JSON）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果</returns>
    public async Task<ToolResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var args = ParseArguments(input, cancellationToken);
            var result = _method.Invoke(_instance, args);

            // 处理异步方法
            if (result is Task task)
            {
                await task.ConfigureAwait(false);

                // 使用类型检查获取泛型 Task<T> 的结果
                var taskType = task.GetType();
                if (taskType.IsGenericType)
                {
                    result = ((dynamic)task).Result;
                }
                else
                {
                    result = null;
                }
            }

            // 处理返回值
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
            // 取消操作不视为错误，重新抛出
            throw;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is OperationCanceledException)
        {
            // 内部方法的取消操作
            throw ex.InnerException;
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
    /// 解析输入参数
    /// </summary>
    private object?[] ParseArguments(string input, CancellationToken cancellationToken)
    {
        var methodParams = _method.GetParameters();
        var args = new object?[methodParams.Length];

        // 如果只有一个非 CancellationToken 参数且是字符串类型，直接使用输入
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

        // 尝试解析 JSON 输入（使用 using 确保释放）
        using var jsonDoc = TryParseJson(input);

        for (int i = 0; i < methodParams.Length; i++)
        {
            var param = methodParams[i];

            if (param.ParameterType == typeof(CancellationToken))
            {
                args[i] = cancellationToken;
                continue;
            }

            // 尝试从 JSON 获取值
            if (jsonDoc?.RootElement.TryGetProperty(param.Name!, out var jsonValue) == true)
            {
                args[i] = ConvertJsonValue(jsonValue, param.ParameterType);
            }
            else if (param.HasDefaultValue)
            {
                args[i] = param.DefaultValue;
            }
            else if (param.ParameterType == typeof(string))
            {
                // 单参数字符串，直接使用输入
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
    /// 尝试解析 JSON，失败返回 null
    /// </summary>
    private static JsonDocument? TryParseJson(string input)
    {
        try
        {
            return JsonDocument.Parse(input);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 转换 JSON 值到目标类型
    /// </summary>
    private static object? ConvertJsonValue(JsonElement element, Type targetType)
    {
        return targetType switch
        {
            _ when targetType == typeof(string) => element.GetString(),
            _ when targetType == typeof(int) => element.GetInt32(),
            _ when targetType == typeof(long) => element.GetInt64(),
            _ when targetType == typeof(double) => element.GetDouble(),
            _ when targetType == typeof(float) => element.GetSingle(),
            _ when targetType == typeof(bool) => element.GetBoolean(),
            _ when targetType == typeof(decimal) => element.GetDecimal(),
            _ => element.GetString(),
        };
    }

    /// <summary>
    /// 获取类型的默认值
    /// </summary>
    private static object? GetDefaultValue(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    /// <summary>
    /// 构建参数的 JSON Schema
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

            // 获取参数描述
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
    /// 获取 .NET 类型对应的 JSON Schema 类型
    /// </summary>
    private static string GetJsonType(Type type)
    {
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
