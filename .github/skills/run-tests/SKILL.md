---
description: "Run and manage xUnit tests for Dawning.Agents project using FluentAssertions and Moq. Trigger: 测试, test, xunit, 跑测试, run tests, coverage, 覆盖率, 测试失败, test failure"
---

> **Skill 使用日志**：使用本 skill 后，在 `/memories/session/skill-log.md` 追加一行：`- {时间} run-tests — {触发原因}`

# Run Tests Skill

## What This Skill Does

Runs and validates test suites for Dawning.Agents.

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
dotnet test --filter "FullyQualifiedName~Dawning.Agents.Tests.Tools"
dotnet test --filter "FullyQualifiedName~FunctionCallingAgentTests"
dotnet test --filter "FullyQualifiedName~RunAsync"
```

## Current Test Status (2026-02)

- Runtime total: `2046` tests passing (from `dotnet test`)
- Test methods (`[Fact]`/`[Theory]`): ~`1822`
- Stack: xUnit + FluentAssertions + Moq

## Test Project Structure

`tests/Dawning.Agents.Tests/` includes major areas:

- Agent, Architecture, Cache, Chroma, Communication
- Configuration, Diagnostics, Discovery, Evaluation
- Handoff, Health, HumanLoop, LLM, Logging, MCP, Memory
- Multimodal, Observability, Orchestration, Prompts
- RAG, Redis, Resilience, Safety, Scaling, Telemetry, Tools, Validation, Weaviate, Workflow

## Script Helpers

- `./.github/skills/run-tests/scripts/run-tests.ps1`
- `./.github/skills/run-tests/scripts/run-tests.sh`

## Failure Workflow

1. Re-run with `--filter` to isolate failures.
2. Fix behavior first, assertions second.
3. Re-run focused tests, then full suite.
