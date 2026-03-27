---
name: troubleshooting
description: |
  Use when: Diagnosing build failures, test failures, deployment issues, LLM integration problems, or performance degradation
  Don't use when:
    - Writing new features (use code-update)
    - Performing code audits (use deep-audit)
    - Running tests (use run-tests)
    - Fixing build errors (use build-project)
    - Security-specific investigation (use security-audit)
  Inputs: Error message, stack trace, or problem description
  Outputs: Root cause diagnosis and resolution steps
  Success criteria: Problem identified and resolved, or clear next steps provided
---

# Troubleshooting & Diagnostics Skill

## Build Failures

### Meziantou.Analyzer Warnings (treated as errors)

- **MA0004**: add `CancellationToken cancellationToken = default`
- **MA0006**: use `string.Equals(a, b, StringComparison.Ordinal)`
- **MA0025**: use `NotSupportedException` instead of `NotImplementedException` for deliberate non-support
- **MA0049**: add `sealed` to non-inheritable classes
- **MA0051**: extract helper methods
- **Suppress if justified**: `#pragma warning disable MA0004`

### IDE / Roslyn Analyzer Errors

- **IDE0011**: add braces to `if`/`else`/`for`/`foreach` statements (required by project config)
- **IDE0060**: remove unused parameter
- **CS8600/CS8602/CS8604**: nullable reference type warnings — add `?`, null checks, or `!` (last resort)

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

- Tool not found: check `IToolReader.GetAllTools()` and `IToolSession.GetSessionTools()`
- Invalid arguments: enable verbose logging
- Budget exceeded: increase `AgentOptions.MaxCostPerRun`
- Runtime mismatch: verify `EphemeralToolDefinition.Runtime` matches installed runtime (Bash/PowerShell/Python)
- Session tool update fails: check `IToolSession.UpdateTool` — tool must exist in session scope
- FileToolStore path error: verify `~/.dawning/tools/` (User) or `{project}/.dawning/tools/` (Global) directory exists

## Performance

```powershell
./scripts/benchmark.ps1 -Filter "*AgentBenchmark*"
```

