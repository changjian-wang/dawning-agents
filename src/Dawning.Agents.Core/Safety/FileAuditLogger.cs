using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dawning.Agents.Abstractions;
using Dawning.Agents.Abstractions.Safety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Safety;

/// <summary>
/// File-based audit logger using JSON Lines format with log rotation support.
/// </summary>
/// <remarks>
/// <para>Each audit record is appended in JSON Lines (.jsonl) format.</para>
/// <para>Supports automatic rotation by file size, retaining a specified number of archive files.</para>
/// <para>Suitable for production environments requiring persistent audit logging.</para>
/// </remarks>
public sealed class FileAuditLogger : IAuditLogger, IAsyncDisposable
{
    private readonly FileAuditOptions _options;
    private readonly AuditOptions _auditOptions;
    private readonly ILogger<FileAuditLogger> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;
    private StreamWriter? _writer;
    private string _currentFilePath;
    private long _currentFileSize;
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the file audit logger.
    /// </summary>
    public FileAuditLogger(
        IOptions<FileAuditOptions> fileOptions,
        IOptions<AuditOptions> auditOptions,
        ILogger<FileAuditLogger>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(fileOptions);
        ArgumentNullException.ThrowIfNull(auditOptions);

        _options = fileOptions.Value;
        _auditOptions = auditOptions.Value;
        _logger = logger ?? NullLogger<FileAuditLogger>.Instance;

        _jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };

