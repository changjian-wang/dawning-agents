---
name: build-project
description: |
  Use when: Building the solution, fixing compilation errors, restoring packages, or checking project references
  Don't use when:
    - Running tests (use run-tests)
    - Formatting code (use csharpier)
    - Deploying containers or K8s (use deployment)
    - Writing new code (use code-update)
    - Diagnosing runtime errors (use troubleshooting)
  Inputs: Build command or compilation error message
  Outputs: Successful build or resolved compilation error with explanation
  Success criteria: `dotnet build` succeeds with 0 errors and 0 warnings
---

# Build Project Skill

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

