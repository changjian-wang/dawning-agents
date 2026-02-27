---
description: "Git operations for Dawning.Agents with conventional commit messages. Trigger: git, commit, 提交, push, branch, 分支, merge, tag, 标签, pre-commit"
---

# Git Workflow Skill

## 目标

应用 Git 工作流和 Conventional Commits 规范。

## 触发条件

- **关键词**：git, commit, 提交, push, branch, 分支, merge, tag, 标签, pre-commit
- **文件模式**：`.git/**`, `.gitignore`
- **用户意图**：提交代码、推送分支、创建标签、查看历史

## 编排

- **前置**：`csharpier`（格式化后提交）
- **后续**：`changelog`（提交后更新变更日志，如需要）

## Skill 使用日志

使用本 skill 后，在 `/memories/repo/skill-usage.md` 追加一行：`- {日期} git-workflow — {触发原因}`

---

## Standard Flow

```bash
git status
git diff
git add -A
git commit -m "type(scope): summary"
git push
```

## Commit Format

```text
type(scope): description
```

### Types

`feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `chore`, `ci`

### Recommended Scopes

- Core domains: `agent`, `llm`, `tools`, `memory`, `workflow`, `orchestration`, `safety`, `resilience`
- Platform modules: `mcp`, `observability`, `logging`, `telemetry`, `diagnostics`, `health`, `scaling`
- Data/RAG: `rag`, `redis`, `chroma`, `pinecone`, `qdrant`, `weaviate`
- App/config/docs: `api`, `samples`, `config`, `docs`, `test`
- Providers: `openai`, `azure`, `serilog`

### Examples

```bash
feat(tools): split registry interfaces into reader and registrar
fix(agent): enforce max cost budget per run
test(validation): add options Validate coverage
chore(config): align sample appsettings defaults
```

## Pre-commit Checks

```bash
dotnet build --nologo -v q
dotnet test --nologo
~/.dotnet/tools/csharpier format .
```

## 验收场景

- **输入**："提交这次修改"
- **预期**：agent 运行 pre-commit 检查，生成符合规范的 commit message，执行 `git add && git commit`
- **上次验证**：2026-02-27
