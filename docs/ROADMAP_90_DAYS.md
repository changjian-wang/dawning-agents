# Dawning.Agents — 90 天企业级个人助手实施路线图

> **目标**：从"企业级 Agent 框架"升级为"可持续超越竞品的企业级个人助手平台"
>
> **三大支柱**：技能自演化 · 连接器 · 运营闭环
>
> **起始版本**：v0.1.0-preview（2482 tests, 12 projects, MCP/安全/评估/路由/可观测已就绪）

---

## 总览

```
         Month 1                    Month 2                    Month 3
  ┌─────────────────┐       ┌─────────────────┐       ┌─────────────────┐
  │  技能演化基础     │       │  连接器 + 反思    │       │  运营闭环 + 产品  │
  │                 │       │                 │       │                 │
  │ W1 效用追踪      │       │ W5 邮件连接器     │       │ W9  线上评测集    │
  │ W2 元数据 + 路由  │       │ W6 日历连接器     │       │ W10 SLO 仪表盘   │
  │ W3 Agent 集成    │       │ W7 反思修复引擎   │       │ W11 灰度发布     │
  │ W4 验证 + 文档    │       │ W8 知识库连接器   │       │ W12 演化策略 +   │
  │                 │       │                 │       │     端到端验收   │
  └─────────────────┘       └─────────────────┘       └─────────────────┘
```

---

## Month 1：技能演化基础（数据 + 路由 + 集成）

### Week 1 — 技能效用追踪（Phase 1）

| 交付物 | 说明 |
|--------|------|
| `IToolUsageTracker` 接口 | `Abstractions/Tools/` 新增，`RecordUsageAsync` / `GetStatsAsync` / `GetLowUtilityToolsAsync` |
| `ToolUsageRecord` / `ToolUsageStats` 模型 | `Abstractions/Tools/ToolUsageModels.cs` |
| `InMemoryToolUsageTracker` 实现 | `Core/Tools/`，`ConcurrentDictionary` + Lock 保护，保留最近 50 条错误 |
| `RedisToolUsageTracker` 实现 | `Redis/Tools/`，复用 `IDatabase`，HINCRBY 原子更新 |
| DI 扩展 `AddToolUsageTracking()` | 默认注册 InMemory，有 Redis 时自动升级 |
| 单元测试 ≥ 15 | 覆盖并发记录、统计准确性、低效用过滤 |

**验收指标**：`dotnet test` 绿灯，InMemory 10K 次 record < 50ms (Benchmark)

### Week 2 — 元数据扩展 + 语义路由（Phase 2 + 3）

| 交付物 | 说明 |
|--------|------|
| `EphemeralToolMetadata` 扩展 | 新增 `Version` / `WhenToUse` / `Limitations` / `FailurePatterns` / `RelatedSkills` / `RevisionCount` / `LastRevisedAt` |
| `ISkillRouter` 接口 | `Abstractions/Tools/`，`RouteAsync` / `RebuildIndexAsync` |
| `ScoredTool` / `SkillRouterOptions` | 配置：`ActivationThreshold=10`，`DefaultTopK=5`，`DefaultMinScore=0.3` |
| `SemanticSkillRouter` 实现 | `Core/Tools/`，复用 `IEmbeddingProvider` + `IVectorStore`，工具描述 + WhenToUse 作为索引文本 |
| DI 扩展 `AddSkillRouter()` | 可选注册，无注册时退化为全量注入 |
| 单元测试 ≥ 12 | Mock Embedding + InMemoryVector，验证 top-K 精度、阈值退化 |

**验收指标**：20 个工具场景下路由准确率 ≥ 80%（top-5 命中目标工具）

### Week 3 — Agent 执行循环集成

| 交付物 | 说明 |
|--------|------|
| `FunctionCallingAgent` 集成效用追踪 | `ExecuteToolCallAsync` 返回后自动 `RecordUsageAsync` |
| `FunctionCallingAgent` 集成语义路由 | `BuildToolDefinitions()` 中：工具数 ≥ 阈值时走 `ISkillRouter.RouteAsync` |
| `ReActAgent` 同步集成 | 同上两项 |
| 集成测试 ≥ 8 | 端到端：Agent 使用路由选工具 → 执行 → 效用记录完整 |

**验收指标**：Session 中创建 15 个工具后，Agent 仍能在 top-5 中选中正确工具

### Week 4 — Month 1 验证 + 文档

| 交付物 | 说明 |
|--------|------|
| 全量 `dotnet test` + Benchmark | 确认无回归，效用追踪性能达标 |
| CSharpier 格式化 | 全仓库干净 |
| `docs/guides/skill-tracking.md` | 用户指南：如何启用效用追踪和语义路由 |
| `CHANGELOG.md` 更新 | 记录 Phase 1-3 变更 |
| Git tag `v0.2.0-preview` | 里程碑发布 |

