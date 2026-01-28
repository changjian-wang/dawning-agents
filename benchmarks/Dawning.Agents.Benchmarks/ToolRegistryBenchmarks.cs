using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools;
using Dawning.Agents.Core.Tools.BuiltIn;

namespace Dawning.Agents.Benchmarks;

/// <summary>
/// 工具注册表性能基准测试
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class ToolRegistryBenchmarks
{
    private ToolRegistry _registry = null!;
    private ITool[] _tools = null!;

    [Params(10, 50, 100)]
    public int ToolCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _registry = new ToolRegistry();

        // 创建模拟工具
        _tools = Enumerable
            .Range(0, ToolCount)
            .Select(i => new MockTool($"tool_{i}", $"Description for tool {i}"))
            .ToArray();

        // 注册所有工具
        foreach (var tool in _tools)
        {
            _registry.Register(tool);
        }
    }

    [Benchmark(Baseline = true)]
    public ITool? GetTool_First() => _registry.GetTool("tool_0");

    [Benchmark]
    public ITool? GetTool_Middle() => _registry.GetTool($"tool_{ToolCount / 2}");

    [Benchmark]
    public ITool? GetTool_Last() => _registry.GetTool($"tool_{ToolCount - 1}");

    [Benchmark]
    public ITool? GetTool_NotFound() => _registry.GetTool("nonexistent_tool");

    [Benchmark]
    public IReadOnlyList<ITool> GetAllTools() => _registry.GetAllTools();

    private class MockTool : ITool
    {
        public string Name { get; }
        public string Description { get; }
        public string ParametersSchema => "{}";
        public bool RequiresConfirmation => false;
        public ToolRiskLevel RiskLevel => ToolRiskLevel.Low;
        public string? Category => "Mock";

        public MockTool(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public Task<ToolResult> ExecuteAsync(string input, CancellationToken ct = default) =>
            Task.FromResult(ToolResult.Ok("Mock result"));
    }
}
