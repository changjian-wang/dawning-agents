using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dawning.Agents.Abstractions.Workflow;

namespace Dawning.Agents.Core.Workflow;

/// <summary>
/// 工作流序列化器
/// </summary>
public class WorkflowSerializer : IWorkflowSerializer
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <inheritdoc />
    public string SerializeToJson(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return JsonSerializer.Serialize(definition, s_jsonOptions);
    }

    /// <inheritdoc />
    public WorkflowDefinition DeserializeFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        return JsonSerializer.Deserialize<WorkflowDefinition>(json, s_jsonOptions)
            ?? throw new InvalidOperationException("无法反序列化工作流定义");
    }

    /// <inheritdoc />
    public string SerializeToYaml(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        // 简化的 YAML 输出（不依赖外部库）
        var sb = new StringBuilder();
        sb.AppendLine($"id: {YamlEscape(definition.Id)}");
        sb.AppendLine($"name: {YamlEscape(definition.Name)}");

        if (!string.IsNullOrEmpty(definition.Description))
        {
            sb.AppendLine($"description: {YamlEscape(definition.Description)}");
        }

        sb.AppendLine($"version: {YamlEscape(definition.Version)}");
        sb.AppendLine($"startNodeId: {YamlEscape(definition.StartNodeId)}");
        sb.AppendLine();
        sb.AppendLine("nodes:");

        foreach (var node in definition.Nodes)
        {
            sb.AppendLine($"  - id: {YamlEscape(node.Id)}");
            sb.AppendLine($"    name: {YamlEscape(node.Name)}");
            sb.AppendLine($"    type: {node.Type}");

            if (node.Config != null && node.Config.Count > 0)
            {
                sb.AppendLine("    config:");
                foreach (var kvp in node.Config)
                {
                    var value = kvp.Value?.ToString() ?? "null";
                    sb.AppendLine($"      {kvp.Key}: {YamlEscape(value)}");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("edges:");

        foreach (var edge in definition.Edges)
        {
            sb.AppendLine($"  - from: {YamlEscape(edge.FromNodeId)}");
            sb.AppendLine($"    to: {YamlEscape(edge.ToNodeId)}");

            if (!string.IsNullOrEmpty(edge.Label))
            {
                sb.AppendLine($"    label: {YamlEscape(edge.Label)}");
            }
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    public WorkflowDefinition DeserializeFromYaml(string yaml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaml);

        // 简化实现：解析基本 YAML 格式
        var lines = yaml.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var id = "";
        var name = "";
        string? description = null;
        var version = "1.0.0";
        var startNodeId = "";
        var nodes = new List<WorkflowNodeDefinition>();
        var edges = new List<WorkflowEdgeDefinition>();

        var currentSection = "";
        WorkflowNodeDefinition? currentNode = null;
        WorkflowEdgeDefinition? currentEdge = null;
        Dictionary<string, object?>? currentConfig = null;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmed = line.TrimStart();
            var indent = line.Length - trimmed.Length;

            if (trimmed.StartsWith("id:", StringComparison.Ordinal) && indent == 0)
            {
                id = trimmed["id:".Length..].Trim();
            }
            else if (trimmed.StartsWith("name:", StringComparison.Ordinal) && indent == 0)
            {
                name = trimmed["name:".Length..].Trim();
            }
            else if (trimmed.StartsWith("description:", StringComparison.Ordinal) && indent == 0)
            {
                description = trimmed["description:".Length..].Trim();
            }
            else if (trimmed.StartsWith("version:", StringComparison.Ordinal) && indent == 0)
            {
                version = trimmed["version:".Length..].Trim();
            }
            else if (trimmed.StartsWith("startNodeId:", StringComparison.Ordinal) && indent == 0)
            {
                startNodeId = trimmed["startNodeId:".Length..].Trim();
            }
            else if (trimmed == "nodes:")
            {
                currentSection = "nodes";
            }
            else if (trimmed == "edges:")
            {
                currentSection = "edges";
                // 完成最后一个节点
                if (currentNode != null)
                {
                    if (currentConfig != null)
                    {
                        currentNode = currentNode with { Config = currentConfig };
                    }
                    nodes.Add(currentNode);
                    currentNode = null;
                    currentConfig = null;
                }
            }
            else if (
                currentSection == "nodes"
                && trimmed.StartsWith("- id:", StringComparison.Ordinal)
            )
            {
                // 新节点
                if (currentNode != null)
                {
                    if (currentConfig != null)
                    {
                        currentNode = currentNode with { Config = currentConfig };
                    }
                    nodes.Add(currentNode);
                }

                currentNode = new WorkflowNodeDefinition
                {
                    Id = trimmed["- id:".Length..].Trim(),
                    Name = "",
                };
                currentConfig = null;
            }
            else if (
                currentSection == "nodes"
                && trimmed.StartsWith("name:", StringComparison.Ordinal)
                && currentNode != null
                && currentConfig == null
            )
            {
                currentNode = currentNode with { Name = trimmed["name:".Length..].Trim() };
            }
            else if (
                currentSection == "nodes"
                && trimmed.StartsWith("type:", StringComparison.Ordinal)
                && currentNode != null
                && currentConfig == null
            )
            {
                if (
                    Enum.TryParse<WorkflowNodeType>(
                        trimmed["type:".Length..].Trim(),
                        true,
                        out var nodeType
                    )
                )
                {
                    currentNode = currentNode with { Type = nodeType };
                }
            }
            else if (currentSection == "nodes" && trimmed == "config:")
            {
                currentConfig = new Dictionary<string, object?>();
            }
            else if (currentConfig != null && trimmed.Contains(':'))
            {
                var parts = trimmed.Split(':', 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();
                    currentConfig[key] = value == "null" ? null : value;
                }
            }
            else if (
                currentSection == "edges"
                && trimmed.StartsWith("- from:", StringComparison.Ordinal)
            )
            {
                // 完成上一条边
                if (currentEdge != null)
                {
                    edges.Add(currentEdge);
                }

                currentEdge = new WorkflowEdgeDefinition
                {
                    FromNodeId = trimmed["- from:".Length..].Trim(),
                    ToNodeId = "",
                };
            }
            else if (
                currentSection == "edges"
                && trimmed.StartsWith("to:", StringComparison.Ordinal)
                && currentEdge != null
            )
            {
                currentEdge = currentEdge with { ToNodeId = trimmed["to:".Length..].Trim() };
            }
            else if (
                currentSection == "edges"
                && trimmed.StartsWith("label:", StringComparison.Ordinal)
                && currentEdge != null
            )
            {
                currentEdge = currentEdge with { Label = trimmed["label:".Length..].Trim() };
            }
        }

        // 完成最后的内容
        if (currentNode != null)
        {
            if (currentConfig != null)
            {
                currentNode = currentNode with { Config = currentConfig };
            }
            nodes.Add(currentNode);
        }
        if (currentEdge != null)
        {
            edges.Add(currentEdge);
        }

        return new WorkflowDefinition
        {
            Id = id,
            Name = name,
            Description = description,
            Version = version,
            StartNodeId = startNodeId,
            Nodes = nodes,
            Edges = edges,
        };
    }

    private static readonly SearchValues<char> s_yamlSpecialChars = SearchValues.Create(
        ":#{'\"[]\n\r"
    );

    private static string YamlEscape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        if (
            value.AsSpan().ContainsAny(s_yamlSpecialChars)
            || value.StartsWith(' ')
            || value.EndsWith(' ')
        )
        {
            return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
        }

        return value;
    }
}

/// <summary>
/// 工作流可视化器
/// </summary>
public class WorkflowVisualizer : IWorkflowVisualizer
{
    /// <inheritdoc />
    public string GenerateMermaid(WorkflowDefinition definition)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");

        // 节点
        foreach (var node in definition.Nodes)
        {
            var shape = GetMermaidShape(node.Type);
            var label = EscapeMermaidLabel(node.Name);
            sb.AppendLine($"    {node.Id}{shape.start}\"{label}\"{shape.end}");
        }

        sb.AppendLine();

        // 边
        foreach (var edge in definition.Edges)
        {
            if (string.IsNullOrEmpty(edge.Label))
            {
                sb.AppendLine($"    {edge.FromNodeId} --> {edge.ToNodeId}");
            }
            else
            {
                sb.AppendLine(
                    $"    {edge.FromNodeId} -->|\"{EscapeMermaidLabel(edge.Label)}\"| {edge.ToNodeId}"
                );
            }
        }

        // 样式
        sb.AppendLine();
        sb.AppendLine("    %% Styles");

        foreach (var node in definition.Nodes)
        {
            var style = GetMermaidStyle(node.Type);
            if (!string.IsNullOrEmpty(style))
            {
                sb.AppendLine($"    style {node.Id} {style}");
            }
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateDot(WorkflowDefinition definition)
    {
        var sb = new StringBuilder();
        sb.AppendLine("digraph workflow {");
        sb.AppendLine("    rankdir=TB;");
        sb.AppendLine("    node [fontname=\"Arial\"];");
        sb.AppendLine();

        // 节点
        foreach (var node in definition.Nodes)
        {
            var (shape, color) = GetDotStyle(node.Type);
            sb.AppendLine(
                $"    {node.Id} [label=\"{EscapeDotLabel(node.Name)}\" shape={shape} style=filled fillcolor=\"{color}\"];"
            );
        }

        sb.AppendLine();

        // 边
        foreach (var edge in definition.Edges)
        {
            if (string.IsNullOrEmpty(edge.Label))
            {
                sb.AppendLine($"    {edge.FromNodeId} -> {edge.ToNodeId};");
            }
            else
            {
                sb.AppendLine(
                    $"    {edge.FromNodeId} -> {edge.ToNodeId} [label=\"{EscapeDotLabel(edge.Label)}\"];"
                );
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static (string start, string end) GetMermaidShape(WorkflowNodeType type)
    {
        return type switch
        {
            WorkflowNodeType.Start => ("((", "))"), // 圆形
            WorkflowNodeType.End => ("((", "))"), // 圆形
            WorkflowNodeType.Condition => ("{", "}"), // 菱形
            WorkflowNodeType.Parallel => ("[[", "]]"), // 方形双边框
            WorkflowNodeType.Loop => ("([", "])"), // 药丸形
            WorkflowNodeType.HumanApproval => ("[/", "/]"), // 平行四边形
            _ => ("[", "]"), // 默认方形
        };
    }

    private static string GetMermaidStyle(WorkflowNodeType type)
    {
        return type switch
        {
            WorkflowNodeType.Start => "fill:#90EE90,stroke:#006400",
            WorkflowNodeType.End => "fill:#FFB6C1,stroke:#DC143C",
            WorkflowNodeType.Agent => "fill:#87CEEB,stroke:#4682B4",
            WorkflowNodeType.Tool => "fill:#DDA0DD,stroke:#8B008B",
            WorkflowNodeType.Condition => "fill:#FFD700,stroke:#DAA520",
            WorkflowNodeType.Parallel => "fill:#98FB98,stroke:#228B22",
            WorkflowNodeType.Loop => "fill:#FFA07A,stroke:#FF4500",
            WorkflowNodeType.HumanApproval => "fill:#F0E68C,stroke:#BDB76B",
            _ => "",
        };
    }

    private static (string shape, string color) GetDotStyle(WorkflowNodeType type)
    {
        return type switch
        {
            WorkflowNodeType.Start => ("circle", "#90EE90"),
            WorkflowNodeType.End => ("doublecircle", "#FFB6C1"),
            WorkflowNodeType.Agent => ("box", "#87CEEB"),
            WorkflowNodeType.Tool => ("box", "#DDA0DD"),
            WorkflowNodeType.Condition => ("diamond", "#FFD700"),
            WorkflowNodeType.Parallel => ("parallelogram", "#98FB98"),
            WorkflowNodeType.Loop => ("ellipse", "#FFA07A"),
            WorkflowNodeType.HumanApproval => ("trapezium", "#F0E68C"),
            _ => ("box", "#FFFFFF"),
        };
    }

    private static string EscapeMermaidLabel(string label)
    {
        return label.Replace("\"", "#quot;").Replace("\n", "<br/>");
    }

    private static string EscapeDotLabel(string label)
    {
        return label.Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}
