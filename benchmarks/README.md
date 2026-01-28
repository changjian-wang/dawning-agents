# Dawning.Agents Benchmarks

Performance benchmarks for Dawning.Agents framework using [BenchmarkDotNet](https://benchmarkdotnet.org/).

## Benchmarks

| Benchmark | Description |
|-----------|-------------|
| `TokenCounterBenchmarks` | Token counting performance for various text lengths |
| `MemoryBenchmarks` | Conversation memory operations (add, get messages) |
| `ToolRegistryBenchmarks` | Tool lookup and registry operations |
| `JsonSerializationBenchmarks` | JSON serialization/deserialization |

## Running Benchmarks

### Using Script (Recommended)

```powershell
# Run all benchmarks (short job)
./scripts/benchmark.ps1

# Run specific benchmarks
./scripts/benchmark.ps1 -Filter "*Memory*"
./scripts/benchmark.ps1 -Filter "*Token*"

# Run with longer iterations for more accurate results
./scripts/benchmark.ps1 -Job Long
```

### Using dotnet CLI

```bash
cd benchmarks/Dawning.Agents.Benchmarks

# List available benchmarks
dotnet run -c Release -- --list flat

# Run all benchmarks
dotnet run -c Release

# Run specific benchmarks
dotnet run -c Release -- --filter "*Memory*"

# Run with specific job
dotnet run -c Release -- --job short
```

## Results

Results are saved to `benchmarks/results/` directory with:
- Summary markdown file
- Detailed HTML report
- Raw data in various formats

## Sample Results

### Token Counter (approximate)

| Method | Text Length | Mean | Allocated |
|--------|-------------|------|-----------|
| CountTokens_ShortText | 50 chars | ~50 ns | 0 B |
| CountTokens_MediumText | 500 chars | ~200 ns | 0 B |
| CountTokens_LongText | 5000 chars | ~2 μs | 0 B |

### Memory Operations (approximate)

| Method | Messages | Mean | Allocated |
|--------|----------|------|-----------|
| BufferMemory_AddMessages | 10 | ~5 μs | ~2 KB |
| BufferMemory_AddMessages | 100 | ~50 μs | ~20 KB |
| WindowMemory_AddMessages | 1000 | ~200 μs | ~5 KB |

### Tool Registry (approximate)

| Method | Tools | Mean | Allocated |
|--------|-------|------|-----------|
| GetTool_First | 100 | ~50 ns | 0 B |
| GetTool_NotFound | 100 | ~100 ns | 0 B |
| GetAllTools | 100 | ~20 ns | 0 B |

## Adding New Benchmarks

1. Create a new benchmark class in `Dawning.Agents.Benchmarks/`
2. Add `[MemoryDiagnoser]` attribute for memory tracking
3. Use `[Benchmark]` attribute on methods
4. Use `[Params]` for parameterized tests

Example:
```csharp
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class MyBenchmarks
{
    [Params(10, 100, 1000)]
    public int Size { get; set; }

    [Benchmark(Baseline = true)]
    public void BaselineMethod() { /* ... */ }

    [Benchmark]
    public void OptimizedMethod() { /* ... */ }
}
```

## Hardware Requirements

- Benchmarks should be run on a quiet machine
- Close other applications during benchmarking
- Use Release configuration only
- Results vary by hardware - use for relative comparisons
