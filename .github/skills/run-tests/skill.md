---
name: run-tests
description: >
  Run and manage xUnit tests for Dawning.Agents project using FluentAssertions 
  and Moq. Use when asked to "run tests", "verify changes", "check if it works",
  "test this", or after making code changes.
---

# Run Tests Skill

## What This Skill Does

Executes and manages unit tests for the Dawning.Agents project using xUnit, FluentAssertions, and Moq.

## When to Use

- "Run tests"
- "Run the test suite"
- "Verify my changes"
- "Does this work?"
- "Test this feature"
- After making any code changes

## Test Commands

### Quick Reference

| Command | Purpose |
|---------|---------|
| `dotnet test --nologo` | Run all tests |
| `dotnet test --filter "ClassName"` | Run tests in class |
| `dotnet test --filter "MethodName"` | Run specific test |
| `dotnet test --collect:"XPlat Code Coverage"` | With coverage |

### Run All Tests

```powershell
cd C:\github\dawning-agents
dotnet test --nologo
```

### Run Specific Tests

```powershell
# By class name
dotnet test --filter "FullyQualifiedName~OllamaProviderTests"

# By method name
dotnet test --filter "FullyQualifiedName~ChatAsync_WithValidInput_ReturnsResponse"

# By namespace
dotnet test --filter "Namespace~Dawning.Agents.Tests.Tools"
```

## Test Structure

### File Location

```
tests/Dawning.Agents.Tests/
├── LLM/
│   └── OllamaProviderTests.cs
├── Agent/
│   └── ReActAgentTests.cs
├── Tools/
│   └── ToolRegistryTests.cs
├── Memory/
│   └── BufferMemoryTests.cs
└── ...
```

### Test Naming Convention

```
MethodName_Scenario_ExpectedResult
```

Examples:
- `ChatAsync_WithValidInput_ReturnsResponse`
- `GetTool_WithUnknownName_ReturnsNull`
- `AddMessage_WhenMemoryFull_RemovesOldest`

## Test Template

Use this template when creating new tests:

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dawning.Agents.Tests;

public class MyServiceTests
{
    private readonly Mock<ILogger<MyService>> _loggerMock;
    private readonly Mock<IDependency> _dependencyMock;
    private readonly MyService _sut; // System Under Test

    public MyServiceTests()
    {
        _loggerMock = new Mock<ILogger<MyService>>();
        _dependencyMock = new Mock<IDependency>();
        _sut = new MyService(
            _dependencyMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task DoSomething_WithValidInput_ReturnsExpectedResult()
    {
        // Arrange
        var input = new Input { Value = "test" };
        _dependencyMock
            .Setup(x => x.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("processed");

        // Act
        var result = await _sut.DoSomethingAsync(input);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task DoSomething_WithInvalidInput_ThrowsException(string? value)
    {
        // Arrange
        var input = new Input { Value = value };

        // Act
        var act = () => _sut.DoSomethingAsync(input);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
```

## Current Status

| Metric | Value |
|--------|-------|
| Framework | xUnit |
| Assertions | FluentAssertions |
| Mocking | Moq |
| Total Tests | ~1,183 |
| Coverage | ~72.9% |

## After Running Tests

| Result | Action |
|--------|--------|
| ✅ All pass | Proceed with commit |
| ❌ Failures | Read error, fix issue, re-run |
