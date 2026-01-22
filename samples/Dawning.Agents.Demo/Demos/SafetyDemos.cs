using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Safety;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Demo.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Demo.Demos;

/// <summary>
/// Safety &amp; Guardrails 演示
/// </summary>
public static class SafetyDemos
{
    /// <summary>
    /// 运行 Safety 演示
    /// </summary>
    public static async Task RunSafetyDemo(ILLMProvider provider)
    {
        ConsoleHelper.PrintSection("Safety & Guardrails 演示");
        Console.WriteLine("演示内容过滤、敏感数据检测、最大长度限制等安全功能\n");

        // 1. 敏感数据检测演示
        await RunSensitiveDataDemo();

        // 2. 最大长度限制演示
        await RunMaxLengthDemo();

        // 3. IGuardrail 接口说明
        PrintGuardrailInterface();

        ConsoleHelper.PrintSuccess("\nSafety 演示完成！");
    }

    private static async Task RunSensitiveDataDemo()
    {
        ConsoleHelper.PrintDivider("1. 敏感数据检测模式");

        Console.WriteLine("  SensitiveDataGuardrail 使用正则表达式检测敏感信息:\n");

        // 模拟敏感数据检测逻辑
        var patterns = new Dictionary<string, string>
        {
            ["信用卡"] = @"\b\d{4}[-\s]?\d{4}[-\s]?\d{4}[-\s]?\d{4}\b",
            ["邮箱"] = @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
            ["手机号"] = @"\b1[3-9]\d{9}\b",
            ["身份证"] = @"\b\d{17}[\dXx]\b",
        };

        var testCases = new[]
        {
            ("请帮我分析这段代码", true),
            ("我的信用卡号是 4111-1111-1111-1111", false),
            ("联系邮箱: test@example.com", false),
            ("电话号码: 13812345678", false),
            ("身份证: 110101199001011234", false),
            ("普通文本没有敏感信息", true),
        };

        foreach (var (text, expectedPass) in testCases)
        {
            var detected = DetectSensitiveData(text, patterns);
            var status = detected == null ? "✅ 安全" : "⚠️ 检测到敏感数据";

            Console.WriteLine($"  {status} \"{text}\"");

            if (detected != null)
            {
                ConsoleHelper.PrintColored($"       类型: {detected}", ConsoleColor.Yellow);
            }
        }

        Console.WriteLine();
        await Task.CompletedTask;
    }

    private static string? DetectSensitiveData(
        string text,
        Dictionary<string, string> patterns
    )
    {
        foreach (var (name, pattern) in patterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(text, pattern))
            {
                return name;
            }
        }
        return null;
    }

    private static async Task RunMaxLengthDemo()
    {
        ConsoleHelper.PrintDivider("2. 最大长度限制 (MaxLengthGuardrail)");

        const int maxLength = 50;
        Console.WriteLine($"  配置: 最大 {maxLength} 字符\n");

        var testCases = new[]
        {
            "短文本",
            "这是一段中等长度的文本内容",
            "这是一段超过50个字符的很长很长很长的文本内容，用于测试最大长度限制功能是否正常工作",
        };

        foreach (var text in testCases)
        {
            var isValid = text.Length <= maxLength;
            var status = isValid ? "✅ 通过" : "❌ 超长";
            var display = text.Length > 40 ? text[..40] + "..." : text;

            Console.WriteLine($"  {status} 长度={text.Length}: \"{display}\"");
        }

        Console.WriteLine();
        await Task.CompletedTask;
    }

    private static void PrintGuardrailInterface()
    {
        ConsoleHelper.PrintDivider("3. IGuardrail 接口说明");

        Console.WriteLine(
            """
              Dawning.Agents 的 Guardrail 系统:

              public interface IGuardrail
              {
                  string Name { get; }
                  Task<GuardrailResult> CheckAsync(string input, CancellationToken ct);
              }

              内置 Guardrails:
              - ContentModerator     - 基于 LLM 的内容审核
              - SensitiveDataGuardrail - 正则模式敏感数据检测
              - MaxLengthGuardrail   - 输入长度限制

              SafeAgent 包装模式:
              - 输入经过所有 InputGuardrails 检查
              - 输出经过所有 OutputGuardrails 检查
              - 任一 Guardrail 拒绝则整体拒绝

            """
        );
    }
}
