namespace Dawning.Agents.Abstractions;

/// <summary>
/// Validatable options interface — all Options classes implement this interface to support startup-time validation.
/// </summary>
/// <remarks>
/// <para>Implementations should check configuration completeness in the <see cref="Validate"/> method,</para>
/// <para>throwing <see cref="InvalidOperationException"/> for invalid configurations.</para>
/// <para>Used with the <c>AddValidatedOptions&lt;T&gt;()</c> extension method to enable fail-fast at startup.</para>
/// </remarks>
public interface IValidatableOptions
{
    /// <summary>
    /// Validates whether the configuration is valid.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the configuration is invalid.</exception>
    void Validate();
}
