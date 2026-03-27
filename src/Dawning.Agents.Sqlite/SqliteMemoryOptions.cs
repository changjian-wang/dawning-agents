using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Sqlite;

/// <summary>
/// Configuration options for SQLite conversation memory.
/// </summary>
public sealed class SqliteMemoryOptions : IValidatableOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Dawning:Agents:Memory:Sqlite";

    /// <summary>
    /// Gets or sets the SQLite connection string.
    /// Defaults to a local file-based database.
    /// </summary>
    public string ConnectionString { get; set; } = "Data Source=dawning_agents.db";

    /// <summary>
    /// Gets or sets whether to automatically create the database schema on startup.
    /// </summary>
    public bool AutoCreateSchema { get; set; } = true;

    /// <inheritdoc />
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(ConnectionString)} must not be empty."
            );
        }
    }
}
