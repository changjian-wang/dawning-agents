// =============================================================================
// Dawning.Agents - Memory Sample
// =============================================================================
// 本示例展示五种 Memory 策略：
// 1. BufferMemory   - 完整存储（短对话）
// 2. WindowMemory   - 滑动窗口（控制 token）
// 3. SummaryMemory  - LLM 摘要压缩（长对话）
// 4. AdaptiveMemory - 自动降级（推荐生产环境）
// 5. VectorMemory   - 向量检索增强（超长程任务）
// =============================================================================

using Dawning.Agents.Samples.Memory;

var sample = new MemorySample();
await sample.RunAsync(args);
