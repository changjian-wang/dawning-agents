# Dawning.Agents 开发指南

Dawning.Agents 是一个 .NET 企业级 AI Agent 框架，设计灵感来自 OpenAI Agents SDK 的极简风格。

## 核心设计原则

1. **极简 API** — API 越少越好，合理默认值，一行完成注册，避免 Builder 过度设计
2. **纯 DI 架构** — 所有服务通过依赖注入获取，禁止静态工厂或直接 new
3. **企业级基础设施** — 必须支持 `IHttpClientFactory`、`ILogger<T>`、`IOptions<T>` + `IConfiguration`、`CancellationToken`
4. **破坏性修改优先** — 开发阶段直接删除旧 API，不使用 `[Obsolete]` 过渡
5. **接口与实现分离** — `Abstractions/` 放接口（零依赖），`Core/` 放实现和 DI 扩展
6. **配置驱动** — 通过 appsettings.json 切换行为，支持环境变量覆盖

## 技术栈

- .NET 10.0、C# 13、file-scoped namespaces
- 本地 LLM: Ollama | 远程 LLM: OpenAI, Azure OpenAI
- 测试: xUnit, FluentAssertions, Moq
- 格式化: CSharpier (`~/.dotnet/tools/csharpier format .`)
- 分析器: Meziantou.Analyzer, TreatWarningsAsErrors=true

## 命名规范

| 类型 | 规范 | 示例 |
|------|------|------|
| 接口 | `I` 前缀 | `ILLMProvider`, `IAgent` |
| 配置类 | `Options` 后缀 + `IValidatableOptions` | `AgentOptions : IValidatableOptions` |
| DI 扩展 | `Add` 前缀 | `AddLLMProvider`, `AddReActAgent` |
| 异步方法 | `Async` 后缀 | `ChatAsync`, `RunAsync` |
| 流式方法 | `StreamAsync` 后缀 | `ChatStreamAsync` |
| 命名空间 | 子文件夹路径 | `Dawning.Agents.Abstractions.Agent`, `Dawning.Agents.Core.Tools` |
