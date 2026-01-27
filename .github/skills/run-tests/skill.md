---
name: run-tests
description: Run and manage tests for Dawning.Agents project
---

# Run Tests Skill

Execute and manage tests for the Dawning.Agents project.

## When to Use

- When asked to run tests
- When asked to verify changes
- When asked to check if something works
- After making code changes

## Test Commands

### Run All Tests

```powershell
cd C:\github\dawning-agents
dotnet test --nologo
```

### Run Tests with Verbose Output

```powershell
dotnet test --nologo -v n
```

### Run Specific Test File

```powershell
dotnet test --filter "FullyQualifiedName~ClassName"
```

### Run Single Test

```powershell
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"
```

### Run Tests with Coverage

```powershell
dotnet test --collect:"XPlat Code Coverage"
```

## Test Patterns in This Project

### Test File Location

```
tests/Dawning.Agents.Tests/
├── LLM/
│   └── OllamaProviderTests.cs
├── Agent/
│   └── ReActAgentTests.cs
├── Tools/
│   └── ToolRegistryTests.cs
└── ...
```

### Test Naming Convention

```csharp
public class MyServiceTests
{
    [Fact]
    public async Task MethodName_Scenario_ExpectedResult()
    {
        // Arrange
        // Act
        // Assert
    }
}
```

### Test Template

```csharp
using FluentAssertions;
using Moq;
using Xunit;

namespace Dawning.Agents.Tests;

public class MyServiceTests
{
    private readonly Mock<ILogger<MyService>> _loggerMock;
    private readonly MyService _sut;

    public MyServiceTests()
    {
        _loggerMock = new Mock<ILogger<MyService>>();
        _sut = new MyService(_loggerMock.Object);
    }

    [Fact]
    public async Task DoSomething_WithValidInput_ReturnsExpectedResult()
    {
        // Arrange
        var input = new Input { Value = "test" };

        // Act
        var result = await _sut.DoSomethingAsync(input);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
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

## Current Test Status

- **Test Framework**: xUnit
- **Assertions**: FluentAssertions
- **Mocking**: Moq
- **Total Tests**: ~1,183
- **Coverage**: ~72.9%

## After Running Tests

1. If tests pass: Proceed with commit
2. If tests fail: 
   - Read error message carefully
   - Check the failing test code
   - Fix the issue and re-run
