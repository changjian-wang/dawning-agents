using System.Reflection;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools;
using FluentAssertions;

namespace Dawning.Agents.Tests.Tools;

/// <summary>
/// MethodTool unit tests
/// </summary>
public sealed class MethodToolTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldSetPropertiesFromAttribute()
    {
        // Arrange
        var method = typeof(TestToolClass).GetMethod(nameof(TestToolClass.SimpleMethod))!;
        var attribute = method.GetCustomAttribute<FunctionToolAttribute>()!;
        var instance = new TestToolClass();

        // Act
        var tool = new MethodTool(method, instance, attribute);

        // Assert
        tool.Name.Should().Be("simple_method");
        tool.Description.Should().Be("A simple method");
        tool.RequiresConfirmation.Should().BeFalse();
        tool.RiskLevel.Should().Be(ToolRiskLevel.Low);
    }

    [Fact]
    public void Constructor_ShouldUseMethodNameWhenAttributeNameIsNull()
    {
        // Arrange
        var method = typeof(TestToolClass).GetMethod(nameof(TestToolClass.MethodWithoutName))!;
        var attribute = method.GetCustomAttribute<FunctionToolAttribute>()!;
        var instance = new TestToolClass();

        // Act
        var tool = new MethodTool(method, instance, attribute);

        // Assert
        tool.Name.Should().Be("MethodWithoutName");
    }

    [Fact]
    public void Constructor_WithNullMethod_ShouldThrow()
    {
        // Arrange
        var attribute = new FunctionToolAttribute("Test");

        // Act & Assert
        var act = () => new MethodTool(null!, null, attribute);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ShouldBuildParametersSchema()
    {
        // Arrange
        var method = typeof(TestToolClass).GetMethod(nameof(TestToolClass.MethodWithParameters))!;
        var attribute = method.GetCustomAttribute<FunctionToolAttribute>()!;
        var instance = new TestToolClass();

        // Act
        var tool = new MethodTool(method, instance, attribute);

        // Assert
        tool.ParametersSchema.Should().Contain("name");
        tool.ParametersSchema.Should().Contain("age");
        tool.ParametersSchema.Should().Contain("string");
        tool.ParametersSchema.Should().Contain("integer");
    }

    [Fact]
    public void Constructor_WithCategoryInAttribute_ShouldSetCategory()
    {
        // Arrange
        var method = typeof(TestToolClass).GetMethod(nameof(TestToolClass.MethodWithCategory))!;
        var attribute = method.GetCustomAttribute<FunctionToolAttribute>()!;
        var instance = new TestToolClass();

        // Act
        var tool = new MethodTool(method, instance, attribute);

        // Assert
        tool.Category.Should().Be("TestCategory");
    }

    [Fact]
    public void Constructor_NoParameters_ShouldReturnEmptySchema()
    {
        // Arrange
        var method = typeof(TestToolClass).GetMethod(nameof(TestToolClass.NoParamsMethod))!;
        var attribute = method.GetCustomAttribute<FunctionToolAttribute>()!;
        var instance = new TestToolClass();

        // Act
        var tool = new MethodTool(method, instance, attribute);

        // Assert
        tool.ParametersSchema.Should().Contain("properties");
        tool.ParametersSchema.Should().Contain("{}");
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_SimpleStringMethod_ShouldExecute()
    {
        // Arrange
        var method = typeof(TestToolClass).GetMethod(nameof(TestToolClass.EchoMethod))!;
        var attribute = method.GetCustomAttribute<FunctionToolAttribute>()!;
        var instance = new TestToolClass();
        var tool = new MethodTool(method, instance, attribute);

        // Act
        var result = await tool.ExecuteAsync("hello");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("Echo: hello");
    }

    [Fact]
    public async Task ExecuteAsync_WithJsonInput_ShouldParseParameters()
    {
        // Arrange
        var method = typeof(TestToolClass).GetMethod(nameof(TestToolClass.MethodWithParameters))!;
        var attribute = method.GetCustomAttribute<FunctionToolAttribute>()!;
        var instance = new TestToolClass();
        var tool = new MethodTool(method, instance, attribute);

        // Act
        var result = await tool.ExecuteAsync("""{"name":"John","age":30}""");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("John");
        result.Output.Should().Contain("30");
    }

    [Fact]
    public async Task ExecuteAsync_AsyncMethod_ShouldAwaitResult()
    {
        // Arrange
        var method = typeof(TestToolClass).GetMethod(nameof(TestToolClass.AsyncMethod))!;
        var attribute = method.GetCustomAttribute<FunctionToolAttribute>()!;
        var instance = new TestToolClass();
        var tool = new MethodTool(method, instance, attribute);

        // Act
        var result = await tool.ExecuteAsync("test");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("Async: test");
    }

    [Fact]
    public async Task ExecuteAsync_AsyncVoidMethod_ShouldComplete()
    {
        // Arrange
        var method = typeof(TestToolClass).GetMethod(nameof(TestToolClass.AsyncVoidMethod))!;
        var attribute = method.GetCustomAttribute<FunctionToolAttribute>()!;
        var instance = new TestToolClass();
        var tool = new MethodTool(method, instance, attribute);

        // Act
        var result = await tool.ExecuteAsync("test");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsToolResult_ShouldReturnAsIs()
    {
        // Arrange
        var method = typeof(TestToolClass).GetMethod(nameof(TestToolClass.ReturnsToolResult))!;
        var attribute = method.GetCustomAttribute<FunctionToolAttribute>()!;
        var instance = new TestToolClass();
        var tool = new MethodTool(method, instance, attribute);

        // Act
        var result = await tool.ExecuteAsync("test");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("Custom result");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNull_ShouldReturnEmptyString()
    {
        // Arrange
        var method = typeof(TestToolClass).GetMethod(nameof(TestToolClass.ReturnsNull))!;
        var attribute = method.GetCustomAttribute<FunctionToolAttribute>()!;
        var instance = new TestToolClass();
        var tool = new MethodTool(method, instance, attribute);

        // Act
        var result = await tool.ExecuteAsync("test");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_MethodThrows_ShouldReturnError()
    {
        // Arrange
        var method = typeof(TestToolClass).GetMethod(nameof(TestToolClass.ThrowingMethod))!;
        var attribute = method.GetCustomAttribute<FunctionToolAttribute>()!;
        var instance = new TestToolClass();
        var tool = new MethodTool(method, instance, attribute);

        // Act
        var result = await tool.ExecuteAsync("test");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Test exception");
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ShouldThrow()
    {
        // Arrange
        var method = typeof(TestToolClass).GetMethod(
            nameof(TestToolClass.MethodWithCancellationToken)
        )!;
        var attribute = method.GetCustomAttribute<FunctionToolAttribute>()!;
        var instance = new TestToolClass();
        var tool = new MethodTool(method, instance, attribute);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            tool.ExecuteAsync("test", cts.Token)
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithDefaultParameter_ShouldUseDefault()
    {
        // Arrange
        var method = typeof(TestToolClass).GetMethod(
            nameof(TestToolClass.MethodWithDefaultParameter)
        )!;
        var attribute = method.GetCustomAttribute<FunctionToolAttribute>()!;
        var instance = new TestToolClass();
        var tool = new MethodTool(method, instance, attribute);

        // Act
        var result = await tool.ExecuteAsync("""{"name":"John"}""");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("25"); // default age
    }

    [Fact]
    public async Task ExecuteAsync_WithIntParameter_ShouldConvert()
    {
        // Arrange
        var method = typeof(TestToolClass).GetMethod(nameof(TestToolClass.IntMethod))!;
        var attribute = method.GetCustomAttribute<FunctionToolAttribute>()!;
        var instance = new TestToolClass();
        var tool = new MethodTool(method, instance, attribute);

        // Act
        var result = await tool.ExecuteAsync("""{"value":42}""");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("42");
    }

    [Fact]
    public async Task ExecuteAsync_WithBoolParameter_ShouldConvert()
    {
        // Arrange
        var method = typeof(TestToolClass).GetMethod(nameof(TestToolClass.BoolMethod))!;
        var attribute = method.GetCustomAttribute<FunctionToolAttribute>()!;
        var instance = new TestToolClass();
        var tool = new MethodTool(method, instance, attribute);

        // Act
        var result = await tool.ExecuteAsync("""{"flag":true}""");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("True");
    }

    [Fact]
    public async Task ExecuteAsync_WithDoubleParameter_ShouldConvert()
    {
        // Arrange
        var method = typeof(TestToolClass).GetMethod(nameof(TestToolClass.DoubleMethod))!;
        var attribute = method.GetCustomAttribute<FunctionToolAttribute>()!;
        var instance = new TestToolClass();
        var tool = new MethodTool(method, instance, attribute);

        // Act
        var result = await tool.ExecuteAsync("""{"value":3.14}""");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("3.14");
    }

    [Fact]
    public async Task ExecuteAsync_WithLongParameter_ShouldConvert()
    {
        // Arrange
        var method = typeof(TestToolClass).GetMethod(nameof(TestToolClass.LongMethod))!;
        var attribute = method.GetCustomAttribute<FunctionToolAttribute>()!;
        var instance = new TestToolClass();
        var tool = new MethodTool(method, instance, attribute);

        // Act
        var result = await tool.ExecuteAsync("""{"value":9999999999}""");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("9999999999");
    }

    [Fact]
    public async Task ExecuteAsync_WithDecimalParameter_ShouldConvert()
    {
        // Arrange
        var method = typeof(TestToolClass).GetMethod(nameof(TestToolClass.DecimalMethod))!;
        var attribute = method.GetCustomAttribute<FunctionToolAttribute>()!;
        var instance = new TestToolClass();
        var tool = new MethodTool(method, instance, attribute);

        // Act
        var result = await tool.ExecuteAsync("""{"value":123.45}""");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("123.45");
    }

    [Fact]
    public async Task ExecuteAsync_WithFloatParameter_ShouldConvert()
    {
        // Arrange
        var method = typeof(TestToolClass).GetMethod(nameof(TestToolClass.FloatMethod))!;
        var attribute = method.GetCustomAttribute<FunctionToolAttribute>()!;
        var instance = new TestToolClass();
        var tool = new MethodTool(method, instance, attribute);

        // Act
        var result = await tool.ExecuteAsync("""{"value":1.5}""");

        // Assert
        result.Success.Should().BeTrue();
    }

    #endregion

    #region Static Method Tests

    [Fact]
    public async Task ExecuteAsync_StaticMethod_ShouldExecuteWithoutInstance()
    {
        // Arrange
        var method = typeof(TestToolClass).GetMethod(nameof(TestToolClass.StaticMethod))!;
        var attribute = method.GetCustomAttribute<FunctionToolAttribute>()!;
        var tool = new MethodTool(method, null, attribute);

        // Act
        var result = await tool.ExecuteAsync("test");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("Static: test");
    }

    #endregion

    #region JSON Schema Type Tests

    [Fact]
    public void ParametersSchema_ShouldContainCorrectTypes()
    {
        // Arrange
        var method = typeof(TestToolClass).GetMethod(nameof(TestToolClass.AllTypesMethod))!;
        var attribute = method.GetCustomAttribute<FunctionToolAttribute>()!;
        var instance = new TestToolClass();

        // Act
        var tool = new MethodTool(method, instance, attribute);

        // Assert
        tool.ParametersSchema.Should().Contain("string");
        tool.ParametersSchema.Should().Contain("integer");
        tool.ParametersSchema.Should().Contain("number");
        tool.ParametersSchema.Should().Contain("boolean");
    }

    #endregion

    #region Helper Test Classes

    private sealed class TestToolClass
    {
        [FunctionTool("A simple method", Name = "simple_method")]
        public string SimpleMethod() => "simple";

        [FunctionTool("No name description")]
        public string MethodWithoutName() => "no name";

        [FunctionTool("Echo input", Name = "echo")]
        public string EchoMethod(string input) => $"Echo: {input}";

        [FunctionTool("Method with parameters", Name = "params")]
        public string MethodWithParameters(string name, int age) => $"Name: {name}, Age: {age}";

        [FunctionTool("Async method", Name = "async_method")]
        public async Task<string> AsyncMethod(string input)
        {
            await Task.Delay(1);
            return $"Async: {input}";
        }

        [FunctionTool("Async void method", Name = "async_void")]
        public async Task AsyncVoidMethod(string input)
        {
            await Task.Delay(1);
        }

        [FunctionTool("Returns ToolResult", Name = "tool_result")]
        public ToolResult ReturnsToolResult() => ToolResult.Ok("Custom result");

        [FunctionTool("Returns null", Name = "returns_null")]
        public string? ReturnsNull() => null;

        [FunctionTool("Throws exception", Name = "throwing")]
        public string ThrowingMethod() => throw new InvalidOperationException("Test exception");

        [FunctionTool("With CancellationToken", Name = "with_ct")]
        public async Task<string> MethodWithCancellationToken(
            string input,
            CancellationToken ct = default
        )
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(1, ct);
            return input;
        }

        [FunctionTool("With default parameter", Name = "default_param")]
        public string MethodWithDefaultParameter(string name, int age = 25) =>
            $"Name: {name}, Age: {age}";

        [FunctionTool("With category", Name = "with_category", Category = "TestCategory")]
        public string MethodWithCategory() => "categorized";

        [FunctionTool("No parameters method", Name = "no_params")]
        public string NoParamsMethod() => "no params";

        [FunctionTool("Integer method", Name = "int_method")]
        public string IntMethod(int value) => value.ToString();

        [FunctionTool("Boolean method", Name = "bool_method")]
        public string BoolMethod(bool flag) => flag.ToString();

        [FunctionTool("Double method", Name = "double_method")]
        public string DoubleMethod(double value) => value.ToString();

        [FunctionTool("Long method", Name = "long_method")]
        public string LongMethod(long value) => value.ToString();

        [FunctionTool("Decimal method", Name = "decimal_method")]
        public string DecimalMethod(decimal value) => value.ToString();

        [FunctionTool("Float method", Name = "float_method")]
        public string FloatMethod(float value) => value.ToString();

        [FunctionTool("Static method", Name = "static")]
        public static string StaticMethod(string input) => $"Static: {input}";

        [FunctionTool("All types parameters", Name = "all_types")]
        public string AllTypesMethod(string s, int i, double d, bool b) => $"{s},{i},{d},{b}";
    }

    #endregion
}
