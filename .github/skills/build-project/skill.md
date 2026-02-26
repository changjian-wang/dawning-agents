---
description: "Build and compile Dawning.Agents .NET project. Handles compilation errors and common build issues. Trigger: 构建, 编译, build, compile, dotnet build, restore, 编译错误, build error"
---

> **Skill 使用日志**：使用本 skill 后，在 `/memories/session/skill-log.md` 追加一行：`- {时间} build-project — {触发原因}`

# Build Project Skill

## What This Skill Does

Builds Dawning.Agents and helps diagnose compile errors.

## Quick Commands

| Command | Purpose |
|---|---|
| `dotnet build --nologo -v q` | Fast quiet build |
| `dotnet build --nologo` | Build with more detail |
| `dotnet clean && dotnet build --nologo` | Clean rebuild |
| `dotnet restore && dotnet build --nologo -v q` | Restore then build |

## Build Whole Solution

```bash
cd /path/to/dawning-agents
dotnet build --nologo -v q
```

## Build Specific Projects

```bash
dotnet build src/Dawning.Agents.Core/Dawning.Agents.Core.csproj --nologo
dotnet build src/Dawning.Agents.MCP/Dawning.Agents.MCP.csproj --nologo
dotnet build samples/Dawning.Agents.Samples.GettingStarted/Dawning.Agents.Samples.GettingStarted.csproj --nologo
```

## Current `src/` Projects

- `Dawning.Agents.Abstractions`
- `Dawning.Agents.Core`
- `Dawning.Agents.OpenAI`
- `Dawning.Agents.Azure`
- `Dawning.Agents.MCP`
- `Dawning.Agents.OpenTelemetry`
- `Dawning.Agents.Serilog`
- `Dawning.Agents.Redis`
- `Dawning.Agents.Chroma`
- `Dawning.Agents.Pinecone`
- `Dawning.Agents.Qdrant`
- `Dawning.Agents.Weaviate`

## Common Build Issues

- Locked output files: stop running samples/services and rebuild
- Missing types/packages: run `dotnet restore`
- Stale artifacts: run `dotnet clean`
- Formatting/analyzer failures: run `~/.dotnet/tools/csharpier format .`

## Script Helpers

- `./.github/skills/build-project/scripts/build.ps1`
- `./.github/skills/build-project/scripts/build.sh`
