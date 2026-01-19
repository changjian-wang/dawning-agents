using System.Text.Json;
using System.Text.Json.Nodes;
using Dawning.Agents.Abstractions.Tools;

namespace Dawning.Agents.Core.Tools.BuiltIn;

/// <summary>
/// JSON 处理工具
/// </summary>
public class JsonTool
{
    private static readonly JsonSerializerOptions PrettyOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// 格式化 JSON
    /// </summary>
    [FunctionTool("格式化 JSON 字符串，使其更易读", Category = "Json")]
    public string FormatJson([ToolParameter("要格式化的 JSON 字符串")] string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "错误: JSON 字符串不能为空";
        }

        try
        {
            var node = JsonNode.Parse(json);
            return node?.ToJsonString(PrettyOptions) ?? "null";
        }
        catch (JsonException ex)
        {
            return $"JSON 解析错误: {ex.Message}";
        }
    }

    /// <summary>
    /// 压缩 JSON
    /// </summary>
    [FunctionTool("压缩 JSON 字符串，移除多余空白", Category = "Json")]
    public string CompactJson([ToolParameter("要压缩的 JSON 字符串")] string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "错误: JSON 字符串不能为空";
        }

        try
        {
            var node = JsonNode.Parse(json);
            return node?.ToJsonString(CompactOptions) ?? "null";
        }
        catch (JsonException ex)
        {
            return $"JSON 解析错误: {ex.Message}";
        }
    }

    /// <summary>
    /// 验证 JSON
    /// </summary>
    [FunctionTool("验证 JSON 字符串是否有效", Category = "Json")]
    public string ValidateJson([ToolParameter("要验证的 JSON 字符串")] string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "无效: JSON 字符串为空";
        }

        try
        {
            var node = JsonNode.Parse(json);
            var type = node switch
            {
                JsonObject => "对象 (Object)",
                JsonArray arr => $"数组 (Array)，包含 {arr.Count} 个元素",
                JsonValue => "值 (Value)",
                null => "null",
                _ => "未知类型",
            };

            return $"有效的 JSON - 类型: {type}";
        }
        catch (JsonException ex)
        {
            return $"无效的 JSON: {ex.Message}";
        }
    }

    /// <summary>
    /// 提取 JSON 路径值
    /// </summary>
    [FunctionTool("从 JSON 中提取指定路径的值", Category = "Json")]
    public string ExtractJsonPath(
        [ToolParameter("JSON 字符串")] string json,
        [ToolParameter("JSON 路径，用点号分隔，如 'data.user.name' 或 'items[0].id'")] string path
    )
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "错误: JSON 字符串不能为空";
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return "错误: 路径不能为空";
        }

        try
        {
            var node = JsonNode.Parse(json);
            if (node == null)
            {
                return "JSON 为 null";
            }

            // 简单的路径解析
            var parts = path.Split('.');
            JsonNode? current = node;

            foreach (var part in parts)
            {
                if (current == null)
                {
                    return $"路径 '{path}' 不存在";
                }

                // 处理数组索引，如 items[0]
                if (part.Contains('[') && part.Contains(']'))
                {
                    var bracketIndex = part.IndexOf('[');
                    var key = part[..bracketIndex];
                    var indexStr = part[(bracketIndex + 1)..^1];

                    if (!string.IsNullOrEmpty(key))
                    {
                        current = current[key];
                    }

                    if (current != null && int.TryParse(indexStr, out var index))
                    {
                        current = current[index];
                    }
                }
                else
                {
                    current = current[part];
                }
            }

            return current?.ToJsonString(PrettyOptions) ?? "null";
        }
        catch (Exception ex)
        {
            return $"提取错误: {ex.Message}";
        }
    }

    /// <summary>
    /// 获取 JSON 结构概览
    /// </summary>
    [FunctionTool("获取 JSON 的结构概览，显示所有键和类型", Category = "Json")]
    public string GetJsonStructure([ToolParameter("JSON 字符串")] string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "错误: JSON 字符串不能为空";
        }

        try
        {
            var node = JsonNode.Parse(json);
            var lines = new List<string> { "JSON 结构:" };
            AnalyzeStructure(node, "", lines);
            return string.Join("\n", lines);
        }
        catch (JsonException ex)
        {
            return $"JSON 解析错误: {ex.Message}";
        }
    }

    private static void AnalyzeStructure(JsonNode? node, string prefix, List<string> lines)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var prop in obj)
                {
                    var type = GetNodeType(prop.Value);
                    lines.Add($"{prefix}{prop.Key}: {type}");
                    if (prop.Value is JsonObject or JsonArray)
                    {
                        AnalyzeStructure(prop.Value, prefix + "  ", lines);
                    }
                }
                break;

            case JsonArray arr:
                if (arr.Count > 0)
                {
                    lines.Add($"{prefix}[0]: {GetNodeType(arr[0])}");
                    if (arr[0] is JsonObject or JsonArray)
                    {
                        AnalyzeStructure(arr[0], prefix + "  ", lines);
                    }
                    if (arr.Count > 1)
                    {
                        lines.Add($"{prefix}... ({arr.Count} 个元素)");
                    }
                }
                break;
        }
    }

    private static string GetNodeType(JsonNode? node) =>
        node switch
        {
            JsonObject obj => $"Object ({obj.Count} 个属性)",
            JsonArray arr => $"Array ({arr.Count} 个元素)",
            JsonValue value => value.GetValueKind() switch
            {
                JsonValueKind.String => "String",
                JsonValueKind.Number => "Number",
                JsonValueKind.True or JsonValueKind.False => "Boolean",
                JsonValueKind.Null => "Null",
                _ => "Value",
            },
            null => "Null",
            _ => "Unknown",
        };
}
