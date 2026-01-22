namespace Dawning.Agents.Demo.Helpers;

/// <summary>
/// 控制台输出辅助方法
/// </summary>
public static class ConsoleHelper
{
    public static void PrintTitle(string title)
    {
        Console.WriteLine($"\n╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine($"║  {title, -58} ║");
        Console.WriteLine($"╚══════════════════════════════════════════════════════════════╝\n");
    }

    public static void PrintSection(string title)
    {
        Console.WriteLine($"━━━ {title} ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    }

    public static void PrintDivider(string title)
    {
        Console.WriteLine($"\n┌─ {title} ─────────────────────────────────────────────┐");
    }

    public static void PrintSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ {message}");
        Console.ResetColor();
    }

    public static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void PrintInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"ℹ {message}");
        Console.ResetColor();
    }

    public static void PrintWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"⚠ {message}");
        Console.ResetColor();
    }

    public static void PrintDim(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void PrintColored(string message, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void PrintBanner()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║             Dawning.Agents 演示                           ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");
    }
}
