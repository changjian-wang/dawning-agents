// =============================================================================
// Dawning.Agents — Showcase: AI 研究助手
// =============================================================================
// 一个完整的 AI 研究助手应用，自然地串联框架的全部核心能力：
//
//   Phase  1  Prompt 模板 — 用模板引擎渲染系统提示
//   Phase  2  知识库构建 — 文档分块 → 向量存储 → 语义搜索 (RAG)
//   Phase  3  安全护栏   — 输入/输出过滤 + 注入防护
//   Phase  4  记忆系统   — Buffer / Window / Summary / Adaptive + SQLite
//   Phase  5  工具 & Agent — 自定义工具 + FunctionCallingAgent + ReActAgent
//   Phase  6  人机协作   — 审批工作流 + HumanInLoopAgent
//   Phase  7  多 Agent 编排 — 顺序 / 并行 / Handoff
//   Phase  8  工作流引擎 — DAG 构建 + 条件 / 循环 + 序列化
//   Phase  9  弹性基础设施 — 熔断器 + 负载均衡 + 特性开关
//   Phase 10  质量评估   — 测试集 + 评估指标
// =============================================================================

using Dawning.Agents.Samples.Showcase;

var sample = new ShowcaseSample();
await sample.RunAsync(args);
