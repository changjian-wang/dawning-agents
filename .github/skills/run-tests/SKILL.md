---
description: "Run and manage xUnit tests for Dawning.Agents project using FluentAssertions and Moq. Trigger: 测试, test, xunit, 跑测试, run tests, coverage, 覆盖率, 测试失败, test failure"
---

# Run Tests Skill

## 目标

运行和验证 Dawning.Agents 的 xUnit 测试套件。

## 触发条件

- **关键词**：测试, test, xunit, 跑测试, run tests, coverage, 覆盖率, 测试失败, test failure
- **文件模式**：`tests/**/*.cs`, `*.Tests.csproj`
- **用户意图**：运行测试、检查覆盖率、修复测试失败

## 编排

- **前置**：`build-project`（构建通过后跑测试）
- **后续**：`csharpier`（测试通过后格式化）

## Skill 使用日志

使用本 skill 后，在 `/memories/repo/skill-usage.md` 追加一行：`- {日期} run-tests — {触发原因}`

---

## Quick Commands

| Command | Purpose |
|---|---|
| `dotnet test --nologo` | Run all tests |
| `dotnet test --nologo -v q` | Quiet full run |
| `dotnet test --filter "FullyQualifiedName~Tools"` | Filter by namespace/class |
| `dotnet test --collect:"XPlat Code Coverage"` | Coverage collection |

## Run All Tests

```bash
cd /path/to/dawning-agents
dotnet test --nologo
```

## Targeted Runs

```bash
dotnet test --filter "FullyQualifiedName~ClassName"
dotnet test --filter "FullyQualifiedName~MethodName" -v detailed
```

## Current Test Status

- Runtime total: **2225** tests passing
- Stack: xUnit + FluentAssertions + Moq

## Test Project Structure

`tests/Dawning.Agents.Tests/` covers: Agent, Architecture, Cache, Chroma, Communication, Configuration, Diagnostics, Discovery, Evaluation, Handoff, Health, HumanLoop, LLM, Logging, MCP, Memory, Multimodal, Observability, Orchestration, Prompts, RAG, Redis, Resilience, Safety, Scaling, Telemetry, Tools, Validation, Weaviate, Workflow

## Failure Workflow

1. Re-run with `--filter` to isolate failures
2. Fix behavior first, assertions second
3. Re-run focused tests, then full suite

## 验收场景

- **输入**："跑一下测试看看有没有失败"
- **预期**：agent 执行 `dotnet test --nologo`，报告通过/失败数量
- **上次验证**：2026-02-27
