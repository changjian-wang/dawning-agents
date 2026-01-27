---
name: build-project
description: >
  Build and compile Dawning.Agents .NET project. Handles compilation errors
  and common build issues. Use when asked to "build", "compile", "check for 
  errors", or before running tests.
---

# Build Project Skill

## What This Skill Does

Builds and compiles the Dawning.Agents .NET project, handling common build issues.

## When to Use

- "Build the project"
- "Compile the code"
- "Check for compilation errors"
- Before running tests
- After making code changes

## Build Commands

### Quick Reference

| Command | Purpose |
|---------|---------|
| `dotnet build --nologo -v q` | Quick build (quiet) |
| `dotnet build --nologo` | Build with warnings |
| `dotnet build -c Release` | Release build |
| `dotnet clean && dotnet build` | Clean rebuild |

### Standard Build

```powershell
cd C:\github\dawning-agents
dotnet build --nologo -v q
```

### Build Specific Project

```powershell
# Core library only
dotnet build src/Dawning.Agents.Core/Dawning.Agents.Core.csproj --nologo

# Demo only
dotnet build samples/Dawning.Agents.Demo/Dawning.Agents.Demo.csproj --nologo
```

## Project Structure

```
Dawning.Agents.sln
├── src/
│   ├── Dawning.Agents.Abstractions/  # Interfaces (zero deps)
│   ├── Dawning.Agents.Core/          # Core implementations
│   ├── Dawning.Agents.OpenAI/        # OpenAI provider
│   └── Dawning.Agents.Azure/         # Azure provider
├── tests/
│   └── Dawning.Agents.Tests/         # Unit tests
└── samples/
    └── Dawning.Agents.Demo/          # Demo app
```

## Common Build Issues

### 1. File Locked by Process

**Error:**
```
error MSB3026: Could not copy file because it is being used
```

**Solution:**
```powershell
Stop-Process -Name "Dawning.Agents.Demo" -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
dotnet build --nologo
```

### 2. Missing Package Reference

**Error:**
```
error CS0246: The type or namespace name 'X' could not be found
```

**Solution:**
```powershell
dotnet restore
dotnet build --nologo
```

### 3. Stale Build Artifacts

**Error:** Strange runtime errors or version mismatches

**Solution:**
```powershell
dotnet clean
dotnet build --nologo
```

### 4. CSharpier Formatting Issues

**Warning:** Code style warnings

**Solution:**
```powershell
dotnet csharpier .
dotnet build --nologo
```

## Build Workflow

```
┌─────────────────┐
│  Code Changes   │
└────────┬────────┘
         ▼
┌─────────────────┐
│  dotnet build   │──── Errors? ──► Fix and retry
└────────┬────────┘
         ▼
┌─────────────────┐
│  dotnet test    │──── Failures? ──► Fix and retry
└────────┬────────┘
         ▼
┌─────────────────┐
│  Ready to commit│
└─────────────────┘
```

## Tech Stack

| Component | Version/Tool |
|-----------|--------------|
| Framework | .NET 10.0 |
| IDE | VS Code / Visual Studio |
| Formatting | CSharpier |
