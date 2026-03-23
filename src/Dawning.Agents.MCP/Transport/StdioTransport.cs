namespace Dawning.Agents.MCP.Transport;

using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using Microsoft.Extensions.Logging;

/// <summary>
/// 标准输入/输出流传输
/// </summary>
/// <remarks>
/// 实现 MCP 协议的标准 stdio 传输，与 Claude Desktop 兼容。
/// 消息格式: Content-Length: {length}\r\n\r\n{json}
/// </remarks>
public sealed class StdioTransport : IMCPTransport
{
    private readonly Stream _inputStream;
    private readonly Stream _outputStream;
    private readonly ILogger<StdioTransport> _logger;
    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private Pipe _pipe = new();
    private volatile bool _isConnected;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;
    private volatile bool _disposed;

    public StdioTransport(ILogger<StdioTransport>? logger = null)
        : this(Console.OpenStandardInput(), Console.OpenStandardOutput(), logger) { }

    public StdioTransport(
        Stream inputStream,
        Stream outputStream,
        ILogger<StdioTransport>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(inputStream);
        ArgumentNullException.ThrowIfNull(outputStream);
        _inputStream = inputStream;
        _outputStream = outputStream;
        _logger =
            logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<StdioTransport>.Instance;
    }

    public bool IsConnected => _isConnected;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CancellationTokenSource? oldCts;
        Task? oldTask;
        CancellationTokenSource newCts;

        lock (_stateLock)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(StdioTransport));
            }

            oldCts = _readCts;
            oldTask = _readTask;

            newCts = new CancellationTokenSource();
            _readCts = newCts;
            _pipe = new Pipe();
            _isConnected = true;
            _readTask = ReadInputAsync(newCts, newCts.Token);
        }

        if (oldCts != null)
        {
            await oldCts.CancelAsync().ConfigureAwait(false);
            oldCts.Dispose();
        }

        if (oldTask != null)
        {
            try
            {
                await oldTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
        }

        // 后台读取任务已在状态锁内更新，防止并发 Start/Dispose 竞态

        _logger.LogDebug("Stdio transport started");
    }

    public async Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
        {
            throw new InvalidOperationException("Transport not connected");
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            var header = $"Content-Length: {bytes.Length}\r\n\r\n";
            var headerBytes = Encoding.UTF8.GetBytes(header);

            await _outputStream.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);
            await _outputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await _outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogTrace("Sent message: {Length} bytes", bytes.Length);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<string?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
        {
            return null;
        }

        try
        {
            var result = await _pipe.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            var buffer = result.Buffer;

            if (TryParseMessage(ref buffer, out var message))
            {
                _pipe.Reader.AdvanceTo(buffer.Start);
                _logger.LogTrace("Received message: {Length} bytes", message?.Length ?? 0);
                return message;
            }

            _pipe.Reader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted)
            {
                _isConnected = false;
                return null;
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// 后台读取输入流
    /// </summary>
    private async Task ReadInputAsync(
        CancellationTokenSource source,
        CancellationToken cancellationToken
    )
    {
        var writer = _pipe.Writer;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var memory = writer.GetMemory(4096);
                var bytesRead = await _inputStream
                    .ReadAsync(memory, cancellationToken)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    break;
                }

                writer.Advance(bytesRead);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading from input stream");
        }
        finally
        {
            await writer.CompleteAsync().ConfigureAwait(false);

            lock (_stateLock)
            {
                if (ReferenceEquals(_readCts, source))
                {
                    _isConnected = false;
                }
            }
        }
    }

    /// <summary>
    /// 尝试解析消息
    /// </summary>
    private static bool TryParseMessage(ref ReadOnlySequence<byte> buffer, out string? message)
    {
        message = null;

        // 查找 Content-Length header
        var headerEnd = FindSequence(buffer, "\r\n\r\n"u8);
        if (headerEnd < 0)
        {
            return false;
        }

        var headerSlice = buffer.Slice(0, headerEnd);
        var headerText = Encoding.UTF8.GetString(headerSlice);

        if (!TryParseContentLength(headerText, out var contentLength) || contentLength <= 0)
        {
            return false;
        }

        var bodyStart = headerEnd + 4; // "\r\n\r\n".Length
        var totalLength = bodyStart + contentLength;

        if (buffer.Length < totalLength)
        {
            return false;
        }

        var bodySlice = buffer.Slice(bodyStart, contentLength);
        message = Encoding.UTF8.GetString(bodySlice);

        buffer = buffer.Slice(totalLength);
        return true;
    }

    /// <summary>
    /// 解析 Content-Length
    /// </summary>
    private static bool TryParseContentLength(string header, out int length)
    {
        length = 0;
        const string prefix = "Content-Length:";

        foreach (var line in header.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed[prefix.Length..].Trim();
                return int.TryParse(value, out length);
            }
        }

        return false;
    }

    /// <summary>
    /// 在 buffer 中查找序列
    /// </summary>
    private static long FindSequence(ReadOnlySequence<byte> buffer, ReadOnlySpan<byte> sequence)
    {
        var reader = new SequenceReader<byte>(buffer);
        if (reader.TryReadTo(out ReadOnlySpan<byte> _, sequence, advancePastDelimiter: false))
        {
            return reader.Consumed;
        }

        return -1;
    }

    public async ValueTask DisposeAsync()
    {
        CancellationTokenSource? readCts;
        Task? readTask;

        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _isConnected = false;
            readCts = _readCts;
            readTask = _readTask;
            _readCts = null;
            _readTask = null;
        }

        if (readCts != null)
        {
            await readCts.CancelAsync().ConfigureAwait(false);
        }

        if (readTask != null)
        {
            try
            {
                await readTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
        }

        readCts?.Dispose();
        _writeLock.Dispose();
        // CompleteAsync is idempotent — safe even if ReadInputAsync already completed the writer
        await _pipe.Writer.CompleteAsync().ConfigureAwait(false);
        await _pipe.Reader.CompleteAsync().ConfigureAwait(false);
    }
}
