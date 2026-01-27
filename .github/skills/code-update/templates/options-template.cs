namespace Dawning.Agents.Abstractions;

/// <summary>
/// Configuration options for {ServiceName}.
/// </summary>
/// <remarks>
/// Configuration in appsettings.json:
/// <code>
/// {
///   "{SectionName}": {
///     "Option1": "value",
///     "Option2": 30
///   }
/// }
/// </code>
/// </remarks>
public class {ServiceName}Options
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "{SectionName}";

    /// <summary>
    /// Gets or sets option 1.
    /// </summary>
    public string Option1 { get; set; } = "default";

    /// <summary>
    /// Gets or sets option 2.
    /// </summary>
    public int Option2 { get; set; } = 30;

    /// <summary>
    /// Validates the options.
    /// </summary>
    /// <exception cref="InvalidOperationException">When options are invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Option1))
        {
            throw new InvalidOperationException($"{nameof(Option1)} is required.");
        }

        if (Option2 <= 0)
        {
            throw new InvalidOperationException($"{nameof(Option2)} must be positive.");
        }
    }
}
