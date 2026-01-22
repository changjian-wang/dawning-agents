namespace Dawning.Agents.Abstractions.Safety;

/// <summary>
/// 护栏基础接口
/// </summary>
public interface IGuardrail
{
    /// <summary>
    /// 护栏名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 护栏描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 是否启用
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// 检查内容
    /// </summary>
    /// <param name="content">要检查的内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检查结果</returns>
    Task<GuardrailResult> CheckAsync(
        string content,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// 输入护栏接口 - 在 LLM 调用前检查用户输入
/// </summary>
public interface IInputGuardrail : IGuardrail { }

/// <summary>
/// 输出护栏接口 - 在 LLM 响应后检查输出内容
/// </summary>
public interface IOutputGuardrail : IGuardrail { }

/// <summary>
/// 护栏管道接口 - 管理多个护栏的执行
/// </summary>
public interface IGuardrailPipeline
{
    /// <summary>
    /// 所有输入护栏
    /// </summary>
    IReadOnlyList<IInputGuardrail> InputGuardrails { get; }

    /// <summary>
    /// 所有输出护栏
    /// </summary>
    IReadOnlyList<IOutputGuardrail> OutputGuardrails { get; }

    /// <summary>
    /// 检查输入内容
    /// </summary>
    /// <param name="input">用户输入</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检查结果</returns>
    Task<GuardrailResult> CheckInputAsync(
        string input,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 检查输出内容
    /// </summary>
    /// <param name="output">LLM 输出</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检查结果</returns>
    Task<GuardrailResult> CheckOutputAsync(
        string output,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 添加输入护栏
    /// </summary>
    IGuardrailPipeline AddInputGuardrail(IInputGuardrail guardrail);

    /// <summary>
    /// 添加输出护栏
    /// </summary>
    IGuardrailPipeline AddOutputGuardrail(IOutputGuardrail guardrail);
}
