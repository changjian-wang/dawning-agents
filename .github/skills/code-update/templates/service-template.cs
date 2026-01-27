using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core;

/// <summary>
/// Implementation of <see cref="I{ServiceName}"/>.
/// </summary>
public class {ServiceName} : I{ServiceName}
{
    private readonly ILogger<{ServiceName}> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="{ServiceName}"/>.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    public {ServiceName}(ILogger<{ServiceName}>? logger = null)
    {
        _logger = logger ?? NullLogger<{ServiceName}>.Instance;
    }

    /// <inheritdoc />
    public async Task<{ReturnType}> {MethodName}Async(
        {InputType} input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        _logger.LogDebug("Processing {Input}...", input);

        // TODO: Implement logic here
        await Task.CompletedTask;

        _logger.LogInformation("{MethodName} completed successfully", nameof({MethodName}Async));

        return new {ReturnType}();
    }
}
