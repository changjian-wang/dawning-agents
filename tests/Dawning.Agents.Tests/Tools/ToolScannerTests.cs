using System.Reflection;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools;
using Dawning.Agents.Core.Tools.BuiltIn;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Dawning.Agents.Tests.Tools;

/// <summary>
/// ToolScanner 单元测试
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
    public void ScanInstance_MathTool_FindsAllMethods()
    {
        var instance = new MathTool();

        var tools = _scanner.ScanInstance(instance).ToList();

        tools.Should().NotBeEmpty();
        tools.Should().Contain(t => t.Name == "Calculate");
        tools.Should().Contain(t => t.Name == "BasicMath");
        tools.Should().Contain(t => t.Name == "MathFunction");
    }

    [Fact]
    public void ScanInstance_DateTimeTool_FindsAllMethods()
    {
        var instance = new DateTimeTool();

        var tools = _scanner.ScanInstance(instance).ToList();

        tools.Should().NotBeEmpty();
        tools.Should().Contain(t => t.Name == "GetCurrentDateTime");
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
        var instance = new MathTool();

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
        var tools = _scanner.ScanType(typeof(MathTool)).ToList();

        // MathTool has instance methods, not static
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
    public void ScanAssembly_CoreAssembly_FindsBuiltInTools()
    {
        var assembly = typeof(MathTool).Assembly;

        var tools = _scanner.ScanAssembly(assembly).ToList();

        tools.Should().NotBeEmpty();
    }

    #endregion

    #region Tool Properties Tests

    [Fact]
    public void ScannedTool_HasCorrectName()
    {
        var instance = new MathTool();

        var tools = _scanner.ScanInstance(instance).ToList();
        var calculateTool = tools.First(t => t.Name == "Calculate");

        calculateTool.Name.Should().Be("Calculate");
    }

    [Fact]
    public void ScannedTool_HasDescription()
    {
        var instance = new MathTool();

        var tools = _scanner.ScanInstance(instance).ToList();
        var calculateTool = tools.First(t => t.Name == "Calculate");

        calculateTool.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ScannedTool_HasCategory()
    {
        var instance = new MathTool();

        var tools = _scanner.ScanInstance(instance).ToList();
        var calculateTool = tools.First(t => t.Name == "Calculate");

        calculateTool.Category.Should().Be("Math");
    }

    [Fact]
    public void ScannedTool_HasParametersSchema()
    {
        var instance = new MathTool();

        var tools = _scanner.ScanInstance(instance).ToList();
        var calculateTool = tools.First(t => t.Name == "Calculate");

        calculateTool.ParametersSchema.Should().NotBeNullOrEmpty();
        calculateTool.ParametersSchema.Should().Contain("expression");
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

    #endregion
}
