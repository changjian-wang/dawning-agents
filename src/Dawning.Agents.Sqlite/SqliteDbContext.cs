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
    private readonly bool _autoCreateSchema;
    private readonly ILogger<SqliteDbContext> _logger;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private volatile bool _schemaEnsured;

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
        _autoCreateSchema = options.Value.AutoCreateSchema;
        _logger = logger ?? NullLogger<SqliteDbContext>.Instance;
    }

    /// <summary>
    /// Creates and opens a new SQLite connection.
    /// Automatically ensures schema on first call when <see cref="SqliteMemoryOptions.AutoCreateSchema"/> is enabled.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An open <see cref="SqliteConnection"/>.</returns>
    public async Task<SqliteConnection> CreateConnectionAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (_autoCreateSchema && !_schemaEnsured)
        {
            await _schemaLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_schemaEnsured)
                {
                    await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
                    _schemaEnsured = true;
                }
            }
            finally
            {
                _schemaLock.Release();
            }
        }

        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    /// <summary>
    /// Ensures the database schema is created.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Ensuring SQLite schema for conversation memory");

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // WAL mode persists on the database file; only needs to be set once
        await connection
            .ExecuteAsync(
                new CommandDefinition(
                    "PRAGMA journal_mode=WAL;",
                    cancellationToken: cancellationToken
                )
            )
            .ConfigureAwait(false);

        await connection
            .ExecuteAsync(
                new CommandDefinition(
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
                    """,
                    cancellationToken: cancellationToken
                )
            )
            .ConfigureAwait(false);

        _schemaEnsured = true;
        _logger.LogInformation("SQLite schema ensured successfully");
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _schemaLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
