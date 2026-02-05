// =============================================================================
// Dawning.Agents - Getting Started Sample
// =============================================================================
// 本示例展示最基础的 Agent 使用方式：
// 1. HelloAgent - 最简单的 Agent 调用
// 2. SimpleChat - 简单聊天
// 3. ToolUsage - 工具使用
// =============================================================================

using Dawning.Agents.Samples.GettingStarted;

var sample = new GettingStartedSample();
await sample.RunAsync(args);
