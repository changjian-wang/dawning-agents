---
description: |
  Use when: Diagnosing build failures, test failures, deployment issues, LLM integration problems, or performance degradation
  Don't use when: Writing new code (use code-update), performing audits (use deep-audit)
  Inputs: Error message, stack trace, or problem description
  Outputs: Root cause diagnosis and resolution steps
  Success criteria: Problem identified and resolved, or clear next steps provided
---

# Troubleshooting & Diagnostics Skill

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

