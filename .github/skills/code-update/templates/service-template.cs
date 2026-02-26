using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.{Area};

/// <summary>
/// Implementation of <see cref="I{ServiceName}"/>.
/// </summary>
public class {ServiceName}(ILogger<{ServiceName}>? logger = null) : I{ServiceName}
{
    private readonly ILogger<{ServiceName}> _logger = logger ?? NullLogger<{ServiceName}>.Instance;

    /// <inheritdoc />
    public async Task<{ReturnType}> {MethodName}Async(
        {InputType} input,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(input);

        _logger.LogDebug("Processing {InputType}", typeof({InputType}).Name);

        // TODO: Implement business logic.
        await Task.CompletedTask;

        throw new NotImplementedException();
    }
}
