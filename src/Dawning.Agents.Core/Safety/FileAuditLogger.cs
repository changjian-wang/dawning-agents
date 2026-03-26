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
/// 基于文件的审计日志记录器 — JSON Lines 格式，支持日志轮转
/// </summary>
/// <remarks>
/// <para>每条审计记录以 JSON Lines（.jsonl）格式追加写入文件</para>
/// <para>支持按大小自动轮转，保留指定数量的历史文件</para>
/// <para>适用于生产环境的持久化审计需求</para>
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
    /// 初始化文件审计日志记录器
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
            _logger.LogError(ex, "写入审计日志文件失败: {Path}", _currentFilePath);
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

        // 获取所有审计日志文件（按时间倒序）
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

        // 关闭当前文件
        if (_writer is not null)
        {
            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            await _writer.DisposeAsync().ConfigureAwait(false);
            _writer = null;
        }

        // 轮转：重命名为带时间戳的归档文件
        var archiveName = Path.Combine(
            _options.Directory,
            $"{_options.FilePrefix}{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.jsonl"
        );
        File.Move(_currentFilePath, archiveName, overwrite: true);

        _logger.LogInformation("审计日志轮转: {Old} -> {New}", _currentFilePath, archiveName);

        // 清理超出保留数的旧文件
        CleanupOldFiles();

        // 重置
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
                _logger.LogDebug("删除过期审计日志: {File}", file);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "删除过期审计日志失败: {File}", file);
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
                    "解析审计日志行失败: {Line}",
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
/// 文件审计日志配置
/// </summary>
public sealed class FileAuditOptions : IValidatableOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Audit:File";

    /// <summary>
    /// 日志文件目录
    /// </summary>
    public string Directory { get; set; } = "logs/audit";

    /// <summary>
    /// 文件名前缀
    /// </summary>
    public string FilePrefix { get; set; } = "audit_";

    /// <summary>
    /// 单文件最大大小（字节），默认 50MB
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024;

    /// <summary>
    /// 保留的归档文件数量
    /// </summary>
    public int MaxRetainedFiles { get; set; } = 30;

    /// <inheritdoc />
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Directory))
        {
            throw new InvalidOperationException("审计日志目录不能为空");
        }

        if (string.IsNullOrWhiteSpace(FilePrefix))
        {
            throw new InvalidOperationException("文件前缀不能为空");
        }

        if (MaxFileSizeBytes < 1024)
        {
            throw new InvalidOperationException("单文件最大大小不能小于 1KB");
        }

        if (MaxRetainedFiles < 1)
        {
            throw new InvalidOperationException("保留文件数量至少为 1");
        }
    }
}
