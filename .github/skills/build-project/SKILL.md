---
description: "Build and compile Dawning.Agents .NET project. Handles compilation errors and common build issues. Trigger: 构建, 编译, build, compile, dotnet build, restore, 编译错误, build error"
---

# Build Project Skill

## 目标

构建 Dawning.Agents 解决方案并诊断编译错误。

## 触发条件

- **关键词**：构建, 编译, build, compile, dotnet build, restore, 编译错误, build error
- **文件模式**：`*.csproj`, `Directory.Build.props`, `*.sln`
- **用户意图**：构建项目、修复编译错误、恢复 NuGet 包

## 编排

- **前置**：`code-update`（代码变更后）
- **后续**：`run-tests`（构建通过后）

## Skill 使用日志

使用本 skill 后，在 `/memories/repo/skill-usage.md` 追加一行：`- {日期} build-project — {触发原因}`

---

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
```

## Current `src/` Projects

Dawning.Agents.Abstractions, Core, OpenAI, Azure, MCP, OpenTelemetry, Serilog, Redis, Chroma, Pinecone, Qdrant, Weaviate

## Common Build Issues

- **Locked output files**: stop running samples/services and rebuild
- **Missing types/packages**: run `dotnet restore`
- **Stale artifacts**: run `dotnet clean`
- **Formatting/analyzer failures**: run `~/.dotnet/tools/csharpier format .`

## 验收场景

- **输入**："dotnet build 报错 CS0246 找不到类型"
- **预期**：agent 检查 using 语句、项目引用、NuGet 包版本
- **上次验证**：2026-02-27
