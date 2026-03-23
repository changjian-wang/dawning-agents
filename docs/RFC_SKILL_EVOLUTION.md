# RFC: 技能自演化系统 (Skill Evolution System)

> **Status**: Draft
> **Created**: 2026-03-23
> **Inspired by**: [Memento-Skills: Let Agents Design Agents](https://arxiv.org/abs/2603.18743)
> **Paper Summary**: [docs/readings/Memento-Skills.md](readings/Memento-Skills.md)

---

## 1. 动机 (Motivation)

Dawning.Agents 已具备动态工具创建的基础能力（`EphemeralToolDefinition` + `IToolSession` +
`IToolStore` + `CreateToolTool`），但缺少让工具从"可用"进化到"好用"再到"自主改进"的闭环机制。

Memento-Skills 论文提出的核心洞察：

> LLM 参数 θ 无需更新，所有"学习"通过外部化技能记忆 M 实现
> —— 部署时学习 (Deployment-time Learning) 第三范式

结合 Dawning.Agents 的企业级定位，本 RFC 提出 **5 项渐进式改进**，将框架从"工具使用"升级为
"工具演化"，形成 **Read → Execute → Reflect → Write** 闭环。

---

## 2. 现状分析 (Current State)

### 已有能力 ✅

| 能力 | 对应组件 | 所在模块 |
|------|---------|---------|
| 动态工具创建 | `CreateToolTool` | Core/Tools/Core |
| 工具持久化 | `IToolStore` / `FileToolStore` | Abstractions + Core |
| 会话级工具管理 | `IToolSession` / `ToolSession` | Abstractions + Core |
| 层级提升 | `PromoteToolAsync` (Session→User/Global) | IToolSession |
| 沙箱执行 | `IToolSandbox` / `ToolSandbox` | Abstractions + Core |
| 工具审批 | `IToolApprovalHandler` | Abstractions + Core |
| 工具分类 | `ITool.Category` + `GetToolsByCategory` | IToolRegistry |
| 模型路由 | `IModelRouter` (成本/延迟/负载均衡/故障转移) | Abstractions + Core |
| 评估框架 | `IAgentEvaluator` + `ABTestRunner` | Abstractions + Core |
| 工作流编排 | `IOrchestrator` + `IWorkflow` | Abstractions + Core |

### 关键缺失 ❌

| 缺失能力 | 影响 |
|----------|------|
| **语义技能路由** | 工具多时 token 浪费、选择不精准 |
| **技能效用追踪** | 无法量化工具好坏，无学习数据基础 |
| **反思修复循环** | 工具失败只重试不改进 |
| **行为上下文元数据** | 路由和诊断缺少上下文信号 |
| **跨会话演化策略** | 好用的工具无法自动沉淀，差工具无法淘汰 |

---

## 3. 改进方案 (Proposed Changes)

### 3.1 Phase 1: 技能效用追踪 (Tool Usage Tracking)

**优先级**: P0 — 无依赖，独立实现
**复杂度**: 低

所有后续改进的数据基础。没有效用数据，就没有学习。

#### 3.1.1 接口定义

```
Abstractions/Tools/IToolUsageTracker.cs
```

```csharp
namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// 工具使用追踪器 — 记录和查询工具执行统计
/// </summary>
public interface IToolUsageTracker
{
    /// <summary>
    /// 记录一次工具执行
    /// </summary>
    Task RecordUsageAsync(
        ToolUsageRecord record,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定工具的统计
    /// </summary>
    Task<ToolUsageStats> GetStatsAsync(
        string toolName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有工具的统计
    /// </summary>
    Task<IReadOnlyList<ToolUsageStats>> GetAllStatsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取低效用工具列表（成功率低于阈值）
    /// </summary>
    Task<IReadOnlyList<ToolUsageStats>> GetLowUtilityToolsAsync(
        float successRateThreshold = 0.3f,
        int minCalls = 3,
        CancellationToken cancellationToken = default);
}
```

```
Abstractions/Tools/ToolUsageModels.cs
```

```csharp
namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// 单次工具执行记录
/// </summary>
public record ToolUsageRecord
{
    public required string ToolName { get; init; }
    public bool Success { get; init; }
    public TimeSpan Duration { get; init; }
    public string? ErrorMessage { get; init; }
    public string? TaskContext { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 工具效用统计
/// </summary>
public record ToolUsageStats
{
    public required string ToolName { get; init; }
    public int TotalCalls { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public float SuccessRate => TotalCalls > 0 ? (float)SuccessCount / TotalCalls : 0f;
    public TimeSpan AverageLatency { get; init; }
    public DateTimeOffset LastUsed { get; init; }
    public IReadOnlyList<string> RecentErrors { get; init; } = [];
}
```

#### 3.1.2 实现

```
Core/Tools/InMemoryToolUsageTracker.cs — 内存实现（默认）
```

- 用 `ConcurrentDictionary<string, ToolUsageAccumulator>` 存储
- 保留最近 N 条错误信息用于诊断
- DI: `services.TryAddSingleton<IToolUsageTracker, InMemoryToolUsageTracker>()`

#### 3.1.3 集成点

- `AgentBase.ExecuteStepAsync` 返回后，当 `Action` 非空时自动 record
- 成功：`ToolResult.Success == true`
- 失败：`ToolResult.Success == false`，记录 `Error`

---

### 3.2 Phase 2: 扩展技能元数据 (Enriched Skill Metadata)

**优先级**: P0 — 无依赖，独立实现
**复杂度**: 低

让每个技能携带"行为上下文"，为路由和诊断提供信号。

#### 3.2.1 扩展 `EphemeralToolMetadata`

```csharp
public class EphemeralToolMetadata
{
    // ── 已有字段 ──
    public string? Author { get; set; }
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
    public IList<string> Tags { get; set; } = new List<string>();

    // ── 新增：行为上下文 ──

    /// <summary>
    /// 版本号（每次修复 +1）
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 适用场景描述（供路由器理解何时使用）
    /// </summary>
    public string? WhenToUse { get; set; }

    /// <summary>
    /// 已知限制
    /// </summary>
    public string? Limitations { get; set; }

    /// <summary>
    /// 历史失败模式（供反思引擎参考）
    /// </summary>
    public IList<string> FailurePatterns { get; set; } = new List<string>();

    /// <summary>
    /// 相关技能名称（供路由器做关联推荐）
    /// </summary>
    public IList<string> RelatedSkills { get; set; } = new List<string>();

    /// <summary>
    /// 修订次数
    /// </summary>
    public int RevisionCount { get; set; }

    /// <summary>
    /// 最后修订时间
    /// </summary>
    public DateTimeOffset? LastRevisedAt { get; set; }
}
```

#### 3.2.2 向后兼容

- 新增字段全部可选，默认值安全
- `FileToolStore` 的 JSON 序列化自动忽略 null（`JsonIgnoreCondition.WhenWritingNull`）
- 旧文件反序列化时新字段取默认值

---

### 3.3 Phase 3: 语义技能路由器 (Semantic Skill Router)

**优先级**: P1 — 依赖 IEmbeddingProvider + IVectorStore（已有）
**复杂度**: 中

工具多于 10 个时，全量注入 prompt 既浪费 token 又降低选择精度。
语义路由器根据任务描述检索 top-K 最相关工具。

#### 3.3.1 接口定义

```
Abstractions/Tools/ISkillRouter.cs
```

```csharp
namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// 语义技能路由器 — 根据任务描述检索最相关的工具
/// </summary>
/// <remarks>
/// <para>使用语义嵌入在工具描述上建立向量索引</para>
/// <para>当工具数量超过阈值时，替代全量注入 prompt</para>
/// </remarks>
public interface ISkillRouter
{
    /// <summary>
    /// 根据任务描述语义匹配最相关的工具
    /// </summary>
    /// <param name="taskDescription">用户任务描述</param>
    /// <param name="topK">返回的最大工具数</param>
    /// <param name="minScore">最小相似度阈值 (0-1)</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>带分数的工具列表，按相关性降序</returns>
    Task<IReadOnlyList<ScoredTool>> RouteAsync(
        string taskDescription,
        int topK = 5,
        float minScore = 0.3f,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 重建工具索引（工具注册/删除后调用）
    /// </summary>
    Task RebuildIndexAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 带语义匹配分数的工具
/// </summary>
/// <param name="Tool">匹配的工具</param>
/// <param name="Score">语义相似度分数 (0-1)</param>
public record ScoredTool(ITool Tool, float Score);
```

#### 3.3.2 实现

```
Core/Tools/SemanticSkillRouter.cs
```

- 复用 `IEmbeddingProvider` 生成工具描述的向量
- 复用 `IVectorStore` 存储和检索
- 工具文本 = `$"{tool.Name}: {tool.Description}\n{tool.ParametersSchema}"`
- 若 `EphemeralToolMetadata.WhenToUse` 非空，追加到检索文本中
- 配合 `IToolUsageTracker` 的效用分数加权（可选）

#### 3.3.3 配置

```csharp
public class SkillRouterOptions : IValidatableOptions
{
    public const string SectionName = "SkillRouter";

    /// <summary>
    /// 启用语义路由的工具数量阈值（低于此值仍全量注入）
    /// </summary>
    public int ActivationThreshold { get; set; } = 10;

    /// <summary>
    /// 默认 top-K
    /// </summary>
    public int DefaultTopK { get; set; } = 5;

    /// <summary>
    /// 默认最小相似度
    /// </summary>
    public float DefaultMinScore { get; set; } = 0.3f;
}
```

#### 3.3.4 集成点

- `ReActAgent.BuildPrompt()` 中：
  - 若 `ISkillRouter` 已注册且工具数 ≥ 阈值 → 用路由器检索 top-K
  - 否则退化为 `GetAllTools()`（向后兼容）
- DI: `services.AddSkillRouter()` 可选注册

---

### 3.4 Phase 4: 反思修复引擎 (Reflection Engine)

**优先级**: P2 — 依赖 Phase 1 (效用追踪) + ILLMProvider
**复杂度**: 高 — 核心创新

这是从"使用工具"升级为"改进工具"的关键能力。

#### 3.4.1 接口定义

```
Abstractions/Agent/IReflectionEngine.cs
```

```csharp
namespace Dawning.Agents.Abstractions.Agent;

/// <summary>
/// 反思引擎 — 工具执行失败后的诊断与修复决策
/// </summary>
/// <remarks>
/// <para>灵感来源: Memento-Skills Read-Write Reflective Learning</para>
/// <para>失败不仅是重试信号，而是训练信号</para>
/// </remarks>
public interface IReflectionEngine
{
    /// <summary>
    /// 对失败的工具执行进行反思，产生修复策略
    /// </summary>
    Task<ReflectionResult> ReflectAsync(
        ReflectionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 反思上下文
/// </summary>
public record ReflectionContext
{
    /// <summary>
    /// 失败的工具
    /// </summary>
    public required ITool FailedTool { get; init; }

    /// <summary>
    /// 工具的输入参数
    /// </summary>
    public required string Input { get; init; }

    /// <summary>
    /// 失败的执行结果
    /// </summary>
    public required ToolResult FailedResult { get; init; }

    /// <summary>
    /// 原始任务描述
    /// </summary>
    public required string TaskDescription { get; init; }

    /// <summary>
    /// 之前的执行步骤
    /// </summary>
    public IReadOnlyList<AgentStep>? PreviousSteps { get; init; }

    /// <summary>
    /// 工具的效用统计
    /// </summary>
    public ToolUsageStats? UsageStats { get; init; }
}

/// <summary>
/// 反思结果
/// </summary>
public record ReflectionResult
{
    /// <summary>
    /// 建议的修复策略
    /// </summary>
    public required ReflectionAction Action { get; init; }

    /// <summary>
    /// 修订后的工具定义（当 Action = ReviseAndRetry 时）
    /// </summary>
    public EphemeralToolDefinition? RevisedDefinition { get; init; }

    /// <summary>
    /// 诊断报告
    /// </summary>
    public string? Diagnosis { get; init; }

    /// <summary>
    /// 置信度 (0-1)
    /// </summary>
    public float Confidence { get; init; }
}

/// <summary>
/// 反思修复策略
/// </summary>
public enum ReflectionAction
{
    /// <summary>
    /// 简单重试（临时性错误，如网络超时）
    /// </summary>
    Retry,

    /// <summary>
    /// 修改工具定义后重试
    /// </summary>
    ReviseAndRetry,

    /// <summary>
    /// 放弃该工具，选择其他工具
    /// </summary>
    Abandon,

    /// <summary>
    /// 创建全新工具
    /// </summary>
    CreateNew,

    /// <summary>
    /// 升级给人类处理
    /// </summary>
    Escalate
}
```

#### 3.4.2 实现

```
Core/Agent/LLMReflectionEngine.cs
```

- 构造 reflection prompt：传入失败工具的定义、错误信息、任务上下文、历史失败模式
- 调用 LLM 产生结构化诊断 JSON
- 解析为 `ReflectionResult`
- 若 `Action == ReviseAndRetry`：LLM 同时输出修订后的工具定义

#### 3.4.3 配置

```csharp
public class ReflectionOptions : IValidatableOptions
{
    public const string SectionName = "Reflection";

    /// <summary>
    /// 是否启用反思引擎
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 同一工具连续失败多少次后触发反思
    /// </summary>
    public int FailureThreshold { get; set; } = 2;

    /// <summary>
    /// 最大反思次数（防止无限修复循环）
    /// </summary>
    public int MaxReflections { get; set; } = 3;

    /// <summary>
    /// 反思使用的模型（可使用较便宜的模型）
    /// </summary>
    public string? ModelOverride { get; set; }
}
```

#### 3.4.4 集成点

AgentBase 执行循环中的钩子：

```
工具执行 → 失败 → 检查 IReflectionEngine 是否注册
        → 已注册且达到阈值 → ReflectAsync()
        → Action == ReviseAndRetry → 更新工具定义 → 重新执行
        → Action == Abandon → 标记该工具，选择其他工具
        → Action == Escalate → 通过 IHumanInteractionHandler 升级
```

---

### 3.5 Phase 5: 跨会话技能演化策略 (Skill Evolution Policy)

**优先级**: P3 — 依赖 Phase 1 + Phase 4
**复杂度**: 中

让高效用工具自动沉淀，低效用工具自动淘汰，技能库持续进化。

#### 3.5.1 接口定义

```
Abstractions/Tools/ISkillEvolutionPolicy.cs
```

```csharp
namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// 技能演化策略 — 决定技能的提升、淘汰和归档
/// </summary>
public interface ISkillEvolutionPolicy
{
    /// <summary>
    /// 评估 session 工具是否应提升到持久化层级
    /// </summary>
    Task<PromotionDecision> EvaluatePromotionAsync(
        string toolName,
        ToolUsageStats stats,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 评估工具是否应淘汰
    /// </summary>
    Task<bool> ShouldRetireAsync(
        string toolName,
        ToolUsageStats stats,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 提升决策
/// </summary>
public record PromotionDecision(
    bool ShouldPromote,
    ToolScope TargetScope,
    string Reason);
```

#### 3.5.2 默认策略

```
Core/Tools/DefaultSkillEvolutionPolicy.cs
```

| 条件 | 决策 |
|------|------|
| 成功率 ≥ 80% 且调用 ≥ 3 次 | 提升到 User scope |
| 成功率 ≥ 90% 且调用 ≥ 10 次 | 提升到 Global scope |
| 成功率 < 20% 且调用 ≥ 5 次 | 淘汰（标记 retired） |
| 30 天内未使用 | 不主动淘汰，但降低路由权重 |

#### 3.5.3 触发点

- `IToolSession.DisposeAsync()` 时：评估所有 session 工具，符合条件者自动提升
- 周期性后台任务（可选）：扫描持久化工具，淘汰低效用工具

---

## 4. 架构总览

```
┌─────────────────────────────────────────────────────────┐
│                     Agent 执行循环                        │
│  ┌──────────┐   ┌──────────┐   ┌──────────────────────┐ │
│  │  Read     │──▶│ Execute  │──▶│   Reflect (on fail)  │ │
│  │  Router   │   │ Sandbox  │   │   LLMReflectionEngine│ │
│  └──────────┘   └──────────┘   └──────────┬───────────┘ │
│       ▲                                     │             │
│       │              ┌──────────┐           │  Write      │
│       │              │  Usage   │◀──────────┘             │
│       └──────────────│ Tracker  │                         │
│                      └──────────┘                         │
│                           │                               │
│                           ▼                               │
│                   ┌──────────────┐                        │
│                   │  Evolution   │                        │
│                   │  Policy      │                        │
│                   └──────┬───────┘                        │
│                          │                                │
│              ┌───────────┼───────────┐                    │
│              ▼           ▼           ▼                    │
│         [Session]    [User]     [Global]                  │
│         (memory)   (~/.dawning)  (project)                │
└─────────────────────────────────────────────────────────┘
```

数据流：
1. **Read** — `ISkillRouter` 语义检索 top-K 工具 → 注入 prompt
2. **Execute** — `IToolSandbox` 执行 → `IToolUsageTracker` 记录结果
3. **Reflect** — 失败时 `IReflectionEngine` 诊断 → 修复/放弃/升级
4. **Write** — 修复后的工具定义写回 `IToolSession` / `IToolStore`
5. **Evolve** — 会话结束时 `ISkillEvolutionPolicy` 评估提升/淘汰

---

## 5. 实施路线图

```
Phase 1 (P0)          Phase 2 (P0)         Phase 3 (P1)
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│ Tool Usage   │    │ Enriched     │    │ Semantic     │
│ Tracker      │    │ Metadata     │    │ Skill Router │
│              │    │              │    │              │
│ Interface +  │    │ Extend       │    │ Interface +  │
│ InMemory impl│    │ Ephemeral    │    │ Vector-based │
│              │    │ ToolMetadata │    │ impl         │
└──────┬───────┘    └──────────────┘    └──────┬───────┘
       │                                        │
       │         Phase 4 (P2)                   │
       │    ┌──────────────┐                    │
       └───▶│ Reflection   │◀───────────────────┘
            │ Engine       │
            │              │
            │ Interface +  │
            │ LLM-based    │
            │ impl         │
            └──────┬───────┘
                   │
            Phase 5 (P3)
            ┌──────────────┐
            │ Evolution    │
            │ Policy       │
            │              │
            │ Auto promote │
            │ Auto retire  │
            └──────────────┘
```

### 每阶段交付物

| Phase | 新增接口 | 新增实现 | 新增测试 | 新增 DI 扩展 |
|-------|---------|---------|---------|-------------|
| 1 | `IToolUsageTracker`, `ToolUsageRecord`, `ToolUsageStats` | `InMemoryToolUsageTracker` | ~15 tests | `AddToolUsageTracking()` |
| 2 | 无（扩展现有类） | 无（扩展现有类） | ~8 tests | 无 |
| 3 | `ISkillRouter`, `ScoredTool`, `SkillRouterOptions` | `SemanticSkillRouter` | ~12 tests | `AddSkillRouter()` |
| 4 | `IReflectionEngine`, `ReflectionContext`, `ReflectionResult`, `ReflectionAction`, `ReflectionOptions` | `LLMReflectionEngine` | ~15 tests | `AddReflectionEngine()` |
| 5 | `ISkillEvolutionPolicy`, `PromotionDecision` | `DefaultSkillEvolutionPolicy` | ~10 tests | `AddSkillEvolution()` |

### 总计

- 新增接口: 4 个
- 新增实现: 4 个
- 新增测试: ~60 个
- 扩展现有类: 1 个 (`EphemeralToolMetadata`)
- 现有 API 保持向后兼容

---

## 6. 设计约束 (Design Constraints)

遵循 Dawning.Agents 六大核心原则：

| 原则 | 如何遵循 |
|------|---------|
| **极简 API** | 每个 Phase 只增加 1 个核心接口 + 1 行 DI 注册 |
| **纯 DI** | 所有新组件通过构造函数注入，无静态工厂 |
| **企业基础设施** | 全部支持 `ILogger<T>`, `IOptions<T>`, `CancellationToken` |
| **破坏性修改优先** | pre-release 阶段直接扩展，无 `[Obsolete]` |
| **接口与实现分离** | 接口在 Abstractions（零依赖），实现在 Core |
| **配置驱动** | `SkillRouterOptions`, `ReflectionOptions` 均通过 appsettings.json 配置 |

---

## 7. 不做的事情 (Non-Goals)

| 不做 | 理由 |
|------|------|
| Markdown 作为技能存储格式 | 已有 JSON-based `EphemeralToolDefinition`，无需迁移 |
| 技能商店 / 云目录 | 前期先做好本地演化闭环 |
| GUI / CLI 部署表面 | 不在本 RFC 范围，属于独立工作流 |
| 绑定特定搜索 API | Web search 保持 provider 无关性 |
| Python 互操作 | .NET 生态内闭环 |

---

## 8. 风险与缓解

| 风险 | 影响 | 缓解 |
|------|------|------|
| 反思循环无限修复 | Token 浪费 + 死循环 | `MaxReflections` 硬限制 + 成本预算 |
| 语义路由准确度不足 | 选错工具 | 低于阈值退化为全量注入（渐进式启用） |
| 工具自动淘汰误杀 | 删除有用工具 | 淘汰=标记 retired 而非物理删除 |
| 元数据扩展破坏序列化 | 旧文件无法读取 | 新字段全部可选，null 安全 |

---

## 9. 参考文献

- [Memento-Skills: Let Agents Design Agents](https://arxiv.org/abs/2603.18743) (Zhou et al., 2026)
- [ReAct: Synergizing Reasoning and Acting in Language Models](https://arxiv.org/abs/2210.03629) (Yao et al., 2022)
- [Voyager: An Open-Ended Embodied Agent with Large Language Models](https://arxiv.org/abs/2305.16291) (Wang et al., 2023)
