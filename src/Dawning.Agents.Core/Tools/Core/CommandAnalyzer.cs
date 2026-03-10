using System.Text.RegularExpressions;

namespace Dawning.Agents.Core.Tools.Core;

/// <summary>
/// 命令安全分析器 — 检测危险命令、敏感路径访问、权限升级
/// </summary>
/// <remarks>
/// <para>BashTool 在执行命令前调用此分析器进行安全检查</para>
/// <para>支持可配置的白名单和黑名单</para>
/// </remarks>
public sealed partial class CommandAnalyzer
{
    private readonly CommandAnalyzerOptions _options;

    /// <summary>
    /// 创建命令分析器
    /// </summary>
    /// <param name="options">分析器选项（可选）</param>
    public CommandAnalyzer(CommandAnalyzerOptions? options = null)
    {
        _options = options ?? new CommandAnalyzerOptions();
    }

    /// <summary>
    /// 分析命令的安全性
    /// </summary>
    /// <param name="command">要分析的命令</param>
    /// <returns>分析结果</returns>
    public CommandAnalysisResult Analyze(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return CommandAnalysisResult.Blocked("Command cannot be empty");
        }

        var normalized = command.Trim();
        var lower = normalized.ToLowerInvariant();

        // Compound commands (&&, ||, ;, |) never get whitelisted —
        // each sub-command must be checked individually
        var isCompound = ContainsChainOperators(lower);
        var isWhitelisted = !isCompound && IsWhitelisted(lower);

        // 1. Destructive and privilege escalation checks (skip for whitelisted simple commands)
        if (!isWhitelisted)
        {
            // 2. Check destructive patterns (highest priority block)
            var destructiveCheck = CheckDestructivePatterns(lower);
            if (destructiveCheck != null)
            {
                return CommandAnalysisResult.Blocked(
                    $"Destructive command detected: {destructiveCheck}"
                );
            }

            // 3. Check privilege escalation
            var escalationCheck = CheckPrivilegeEscalation(lower);
            if (escalationCheck != null)
            {
                return CommandAnalysisResult.Blocked(
                    $"Privilege escalation detected: {escalationCheck}"
                );
            }
        }

        // 4. Warning checks always run (even for whitelisted commands)
        var pathCheck = CheckSensitivePaths(normalized);
        if (pathCheck != null)
        {
            return CommandAnalysisResult.Warning($"Access to sensitive path: {pathCheck}");
        }

        // 5. Check network activity
        var networkCheck = CheckNetworkActivity(lower);
        if (networkCheck != null)
        {
            return CommandAnalysisResult.Warning($"Network activity detected: {networkCheck}");
        }

