namespace Dawning.Agents.Abstractions;

/// <summary>
/// {ServiceDescription}
/// </summary>
public interface I{ServiceName}
{
    /// <summary>
    /// {MethodDescription}
    /// </summary>
    /// <param name="input">The input to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>{ReturnDescription}</returns>
    Task<{ReturnType}> {MethodName}Async(
        {InputType} input,
        CancellationToken cancellationToken = default);
}
