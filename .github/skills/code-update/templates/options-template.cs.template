using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.{Area};

/// <summary>
/// Configuration options for {ServiceName}.
/// </summary>
public class {ServiceName}Options : IValidatableOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "{SectionName}";

    /// <summary>
    /// Option 1.
    /// </summary>
    public string Option1 { get; set; } = "default";

    /// <summary>
    /// Option 2.
    /// </summary>
    public int Option2 { get; set; } = 30;

    /// <inheritdoc />
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Option1))
        {
            throw new InvalidOperationException($"{nameof(Option1)} is required.");
        }

        if (Option2 <= 0)
        {
            throw new InvalidOperationException($"{nameof(Option2)} must be greater than 0.");
        }
    }
}