---

## Month 2：连接器 + 反思修复引擎

### Week 5 — 连接器抽象层 + 邮件连接器

| 交付物 | 说明 |
|--------|------|
| `IConnector` 接口 | `Abstractions/Connectors/`，统一生命周期：`ConnectAsync` / `DisconnectAsync` / `IsConnected` |
| `IConnectorAuthProvider` | OAuth2/API Key 统一认证抽象 |
| 连接器注册 `AddConnector<T>()` | 泛型注册 + 健康检查集成 |
| `EmailConnector` 接口 | `IEmailConnector`：`ListMailsAsync` / `ReadMailAsync` / `SendMailAsync` / `SearchMailAsync` |
| `MicrosoftGraphEmailConnector` | 基于 Microsoft Graph API（`IHttpClientFactory`），支持 OAuth2 delegated/app-only |
| 邮件相关工具 | `ReadEmailTool` / `SearchEmailTool` / `DraftReplyTool`，自动注册到 `IToolRegistrar` |
| 单元测试 ≥ 15 | Mock Graph API，验证认证流、分页、错误处理 |

**验收指标**：Agent 可执行"帮我查昨天未读邮件并起草回复"端到端流程（Mock 环境）

### Week 6 — 日历连接器

| 交付物 | 说明 |
|--------|------|
| `ICalendarConnector` 接口 | `GetEventsAsync` / `CreateEventAsync` / `FindFreeSlotAsync` / `UpdateEventAsync` |
| `MicrosoftGraphCalendarConnector` | 复用 Week 5 的 Graph auth 基础设施 |
| 日历工具 | `CheckCalendarTool` / `ScheduleMeetingTool` / `FindFreeTimeTool` |
| 冲突检测逻辑 | 创建会议前自动检查时间冲突，提示用户确认 |
| 单元测试 ≥ 10 | 覆盖时区处理、冲突检测、分页 |

**验收指标**：Agent 可执行"帮我约下周三下午和张三的 30 分钟会议"（Mock 环境）

### Week 7 — 反思修复引擎（Phase 4）

| 交付物 | 说明 |
|--------|------|
| `IReflectionEngine` 接口 | `Abstractions/Agent/`，`ReflectAsync(ReflectionContext)` |
| `ReflectionContext` / `ReflectionResult` / `ReflectionAction` 模型 | 含 5 种策略：Retry / ReviseAndRetry / Abandon / CreateNew / Escalate |
| `ReflectionOptions` | `FailureThreshold=2`，`MaxReflections=3`，`ModelOverride` 可选 |
| `LLMReflectionEngine` 实现 | `Core/Agent/`，构造 reflection prompt → LLM 结构化诊断 → 解析决策 |
| Agent 循环集成 | 工具失败达阈值 → 触发反思 → ReviseAndRetry 时 `IToolSession.UpdateTool` |
| 单元测试 ≥ 15 | Mock LLM 输出各种策略，验证状态转换、最大反思次数限制 |

**验收指标**：连续失败 2 次后自动触发反思；ReviseAndRetry 后工具版本号 +1

### Week 8 — 知识库连接器

| 交付物 | 说明 |
|--------|------|
| `IKnowledgeBaseConnector` 接口 | `SearchAsync` / `GetDocumentAsync` / `ListCollectionsAsync` |
| `NotionConnector` 实现 | Notion API v1，支持数据库查询和页面读取 |
| `ConfluenceConnector` 实现 | Confluence REST API，支持 CQL 搜索 |
| 知识库工具 | `SearchKnowledgeBaseTool` / `ReadDocumentTool` |
| RAG 自动集成 | 连接器拉取文档 → `DocumentChunker` → `IVectorStore`，支持增量同步 |
| 单元测试 ≥ 12 | 覆盖分页、权限错误、增量同步逻辑 |

**验收指标**：Agent 可执行"从 Notion 中找到关于部署流程的文档并总结"（Mock 环境）

---

## Month 3：运营闭环 + 产品化

### Week 9 — 线上评测集

| 交付物 | 说明 |
|--------|------|
| `EvaluationDataset` 模型 | 标准化评测集格式：`{ input, expectedOutput, expectedTools, tags, difficulty }` |
| 种子评测集 | 50 条覆盖：单轮问答、多步工具调用、邮件处理、日历操作、知识检索 |
| `EvaluationRunner` CLI | `dotnet dawning evaluate --dataset seed.json --output report.json` |
| 回归对比 | 自动与上次报告对比，输出 diff（成功率/步骤数/时延/成本） |
| CI 集成 | GitHub Actions workflow：PR 触发评测，成功率下降 > 5% 则阻塞合并 |

