using System.Reflection;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Dawning.Agents.Tests.Tools;

/// <summary>
/// ToolScanner unit tests
/// </summary>
public class ToolScannerTests
{
    private readonly Mock<ILogger<ToolScanner>> _mockLogger;
    private readonly ToolScanner _scanner;

    public ToolScannerTests()
    {
        _mockLogger = new Mock<ILogger<ToolScanner>>();
        _scanner = new ToolScanner(_mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_UsesNullLogger()
    {
        var act = () => new ToolScanner(logger: null);

        act.Should().NotThrow();
    }

    #endregion

    #region ScanInstance Tests

    [Fact]
    public void ScanInstance_WithNullInstance_ThrowsArgumentNullException()
    {
        var act = () => _scanner.ScanInstance(null!).ToList();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ScanInstance_TestCalculatorTool_FindsAllMethods()
    {
        var instance = new TestCalculatorTool();

        var tools = _scanner.ScanInstance(instance).ToList();

        tools.Should().NotBeEmpty();
        tools.Should().Contain(t => t.Name == "Add");
        tools.Should().Contain(t => t.Name == "Multiply");
    }

    [Fact]
    public void ScanInstance_TestGreetingTool_FindsAllMethods()
    {
        var instance = new TestGreetingTool();

        var tools = _scanner.ScanInstance(instance).ToList();

        tools.Should().NotBeEmpty();
        tools.Should().Contain(t => t.Name == "Greet");
    }

    [Fact]
    public void ScanInstance_ClassWithNoTools_ReturnsEmpty()
    {
        var instance = new ClassWithNoTools();

        var tools = _scanner.ScanInstance(instance).ToList();

        tools.Should().BeEmpty();
    }

    [Fact]
    public void ScanInstance_ReturnsMethodTool()
    {
        var instance = new TestCalculatorTool();

        var tools = _scanner.ScanInstance(instance).ToList();

        tools.Should().AllBeAssignableTo<ITool>();
        tools.First().Should().BeOfType<MethodTool>();
    }

    #endregion

    #region ScanType Tests

    [Fact]
    public void ScanType_WithNullType_ThrowsArgumentNullException()
    {
        var act = () => _scanner.ScanType(null!).ToList();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ScanType_WithStaticTools_FindsMethods()
    {
        var tools = _scanner.ScanType(typeof(StaticToolClass)).ToList();

        tools.Should().NotBeEmpty();
        tools.Should().Contain(t => t.Name == "StaticTool");
    }

    [Fact]
    public void ScanType_WithNoStaticTools_ReturnsEmpty()
    {
        var tools = _scanner.ScanType(typeof(TestCalculatorTool)).ToList();

        // TestCalculatorTool has instance methods, not static
        tools.Should().BeEmpty();
    }

    #endregion

    #region ScanAssembly Tests

    [Fact]
    public void ScanAssembly_WithNullAssembly_ThrowsArgumentNullException()
    {
        var act = () => _scanner.ScanAssembly(null!).ToList();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ScanAssembly_CoreAssembly_FindsTools()
    {
        var assembly = typeof(ToolRegistry).Assembly;

        var tools = _scanner.ScanAssembly(assembly).ToList();

        // Core assembly may or may not have FunctionTool-decorated methods
        // Just verify no exception
        tools.Should().NotBeNull();
    }

    #endregion

    #region Tool Properties Tests

    [Fact]
    public void ScannedTool_HasCorrectName()
    {
        var instance = new TestCalculatorTool();

        var tools = _scanner.ScanInstance(instance).ToList();
        var addTool = tools.First(t => t.Name == "Add");

        addTool.Name.Should().Be("Add");
    }

    [Fact]
    public void ScannedTool_HasDescription()
    {
        var instance = new TestCalculatorTool();

        var tools = _scanner.ScanInstance(instance).ToList();
        var addTool = tools.First(t => t.Name == "Add");

        addTool.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ScannedTool_HasCategory()
    {
        var instance = new TestCalculatorTool();

        var tools = _scanner.ScanInstance(instance).ToList();
        var addTool = tools.First(t => t.Name == "Add");

        addTool.Category.Should().Be("TestCalc");
    }

    [Fact]
    public void ScannedTool_HasParametersSchema()
    {
        var instance = new TestCalculatorTool();

        var tools = _scanner.ScanInstance(instance).ToList();
        var addTool = tools.First(t => t.Name == "Add");

        addTool.ParametersSchema.Should().NotBeNullOrEmpty();
        addTool.ParametersSchema.Should().Contain("a");
    }

    #endregion

    #region Test Helpers

    private class ClassWithNoTools
    {
        public string RegularMethod() => "Not a tool";
    }

    private static class StaticToolClass
    {
        [FunctionTool("A static tool for testing")]
        public static string StaticTool() => "Static result";
    }

    /// <summary>
    /// Test calculator tool (replaces the deleted MathTool)
    /// </summary>
    private class TestCalculatorTool
    {
        [FunctionTool("Add two numbers", Category = "TestCalc")]
        public string Add(
            [ToolParameter("First number")] double a,
            [ToolParameter("Second number")] double b
        ) => (a + b).ToString();

        [FunctionTool("Multiply two numbers", Category = "TestCalc")]
        public string Multiply(
            [ToolParameter("First number")] double a,
            [ToolParameter("Second number")] double b
        ) => (a * b).ToString();
    }

    /// <summary>
    /// Test greeting tool (replaces the deleted DateTimeTool)
    /// </summary>
    private class TestGreetingTool
    {
        [FunctionTool("Greet someone", Category = "TestGreeting")]
        public string Greet([ToolParameter("Name to greet")] string name) => $"Hello, {name}!";
    }

    #endregion
}
