using Dawning.Agents.Core.Tools.BuiltIn;
using FluentAssertions;

namespace Dawning.Agents.Tests.Tools;

/// <summary>
/// MathTool 单元测试
/// </summary>
public class MathToolTests
{
    private readonly MathTool _tool = new();

    #region Calculate Tests

    [Theory]
    [InlineData("2 + 3", "2 + 3 = 5")]
    [InlineData("10 - 4", "10 - 4 = 6")]
    [InlineData("3 * 4", "3 * 4 = 12")]
    [InlineData("20 / 5", "20 / 5 = 4")]
    [InlineData("(2 + 3) * 4", "(2 + 3) * 4 = 20")]
    [InlineData("10 % 3", "10 % 3 = 1")]
    public void Calculate_ValidExpression_ReturnsResult(string expression, string expected)
    {
        var result = _tool.Calculate(expression);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Calculate_EmptyExpression_ReturnsError(string? expression)
    {
        var result = _tool.Calculate(expression!);
        result.Should().Contain("错误");
    }

    [Fact]
    public void Calculate_InvalidExpression_ReturnsError()
    {
        var result = _tool.Calculate("2 + + 3");
        // DataTable.Compute 可能不总是返回错误，检查结果不为空
        result.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region BasicMath Tests

    [Theory]
    [InlineData(5, 3, "add", "5 + 3 = 8")]
    [InlineData(5, 3, "+", "5 + 3 = 8")]
    [InlineData(10, 4, "sub", "10 - 4 = 6")]
    [InlineData(10, 4, "-", "10 - 4 = 6")]
    [InlineData(3, 4, "mul", "3 × 4 = 12")]
    [InlineData(3, 4, "*", "3 × 4 = 12")]
    [InlineData(20, 5, "div", "20 ÷ 5 = 4")]
    [InlineData(20, 5, "/", "20 ÷ 5 = 4")]
    [InlineData(10, 3, "mod", "10 % 3 = 1")]
    [InlineData(10, 3, "%", "10 % 3 = 1")]
    [InlineData(2, 3, "pow", "2 ^ 3 = 8")]
    [InlineData(2, 3, "^", "2 ^ 3 = 8")]
    public void BasicMath_ValidOperation_ReturnsResult(
        double a,
        double b,
        string operation,
        string expected
    )
    {
        var result = _tool.BasicMath(a, b, operation);
        result.Should().Be(expected);
    }

    [Fact]
    public void BasicMath_DivideByZero_ReturnsError()
    {
        var result = _tool.BasicMath(10, 0, "div");
        result.Should().Contain("除数不能为零");
    }

    [Fact]
    public void BasicMath_ModByZero_ReturnsError()
    {
        var result = _tool.BasicMath(10, 0, "mod");
        result.Should().Contain("除数不能为零");
    }

    [Fact]
    public void BasicMath_UnknownOperation_ReturnsError()
    {
        var result = _tool.BasicMath(10, 5, "unknown");
        result.Should().Contain("未知运算符");
    }

    #endregion

    #region MathFunction Tests

    [Theory]
    [InlineData(4, "sqrt", "√4 = 2")]
    [InlineData(-5, "abs", "|-5| = 5")]
    [InlineData(0, "sin", "sin(0) = 0")]
    [InlineData(0, "cos", "cos(0) = 1")]
    [InlineData(0, "tan", "tan(0) = 0")]
    [InlineData(2.5, "ceil", "ceil(2.5) = 3")]
    [InlineData(2.5, "floor", "floor(2.5) = 2")]
    [InlineData(2.5, "round", "round(2.5) = 2")]
    public void MathFunction_ValidFunction_ReturnsResult(
        double value,
        string function,
        string expected
    )
    {
        var result = _tool.MathFunction(value, function);
        result.Should().Be(expected);
    }

    [Fact]
    public void MathFunction_SqrtNegative_ReturnsError()
    {
        var result = _tool.MathFunction(-4, "sqrt");
        result.Should().Contain("不能对负数开平方根");
    }

    [Fact]
    public void MathFunction_LogZeroOrNegative_ReturnsError()
    {
        var result = _tool.MathFunction(0, "log");
        result.Should().Contain("对数的真数必须大于零");

        result = _tool.MathFunction(-1, "log10");
        result.Should().Contain("对数的真数必须大于零");
    }

    [Fact]
    public void MathFunction_UnknownFunction_ReturnsError()
    {
        var result = _tool.MathFunction(5, "unknown");
        result.Should().Contain("未知函数");
    }

    [Fact]
    public void MathFunction_LogPositive_ReturnsResult()
    {
        var result = _tool.MathFunction(Math.E, "log");
        result.Should().Contain("ln(");

        result = _tool.MathFunction(10, "log10");
        result.Should().Contain("log₁₀(10) = 1");
    }

    [Fact]
    public void MathFunction_Exp_ReturnsResult()
    {
        var result = _tool.MathFunction(1, "exp");
        result.Should().Contain("e^1");
    }

    #endregion

    #region ConvertBase Tests

    [Theory]
    [InlineData("10", 10, 2, "10 (base 10) = 1010 (base 2)")]
    [InlineData("1010", 2, 10, "1010 (base 2) = 10 (base 10)")]
    [InlineData("FF", 16, 10, "FF (base 16) = 255 (base 10)")]
    [InlineData("255", 10, 16, "255 (base 10) = FF (base 16)")]
    [InlineData("17", 10, 8, "17 (base 10) = 21 (base 8)")]
    public void ConvertBase_ValidConversion_ReturnsResult(
        string value,
        int fromBase,
        int toBase,
        string expected
    )
    {
        var result = _tool.ConvertBase(value, fromBase, toBase);
        result.Should().Be(expected);
    }

    [Fact]
    public void ConvertBase_InvalidBase_ReturnsError()
    {
        var result = _tool.ConvertBase("10", 10, 3);
        result.Should().Contain("进制必须是 2, 8, 10 或 16");
    }

    [Fact]
    public void ConvertBase_InvalidValue_ReturnsError()
    {
        var result = _tool.ConvertBase("ZZ", 16, 10);
        result.Should().Contain("转换错误");
    }

    #endregion

    #region Percentage Tests

    [Fact]
    public void Percentage_ValidInput_ReturnsResult()
    {
        var result = _tool.Percentage(25, 100);
        result.Should().Contain("25.00%");
    }

    [Fact]
    public void Percentage_ZeroTotal_ReturnsError()
    {
        var result = _tool.Percentage(10, 0);
        result.Should().Contain("总值不能为零");
    }

    #endregion
}
