namespace Dawning.Agents.Abstractions.Prompts;

/// <summary>
/// 提示词模板接口
/// </summary>
public interface IPromptTemplate
{
    /// <summary>
    /// 模板名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 模板内容（包含占位符）
    /// </summary>
    string Template { get; }

    /// <summary>
    /// 格式化模板，替换占位符
    /// </summary>
    /// <param name="variables">变量字典</param>
    /// <returns>格式化后的字符串</returns>
    string Format(IDictionary<string, object> variables);
}
