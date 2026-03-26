namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// Ephemeral script tool definition — describes a script tool that can be created at runtime.
/// </summary>
public class EphemeralToolDefinition
{
    /// <summary>
    /// Tool name (snake_case, unique identifier).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Tool purpose description (for the LLM to understand).
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Script runtime.
    /// </summary>
    public ScriptRuntime Runtime { get; set; } = ScriptRuntime.Bash;

    /// <summary>
    /// Script content; use $param_name to reference parameters.
    /// </summary>
    public required string Script { get; set; }

    /// <summary>
    /// Tool parameter list.
    /// </summary>
    public IList<ScriptParameter> Parameters { get; set; } = new List<ScriptParameter>();

    /// <summary>
    /// Persistence scope.
    /// </summary>
    public ToolScope Scope { get; set; } = ToolScope.Session;

    /// <summary>
    /// Metadata.
    /// </summary>
    public EphemeralToolMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Script parameter definition.
/// </summary>
public record ScriptParameter
{
    /// <summary>
    /// Parameter name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Parameter description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Parameter type (string, int, bool).
    /// </summary>
    public string Type { get; init; } = "string";

    /// <summary>
    /// Whether the parameter is required.
    /// </summary>
    public bool Required { get; init; } = true;

    /// <summary>
    /// Default value.
    /// </summary>
    public string? DefaultValue { get; init; }
}

/// <summary>
/// Ephemeral tool metadata (with behavioral context for the router and reflection engine).
/// </summary>
public class EphemeralToolMetadata
{
    /// <summary>
    /// Author.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Creation time.
    /// </summary>
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Tags.
    /// </summary>
    public IList<string> Tags { get; set; } = new List<string>();

    /// <summary>
    /// Version number (incremented on each fix).
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Usage scenario description (for the skill router to understand when to use this tool).
    /// </summary>
    public string? WhenToUse { get; set; }

    /// <summary>
    /// Known limitations.
    /// </summary>
    public string? Limitations { get; set; }

    /// <summary>
    /// Historical failure patterns (for the reflection engine to reference during diagnosis).
    /// </summary>
    public IList<string> FailurePatterns { get; set; } = new List<string>();

    /// <summary>
    /// Related skill names (for the router to make association recommendations).
    /// </summary>
    public IList<string> RelatedSkills { get; set; } = new List<string>();

    /// <summary>
    /// Revision count.
    /// </summary>
    public int RevisionCount { get; set; }

    /// <summary>
    /// Last revision time.
    /// </summary>
    public DateTimeOffset? LastRevisedAt { get; set; }
}

/// <summary>
/// Script runtime type.
/// </summary>
public enum ScriptRuntime
{
    /// <summary>
    /// Bash / sh (default on Linux / macOS).
    /// </summary>
    Bash,

    /// <summary>
    /// PowerShell (default on Windows, cross-platform available).
    /// </summary>
    PowerShell,

    /// <summary>
    /// Python 3.
    /// </summary>
    Python,
}

/// <summary>
/// Tool persistence scope.
/// </summary>
public enum ToolScope
{
    /// <summary>
    /// Session level — lives only within the current session (in-memory).
    /// </summary>
    Session,

    /// <summary>
    /// User level — persisted across projects (~/.dawning/tools/).
    /// </summary>
    User,

    /// <summary>
    /// Global level — project-level persistence ({project}/.dawning/tools/), can be committed to git.
    /// </summary>
    Global,
}
