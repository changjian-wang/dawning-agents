---
description: "Use when: Making git commits with conventional format, running pre-commit checks, creating branches or tags\nDon't use when: Writing code (use code-update), building (use build-project), formatting (use csharpier)\nInputs: Changes to commit, or branch/tag to create\nOutputs: Git commit with conventional message format, or branch/tag created\nSuccess criteria: Commit message follows `type(scope): subject` format, pre-commit checks pass"
---

# Git Workflow Skill

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