        return CommandAnalysisResult.Allowed();
    }

    private static bool ContainsChainOperators(string command)
    {
        return command.Contains("&&", StringComparison.Ordinal)
            || command.Contains("||", StringComparison.Ordinal)
            || command.Contains(';')
            || command.Contains('|');
    }

    private bool IsWhitelisted(string command)
    {
        foreach (var prefix in _options.WhitelistedPrefixes)
        {
            if (command.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? CheckDestructivePatterns(string command)
    {
        foreach (var pattern in DestructivePatterns)
        {
            if (command.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return pattern;
            }
        }

        // Regex-based patterns for more complex matches
        if (ForkBombRegex().IsMatch(command))
        {
            return "fork bomb";
        }

        if (DevNullRedirectRegex().IsMatch(command))
        {
            return "redirect to device";
        }

        return null;
    }

    private static string? CheckPrivilegeEscalation(string command)
    {
        foreach (var pattern in PrivilegeEscalationPatterns)
        {
            if (command.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return pattern;
            }
        }

        if (ChmodDangerousRegex().IsMatch(command))
        {
            return "chmod with dangerous permissions";
        }

        if (ChownRootRegex().IsMatch(command))
        {
            return "chown to root";
        }

        return null;
    }

    private string? CheckSensitivePaths(string command)
    {
        foreach (var path in _options.SensitivePaths)
        {
            if (command.Contains(path, StringComparison.Ordinal))
            {
                return path;
            }
        }

        return null;
    }

    private static string? CheckNetworkActivity(string command)
    {
        foreach (var tool in NetworkTools)
        {
            // Check if the command starts with or contains the network tool as a command
            if (NetworkToolRegex(tool).IsMatch(command))
            {
                return tool;
            }
        }

        return null;
    }

    private static Regex NetworkToolRegex(string tool)
    {
        // Match the tool name as a standalone command (not part of a path/word)
        var pattern = $@"(?:^|[;&|]\s*){Regex.Escape(tool)}\b";
        return new Regex(pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
    }

    #region Patterns

    /// <summary>
    /// 破坏性命令模式
    /// </summary>
    private static readonly string[] DestructivePatterns =
    [
        "rm -rf /",
        "rm -rf /*",
        "rm -rf ~",
        "rm -rf $HOME",
        "mkfs.",
        "dd if=/dev/",
        "> /dev/sda",
        "shutdown",
        "reboot",
        "halt",
        "init 0",
        "init 6",
        "systemctl poweroff",
        "systemctl reboot",
    ];

    /// <summary>
    /// 权限升级模式
    /// </summary>
    private static readonly string[] PrivilegeEscalationPatterns =
    [
        "sudo ",
        "sudo\t",
        "su -",
        "su root",
        "doas ",
        "pkexec ",
    ];

    /// <summary>
    /// 网络工具
    /// </summary>
    private static readonly string[] NetworkTools =
    [
        "curl",
        "wget",
        "nc",
        "ncat",
        "netcat",
        "ssh",
        "scp",
        "rsync",
        "ftp",
        "sftp",
        "telnet",
    ];

    [GeneratedRegex(@":\(\)\s*\{.*\|.*&\s*\}\s*;", RegexOptions.Compiled)]
    private static partial Regex ForkBombRegex();

    [GeneratedRegex(@">\s*/dev/[a-z]", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex DevNullRedirectRegex();

    [GeneratedRegex(
        @"chmod\s+(-R\s+)?[0-7]*7[0-7]*\s+/(?!\S)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    )]
    private static partial Regex ChmodDangerousRegex();

    [GeneratedRegex(@"chown\s+(-R\s+)?root[:\s]", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ChownRootRegex();

    #endregion
}

/// <summary>
/// 命令分析器配置选项
/// </summary>
public class CommandAnalyzerOptions
{
    /// <summary>
    /// 白名单命令前缀 — 匹配的命令始终允许执行
    /// </summary>
    /// <remarks>
    /// 默认包含常用安全命令: echo, ls, pwd, cat, head, tail, wc, grep, find, which,
    /// git status/log/diff/branch, dotnet --version/--info, date, env
    /// </remarks>
    public List<string> WhitelistedPrefixes { get; set; } =
    [
        "echo ",
        "ls",
        "pwd",
        "cat ",
        "head ",
        "tail ",
        "wc ",
        "grep ",
        "find ",
        "which ",
        "type ",
        "file ",
        "git status",
        "git log",
        "git diff",
        "git branch",
        "git remote",
        "git tag",
        "git show",
        "dotnet --version",
        "dotnet --info",
        "dotnet --list-sdks",
        "dotnet --list-runtimes",
        "node --version",
        "npm --version",
        "python --version",
        "date",
        "env",
        "printenv",
        "uname",
        "whoami",
        "hostname",
        "df ",
        "du ",
        "free",
        "top -l 1",
        "ps ",
    ];

    /// <summary>
    /// 敏感路径 — 访问这些路径会产生警告
    /// </summary>
    public List<string> SensitivePaths { get; set; } =
    [
        "/etc/passwd",
        "/etc/shadow",
        "/etc/sudoers",
        "~/.ssh/",
        "$HOME/.ssh/",
        "~/.gnupg/",
        "~/.aws/",
        "~/.azure/",
        "/root/",
        "/var/log/auth",
    ];
}

/// <summary>
/// 命令分析结果
/// </summary>
public record CommandAnalysisResult
{
    /// <summary>
    /// 是否允许执行
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// 是否有警告（允许但需注意）
    /// </summary>
    public bool HasWarning { get; init; }

    /// <summary>
    /// 描述信息（阻止原因或警告内容）
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// 创建"允许"结果
    /// </summary>
    public static CommandAnalysisResult Allowed() => new() { IsAllowed = true };

    /// <summary>
    /// 创建"阻止"结果
    /// </summary>
    /// <param name="reason">阻止原因</param>
    public static CommandAnalysisResult Blocked(string reason) =>
        new() { IsAllowed = false, Message = reason };

    /// <summary>
    /// 创建"警告"结果（允许但有风险）
    /// </summary>
    /// <param name="warning">警告信息</param>
    public static CommandAnalysisResult Warning(string warning) =>
        new()
        {
            IsAllowed = true,
            HasWarning = true,
            Message = warning,
        };
}