        Directory.CreateDirectory(_options.Directory);
        _currentFilePath = GetCurrentFilePath();
        _currentFileSize = File.Exists(_currentFilePath)
            ? new FileInfo(_currentFilePath).Length
            : 0;
    }

    /// <inheritdoc />
    public async Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_auditOptions.Enabled)
        {
            return;
        }

        var processedEntry = ProcessEntry(entry);
        var json = JsonSerializer.Serialize(processedEntry, _jsonOptions);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await RotateIfNeededAsync(cancellationToken).ConfigureAwait(false);

            _writer ??= new StreamWriter(
                new FileStream(
                    _currentFilePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true
                )
            );

            await _writer.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);

            _currentFileSize += json.Length + Environment.NewLine.Length;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to write audit log file: {Path}", _currentFilePath);
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditEntry>> QueryAsync(
        AuditFilter filter,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var results = new List<AuditEntry>();

        // Get all audit log files (ordered by time descending)
        var files = Directory
            .GetFiles(_options.Directory, $"{_options.FilePrefix}*.jsonl")
            .OrderByDescending(f => f);

        foreach (var file in files)
        {
            if (results.Count >= filter.MaxResults)
            {
                break;
            }

            await foreach (
                var entry in ReadEntriesFromFileAsync(file, cancellationToken).ConfigureAwait(false)
            )
            {
                if (MatchesFilter(entry, filter))
                {
                    results.Add(entry);
                    if (results.Count >= filter.MaxResults)
                    {
                        break;
                    }
                }
            }
        }

        return results.OrderByDescending(e => e.Timestamp).Take(filter.MaxResults).ToList();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_writer is not null)
            {
                await _writer.FlushAsync().ConfigureAwait(false);
                await _writer.DisposeAsync().ConfigureAwait(false);
                _writer = null;
            }
        }
        finally
        {
            _writeLock.Release();
            _writeLock.Dispose();
        }
    }

    private async Task RotateIfNeededAsync(CancellationToken cancellationToken)
    {
        if (_currentFileSize < _options.MaxFileSizeBytes)
        {
            return;
        }

        // Close current file
        if (_writer is not null)
        {
            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            await _writer.DisposeAsync().ConfigureAwait(false);
            _writer = null;
        }

        // Rotate: rename to timestamped archive file
        var archiveName = Path.Combine(
            _options.Directory,
            $"{_options.FilePrefix}{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.jsonl"
        );
        File.Move(_currentFilePath, archiveName, overwrite: true);

        _logger.LogInformation("Audit log rotated: {Old} -> {New}", _currentFilePath, archiveName);

        // Clean up files exceeding the retention count
        CleanupOldFiles();

        // Reset
        _currentFilePath = GetCurrentFilePath();
        _currentFileSize = 0;
    }

    private void CleanupOldFiles()
    {
        var files = Directory
            .GetFiles(_options.Directory, $"{_options.FilePrefix}*.jsonl")
            .Where(f => f != _currentFilePath)
            .OrderByDescending(f => f)
            .ToList();

        foreach (var file in files.Skip(_options.MaxRetainedFiles))
        {
            try
            {
                File.Delete(file);
                _logger.LogDebug("Deleted expired audit log: {File}", file);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete expired audit log: {File}", file);
            }
        }
    }

    private string GetCurrentFilePath() =>
        Path.Combine(_options.Directory, $"{_options.FilePrefix}current.jsonl");

    private async IAsyncEnumerable<AuditEntry> ReadEntriesFromFileAsync(
        string filePath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        using var reader = new StreamReader(
            new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        );

        string? line;
        while (
            (line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            AuditEntry? entry = null;
            try
            {
                entry = JsonSerializer.Deserialize<AuditEntry>(line, _jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to parse audit log line: {Line}",
                    line[..Math.Min(100, line.Length)]
                );
            }

            if (entry is not null)
            {
                yield return entry;
            }
        }
    }

    private static bool MatchesFilter(AuditEntry entry, AuditFilter filter)
    {
        if (!string.IsNullOrEmpty(filter.SessionId) && entry.SessionId != filter.SessionId)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(filter.AgentName) && entry.AgentName != filter.AgentName)
        {
            return false;
        }

        if (filter.EventType.HasValue && entry.EventType != filter.EventType.Value)
        {
            return false;
        }

        if (filter.StartTime.HasValue && entry.Timestamp < filter.StartTime.Value)
        {
            return false;
        }

        if (filter.EndTime.HasValue && entry.Timestamp > filter.EndTime.Value)
        {
            return false;
        }

        if (filter.Status.HasValue && entry.Status != filter.Status.Value)
        {
            return false;
        }

        return true;
    }

    private AuditEntry ProcessEntry(AuditEntry entry)
    {
        var maxLen = _auditOptions.MaxContentLength;

        return entry with
        {
            Input = TruncateIfNeeded(entry.Input, maxLen, _auditOptions.LogInput),
            Output = TruncateIfNeeded(entry.Output, maxLen, _auditOptions.LogOutput),
            ToolArgs = TruncateIfNeeded(entry.ToolArgs, maxLen, _auditOptions.LogToolArgs),
        };
    }

    private static string? TruncateIfNeeded(string? content, int maxLength, bool shouldLog)
    {
        if (!shouldLog || string.IsNullOrEmpty(content))
        {
            return shouldLog ? content : "[REDACTED]";
        }

        return content.Length <= maxLength ? content : content[..maxLength] + "...[TRUNCATED]";
    }
}

/// <summary>
/// File audit log configuration options.
/// </summary>
public sealed class FileAuditOptions : IValidatableOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Audit:File";

    /// <summary>
    /// Gets or sets the audit log file directory.
    /// </summary>
    public string Directory { get; set; } = "logs/audit";

    /// <summary>
    /// Gets or sets the file name prefix.
    /// </summary>
    public string FilePrefix { get; set; } = "audit_";

    /// <summary>
    /// Gets or sets the maximum file size in bytes. Defaults to 50 MB.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the number of archived files to retain.
    /// </summary>
    public int MaxRetainedFiles { get; set; } = 30;

    /// <inheritdoc />
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Directory))
        {
            throw new InvalidOperationException("Audit log directory must not be empty");
        }

        if (string.IsNullOrWhiteSpace(FilePrefix))
        {
            throw new InvalidOperationException("File prefix must not be empty");
        }

        if (MaxFileSizeBytes < 1024)
        {
            throw new InvalidOperationException("Maximum file size must not be less than 1 KB");
        }

        if (MaxRetainedFiles < 1)
        {
            throw new InvalidOperationException("Retained file count must be at least 1");
        }
    }
}
