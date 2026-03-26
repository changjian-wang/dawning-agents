namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// Tool with semantic match score.
/// </summary>
/// <param name="Tool">The matched tool.</param>
/// <param name="Score">Semantic similarity score (0–1).</param>
public record ScoredTool(ITool Tool, float Score);

/// <summary>
/// Semantic skill router — retrieves the most relevant tools based on task description.
/// </summary>
/// <remarks>
/// <para>Uses semantic embeddings to build a vector index on tool descriptions.</para>
/// <para>Replaces full prompt injection when the number of tools exceeds a threshold.</para>
/// </remarks>
public interface ISkillRouter
{
    /// <summary>
    /// Semantically matches the most relevant tools based on task description.
    /// </summary>
    /// <param name="taskDescription">User task description.</param>
    /// <param name="topK">Maximum number of tools to return.</param>
    /// <param name="minScore">Minimum similarity threshold (0–1).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Scored tool list, sorted by relevance in descending order.</returns>
    Task<IReadOnlyList<ScoredTool>> RouteAsync(
        string taskDescription,
        int topK = 5,
        float minScore = 0.3f,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Rebuilds the tool index (should be called after tool registration/removal).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RebuildIndexAsync(CancellationToken cancellationToken = default);
}
