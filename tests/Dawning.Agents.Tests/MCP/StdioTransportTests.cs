namespace Dawning.Agents.Tests.MCP;

using Dawning.Agents.MCP.Transport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class StdioTransportTests
{
    [Fact]
    public async Task StartAsync_WhenStartupTokenCanceledLater_ShouldRemainConnected()
    {
        using var input = new BlockingReadStream();
        using var output = new MemoryStream();
        await using var transport = new StdioTransport(
            input,
            output,
            NullLogger<StdioTransport>.Instance
        );

        using var startupCts = new CancellationTokenSource();
        await transport.StartAsync(startupCts.Token);

        startupCts.Cancel();
        await Task.Delay(100);

        transport.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_WhenReadLoopAlreadyCompleted_ShouldNotThrow()
    {
        using var input = new MemoryStream();
        using var output = new MemoryStream();
        await using var transport = new StdioTransport(
            input,
            output,
            NullLogger<StdioTransport>.Instance
        );

        await transport.StartAsync();
        await Task.Delay(50);

        var act = async () => await transport.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    private sealed class BlockingReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position
        {
            get => 0;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override void Flush() { }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            return new ValueTask<int>(
                Task.Delay(Timeout.Infinite, cancellationToken)
                    .ContinueWith(_ => 0, cancellationToken)
            );
        }
    }
}
