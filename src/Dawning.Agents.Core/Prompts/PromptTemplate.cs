using System.Text.RegularExpressions;
using Dawning.Agents.Abstractions.Prompts;

namespace Dawning.Agents.Core.Prompts;

/// <summary>
/// 简单的提示词模板实现，支持 {variable} 形式的占位符
/// </summary>
public partial class PromptTemplate : IPromptTemplate
{
    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex PlaceholderRegex();

    /// <summary>
    /// 模板名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 模板内容（包含 {variable} 形式的占位符）
    /// </summary>
    public string Template { get; }

    public PromptTemplate(string name, string template)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Template = template ?? throw new ArgumentNullException(nameof(template));
    }

    /// <summary>
    /// 格式化模板，替换占位符
    /// </summary>
    /// <param name="variables">变量字典，键为占位符名称，值为替换内容</param>
    /// <returns>格式化后的字符串</returns>
    public string Format(IDictionary<string, object> variables)
    {
        if (variables == null || variables.Count == 0)
        {
            return Template;
        }

        return PlaceholderRegex()
            .Replace(
                Template,
                match =>
                {
                    var key = match.Groups[1].Value;
                    return variables.TryGetValue(key, out var value)
                        ? value?.ToString() ?? string.Empty
                        : match.Value; // 保留未找到的占位符
                }
            );
    }

    /// <summary>
    /// 创建提示词模板
    /// </summary>
    public static PromptTemplate Create(string name, string template) => new(name, template);
}
