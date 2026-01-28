using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Core.Memory;

namespace Dawning.Agents.Benchmarks;

/// <summary>
/// 对话记忆性能基准测试
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class MemoryBenchmarks
{
    private ITokenCounter _tokenCounter = null!;
    private ConversationMessage[] _messages = null!;

    [Params(10, 100, 1000)]
    public int MessageCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _tokenCounter = new SimpleTokenCounter();

        _messages = Enumerable.Range(0, MessageCount)
            .Select(i => new ConversationMessage
            {
                Role = i % 2 == 0 ? "user" : "assistant",
                Content = $"This is message number {i} with some content for testing purposes."
            })
            .ToArray();
    }

    [Benchmark(Baseline = true)]
    public async Task BufferMemory_AddMessages()
    {
        var memory = new BufferMemory(_tokenCounter);
        foreach (var msg in _messages)
        {
            await memory.AddMessageAsync(msg);
        }
    }

    [Benchmark]
    public async Task WindowMemory_AddMessages()
    {
        var memory = new WindowMemory(_tokenCounter, 20);
        foreach (var msg in _messages)
        {
            await memory.AddMessageAsync(msg);
        }
    }

    [Benchmark]
    public async Task BufferMemory_GetMessages()
    {
        var memory = new BufferMemory(_tokenCounter);
        foreach (var msg in _messages)
        {
            await memory.AddMessageAsync(msg);
        }
        await memory.GetMessagesAsync();
    }

    [Benchmark]
    public async Task WindowMemory_GetMessages()
    {
        var memory = new WindowMemory(_tokenCounter, 20);
        foreach (var msg in _messages)
        {
            await memory.AddMessageAsync(msg);
        }
        await memory.GetMessagesAsync();
    }
}
