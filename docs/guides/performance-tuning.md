# 性能调优指南

本指南介绍如何优化 Dawning.Agents 在生产环境中的性能表现。

## 目录

- [Token 优化](#token-优化)
- [Memory 策略选择](#memory-策略选择)
- [并发控制](#并发控制)
- [批处理优化](#批处理优化)
- [缓存策略](#缓存策略)
- [向量存储优化](#向量存储优化)
- [监控与诊断](#监控与诊断)

---

## Token 优化

### 1. 选择合适的 Memory 类型

| Memory 类型 | Token 消耗 | 适用场景 |
|------------|-----------|----------|
| `BufferMemory` | 高（无限增长） | 短对话、测试 |
| `WindowMemory` | 中（固定窗口） | 简单任务 |
| `SummaryMemory` | 低（压缩历史） | 长对话 |
| `AdaptiveMemory` | 自动优化 | **推荐生产环境** |
| `VectorMemory` | 最低（按需检索） | 超长程任务 |

```csharp
// 生产环境推荐配置
services.AddAdaptiveMemory(
    downgradeThreshold: 4000,   // 4K tokens 时自动降级
    maxRecentMessages: 6,
    summaryThreshold: 10
);
```

### 2. 限制工具输出

```csharp
[FunctionTool("搜索文档")]
public string SearchDocuments(string query, int maxResults = 5)
{
    var results = _search.Search(query, maxResults);
    
    // 限制返回内容长度
    return string.Join("\n", results.Select(r => 
        r.Content.Length > 500 ? r.Content[..500] + "..." : r.Content
    ));
}
```

### 3. System Prompt 精简

```csharp
// ❌ 避免：冗长的系统提示
var agent = new ReActAgent(
    instructions: "你是一个智能助手。你需要帮助用户完成各种任务...(省略 2000 字)"
);

// ✅ 推荐：简洁的系统提示
var agent = new ReActAgent(
    instructions: """
    你是客服助手。规则：
    1. 先查询订单状态
    2. 无法解决时转人工
    输出格式：简洁回答，不超过 100 字。
    """
);
```

---

## Memory 策略选择

### 决策树

```
任务类型判断
    │
    ├─ 单轮问答 → BufferMemory
    │
    ├─ 多轮对话（<10轮）→ WindowMemory
    │
    ├─ 长对话（10-50轮）→ SummaryMemory 或 AdaptiveMemory
    │
    └─ 超长程任务（>50轮）→ VectorMemory
```

### VectorMemory 配置建议

```json
{
  "Memory": {
    "Type": "Vector",
    "MaxRecentMessages": 6,
    "RetrieveTopK": 5,
    "MinRelevanceScore": 0.6
  }
}
```

| 参数 | 建议值 | 说明 |
|------|--------|------|
| `MaxRecentMessages` | 4-8 | 越小 token 越少，但可能丢失近期上下文 |
| `RetrieveTopK` | 3-7 | 越大上下文越丰富，但 token 越多 |
| `MinRelevanceScore` | 0.5-0.7 | 越高越精准，但可能漏掉相关内容 |

---

## 并发控制

### 1. Rate Limiter 配置

```csharp
services.AddRateLimiter(options =>
{
    options.MaxRequestsPerMinute = 60;
    options.MaxTokensPerMinute = 100000;
    options.MaxConcurrentRequests = 10;
});
```

### 2. 多 Agent 并发

```csharp
// 使用 SemaphoreSlim 控制并发
private readonly SemaphoreSlim _concurrencyLimiter = new(5);

public async Task<List<AgentResponse>> RunAgentsInParallelAsync(
    IEnumerable<(IAgent Agent, string Input)> tasks)
{
    var results = await Task.WhenAll(tasks.Select(async t =>
    {
        await _concurrencyLimiter.WaitAsync();
        try
        {
            return await t.Agent.RunAsync(t.Input);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }));
    
    return results.ToList();
}
```

### 3. 超时配置

```csharp
var options = new AgentOptions
{
    MaxIterations = 10,           // 最大思考轮数
    TimeoutSeconds = 120,         // 总超时
    ToolTimeoutSeconds = 30,      // 单工具超时
};
```

---

## 批处理优化

### 1. Embedding 批处理

```csharp
// ❌ 逐条处理
foreach (var text in texts)
{
    var embedding = await provider.EmbedAsync(text);
}

// ✅ 批处理
var embeddings = await provider.EmbedBatchAsync(texts);
```

### 2. 向量存储批量写入

```csharp
// ❌ 逐条添加
foreach (var chunk in chunks)
{
    await vectorStore.AddAsync(chunk);
}

// ✅ 批量添加
await vectorStore.AddBatchAsync(chunks);
```

---

## 缓存策略

### 1. 工具结果缓存

```csharp
public class CachedWeatherTool : ITool
{
    private readonly IMemoryCache _cache;
    
    public async Task<ToolResult> ExecuteAsync(string input, CancellationToken ct)
    {
        var cacheKey = $"weather:{input}";
        
        if (_cache.TryGetValue(cacheKey, out string? cached))
        {
            return ToolResult.Success(cached);
        }
        
        var result = await GetWeatherAsync(input);
        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(30));
        
        return ToolResult.Success(result);
    }
}
```

### 2. Embedding 缓存

```csharp
public class CachedEmbeddingProvider : IEmbeddingProvider
{
    private readonly IEmbeddingProvider _inner;
    private readonly IDistributedCache _cache;
    
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        var hash = ComputeHash(text);
        var cached = await _cache.GetAsync(hash, ct);
        
        if (cached != null)
        {
            return DeserializeEmbedding(cached);
        }
        
        var embedding = await _inner.EmbedAsync(text, ct);
        await _cache.SetAsync(hash, SerializeEmbedding(embedding), ct);
        
        return embedding;
    }
}
```

---

## 向量存储优化

### 1. 索引配置

| 向量存储 | 推荐索引 | 配置 |
|----------|----------|------|
| Qdrant | HNSW | `m=16, ef_construct=100` |
| Pinecone | - | 自动优化 |
| Redis | HNSW | `M=16, EF_CONSTRUCTION=200` |

### 2. 分片策略

```csharp
// 按会话分片，避免跨会话检索
var vectorMemory = new VectorMemory(
    vectorStore,
    embeddingProvider,
    tokenCounter,
    sessionId: $"session-{userId}-{conversationId}"
);
```

### 3. 定期清理

```csharp
// 清理过期数据
await vectorStore.DeleteByFilterAsync(new Dictionary<string, string>
{
    ["createdBefore"] = DateTime.UtcNow.AddDays(-30).ToString("O")
});
```

---

## 监控与诊断

### 1. 关键指标

| 指标 | 目标值 | 告警阈值 |
|------|--------|----------|
| Agent 响应时间 | < 5s | > 15s |
| Token 使用量/请求 | < 2000 | > 5000 |
| 工具调用失败率 | < 1% | > 5% |
| Memory 压缩率 | > 50% | < 20% |

### 2. 日志配置

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Dawning.Agents": "Debug"
      }
    },
    "Enrich": ["WithProperty:TokenCount"]
  }
}
```

### 3. OpenTelemetry 集成

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddSource("Dawning.Agents")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
    )
    .WithMetrics(builder => builder
        .AddMeter("Dawning.Agents")
        .AddAspNetCoreInstrumentation()
    );
```

---

## 性能基准

### 测试环境
- CPU: 8 核
- RAM: 16GB
- LLM: GPT-4o

### 基准数据

| 场景 | Memory 类型 | 平均响应 | Token/请求 |
|------|------------|----------|-----------|
| 单轮问答 | Buffer | 1.2s | 500 |
| 10轮对话 | Window(10) | 2.5s | 1500 |
| 50轮对话 | Summary | 3.5s | 1200 |
| 50轮对话 | Adaptive | 3.2s | 1100 |
| 100轮对话 | Vector | 4.0s | 1000 |

---

## 最佳实践清单

- [ ] 根据任务类型选择合适的 Memory
- [ ] 配置 Rate Limiter 防止超限
- [ ] 工具输出限制长度
- [ ] 高频工具添加缓存
- [ ] Embedding 使用批处理
- [ ] 监控 Token 使用量
- [ ] 定期清理向量存储

---

*最后更新: 2026-02-04*
