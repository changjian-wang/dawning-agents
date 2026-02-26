---
name: git-workflow
description: "Git operations for Dawning.Agents with conventional commit messages."
---

# Git Workflow Skill

## What This Skill Does

Applies Git workflow and conventional commits for Dawning.Agents.

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
test(validation): add options Validate coverage for safety and cache
chore(config): align sample appsettings defaults
```

## Pre-commit Checks

```bash
dotnet build --nologo -v q
dotnet test --nologo
~/.dotnet/tools/csharpier format .
```

## Helpers

- `./.github/skills/git-workflow/scripts/pre-commit.ps1`
- `./.github/skills/git-workflow/scripts/pre-commit.sh`
