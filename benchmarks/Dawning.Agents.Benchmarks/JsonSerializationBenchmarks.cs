using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using System.Text.Json;

namespace Dawning.Agents.Benchmarks;

/// <summary>
/// JSON 序列化性能基准测试
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class JsonSerializationBenchmarks
{
    private ChatMessage _simpleMessage = null!;
    private ChatMessage[] _messageArray = null!;
    private string _simpleJson = null!;
    private string _arrayJson = null!;
    private JsonSerializerOptions _options = null!;

    [Params(1, 10, 100)]
    public int MessageCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        _simpleMessage = new ChatMessage("user", "Hello, this is a test message for benchmarking.");

        _messageArray = Enumerable.Range(0, MessageCount)
            .Select(i => new ChatMessage(
                i % 2 == 0 ? "user" : "assistant",
                $"Message {i}: This is a test message with some content for benchmarking purposes."))
            .ToArray();

        _simpleJson = JsonSerializer.Serialize(_simpleMessage, _options);
        _arrayJson = JsonSerializer.Serialize(_messageArray, _options);
    }

    [Benchmark(Baseline = true)]
    public string Serialize_SingleMessage()
        => JsonSerializer.Serialize(_simpleMessage, _options);

    [Benchmark]
    public string Serialize_MessageArray()
        => JsonSerializer.Serialize(_messageArray, _options);

    [Benchmark]
    public ChatMessage? Deserialize_SingleMessage()
        => JsonSerializer.Deserialize<ChatMessage>(_simpleJson, _options);

    [Benchmark]
    public ChatMessage[]? Deserialize_MessageArray()
        => JsonSerializer.Deserialize<ChatMessage[]>(_arrayJson, _options);

    public record ChatMessage(string Role, string Content);
}
