using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Sqlite;

/// <summary>
/// Manages SQLite database connections and schema initialization.
/// </summary>
public sealed class SqliteDbContext : IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteDbContext> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteDbContext"/> class.
    /// </summary>
    /// <param name="options">The SQLite memory options.</param>
    /// <param name="logger">An optional logger instance.</param>
    public SqliteDbContext(
        IOptions<SqliteMemoryOptions> options,
        ILogger<SqliteDbContext>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        _connectionString = options.Value.ConnectionString;
        _logger = logger ?? NullLogger<SqliteDbContext>.Instance;
    }

    /// <summary>
    /// Creates and opens a new SQLite connection.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An open <see cref="SqliteConnection"/>.</returns>
    public async Task<SqliteConnection> CreateConnectionAsync(
        CancellationToken cancellationToken = default
    )
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        // Enable WAL mode for better concurrent read performance
        await connection.ExecuteAsync("PRAGMA journal_mode=WAL;").ConfigureAwait(false);
        return connection;
    }

    /// <summary>
    /// Ensures the database schema is created.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Ensuring SQLite schema for conversation memory");

        await using var connection = await CreateConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        await connection
            .ExecuteAsync(
                """
                CREATE TABLE IF NOT EXISTS conversation_messages (
                    id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    session_id  TEXT NOT NULL,
                    role        TEXT NOT NULL,
                    content     TEXT NOT NULL,
                    token_count INTEGER NOT NULL DEFAULT 0,
                    created_at  TEXT NOT NULL DEFAULT (datetime('now')),
                    CONSTRAINT chk_role CHECK (role IN ('system', 'user', 'assistant', 'tool'))
                );

                CREATE INDEX IF NOT EXISTS idx_messages_session_id
                    ON conversation_messages (session_id);

                CREATE INDEX IF NOT EXISTS idx_messages_session_created
                    ON conversation_messages (session_id, created_at);
                """
            )
            .ConfigureAwait(false);

        _logger.LogInformation("SQLite schema ensured successfully");
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        // SqliteConnection is created per-operation, nothing to dispose here
        return ValueTask.CompletedTask;
    }
}
