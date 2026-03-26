namespace Dawning.Agents.Abstractions.Prompts;

/// <summary>
/// Prompt template interface.
/// </summary>
public interface IPromptTemplate
{
    /// <summary>
    /// Template name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Template content (with placeholders).
    /// </summary>
    string Template { get; }

    /// <summary>
    /// Formats the template by substituting placeholders.
    /// </summary>
    /// <param name="variables">Variable dictionary.</param>
    /// <returns>The formatted string.</returns>
    string Format(IReadOnlyDictionary<string, object> variables);
}
