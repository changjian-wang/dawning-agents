# Contributing to Dawning.Agents

感谢你对 Dawning.Agents 的关注！我们欢迎所有形式的贡献。

> [!WARNING]
> 本仓库已于 **2026-04-03** 进入弃用状态（Deprecated）。
> 当前不再接收新功能类 PR。仅在维护者确认的阻断级问题（严重安全/关键稳定性）场景下接受修复。
> Agent Framework 的后续演进将迁移到新仓库。

## 开发环境

| 依赖 | 版本 | 说明 |
|------|------|------|
| .NET SDK | 10.0+ | 必须 |
| Ollama | 0.1.0+ | 本地模型，可选 |
| CSharpier | latest | 代码格式化 |

```bash
# 克隆仓库
git clone https://github.com/changjian-wang/dawning-agents.git
cd dawning-agents

# 构建
dotnet build

# 运行测试
dotnet test

# 格式化代码
dotnet tool restore
dotnet tool run csharpier .
```

## Fork & PR 流程

1. **Fork** 本仓库
2. **创建分支**: `git checkout -b feat/your-feature`
3. **编写代码**，确保附带测试
4. **本地验证**:
   ```bash
   dotnet build --configuration Release
   dotnet test
   dotnet tool run csharpier .
   ```
5. **提交**: 遵守下方 Commit 规范
6. **推送**: `git push origin feat/your-feature`
7. **创建 Pull Request**，描述变更内容

## Commit 规范

遵循 [Conventional Commits](https://www.conventionalcommits.org/)：

```
<type>(<scope>): <subject>
```

### Type

| Type | 说明 |
|------|------|
| `feat` | 新功能 |
| `fix` | Bug 修复 |
| `docs` | 文档变更 |
| `refactor` | 重构（不改变行为） |
| `test` | 测试相关 |
| `chore` | 构建/CI/工具链 |

### Scope

`core` · `abstractions` · `openai` · `azure` · `mcp` · `redis` · `qdrant` · `pinecone` · `chroma` · `weaviate` · `otel` · `serilog` · `samples` · `docs`

### 示例

```
feat(core): add retry policy to agent runner
fix(openai): handle rate limit 429 response
docs(samples): add SSE streaming example
test(mcp): add transport layer unit tests
chore(ci): update build workflow to .NET 10
```

## 代码风格

- **格式化**: [CSharpier](https://csharpier.com/) — `dotnet tool run csharpier .`
- **分析器**: [Meziantou.Analyzer](https://github.com/meziantou/Meziantou.Analyzer) — 全部启用
- **编译警告**: `TreatWarningsAsErrors=true` — 不允许任何编译警告
- **命名空间**: file-scoped namespaces
- **异步方法**: `Async` 后缀（流式用 `StreamAsync`）
- **接口**: `I` 前缀（`ILLMProvider`, `IAgent`）

## 测试要求

- 新功能**必须**附带单元测试
- 运行 `dotnet test` 确保全部通过
- 测试使用 xUnit + FluentAssertions + Moq
- 不依赖外部服务（Mock 所有 I/O）

## Issue & PR 模板

- **Bug Report**: 描述问题、复现步骤、期望行为、环境信息
- **Feature Request**: 描述需求、使用场景、提议的 API 设计
- **PR**: 关联 Issue、描述变更、自检清单

## 许可证

贡献的代码将使用与本项目相同的 [MIT License](LICENSE)。
