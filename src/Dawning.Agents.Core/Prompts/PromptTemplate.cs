using System.Text.RegularExpressions;
using Dawning.Agents.Abstractions.Prompts;

namespace Dawning.Agents.Core.Prompts;

/// <summary>
/// A simple prompt template implementation that supports <c>{variable}</c> placeholders.
/// </summary>
public partial class PromptTemplate : IPromptTemplate
{
    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex PlaceholderRegex();

    /// <summary>
    /// Gets the template name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the template content containing <c>{variable}</c> placeholders.
    /// </summary>
    public string Template { get; }

    public PromptTemplate(string name, string template)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Template = template ?? throw new ArgumentNullException(nameof(template));
    }

    /// <summary>
    /// Formats the template by replacing placeholders with the supplied variable values.
    /// </summary>
    /// <param name="variables">A dictionary of variables where keys are placeholder names and values are the replacement content.</param>
    /// <returns>The formatted string.</returns>
    public string Format(IReadOnlyDictionary<string, object> variables)
    {
        if (variables == null || variables.Count == 0)
        {
            return Template;
        }

        return PlaceholderRegex()
            .Replace(
                Template,
                match =>
                {
                    var key = match.Groups[1].Value;
                    return variables.TryGetValue(key, out var value)
                        ? value?.ToString() ?? string.Empty
                        : match.Value; // Preserve unresolved placeholders
                }
            );
    }

    /// <summary>
    /// Creates a new <see cref="PromptTemplate"/> instance.
    /// </summary>
    public static PromptTemplate Create(string name, string template) => new(name, template);
}
