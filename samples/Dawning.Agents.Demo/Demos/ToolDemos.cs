using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Demo.Helpers;

namespace Dawning.Agents.Demo.Demos;

/// <summary>
/// 工具相关演示
/// </summary>
public static class ToolDemos
{
    /// <summary>
    /// 演示 PackageManagerTool 包管理工具
    /// </summary>
    public static async Task RunPackageManagerDemo(IToolRegistry registry)
    {
        ConsoleHelper.PrintTitle("📦 PackageManagerTool 演示");

        // 获取所有 PackageManager 类别的工具
        var pmTools = registry.GetToolsByCategory("PackageManager").ToList();

        Console.WriteLine($"\n已注册的包管理工具 ({pmTools.Count} 个):\n");

        // 按包管理器类型分组显示
        var wingetTools = pmTools.Where(t => t.Name.StartsWith("Winget")).ToList();
        var pipTools = pmTools.Where(t => t.Name.StartsWith("Pip")).ToList();
        var npmTools = pmTools.Where(t => t.Name.StartsWith("Npm")).ToList();
        var dotnetTools = pmTools.Where(t => t.Name.StartsWith("DotnetTool")).ToList();

        PrintToolGroup("Winget (Windows)", "🪟", wingetTools);
        PrintToolGroup("Pip (Python)", "🐍", pipTools);
        PrintToolGroup("Npm (Node.js)", "📦", npmTools);
        PrintToolGroup("Dotnet Tool (.NET)", "🔷", dotnetTools);

        // 演示工具执行
        ConsoleHelper.PrintDivider("📋 工具演示");

        Console.WriteLine("\n1️⃣ 演示 DotnetToolList (安全只读操作):\n");
        var dotnetListTool = pmTools.FirstOrDefault(t => t.Name == "DotnetToolList");
        if (dotnetListTool != null)
        {
            ConsoleHelper.PrintInfo($"执行 {dotnetListTool.Name}...");
            var result = await dotnetListTool.ExecuteAsync("{\"global\": true}");
            if (result.Success)
            {
                ConsoleHelper.PrintSuccess("执行成功:");
                // 只显示前 10 行
                var lines = result.Output.Split('\n').Take(15);
                foreach (var line in lines)
                {
                    Console.WriteLine($"  {line}");
                }
                if (result.Output.Split('\n').Length > 15)
                {
                    ConsoleHelper.PrintDim("  ... (更多输出已省略)");
                }
            }
            else
            {
                ConsoleHelper.PrintError($"执行失败: {result.Error}");
            }
        }

        Console.WriteLine("\n2️⃣ 演示 DotnetToolSearch (安全只读操作):\n");
        var dotnetSearchTool = pmTools.FirstOrDefault(t => t.Name == "DotnetToolSearch");
        if (dotnetSearchTool != null)
        {
            ConsoleHelper.PrintInfo("搜索 'dotnet-ef'...");
            var result = await dotnetSearchTool.ExecuteAsync("{\"query\": \"dotnet-ef\"}");
            if (result.Success)
            {
                ConsoleHelper.PrintSuccess("搜索结果:");
                var lines = result.Output.Split('\n').Take(10);
                foreach (var line in lines)
                {
                    Console.WriteLine($"  {line}");
                }
            }
            else
            {
                ConsoleHelper.PrintError($"搜索失败: {result.Error}");
            }
        }

        Console.WriteLine("\n3️⃣ 高风险操作演示 (模拟):\n");
        ConsoleHelper.PrintWarning("以下操作标记为高风险，实际执行时需要用户确认：");

        var highRiskTools = pmTools.Where(t => t.RiskLevel == ToolRiskLevel.High).Take(5);
        foreach (var tool in highRiskTools)
        {
            Console.WriteLine($"  🔴 {tool.Name}");
            ConsoleHelper.PrintDim(
                $"     {tool.Description[..Math.Min(70, tool.Description.Length)]}..."
            );
        }

        // 统计信息
        ConsoleHelper.PrintDivider("📊 统计信息");
        Console.WriteLine($"  总工具数: {pmTools.Count}");
        Console.WriteLine(
            $"  低风险 (只读): {pmTools.Count(t => t.RiskLevel == ToolRiskLevel.Low)}"
        );
        Console.WriteLine(
            $"  高风险 (需确认): {pmTools.Count(t => t.RiskLevel == ToolRiskLevel.High)}"
        );
    }

    private static void PrintToolGroup(string groupName, string icon, IList<ITool> tools)
    {
        Console.WriteLine($"  {icon} {groupName} ({tools.Count} 个工具):");
        foreach (var tool in tools)
        {
            var riskIcon = tool.RiskLevel switch
            {
                ToolRiskLevel.Low => "🟢",
                ToolRiskLevel.Medium => "🟡",
                ToolRiskLevel.High => "🔴",
                _ => "⚪",
            };
            var confirmIcon = tool.RequiresConfirmation ? "🔒" : "";
            Console.WriteLine($"      {riskIcon} {tool.Name} {confirmIcon}");
            ConsoleHelper.PrintDim(
                $"         {tool.Description[..Math.Min(60, tool.Description.Length)]}..."
            );
        }
        Console.WriteLine();
    }
}
