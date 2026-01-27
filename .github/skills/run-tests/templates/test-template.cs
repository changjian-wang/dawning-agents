using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dawning.Agents.Tests;

/// <summary>
/// Tests for {ServiceName}.
/// </summary>
public class {ServiceName}Tests
{
    private readonly Mock<ILogger<{ServiceName}>> _loggerMock;
    private readonly {ServiceName} _sut; // System Under Test

    public {ServiceName}Tests()
    {
        _loggerMock = new Mock<ILogger<{ServiceName}>>();
        _sut = new {ServiceName}(_loggerMock.Object);
    }

    [Fact]
    public async Task {MethodName}_WithValidInput_ReturnsExpectedResult()
    {
        // Arrange
        var input = "test input";

        // Act
        var result = await _sut.{MethodName}Async(input);

        // Assert
        result.Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task {MethodName}_WithInvalidInput_ThrowsArgumentException(string? value)
    {
        // Arrange & Act
        var act = () => _sut.{MethodName}Async(value!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_UsesNullLogger()
    {
        // Arrange & Act
        var service = new {ServiceName}(null);

        // Assert - should not throw
        service.Should().NotBeNull();
    }
}
