---
description: |
  Use when: Performing deep code audit to find bugs, security issues, design flaws, and test gaps across all 12 projects
  Don't use when:
    - Quick code review of a few files (use code-review)
    - Security-only audit without full scope (use security-audit)
    - Fixing issues without auditing first (use code-update)
    - Running tests (use run-tests)
    - Checking a single performance concern (use performance)
  Inputs: Request for codebase audit, optionally with focus area or scanning angle
  Outputs: Structured audit report with findings by severity, then fix → build → test → format verification
  Success criteria: Novel bugs found, all fixes verified (build 0/0, tests pass, CSharpier clean)
---

# Deep Code Audit Skill

## 概述

本 Skill 用于对 Dawning.Agents 代码库进行深度审计。支持两种模式：

- **全量审计**（首轮）：逐项目逐文件阅读所有 .cs 文件
- **增量审计**（后续轮次）：基于新颖扫描角度的定向扫描

> **经验数据**：经过 48+ 轮审计共修复 ~210 个 bug，简单 bug 在前 10 轮已耗尽。后续轮次必须使用创新的扫描角度才能发现新问题。

---

## Phase 0: 前置检查

1. **验证用户提交**：使用 `get_changed_files` 确认上一轮修改已提交
2. **读取已知问题清单**：从 conversation summary 中加载 deferred/accepted issues 列表
3. **选择扫描模式**：首轮用全量模式，后续用增量模式

---

## Phase 1A: 全量审计模式（首轮）

按以下顺序逐项目、逐文件完整阅读（不跳过任何 .cs 文件）：

```
1. Dawning.Agents.Abstractions    7. Dawning.Agents.Serilog
2. Dawning.Agents.Core            8. Dawning.Agents.Redis
3. Dawning.Agents.OpenAI          9. Dawning.Agents.Chroma
4. Dawning.Agents.Azure          10. Dawning.Agents.Pinecone
5. Dawning.Agents.MCP            11. Dawning.Agents.Qdrant
6. Dawning.Agents.OpenTelemetry  12. Dawning.Agents.Weaviate
```

## Phase 1B: 增量审计模式（后续轮次）

### 扫描角度策略

每轮选择 **2-3 个全新扫描角度**，用子代理并行执行。角度必须在之前轮次中未使用过。

#### 已验证有效的扫描角度库（按发现率排序）

**高发现率角度（每轮 3-7 个 bug）：**

| 角度 | 描述 | 典型 Bug 示例 |
|------|------|--------------|
| 线程安全与原子性 | 共享状态 torn reads、Interlocked 与 lock 混用、volatile 缺失 | ModelStatistics 128-bit 字段 torn read |
| CancellationToken 传播链 | CT 参数存在但未传递到内部调用 | SearchTool 未传递 CT 给 GrepSearch |
| DI 生命周期不匹配 | Singleton 持有 Scoped、captive dependency、Options.Create() 绕过 | WorkflowEngine Singleton 解析 Scoped IAgent |
| 异步模式正确性 | ConfigureAwait(false)、await foreach、fire-and-forget | 6x await foreach 缺少 ConfigureAwait |
| 资源泄漏与 Dispose | IDisposable 缺失、double-dispose、挂起的 TCS 未取消 | AsyncCallbackHandler 无 IDisposable |
| 参数校验一致性 | 同类中有的方法校验有的不校验、null 穿透多层 | ObservableAgent→AgentLogger null 穿透两层 |

**中发现率角度（每轮 2-4 个 bug）：**

| 角度 | 描述 | 典型 Bug 示例 |
|------|------|--------------|
| 事件与回调模式 | event Invoke 异常传播、Timer 回调无 try/catch | AsyncCallbackHandler 4x Invoke 无保护 |
| 数值溢出与截断 | ulong→int、Convert.ToInt32、Sum 溢出 | QdrantVectorStore ulong→int 截断 |
| 错误处理与异常类型 | ChannelClosedException 未处理、OCE 吞没 | AgentRequestQueue 写入关闭的 Channel |
| 重试与 failover 语义 | off-by-one（fencepost error）、最后一次尝试异常丢失 | DistributedLoadBalancer FailoverRetries=0 问题 |
| 配置与序列化 | YAML/JSON 解析边界、.env 引号语义 | .env 单引号内转义字符处理错误 |
| 防御性编程缺失 | 构造函数依赖校验、集合 null 校验 | OrchestratorBase.AddAgents 集合未校验 |

**低发现率但高价值角度：**

