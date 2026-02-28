---
description: "Use when: Running xUnit tests, checking coverage, debugging test failures, or running targeted test subsets\nDon't use when: Writing new test code (use code-update), building (use build-project)\nInputs: Test command or test failure to investigate\nOutputs: Test results with pass/fail counts, coverage report, or failure diagnosis\nSuccess criteria: All 2225+ tests pass, no regressions introduced"
---

# Run Tests Skill

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

