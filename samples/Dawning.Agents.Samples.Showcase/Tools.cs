using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools;

namespace Dawning.Agents.Samples.Showcase;

/// <summary>
/// 天气查询工具 — 演示自定义 ITool 实现
/// </summary>
public class WeatherTool : ITool
{
    public string Name => "get_weather";
    public string Description => "获取指定城市的天气信息";
    public string ParametersSchema =>
        """{"type":"object","properties":{"city":{"type":"string"}},"required":["city"]}""";
    public bool RequiresConfirmation => false;
    public ToolRiskLevel RiskLevel => ToolRiskLevel.Low;
    public string? Category => "weather";

    public Task<ToolResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        var city = "Unknown";
        var match = System.Text.RegularExpressions.Regex.Match(
            input,
            "\"city\"\\s*:\\s*\"([^\"]+)\""
        );
        if (match.Success)
        {
            city = match.Groups[1].Value;
        }

        return Task.FromResult(
            ToolResult.Ok($"{{\"city\":\"{city}\",\"temp\":\"22°C\",\"condition\":\"晴\"}}")
        );
    }
}

/// <summary>
/// 数学工具集 — 演示 [FunctionTool] 属性
/// </summary>
public class MathTools
{
    [FunctionTool("计算两个数字的和", Name = "add")]
    public static string Add(int a, int b) => (a + b).ToString();

    [FunctionTool("计算两个数字的乘积", Name = "multiply")]
    public static string Multiply(int a, int b) => (a * b).ToString();
}