| 角度 | 描述 | 典型 Bug 示例 |
|------|------|--------------|
| Polly/Resilience 交互 | 内部 CT 与外部 CT 绑定、策略覆盖范围 | ResilientLLMProvider 绑定 Polly 内部 CT |
| 装饰器/代理模式 | 包装层信息丢失、元数据未透传 | HumanInLoopAgent metadata 丢失 |
| 进程管理 | Process.Start 未 Kill/Dispose、shell 注入 | ToolSandbox 进程泄漏 |
| 动态工具生命周期 | IToolSession/IToolStore 状态一致性、UpdateTool 版本递增、FileToolStore 路径穿越 | EphemeralTool Runtime 未传递到 Sandbox |
| 多运行时注入 | ScriptRuntime.PowerShell/Python 命令注入、参数转义 | ToolSandbox GetShellForRuntime 注入风险 |
| 并发集合语义 | ConcurrentDictionary TOCTOU、Channel 状态竞态、GetOrAdd+lock 顺序 | InMemorySharedState OnChange TOCTOU |
| 环形缓冲区/滑动窗口 | 模数运算溢出、负索引 | HistogramMetric ring buffer 语义 |

**R39-R48 新增验证角度（已扫描，发现率低但值得复查）：**

| 角度 | 描述 | 典型 Bug 示例 |
|------|------|--------------|
| Regex CultureInvariant | Regex 操作缺少 CultureInvariant 标志 | 6 处 Regex 缺少 RegexOptions.CultureInvariant |
| Task.Run CancellationToken | Task.Run 未传递 CT 导致无法及时取消 | 4 处 Task.Run 缺少 CT 参数 |
| IAsyncDisposable 缺失 | 仅实现 IDisposable 但使用异步资源 | HotReloadableLLMProvider 需要 IAsyncDisposable |
| 数值精度与溢出 | 整数除法截断、Math.Clamp 缺失 | AgentAutoScaler 整数除法截断 |
| 集合操作竞态与 TOCTOU | ConcurrentDictionary GetOrAdd+lock 顺序错误 | InMemorySharedState OnChange 竞态 |

### 子代理 Prompt 模板

```
你是 .NET 代码审计专家。请对 dawning-agents 仓库的所有 src/ 目录下 .cs 文件
进行 [角度名] 专项扫描。

扫描要求：
1. 逐文件读取所有相关代码
2. 仅报告 genuinely new 问题（排除以下已知问题：[列出已知问题]）
3. 每个发现给出：文件路径、行号、代码片段、问题描述、修复建议、严重级别
4. 按严重级别排序输出

排除的已知问题 ID 列表：
[从 conversation summary 粘贴]
```

---

## Phase 2: 检查维度（18 个）

在原有 12 个维度基础上，增加 6 个从实战中提炼的高价值维度：

| # | 维度 | 审查要点 |
|---|------|---------|
| 1 | **安全** | 注入、路径穿越、敏感信息泄露、不安全的反序列化 |
| 2 | **资源管理** | IDisposable/IAsyncDisposable、using、Stream/HttpClient 泄漏 |
| 3 | **线程安全** | 共享可变状态、Lock/ConcurrentDictionary、竞态、死锁 |
| 4 | **异步正确性** | `.Result`/`.Wait()` 阻塞、CT 传递、ConfigureAwait(false) |
| 5 | **空引用** | NRE、null-forgiving `!`、null 穿透多层 |
| 6 | **DI 合规** | 构造函数注入、无 `new` 运行时服务、生命周期正确性 |
| 7 | **Options 校验** | `IValidatableOptions`、`Validate()` 覆盖率 |
| 8 | **错误处理** | 精确异常类型、无裸 catch、无静默吞异常 |
| 9 | **日志** | `ILogger<T>` 覆盖、日志级别、结构化参数 |
| 10 | **命名与规范** | 命名空间、接口前缀、Async 后缀、`_` 前缀 |
| 11 | **死代码** | 未使用参数、未引用方法、TODO/FIXME/HACK |
| 12 | **性能** | 不必要分配、LINQ 热路径、字符串拼接 |
| 13 | **原子性与 torn reads** | 多字段一致性读取、128-bit 字段需 lock、Interlocked 正确性 |
| 14 | **事件/Timer/回调安全** | event Invoke 异常隔离、Timer 回调 try/catch、委托泄漏 |
| 15 | **DI 生命周期** | Singleton 持有 Scoped（captive dependency）、Options.Create() 绕过 |
| 16 | **数值边界** | 整数溢出、ulong→int 截断、Convert.ToXxx 溢出、模数负数 |
| 17 | **Channel/Queue 生命周期** | ChannelClosedException、Complete 后写入、TCS 挂起清理 |
| 18 | **Polly/Resilience 交互** | 内部 CT 绑定、策略覆盖范围、stream 失败上报 |

---

## Phase 3: 分类与过滤

### 3.1 对比已知问题清单

