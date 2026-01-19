using System.Data;
using Dawning.Agents.Abstractions.Tools;

namespace Dawning.Agents.Core.Tools.BuiltIn;

/// <summary>
/// 数学计算工具
/// </summary>
public class MathTool
{
    /// <summary>
    /// 计算数学表达式
    /// </summary>
    [FunctionTool("计算数学表达式，支持加减乘除、括号和取模运算", Category = "Math")]
    public string Calculate(
        [ToolParameter("数学表达式，如 '2 + 3 * 4' 或 '(10 + 5) / 3'")] string expression
    )
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return "错误: 表达式不能为空";
        }

        try
        {
            // 使用 DataTable.Compute 进行安全的数学计算
            var result = new DataTable().Compute(expression, null);
            return $"{expression} = {result}";
        }
        catch (Exception ex)
        {
            return $"计算错误: {ex.Message}";
        }
    }

    /// <summary>
    /// 基础数学运算
    /// </summary>
    [FunctionTool("执行基础数学运算", Category = "Math")]
    public string BasicMath(
        [ToolParameter("第一个数")] double a,
        [ToolParameter("第二个数")] double b,
        [ToolParameter("运算符: add(加), sub(减), mul(乘), div(除), mod(取模), pow(幂)")]
            string operation
    )
    {
        var result = operation.ToLowerInvariant() switch
        {
            "add" or "+" => $"{a} + {b} = {a + b}",
            "sub" or "-" => $"{a} - {b} = {a - b}",
            "mul" or "*" => $"{a} × {b} = {a * b}",
            "div" or "/" when b != 0 => $"{a} ÷ {b} = {a / b}",
            "div" or "/" => "错误: 除数不能为零",
            "mod" or "%" when b != 0 => $"{a} % {b} = {a % b}",
            "mod" or "%" => "错误: 除数不能为零",
            "pow" or "^" => $"{a} ^ {b} = {Math.Pow(a, b)}",
            _ => $"未知运算符: {operation}，支持: add, sub, mul, div, mod, pow",
        };

        return result;
    }

    /// <summary>
    /// 数学函数计算
    /// </summary>
    [FunctionTool("计算数学函数，如平方根、对数、三角函数等", Category = "Math")]
    public string MathFunction(
        [ToolParameter("数值")] double value,
        [ToolParameter(
            "函数名: sqrt(平方根), abs(绝对值), sin, cos, tan, log, log10, exp, ceil, floor, round"
        )]
            string function
    )
    {
        var result = function.ToLowerInvariant() switch
        {
            "sqrt" when value >= 0 => $"√{value} = {Math.Sqrt(value)}",
            "sqrt" => "错误: 不能对负数开平方根",
            "abs" => $"|{value}| = {Math.Abs(value)}",
            "sin" => $"sin({value}) = {Math.Sin(value)}",
            "cos" => $"cos({value}) = {Math.Cos(value)}",
            "tan" => $"tan({value}) = {Math.Tan(value)}",
            "log" when value > 0 => $"ln({value}) = {Math.Log(value)}",
            "log" => "错误: 对数的真数必须大于零",
            "log10" when value > 0 => $"log₁₀({value}) = {Math.Log10(value)}",
            "log10" => "错误: 对数的真数必须大于零",
            "exp" => $"e^{value} = {Math.Exp(value)}",
            "ceil" => $"ceil({value}) = {Math.Ceiling(value)}",
            "floor" => $"floor({value}) = {Math.Floor(value)}",
            "round" => $"round({value}) = {Math.Round(value)}",
            _ =>
                $"未知函数: {function}，支持: sqrt, abs, sin, cos, tan, log, log10, exp, ceil, floor, round",
        };

        return result;
    }

    /// <summary>
    /// 数值转换
    /// </summary>
    [FunctionTool("在不同进制之间转换数值", Category = "Math")]
    public string ConvertBase(
        [ToolParameter("要转换的数值（字符串形式）")] string value,
        [ToolParameter("源进制 (2, 8, 10, 16)")] int fromBase,
        [ToolParameter("目标进制 (2, 8, 10, 16)")] int toBase
    )
    {
        var validBases = new[] { 2, 8, 10, 16 };
        if (!validBases.Contains(fromBase) || !validBases.Contains(toBase))
        {
            return "错误: 进制必须是 2, 8, 10 或 16";
        }

        try
        {
            var decimalValue = Convert.ToInt64(value, fromBase);
            var result = Convert.ToString(decimalValue, toBase).ToUpperInvariant();
            return $"{value} (base {fromBase}) = {result} (base {toBase})";
        }
        catch (Exception ex)
        {
            return $"转换错误: {ex.Message}";
        }
    }

    /// <summary>
    /// 百分比计算
    /// </summary>
    [FunctionTool("计算百分比", Category = "Math")]
    public string Percentage(
        [ToolParameter("部分值")] double part,
        [ToolParameter("总值")] double total
    )
    {
        if (total == 0)
        {
            return "错误: 总值不能为零";
        }

        var percentage = (part / total) * 100;
        return $"{part} / {total} = {percentage:F2}%";
    }
}
