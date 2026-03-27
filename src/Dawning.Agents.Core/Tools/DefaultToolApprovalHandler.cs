using System.Text.RegularExpressions;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Tools;

/// <summary>
/// Default tool approval handler — policy-based approval implementation.
/// </summary>
public sealed class DefaultToolApprovalHandler : IToolApprovalHandler
{
    private readonly ILogger<DefaultToolApprovalHandler> _logger;
    private readonly ApprovalStrategy _strategy;
    private readonly HashSet<string> _autoApprovedUrls;
    private readonly HashSet<string> _autoApprovedCommands;
    private readonly Lock _lock = new();

    private static readonly string[] s_trustedDomains =
    [
        "localhost",
        "127.0.0.1",
        "github.com",
        "api.github.com",
        "raw.githubusercontent.com",
        "microsoft.com",
        "azure.com",
        "nuget.org",
    ];

    private static readonly string[] s_dangerousCommands =
    [
        "rm -rf /",
        "rm -rf ~",
        "del /s /q c:\\",
        "format",
        "mkfs",
        ":(){:|:&};:",
        "dd if=/dev/zero",
        "chmod -R 777 /",
        "shutdown",
        "reboot",
    ];

    private static readonly string[] s_safeCommands =
    [
        "ls",
        "dir",
        "pwd",
        "cd",
        "cat",
        "type",
        "echo",
        "git status",
        "git log",
        "git diff",
        "git branch",
        "dotnet --version",
        "node --version",
        "python --version",
    ];

    /// <summary>
    /// Creates the default approval handler.
    /// </summary>
    /// <param name="strategy">The approval strategy.</param>
    /// <param name="logger">The logger.</param>
    public DefaultToolApprovalHandler(
        ApprovalStrategy strategy = ApprovalStrategy.RiskBased,
        ILogger<DefaultToolApprovalHandler>? logger = null
    )
    {
        _strategy = strategy;
        _logger = logger ?? NullLogger<DefaultToolApprovalHandler>.Instance;
        _autoApprovedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _autoApprovedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Requests approval for tool execution.
    /// </summary>
    public Task<bool> RequestApprovalAsync(
        ITool tool,
        string input,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(tool, nameof(tool));

        var approved = _strategy switch
        {
            ApprovalStrategy.AlwaysApprove => true,
            ApprovalStrategy.AlwaysDeny => false,
            ApprovalStrategy.RiskBased => ApproveByRisk(tool),
            ApprovalStrategy.Interactive => false, // Interactive mode defaults to deny; requires UI implementation
            _ => false,
        };

        _logger.LogDebug(
            "Tool {ToolName} approval result: {Approved} (strategy: {Strategy}, risk: {RiskLevel})",
            tool.Name,
            approved,
            _strategy,
            tool.RiskLevel
        );

        return Task.FromResult(approved);
    }

    /// <summary>
    /// Requests approval for URL access.
    /// </summary>
    public Task<bool> RequestUrlApprovalAsync(
        ITool tool,
        string url,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(tool, nameof(tool));
        ArgumentException.ThrowIfNullOrWhiteSpace(url, nameof(url));

        // Check if in auto-approval list
        lock (_lock)
        {
            if (_autoApprovedUrls.Contains(url) || IsAutoApprovedDomain(url))
            {
                _logger.LogDebug("URL {Url} is in the auto-approval list", url);
                return Task.FromResult(true);
            }
        }

        // Decide based on strategy
        var approved = _strategy switch
        {
            ApprovalStrategy.AlwaysApprove => true,
            ApprovalStrategy.AlwaysDeny => false,
            ApprovalStrategy.RiskBased => IsTrustedUrl(url),
            ApprovalStrategy.Interactive => false,
            _ => false,
        };

        _logger.LogDebug("URL {Url} approval result: {Approved}", url, approved);
        return Task.FromResult(approved);
    }

    /// <summary>
    /// Requests approval for terminal command execution.
    /// </summary>
    public Task<bool> RequestCommandApprovalAsync(
        ITool tool,
        string command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(tool, nameof(tool));
        ArgumentException.ThrowIfNullOrWhiteSpace(command, nameof(command));

        // Check if in auto-approval list
        lock (_lock)
        {
            if (_autoApprovedCommands.Contains(command))
            {
                _logger.LogDebug("Command {Command} is in the auto-approval list", command);
                return Task.FromResult(true);
            }
        }

        // Check for dangerous commands
        if (IsDangerousCommand(command))
        {
            _logger.LogWarning(
                "Dangerous command detected: {CommandName}",
                command.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                    ?? "unknown"
            );
            return Task.FromResult(false);
        }

        // Decide based on strategy
        var approved = _strategy switch
        {
            ApprovalStrategy.AlwaysApprove => true,
            ApprovalStrategy.AlwaysDeny => false,
            ApprovalStrategy.RiskBased => IsSafeCommand(command),
            ApprovalStrategy.Interactive => false,
            _ => false,
        };

        _logger.LogDebug("Command {Command} approval result: {Approved}", command, approved);
        return Task.FromResult(approved);
    }

    /// <summary>
    /// Adds an auto-approved URL.
    /// </summary>
    public void AddAutoApprovedUrl(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        lock (_lock)
        {
            _autoApprovedUrls.Add(url);
        }
    }

    /// <summary>
    /// Adds an auto-approved command.
    /// </summary>
    public void AddAutoApprovedCommand(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        lock (_lock)
        {
            _autoApprovedCommands.Add(command);
        }
    }

    private static bool ApproveByRisk(ITool tool)
    {
        // Low risk is auto-approved; Medium/High requires confirmation
        return tool.RiskLevel == ToolRiskLevel.Low && !tool.RequiresConfirmation;
    }

    private static bool IsTrustedUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var host = uri.Host.ToLowerInvariant();

            return s_trustedDomains.Any(d =>
                host == d || host.EndsWith("." + d, StringComparison.OrdinalIgnoreCase)
            );
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsAutoApprovedDomain(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host == "localhost" || uri.Host == "127.0.0.1";
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsDangerousCommand(string command)
    {
        var normalized = Regex
            .Replace(command, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1))
            .ToLowerInvariant()
            .Trim();
        return s_dangerousCommands.Any(d => normalized.Contains(d, StringComparison.Ordinal));
    }

    private static bool IsSafeCommand(string command)
    {
        var cmdLower = command.ToLowerInvariant().Trim();
        return s_safeCommands.Any(s =>
            cmdLower == s || cmdLower.StartsWith(s + " ", StringComparison.Ordinal)
        );
    }
}
