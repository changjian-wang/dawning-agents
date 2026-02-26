---
description: "Troubleshooting and diagnostics for Dawning.Agents: build failures, test failures, deployment issues, LLM debugging, performance analysis, and common error patterns. Use when diagnosing errors, debugging failures, or investigating performance problems."
---

# Troubleshooting & Diagnostics

## Build Failures

### Meziantou.Analyzer Warnings (treated as errors)

`TreatWarningsAsErrors=true` means analyzer warnings break the build.

Common fixes:
- **MA0004 (async method missing cancellation)**: add `CancellationToken cancellationToken = default`
- **MA0006 (use string.Equals)**: replace `==` with `string.Equals(a, b, StringComparison.Ordinal)`
- **MA0049 (type should be sealed)**: add `sealed` to non-inheritable classes
- **MA0051 (method too long)**: extract helper methods
- **Suppress if justified**: `#pragma warning disable MA0004` with comment

### CSharpier Conflicts

```
error CSHARPIER: File is not formatted
```

Fix: `~/.dotnet/tools/csharpier format .`

If CSharpier and analyzer disagree, CSharpier wins (format after fixing analyzer issues).

### Missing Type / Namespace

- Check `<ProjectReference>` in `.csproj` — common when adding a new dependency between projects
- Check namespace matches folder path: `Dawning.Agents.Abstractions.{Area}` or `Dawning.Agents.Core.{Area}`
- Run `dotnet restore` after adding project references

### Locked Files

```
error MSB3021: Unable to copy file ... because it is being used by another process
```

Fix: `dotnet build-server shutdown` then retry.

## Test Failures

### Diagnostic Commands

```bash
# Run specific test class
dotnet test --filter "FullyQualifiedName~ClassName"

# Verbose output for one test
dotnet test --filter "FullyQualifiedName~MethodName" -v detailed

# List all tests without running
dotnet test --list-tests
```

### Common Patterns

**Mock setup missing**: `Moq.MockException: IXxx.Method invocation failed with mock behavior Strict`
→ Add missing `Setup()` call on the mock.

**FluentAssertions type mismatch**: `Expected item[0] to be ... but found ...`
→ Check `.BeEquivalentTo()` options; use `.Excluding()` for ignored properties.

**Timeout / async deadlock**: test hangs indefinitely
→ Ensure `await` is used (not `.Result` or `.Wait()`). Check `CancellationToken` propagation.

**Flaky tests**: test passes sometimes, fails others
→ Look for shared mutable state, timing-dependent assertions, or non-deterministic ordering. Use `[Collection]` to isolate or add `.InOrder()` qualifier.

**IValidatableOptions.Validate() tests**: each Options class must validate required fields and return error messages.
→ Pattern: create Options with invalid values → call `Validate()` → `errors.Should().ContainSingle()`.

### Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
# Results in TestResults/*/coverage.cobertura.xml
```

## Deployment Issues

### Docker Build Fails

- **Restore fails**: Dockerfile only copies specific `.csproj` files; if you added a new project reference, update the Dockerfile
- **Image too large**: ensure multi-stage build discards SDK layer; base image should be `aspnet:10.0-alpine`

### Health Check Fails

```bash
# Test locally
curl -f http://localhost:8080/health/live
curl -f http://localhost:8080/health/ready
```

If `/health/live` fails: app didn't start (check logs).
If `/health/ready` fails: dependency down (Redis, PostgreSQL).

Docker health check: `wget --no-verbose --tries=1 --spider http://localhost:8080/health/live`

### K8s Pod CrashLoopBackOff

```bash
kubectl logs -n dawning-agents <pod-name> --previous
kubectl describe pod -n dawning-agents <pod-name>
```

Common causes: missing ConfigMap/Secret, wrong connection strings, insufficient memory (increase resource limits).

### HPA Not Scaling

```bash
kubectl get hpa -n dawning-agents
kubectl top pods -n dawning-agents
```

Check metrics-server is running. HPA targets: CPU 70%, memory 80%.

## LLM Integration Debugging

### Ollama Not Responding

```bash
# Check Ollama is running
curl http://localhost:11434/api/tags

# Pull model if missing
ollama pull qwen2.5:0.5b
```

### Token Limit Exceeded

LLM returns truncated response or error → check `MaxTokens` in options, reduce prompt length, or enable memory summarization (via `AdaptiveMemory`).

### Function Calling Failures

- **Tool not found**: tool name returned by LLM doesn't match registered tool → check `IToolReader.GetAllAsync()` for available tools
- **Invalid arguments**: LLM sends malformed JSON → enable verbose logging to inspect raw tool call JSON
- **Tool execution error**: exceptions in tool handlers → check `ILogger<T>` output; tools should throw descriptive exceptions

### Cost Tracking

```csharp
AgentResponse response = await agent.RunAsync("query");
Console.WriteLine($"Total cost: ${response.TotalCost}");
```

If `BudgetExceededException` is thrown: agent exceeded `AgentOptions.MaxCostPerRun`. Increase budget or optimize prompt to reduce turns.

## Performance Analysis

### Benchmarks

```powershell
./scripts/benchmark.ps1 -Filter "*AgentBenchmark*"
```

Results saved to `benchmarks/results/{timestamp}/`. Uses BenchmarkDotNet.

### Slow Test Suite

```bash
# Time the full suite
time dotnet test --nologo -v q

# Find slow tests
dotnet test --nologo -v normal 2>&1 | grep -E "Duration|Passed|Failed"
```

Expected: ~2,046 tests in ≤15s. If significantly slower, check for missing `[Fact]` → `[Theory]` deduplication or excessive I/O mocking.

## Logging

Enable verbose logging in `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Dawning.Agents": "Trace"
    }
  }
}
```

For structured logging (Serilog), check `deploy/observability/` stack sends logs to Loki → view in Grafana.
