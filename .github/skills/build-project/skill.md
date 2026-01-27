---
name: build-project
description: Build and compile Dawning.Agents project
---

# Build Project Skill

Build and compile the Dawning.Agents project.

## When to Use

- When asked to build or compile
- Before running tests
- After making code changes
- When checking for compilation errors

## Build Commands

### Quick Build

```powershell
cd C:\github\dawning-agents
dotnet build --nologo -v q
```

### Full Build with Warnings

```powershell
dotnet build --nologo
```

### Release Build

```powershell
dotnet build -c Release --nologo
```

### Clean and Rebuild

```powershell
dotnet clean
dotnet build --nologo
```

### Build Specific Project

```powershell
dotnet build src/Dawning.Agents.Core/Dawning.Agents.Core.csproj --nologo
```

## Project Structure

```
Dawning.Agents.sln
├── src/
│   ├── Dawning.Agents.Abstractions/  # Interfaces (zero dependencies)
│   ├── Dawning.Agents.Core/          # Core implementations
│   ├── Dawning.Agents.OpenAI/        # OpenAI provider
│   └── Dawning.Agents.Azure/         # Azure OpenAI provider
├── tests/
│   └── Dawning.Agents.Tests/         # Unit tests
└── samples/
    └── Dawning.Agents.Demo/          # Demo application
```

## Common Build Issues

### 1. File Locked by Process

```
error MSB3026: Could not copy file because it is being used
```

**Solution**: Kill the process holding the file

```powershell
Stop-Process -Name "Dawning.Agents.Demo" -ErrorAction SilentlyContinue
dotnet build --nologo
```

### 2. Missing Package Reference

```
error CS0246: The type or namespace name 'X' could not be found
```

**Solution**: Restore packages

```powershell
dotnet restore
dotnet build --nologo
```

### 3. Version Mismatch

**Solution**: Clean and rebuild

```powershell
dotnet clean
dotnet build --nologo
```

## Build Workflow

1. **After code changes**: `dotnet build --nologo -v q`
2. **If build fails**: Fix errors, rebuild
3. **If build succeeds**: Run tests with `dotnet test --nologo`
4. **Before commit**: Ensure clean build and all tests pass
