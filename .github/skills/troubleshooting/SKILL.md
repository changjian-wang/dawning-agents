---
description: "Troubleshooting and diagnostics for Dawning.Agents: build/test/deploy failures, LLM debugging, performance. Trigger: 排错, 报错, error, debug, 调试, troubleshoot, diagnose, 性能, performance, 失败, failure, crash"
---

# Troubleshooting & Diagnostics Skill

## 目标

诊断和解决构建、测试、部署和 LLM 集成中的常见问题。

## 触发条件

- **关键词**：排错, 报错, error, debug, 调试, troubleshoot, diagnose, 性能, performance, 失败, failure, crash
- **文件模式**：`*.log`, `TestResults/**`
- **用户意图**：修复报错、调试问题、分析性能、诊断部署故障

## 编排

- **前置**：任意 skill（出错后触发）
- **后续**：`code-update`（需要修代码时）或 `deployment`（部署问题时）

---

## Build Failures

### Meziantou.Analyzer Warnings (treated as errors)

- **MA0004**: add `CancellationToken cancellationToken = default`
- **MA0006**: use `string.Equals(a, b, StringComparison.Ordinal)`
- **MA0049**: add `sealed` to non-inheritable classes
- **MA0051**: extract helper methods
- **Suppress if justified**: `#pragma warning disable MA0004`

### CSharpier Conflicts

```bash
~/.dotnet/tools/csharpier format .
```

If CSharpier and analyzer disagree, CSharpier wins.

### Missing Type / Namespace

- Check `<ProjectReference>` in `.csproj`
- Check namespace matches folder path
- Run `dotnet restore`

### Locked Files

```bash
dotnet build-server shutdown
```

## Test Failures

### Diagnostic Commands

```bash
dotnet test --filter "FullyQualifiedName~ClassName"
dotnet test --filter "FullyQualifiedName~MethodName" -v detailed
```

### Common Patterns

- **Mock setup missing**: add `Setup()` call on mock
- **FluentAssertions type mismatch**: check `.BeEquivalentTo()` options
- **Timeout / async deadlock**: use `await` not `.Result`
- **Flaky tests**: shared mutable state or non-deterministic ordering

## Deployment Issues

### Docker Build Fails

- Restore fails: update Dockerfile `.csproj` COPY list
- Image too large: ensure multi-stage build

### K8s CrashLoopBackOff

```bash
kubectl logs -n dawning-agents <pod-name> --previous
kubectl describe pod -n dawning-agents <pod-name>
```

## LLM Integration Debugging

### Ollama Not Responding

```bash
curl http://localhost:11434/api/tags
ollama pull qwen2.5:0.5b
```

### Function Calling Failures

- Tool not found: check `IToolReader.GetAllAsync()`
- Invalid arguments: enable verbose logging
- Budget exceeded: increase `AgentOptions.MaxCostPerRun`

## Performance

```powershell
./scripts/benchmark.ps1 -Filter "*AgentBenchmark*"
```

## 验收场景

- **输入**："构建报错 MA0004，怎么修？"
- **预期**：agent 解释需要添加 CancellationToken 参数，提供修复代码
- **上次验证**：2026-02-27
