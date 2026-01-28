using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Core.Memory;

namespace Dawning.Agents.Benchmarks;

/// <summary>
/// Token 计数器性能基准测试
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class TokenCounterBenchmarks
{
    private SimpleTokenCounter _counter = null!;
    private string _shortText = null!;
    private string _mediumText = null!;
    private string _longText = null!;
    private string _chineseText = null!;
    private string _mixedText = null!;

    [GlobalSetup]
    public void Setup()
    {
        _counter = new SimpleTokenCounter();

        // 短文本 (~50 chars)
        _shortText = "Hello, this is a short text for testing.";

        // 中等文本 (~500 chars)
        _mediumText = string.Join(" ", Enumerable.Repeat(
            "The quick brown fox jumps over the lazy dog.", 10));

        // 长文本 (~5000 chars)
        _longText = string.Join(" ", Enumerable.Repeat(
            "The quick brown fox jumps over the lazy dog. This is a longer sentence for testing purposes.", 50));

        // 中文文本
        _chineseText = string.Join("", Enumerable.Repeat(
            "这是一段用于测试的中文文本，包含各种常见的汉字。", 20));

        // 混合文本
        _mixedText = string.Join(" ", Enumerable.Repeat(
            "Hello 你好 World 世界 Test 测试", 30));
    }

    [Benchmark(Baseline = true)]
    public int CountTokens_ShortText() => _counter.CountTokens(_shortText);

    [Benchmark]
    public int CountTokens_MediumText() => _counter.CountTokens(_mediumText);

    [Benchmark]
    public int CountTokens_LongText() => _counter.CountTokens(_longText);

    [Benchmark]
    public int CountTokens_ChineseText() => _counter.CountTokens(_chineseText);

    [Benchmark]
    public int CountTokens_MixedText() => _counter.CountTokens(_mixedText);
}
