---
description: "Deep code audit for Dawning.Agents: systematically read every line of code, understand logic, and report bugs, security issues, design flaws, and test gaps. Trigger: 深度阅读, 深度审计, deep audit, deep read, code audit, 全面审查, 逐行阅读, codebase review, 代码体检"
---

# Deep Code Audit Skill

## 目标

对 Dawning.Agents 全部源代码进行逐文件、逐行级别的深度阅读和理解，输出结构化审计报告。

## 触发条件

- **关键词**：深度阅读, 深度审计, deep audit, deep read, code audit, 全面审查, 逐行阅读, codebase review, 代码体检
- **文件模式**：`src/**/*.cs`, `tests/**/*.cs`
- **用户意图**：全面代码审计、安全审查、质量体检

## 编排

- **前置**：无
- **后续**：`code-update` → `build-project` → `run-tests` → `csharpier` → `git-workflow`

## Skill 使用日志

使用本 skill 后，在 `/memories/repo/skill-usage.md` 追加一行：`- {日期} deep-audit — {触发原因}`

---

## 执行流程（必须严格按序）

### Phase 1: 读取所有 src/ 项目

按以下顺序逐项目、逐文件完整阅读（不跳过任何 .cs 文件）：

```
1. Dawning.Agents.Abstractions
2. Dawning.Agents.Core
3. Dawning.Agents.OpenAI
4. Dawning.Agents.Azure
5. Dawning.Agents.MCP
6. Dawning.Agents.OpenTelemetry
7. Dawning.Agents.Serilog
8. Dawning.Agents.Redis
9. Dawning.Agents.Chroma
10. Dawning.Agents.Pinecone
11. Dawning.Agents.Qdrant
12. Dawning.Agents.Weaviate
```

### Phase 2: 每个文件必须检查的维度（12 个）

| # | 维度 | 审查要点 |
|---|------|---------|
| 1 | **安全** | 注入、路径穿越、敏感信息泄露、不安全的反序列化 |
| 2 | **资源管理** | IDisposable/IAsyncDisposable、using 语句、Stream/HttpClient 泄漏 |
| 3 | **线程安全** | 共享可变状态、Lock/ConcurrentDictionary、竞态条件、死锁 |
| 4 | **异步正确性** | `.Result`/`.Wait()` 同步阻塞、CancellationToken 传递 |
| 5 | **空引用** | NullReferenceException、null-forgiving `!` 使用 |
| 6 | **DI 合规** | 构造函数注入、无 `new` 运行时服务、生命周期正确性 |
| 7 | **Options 校验** | `IValidatableOptions`、`Validate()` 覆盖率 |
| 8 | **错误处理** | 精确异常类型、无裸 catch、无静默吞异常 |
| 9 | **日志** | `ILogger<T>` 覆盖、日志级别合理性、结构化参数 |
| 10 | **命名与规范** | 命名空间、接口前缀、Async 后缀、私有字段 `_` 前缀 |
| 11 | **死代码** | 未使用参数、未引用方法、TODO/FIXME/HACK |
| 12 | **性能** | 不必要分配、LINQ 热路径、字符串拼接 |

### Phase 3: 读取测试项目

检查：哪些模块有测试、哪些缺失、是否有 `[Fact(Skip = ...)]`

### Phase 4: 输出报告

```markdown
# 深度代码审计报告 - {日期}

## 概要
- 扫描文件数：N（CRITICAL: N, HIGH: N, MEDIUM: N, LOW: N）

## CRITICAL / HIGH / MEDIUM / LOW
| # | 维度 | 文件 | 行 | 描述 | 修复建议 |

## 测试覆盖缺口
| 模块 | 现有测试数 | 缺失覆盖 |

## 与上次审计对比
```

### Phase 5: 持久化

将报告摘要写入 `/memories/session/audit-report.md`

## 执行原则

- **不跳过任何文件** — 即使只有一个枚举
- **不猜测** — 所有发现基于实际代码，给出文件名和行号
- **使用子代理** — 推荐 Explore 子代理并行读取不同项目
- **分批汇报** — 每 2-3 个项目后汇报进度

## 验收场景

- **输入**："对整个项目做一次深度代码审计"
- **预期**：agent 逐项目读取所有 .cs 文件，按 12 维度检查，输出结构化报告
- **上次验证**：2026-02-27
