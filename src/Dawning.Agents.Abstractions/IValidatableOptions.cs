namespace Dawning.Agents.Abstractions;

/// <summary>
/// 可验证的选项接口 — 所有 Options 类实现此接口以支持启动时校验
/// </summary>
/// <remarks>
/// <para>实现类应在 <see cref="Validate"/> 方法中检查配置完整性，</para>
/// <para>对无效配置抛出 <see cref="InvalidOperationException"/>。</para>
/// <para>配合 <c>AddValidatedOptions&lt;T&gt;()</c> 扩展方法使用，可实现启动时 fail-fast。</para>
/// </remarks>
public interface IValidatableOptions
{
    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    /// <exception cref="InvalidOperationException">配置无效时抛出</exception>
    void Validate();
}
