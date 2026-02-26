---
description: "Deep code audit for Dawning.Agents: systematically read every line of code, understand logic, and report bugs, security issues, design flaws, and test gaps. Trigger: 深度阅读, 深度审计, deep audit, deep read, code audit, 全面审查, 逐行阅读, codebase review, 代码体检"
---

> **Skill 使用日志**：使用本 skill 后，在 `/memories/session/skill-log.md` 追加一行：`- {时间} deep-audit — {触发原因}`

# Deep Code Audit Skill

## 目的

对 Dawning.Agents 全部源代码进行**逐文件、逐行级别**的深度阅读和理解，输出结构化审计报告。

## 执行流程（必须严格按序）

### Phase 1: 读取所有 src/ 项目

按以下顺序逐项目、逐文件完整阅读（不跳过任何 .cs 文件）：

```
1. Dawning.Agents.Abstractions   — 接口、模型、Options、枚举
2. Dawning.Agents.Core           — Agent 实现、Tools、Memory、Orchestration、Safety、Resilience
3. Dawning.Agents.OpenAI         — OpenAI provider
4. Dawning.Agents.Azure          — Azure OpenAI provider
5. Dawning.Agents.MCP            — MCP 协议客户端/服务端
6. Dawning.Agents.OpenTelemetry  — 可观测性
7. Dawning.Agents.Serilog        — 结构化日志
8. Dawning.Agents.Redis          — Redis 缓存、队列、锁
9. Dawning.Agents.Chroma         — Chroma 向量数据库
10. Dawning.Agents.Pinecone      — Pinecone 向量数据库
11. Dawning.Agents.Qdrant        — Qdrant 向量数据库
12. Dawning.Agents.Weaviate      — Weaviate 向量数据库
```

### Phase 2: 每个文件必须检查的维度

对每个 .cs 文件，逐行审查以下 **12 个维度**：

| # | 维度 | 审查要点 |
|---|------|---------|
| 1 | **安全** | 注入（SQL/GraphQL/Command）、路径穿越、敏感信息泄露、不安全的反序列化 |
| 2 | **资源管理** | IDisposable/IAsyncDisposable 是否正确实现、using 语句、Process/HttpClient/Stream 泄漏 |
| 3 | **线程安全** | 共享可变状态、Lock/ConcurrentDictionary 使用、竞态条件、死锁风险 |
| 4 | **异步正确性** | 是否有 `.Result`/`.Wait()`/`.GetAwaiter().GetResult()` 同步阻塞、ConfigureAwait、CancellationToken 传递 |
| 5 | **空引用** | 可能的 NullReferenceException、null-forgiving `!` 使用是否合理 |
| 6 | **DI 合规** | 是否通过构造函数注入、有无 `new` 运行时服务、生命周期（Singleton/Scoped/Transient）是否正确 |
| 7 | **Options 校验** | 公开 Options 类是否实现 `IValidatableOptions`、`Validate()` 是否覆盖所有必填字段和范围 |
| 8 | **错误处理** | 异常类型是否精确（非裸 catch）、异常信息是否有意义、是否有静默吞异常 |
| 9 | **日志** | 关键路径是否有 `ILogger<T>` 日志、日志级别是否合理、是否有结构化参数 |
| 10 | **命名与规范** | 命名空间是否匹配文件夹、接口 I 前缀、Async 后缀、私有字段 _ 前缀 |
| 11 | **死代码** | 未使用的参数、未引用的方法、注释掉的代码、TODO/FIXME/HACK |
| 12 | **性能** | 不必要的分配、LINQ 在热路径中的使用、字符串拼接 vs StringBuilder |

### Phase 3: 读取测试项目

读取 `tests/Dawning.Agents.Tests/` 全部子目录，检查：

- 哪些 src/ 模块有对应测试、哪些缺失
- 哪些 Options 类有 `Validate()` 测试、哪些缺失
- 是否有注释掉的测试或 `[Fact(Skip = ...)]`

### Phase 4: 输出报告

按以下格式生成**结构化报告**：

```markdown
# 深度代码审计报告 - {日期}

## 概要
- 扫描文件数：N
- 发现总数：N（CRITICAL: N, HIGH: N, MEDIUM: N, LOW: N）
- 测试覆盖率缺口：N 个模块

## CRITICAL（必须立即修复）
| # | 维度 | 文件 | 行 | 描述 | 修复建议 |
|---|------|------|----|------|---------|

## HIGH（尽快修复）
| # | 维度 | 文件 | 行 | 描述 | 修复建议 |
|---|------|------|----|------|---------|

## MEDIUM（下一轮迭代）
| # | 维度 | 文件 | 行 | 描述 | 修复建议 |
|---|------|------|----|------|---------|

## LOW（打磨项）
| # | 维度 | 文件 | 行 | 描述 | 修复建议 |
|---|------|------|----|------|---------|

## 测试覆盖缺口
| 模块 | 现有测试数 | 缺失覆盖 |
|------|-----------|---------|

## 与上次审计对比（如有）
- 新增问题：
- 已修复问题：
- 未变化问题：
```

### Phase 5: 持久化

1. 将报告摘要写入 `/memories/session/audit-report.md`
2. 如果 `/memories/session/` 中存在上一次审计记录，进行差异对比并在报告末尾附上"与上次审计对比"

## 执行原则

- **不跳过任何文件** — 即使文件看起来简单（如只有一个枚举），也要完整读取
- **不猜测** — 所有发现必须基于实际读到的代码，给出具体文件名和行号
- **优先看变更** — 如果知道最近修改了哪些文件（git diff），优先审查这些文件
- **使用子代理** — 推荐使用 Explore 子代理并行读取不同项目，提高效率
- **分批汇报** — 每完成 2-3 个项目后向用户汇报进度，不要等全部读完
