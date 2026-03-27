using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Dawning.Agents.Core.Tools.Core;

/// <summary>
/// Command security analyzer — detects dangerous commands, sensitive path access, and privilege escalation.
/// </summary>
/// <remarks>
/// <para>BashTool invokes this analyzer for security checks before executing commands.</para>
/// <para>Supports configurable allowlists and blocklists.</para>
/// </remarks>
public sealed partial class CommandAnalyzer
{
    private readonly CommandAnalyzerOptions _options;

    /// <summary>
    /// Creates a command analyzer.
    /// </summary>
    /// <param name="options">Analyzer options (optional).</param>
    public CommandAnalyzer(CommandAnalyzerOptions? options = null)
    {
        _options = options ?? new CommandAnalyzerOptions();
    }

    /// <summary>
    /// Analyzes command security.
    /// </summary>
    /// <param name="command">The command to analyze.</param>
    /// <returns>The analysis result.</returns>
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
        foreach (var pattern in s_destructivePatterns)
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
        foreach (var pattern in s_privilegeEscalationPatterns)
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
        foreach (var tool in s_networkTools)
        {
            // Check if the command starts with or contains the network tool as a command
            if (GetNetworkToolRegex(tool).IsMatch(command))
            {
                return tool;
            }
        }

        return null;
    }

    private static readonly ConcurrentDictionary<string, Regex> s_networkToolRegexCache = new(
        StringComparer.OrdinalIgnoreCase
    );

    private static Regex GetNetworkToolRegex(string tool)
    {
        return s_networkToolRegexCache.GetOrAdd(
            tool,
            static t =>
            {
                var pattern = $@"(?:^|[;&|]\s*){Regex.Escape(t)}\b";
                return new Regex(
                    pattern,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
                    TimeSpan.FromSeconds(1)
                );
            }
        );
    }

    #region Patterns

    /// <summary>
    /// Destructive command patterns.
    /// </summary>
    private static readonly string[] s_destructivePatterns =
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
    /// Privilege escalation patterns.
    /// </summary>
    private static readonly string[] s_privilegeEscalationPatterns =
    [
        "sudo ",
        "sudo\t",
        "su -",
        "su root",
        "doas ",
        "pkexec ",
    ];

    /// <summary>
    /// Network tools.
    /// </summary>
    private static readonly string[] s_networkTools =
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
/// Command analyzer configuration options.
/// </summary>
public class CommandAnalyzerOptions
{
    /// <summary>
    /// Allowlisted command prefixes — matching commands are always allowed to execute.
    /// </summary>
    /// <remarks>
    /// Defaults include common safe commands: echo, ls, pwd, cat, head, tail, wc, grep, find, which,
    /// git status/log/diff/branch, dotnet --version/--info, date, env.
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
    /// Sensitive paths — accessing these paths produces a warning.
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
/// Command analysis result.
/// </summary>
public record CommandAnalysisResult
{
    /// <summary>
    /// Gets a value indicating whether execution is allowed.
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// Gets a value indicating whether there is a warning (allowed but requires attention).
    /// </summary>
    public bool HasWarning { get; init; }

    /// <summary>
    /// Gets the description (block reason or warning content).
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Creates an "allowed" result.
    /// </summary>
    public static CommandAnalysisResult Allowed() => new() { IsAllowed = true };

    /// <summary>
    /// Creates a "blocked" result.
    /// </summary>
    /// <param name="reason">The block reason.</param>
    public static CommandAnalysisResult Blocked(string reason) =>
        new() { IsAllowed = false, Message = reason };

    /// <summary>
    /// Creates a "warning" result (allowed but risky).
    /// </summary>
    /// <param name="warning">The warning message.</param>
    public static CommandAnalysisResult Warning(string warning) =>
        new()
        {
            IsAllowed = true,
            HasWarning = true,
            Message = warning,
        };
}