**验收指标**：种子评测集成功率 ≥ 75%，CI 中自动运行

### Week 10 — SLO 仪表盘

| 交付物 | 说明 |
|--------|------|
| SLO 定义文档 | 4 个核心 SLO：成功率 ≥ 85%、P95 时延 ≤ 30s、单任务成本 ≤ $0.05、工具错误率 ≤ 10% |
| Prometheus 指标导出 | 复用 `Dawning.Agents.OpenTelemetry`，新增 `agent_task_success_rate` / `agent_task_cost_usd` / `agent_tool_error_rate` |
| Grafana 仪表盘 JSON | 4 个面板：成功率趋势、时延分布、成本瀑布、工具错误 Top-10 |
| 告警规则 | 成功率 < 80% 持续 5min → PagerDuty/Slack 告警 |
| `deploy/observability/` 更新 | docker-compose 一键启动：Prometheus + Grafana + Loki + Tempo |

**验收指标**：docker-compose up 后仪表盘可展示实时 SLO 数据

### Week 11 — 灰度发布与回滚

| 交付物 | 说明 |
|--------|------|
| `IFeatureFlag` 接口 | `IsEnabledAsync(featureName, context)` — 简单百分比灰度 |
| `InMemoryFeatureFlag` / `ConfigurationFeatureFlag` 实现 | 支持 appsettings.json 动态切换 |
| Agent 灰度包装 | `GradualRolloutAgent`：按百分比路由到新/旧 Agent 实现 |
| 自动回滚策略 | 灰度窗口内成功率低于阈值 → 自动切回旧版 → 告警通知 |
| 集成测试 ≥ 8 | 模拟灰度升级、自动回滚、并发请求路由 |

**验收指标**：灰度从 10% → 50% → 100%，中间若成功率下降自动回滚到上一版

### Week 12 — 技能演化策略（Phase 5）+ 端到端验收

| 交付物 | 说明 |
|--------|------|
| `ISkillEvolutionPolicy` 接口 + `DefaultSkillEvolutionPolicy` | 自动提升（成功率 ≥ 80% + ≥ 3 次 → User；≥ 90% + ≥ 10 次 → Global）、自动淘汰（< 20% + ≥ 5 次） |
| Session 结束时自动评估 | `IToolSession.DisposeAsync` 触发演化策略 |
| 端到端场景验证 | 3 个完整场景跑通：① 邮件助手  ② 会议助手  ③ 知识问答助手 |
| 全量回归 | 评测集成功率 ≥ 80%，所有 SLO 达标 |
| 文档更新 | README 更新、CHANGELOG、发布说明 |
| Git tag `v0.3.0-preview` | 里程碑发布 |

---

## 交付物汇总

| 类别 | 数量 |
|------|------|
| **新增接口** | ~12 个（IToolUsageTracker, ISkillRouter, IReflectionEngine, ISkillEvolutionPolicy, IConnector, IEmailConnector, ICalendarConnector, IKnowledgeBaseConnector, IConnectorAuthProvider, IFeatureFlag, EvaluationDataset, EvaluationRunner） |
| **新增实现** | ~20 个（含 InMemory/Redis 双实现、3 个连接器、3 套工具、反思引擎、演化策略、灰度、评测 CLI） |
| **新增测试** | ≥ 120 个 |
| **新增文档** | 5+ 篇指南 |
| **里程碑** | v0.2.0-preview (W4), v0.3.0-preview (W12) |

## 成功标准（W12 结束时）

| 指标 | 目标 |
|------|------|
| 评测集成功率 | ≥ 80% |
| P95 任务时延 | ≤ 30s |
| 单任务平均成本 | ≤ $0.05 |
| 工具错误率 | ≤ 10% |
| 技能自动提升率 | Session 工具中 ≥ 30% 自动晋升 |
| 连接器覆盖 | 邮件 + 日历 + 知识库 |
| 灰度回滚 | 自动回滚延迟 ≤ 5min |
| 全量测试 | ≥ 2600 tests passing |

---

## 风险与缓解

| 风险 | 概率 | 缓解措施 |
|------|------|---------|
| Microsoft Graph API 限流 | 中 | 指数退避 + 批量请求 + 缓存 |
| 反思引擎产生无效修复 | 中 | MaxReflections 限制 + 沙箱验证 + 人工审批门 |
| 语义路由精度不足 | 低 | 回退到全量注入 + 效用加权调优 |
| Embedding 模型切换导致索引失效 | 低 | 版本化索引 + 自动重建 |
| 连接器 OAuth 令牌过期 | 中 | 自动刷新 + 优雅降级 + 用户通知 |
