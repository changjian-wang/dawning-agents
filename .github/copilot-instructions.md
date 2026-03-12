# Dawning.Agents

## 项目概述

Dawning.Agents 是一个 .NET 企业级 AI Agent 框架，设计灵感来自 OpenAI Agents SDK 的极简风格。

- **目标用户**：需要在 .NET 生态中构建 LLM 驱动 Agent 的企业开发者
- **核心价值**：极简 API + 纯依赖注入 + 企业级基础设施（可观测性、弹性、安全）
- **当前阶段**：pre-release（0.1.0-preview），快速迭代，API 不稳定

## 核心原则（6 条，所有代码变更必须遵守）

1. **极简 API** — API 越少越好，合理默认值，一行完成注册，禁止 Builder 过度设计
2. **纯 DI 架构** — 所有服务通过构造函数注入，禁止静态工厂、禁止 `new` 运行时服务
3. **企业级基础设施** — 必须支持 `IHttpClientFactory`、`ILogger<T>`、`IOptions<T>`、`CancellationToken`
4. **破坏性修改优先** — 开发阶段直接删除旧 API，禁止 `[Obsolete]` 过渡
5. **接口与实现分离** — `Abstractions/` 放接口（零依赖），`Core/` 放实现和 DI 扩展
6. **配置驱动** — 通过 appsettings.json 切换行为，支持环境变量覆盖

## 技术栈

- .NET 10.0、C# 13、file-scoped namespaces
- LLM: Ollama (本地) | OpenAI, Azure OpenAI (远程)
- 测试: xUnit + FluentAssertions + Moq
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

## Skill 索引

完整的领域知识分布在以下 skills 中，按需自动触发：

| Skill | 覆盖范围 | 触发关键词 |
|-------|---------|-----------|
| architecture | 项目结构、模块边界、核心接口、DI API | 架构, project structure, namespace |
| code-update | 编码模式、模板、命名空间规则、禁止事项 | 写代码, implement, fix bug, refactor |
| code-review | 代码审查清单、架构合规、质量门禁 | 审查, review, check code |
| build-project | 构建命令、常见编译错误 | 构建, build, compile |
| run-tests | 测试执行、覆盖率、2253 tests | 测试, test, coverage |
| csharpier | CSharpier 格式化规则 | 格式化, format, code style |
| git-workflow | 提交规范、scope 列表、pre-commit | git, commit, push, tag |
| markdown | Markdown/XML 文档规范 | markdown, 写文档, API docs |
| nuget-release | 版本管理、打包发布、CI/CD | nuget, release, publish, version |
| deployment | Docker、K8s、可观测性、回滚 | deploy, docker, k8s, rollback |
| changelog | CHANGELOG 格式、DocFX、release notes | changelog, release notes |
| troubleshooting | 构建/测试/部署排错、LLM 调试 | 排错, error, debug, troubleshoot |
| deep-audit | 逐行深度代码审计、安全/线程/资源/DI 全维度检查 | 深度审计, deep audit, 代码体检 |
| security-audit | 安全专项：OWASP Top 10、依赖漏洞、密钥泄露 | 安全审计, security, vulnerability |
| performance | BenchmarkDotNet、热路径、内存分配、异步开销 | 性能, benchmark, 热路径, allocation, optimize |
| dependency-update | NuGet 依赖升级、破坏性变更评估、CVE 修补 | 依赖更新, NuGet update, 升级, outdated, CVE |

## 标准编排链

代码变更必须按以下顺序执行 skill：

1. `code-update` → `build-project` → `run-tests` → `csharpier` → `git-workflow`

审计修复链：

2. `deep-audit` → `code-update` → `build-project` → `run-tests` → `csharpier` → `git-workflow`

发布链：

3. `changelog` → `nuget-release` → `build-project` → `run-tests` → `csharpier` → `git-workflow` → `deployment`

## Custom Agents

| Agent | 用途 | 调用方式 |
|-------|------|---------|
| `@auditor` | 全量代码审计 → 修复 → 构建 → 测试 → 提交 | `@auditor 全面审计` |
| `@releaser` | 变更日志 → 版本号 → 构建 → 测试 → 打包 → 标签 | `@releaser 发布 0.2.0` |

## Skill 使用规则

1. 代码变更的完整流程：code-update → build-project → run-tests → csharpier → git-workflow
