namespace Dawning.Agents.Tests.MCP;

using System.Reflection;
using Dawning.Agents.MCP.Client;
using Dawning.Agents.MCP.Transport;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

public class MCPClientCancellationTests
{
    [Fact]
    public async Task ListToolsAsync_WhenDisposedDuringPendingRequest_ShouldThrowObjectDisposedException()
    {
        var options = Options.Create(new MCPClientOptions { RequestTimeoutSeconds = 30 });

        var client = new MCPClient(options);
        var transport = new PendingTransport();

        SetPrivateField(client, "_transport", transport);
        SetPrivateField(client, "_initialized", true);

        var requestTask = client.ListToolsAsync();

        // Ensure request has been sent and is pending before disposal
        await transport.SentSignal.Task;

        await client.DisposeAsync();

        var act = async () => await requestTask;
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        var field = target
            .GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        field!.SetValue(target, value);
    }

    private sealed class PendingTransport : IMCPTransport
    {
        public TaskCompletionSource<bool> SentSignal { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsConnected => true;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SendAsync(string message, CancellationToken cancellationToken = default)
        {
            SentSignal.TrySetResult(true);
            return Task.CompletedTask;
        }

        public Task<string?> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
