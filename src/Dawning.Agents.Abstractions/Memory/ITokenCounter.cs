using Dawning.Agents.Abstractions.LLM;

namespace Dawning.Agents.Abstractions.Memory;

/// <summary>
/// Token 计数器接口
/// </summary>
/// <remarks>
/// <para>用于计算文本的 token 数量，帮助管理 LLM 上下文窗口</para>
/// <para>实现类型包括：SimpleTokenCounter（估算）、TiktokenCounter（精确）</para>
/// </remarks>
public interface ITokenCounter
{
    /// <summary>
    /// 计算给定文本中的 token 数量
    /// </summary>
    /// <param name="text">要计算的文本</param>
    /// <returns>token 数量</returns>
    int CountTokens(string text);

    /// <summary>
    /// 计算消息列表的 token 数量（包括角色开销）
    /// </summary>
    /// <param name="messages">消息列表</param>
    /// <returns>token 总数</returns>
    int CountTokens(IEnumerable<ChatMessage> messages);

    /// <summary>
    /// 获取此计数器对应的模型名称
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// 获取最大上下文窗口大小
    /// </summary>
    int MaxContextTokens { get; }
}
