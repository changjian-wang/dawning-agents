---
description: "Performance analysis for Dawning.Agents: BenchmarkDotNet, hot paths, memory allocation, LINQ optimization, async overhead. Trigger: 性能, performance, benchmark, 基准测试, 热路径, hot path, 内存分配, allocation, 优化, optimize"
---

# Performance Skill

## 目标

分析和优化 Dawning.Agents 的性能，包括基准测试执行、热路径识别、内存分配优化。

## 触发条件

- **关键词**：性能, performance, benchmark, 基准测试, 热路径, hot path, 内存分配, allocation, 优化, optimize, 延迟, latency, throughput, 吞吐量
- **文件模式**：`benchmarks/**/*.cs`, `src/**/*.cs`
- **用户意图**：运行基准测试、分析性能瓶颈、优化内存分配、减少 GC 压力

## 编排

- **前置**：`build-project`（构建通过后）
- **后续**：`run-tests`（优化后跑测试确认不破坏功能）

---

## 基准测试

### 现有 Benchmarks

| Benchmark | 文件 | 说明 |
|-----------|------|------|
| `TokenCounterBenchmarks` | `benchmarks/.../TokenCounterBenchmarks.cs` | 不同文本长度的 Token 计数 |
| `MemoryBenchmarks` | `benchmarks/.../MemoryBenchmarks.cs` | 对话记忆操作（添加、获取） |
| `ToolRegistryBenchmarks` | `benchmarks/.../ToolRegistryBenchmarks.cs` | 工具查找和注册 |
| `JsonSerializationBenchmarks` | `benchmarks/.../JsonSerializationBenchmarks.cs` | JSON 序列化/反序列化 |

### 运行命令

```bash
# 运行全部 (short job)
cd /path/to/dawning-agents
dotnet run -c Release --project benchmarks/Dawning.Agents.Benchmarks

# 过滤特定 benchmark
dotnet run -c Release --project benchmarks/Dawning.Agents.Benchmarks -- --filter "*Memory*"

# PowerShell 脚本
./scripts/benchmark.ps1
./scripts/benchmark.ps1 -Filter "*Token*"
./scripts/benchmark.ps1 -Job Long
```

### 结果解读

| 指标 | 含义 | 关注阈值 |
|------|------|---------|
| Mean | 平均耗时 | 与上次对比，劣化 > 10% 需排查 |
| Allocated | 内存分配 | 热路径应趋近零分配 |
| Gen0/Gen1/Gen2 | GC 回收次数 | Gen2 > 0 需要关注 |
| StdDev | 标准差 | 过大表示结果不稳定 |

## 性能审查维度（6 个）

### 1. 热路径分配

| 检查项 | 说明 |
|--------|------|
| LINQ 在循环内 | `Where/Select/ToList` 在高频调用路径中应改为 `for` 循环 |
| 字符串拼接 | 循环中用 `+` 拼接应改为 `StringBuilder` 或 `string.Create` |
| 装箱 | 值类型传给 `object` 参数导致装箱 |
| 闭包捕获 | Lambda 闭包捕获导致隐式 allocation |
| `params` 数组 | 每次调用 `params` 方法都会分配数组 |

### 2. 异步开销

| 检查项 | 说明 |
|--------|------|
| `ValueTask` | 高频异步方法是否可以用 `ValueTask` 代替 `Task` |
| `ConfigureAwait(false)` | 库代码中是否正确使用 |
| 同步快路径 | 缓存命中等场景是否避免了异步状态机 |
| async void | 禁止使用（除事件处理器） |

### 3. 集合使用

| 检查项 | 说明 |
|--------|------|
| 初始容量 | `List<T>`, `Dictionary<K,V>` 已知大小时应指定 capacity |
| `IReadOnlyList` 返回类型 | 避免不必要的 `.ToList()` 转换 |
| `Span<T>` / `Memory<T>` | 切片操作是否可以避免数组复制 |
| `FrozenDictionary` | 只读字典是否可用 .NET 8+ 的 Frozen 集合 |

### 4. 序列化

| 检查项 | 说明 |
|--------|------|
| `JsonSerializerOptions` 缓存 | 是否复用 Options 实例（构造开销大） |
| Source Generator | 高频序列化是否使用 `JsonSerializerContext` |
| 不必要的序列化 | 是否在链路中重复序列化/反序列化同一对象 |

### 5. 缓存

| 检查项 | 说明 |
|--------|------|
| LLM 响应缓存 | 相同 prompt 是否命中语义缓存 |
| Tool Schema 缓存 | 工具注册表是否缓存了 JSON Schema |
| 反射缓存 | `PropertyInfo` / `MethodInfo` 是否缓存 |

### 6. 并发

| 检查项 | 说明 |
|--------|------|
| 锁竞争 | `lock` 是否可以换成 `ReaderWriterLockSlim` |
| Channel vs Queue | 生产者-消费者是否使用 `Channel<T>` |
| 连接池 | HTTP / Redis 连接是否复用 |

## 新增 Benchmark 模板

```csharp
using BenchmarkDotNet.Attributes;

namespace Dawning.Agents.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(BenchmarkDotNet.Jobs.RuntimeMoniker.Net100)]
public class NewFeatureBenchmarks
{
    [GlobalSetup]
    public void Setup()
    {
        // 初始化
    }

    [Benchmark(Baseline = true)]
    public void CurrentImplementation()
    {
        // 当前实现
    }

    [Benchmark]
    public void OptimizedImplementation()
    {
        // 优化后的实现
    }
}
```

## 优化工作流

1. 先跑 benchmark 建立 baseline
2. 使用 `dotnet-counters` 或 `dotnet-trace` 定位瓶颈
3. 实施优化
4. 重跑 benchmark 对比
5. 确认测试仍全部通过

## 验收场景

- **输入**："ToolRegistry 查找太慢，帮我分析一下"
- **预期**：agent 运行 ToolRegistryBenchmarks，检查查找方法的热路径分配，提出优化建议
- **上次验证**：2026-02-27 ✅