每个发现必须检查是否在 conversation summary 的 deferred/accepted issues 列表中。已知问题**直接跳过**。

### 3.2 严重级别定义

| 级别 | 定义 | 修复优先级 |
|------|------|-----------|
| CRITICAL | 数据损坏、安全漏洞、服务崩溃 | 必须立即修复 |
| HIGH | 功能异常、资源泄漏、竞态条件导致错误行为 | 本轮修复 |
| MEDIUM | 防御性校验缺失、错误信息不清、不一致行为 | 本轮修复（除设计层面问题） |
| LOW | 代码风格、微优化、理论上可能但实际极难触发 | 推迟 |

### 3.3 推迟条件（不修复，记入 deferred 列表）

- 需要架构级重构（如接口变更影响所有实现）
- 属于设计选择而非 bug（如 WeightedRoundRobin 实际是加权随机）
- 修复风险大于收益（如改 Singleton 为 Scoped 影响所有消费者）
- 理论极端场景（如 double 精度 >2^53）
- 架构级延迟项已修复：`_disposed` volatile、ValidateOnStart、public API 参数验证、Options 验证增强

---

## Phase 4: 修复

对所有 CRITICAL/HIGH/MEDIUM 级别的非推迟问题执行修复。

### 修复原则

1. **最小改动** — 只修 bug 本身，不做"顺便"重构
2. **保持 API 兼容** — 除非是参数校验（添加 ThrowIfNull 不破坏现有调用）
3. **遵循现有模式** — 看同文件/同类中的已有模式来保持一致
4. **批量修复** — 同类型 bug（如参数校验）用 multi_replace_string_in_file 一次完成

---

## Phase 4.5: 修复回归审查（MANDATORY）

> **背景**：实战经验表明，约 60% 的「下一轮新发现」实际上是上一轮修复引入的回归。
> 典型链条：sync 属性→加 sync-over-async 初始化→改 async 去掉锁→引入竞态。
> 此阶段的目的是在提交前打断这条回归链。

**对每个修改过的文件（`git diff --name-only`），执行以下检查：**

### 4.5.1 重新阅读完整文件

不要只看 diff —— 阅读修改后的完整文件，因为 bug 往往出现在修改与未修改代码的交互处。

### 4.5.2 针对新增/修改代码逐项检查

对照 Phase 2 的 18 个维度，**专注于新写的代码**，特别关注：

| 高频回归模式 | 检查要点 |
|-------------|---------|
| 加锁后忘记 Dispose | 新增 SemaphoreSlim/Lock 是否在 `DisposeAsync` 中释放 |
| 去锁引入竞态 | 删除锁/改 async 后，共享状态是否仍有互斥保护 |
| 多字段原子性 | 新写的 `Volatile.Write` + `_flag = true` 是否在同一个锁内 |
| 新 async 方法的 CT 传递 | 新写的 async 方法内部调用是否全部传递了 `CancellationToken` |
| DI 工厂遗漏依赖 | 新增的构造函数参数是否在 DI 注册工厂中同步更新 |
| 公开方法未设标志 | 如 `EnsureSchemaAsync` 公开调用后，内部标志是否正确更新 |

### 4.5.3 判定标准

- 如果发现回归 → 立即修复，然后对修复后的代码再执行一遍 4.5（递归直到无回归）
- 如果未发现回归 → 进入 Phase 5

---

## Phase 5: 验证链（必须全部通过）

```bash
# 1. 构建（必须 0 warnings, 0 errors）
dotnet build --nologo -v q

# 2. 测试（必须全部通过，当前基线 2562）
dotnet test --nologo -v q

# 3. 格式化
~/.dotnet/tools/csharpier format .
```

---

## Phase 6: 输出报告

```markdown
## R{N} 完成 — 修复汇总

本轮共修复 **N 个 bug**，涉及 M 个文件。

### 行为/功能 Bug（N 个）
| # | 文件 | 问题 | 修复 |

### 参数校验 Bug（N 个）
| # | 文件 | 缺失校验 |

### 推迟问题
- [描述] — 推迟原因
```

---

## 执行原则

1. **不重复报告** — 严格过滤 deferred/accepted issues
2. **不猜测** — 所有发现基于实际代码，给出文件名和行号
3. **创新扫描角度** — 每轮的角度不得与之前轮次重复
4. **使用子代理并行扫描** — 2-3 个角度同时扫描，提高效率
5. **分批汇报** — 扫描完成后先展示分类结果，确认后再修复
6. **单轮闭环** — 每轮必须走完 扫描→分类→修复→**回归审查**→验证 全链路
7. **修复即审查** — 每个修复必须经过 Phase 4.5 回归检查后才能进入验证链，防止「修复引入回归」的连锁反应

