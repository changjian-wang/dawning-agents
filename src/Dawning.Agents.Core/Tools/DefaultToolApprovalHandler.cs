using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Tools;

/// <summary>
/// 默认工具审批处理器 - 基于策略的审批实现
/// </summary>
public class DefaultToolApprovalHandler : IToolApprovalHandler
{
    private readonly ILogger<DefaultToolApprovalHandler> _logger;
    private readonly ApprovalStrategy _strategy;
    private readonly HashSet<string> _autoApprovedUrls;
    private readonly HashSet<string> _autoApprovedCommands;

    /// <summary>
    /// 创建默认审批处理器
    /// </summary>
    /// <param name="strategy">审批策略</param>
    /// <param name="logger">日志记录器</param>
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
    /// 请求工具执行批准
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
            ApprovalStrategy.Interactive => false, // 交互式模式默认拒绝，需要 UI 实现
            _ => false,
        };

        _logger.LogDebug(
            "工具 {ToolName} 审批结果: {Approved} (策略: {Strategy}, 风险: {RiskLevel})",
            tool.Name,
            approved,
            _strategy,
            tool.RiskLevel
        );

        return Task.FromResult(approved);
    }

    /// <summary>
    /// 请求 URL 访问批准
    /// </summary>
    public Task<bool> RequestUrlApprovalAsync(
        ITool tool,
        string url,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(tool, nameof(tool));
        ArgumentException.ThrowIfNullOrWhiteSpace(url, nameof(url));

        // 检查是否在自动批准列表中
        if (_autoApprovedUrls.Contains(url) || IsAutoApprovedDomain(url))
        {
            _logger.LogDebug("URL {Url} 在自动批准列表中", url);
            return Task.FromResult(true);
        }

        // 根据策略决定
        var approved = _strategy switch
        {
            ApprovalStrategy.AlwaysApprove => true,
            ApprovalStrategy.AlwaysDeny => false,
            ApprovalStrategy.RiskBased => IsTrustedUrl(url),
            ApprovalStrategy.Interactive => false,
            _ => false,
        };

        _logger.LogDebug("URL {Url} 审批结果: {Approved}", url, approved);
        return Task.FromResult(approved);
    }

    /// <summary>
    /// 请求终端命令执行批准
    /// </summary>
    public Task<bool> RequestCommandApprovalAsync(
        ITool tool,
        string command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(tool, nameof(tool));
        ArgumentException.ThrowIfNullOrWhiteSpace(command, nameof(command));

        // 检查是否在自动批准列表中
        if (_autoApprovedCommands.Contains(command))
        {
            _logger.LogDebug("命令 {Command} 在自动批准列表中", command);
            return Task.FromResult(true);
        }

        // 检查危险命令
        if (IsDangerousCommand(command))
        {
            _logger.LogWarning("检测到危险命令: {Command}", command);
            return Task.FromResult(false);
        }

        // 根据策略决定
        var approved = _strategy switch
        {
            ApprovalStrategy.AlwaysApprove => true,
            ApprovalStrategy.AlwaysDeny => false,
            ApprovalStrategy.RiskBased => IsSafeCommand(command),
            ApprovalStrategy.Interactive => false,
            _ => false,
        };

        _logger.LogDebug("命令 {Command} 审批结果: {Approved}", command, approved);
        return Task.FromResult(approved);
    }

    /// <summary>
    /// 添加自动批准的 URL
    /// </summary>
    public void AddAutoApprovedUrl(string url)
    {
        _autoApprovedUrls.Add(url);
    }

    /// <summary>
    /// 添加自动批准的命令
    /// </summary>
    public void AddAutoApprovedCommand(string command)
    {
        _autoApprovedCommands.Add(command);
    }

    private static bool ApproveByRisk(ITool tool)
    {
        // Low 风险自动批准，Medium/High 需要确认
        return tool.RiskLevel == ToolRiskLevel.Low && !tool.RequiresConfirmation;
    }

    private static bool IsTrustedUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var host = uri.Host.ToLowerInvariant();

            // 常见可信域名
            var trustedDomains = new[]
            {
                "localhost",
                "127.0.0.1",
                "github.com",
                "api.github.com",
                "raw.githubusercontent.com",
                "microsoft.com",
                "azure.com",
                "nuget.org",
            };

            return trustedDomains.Any(d => host == d || host.EndsWith("." + d));
        }
        catch
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
        catch
        {
            return false;
        }
    }

    private static bool IsDangerousCommand(string command)
    {
        var dangerous = new[]
        {
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
        };

        var cmdLower = command.ToLowerInvariant();
        return dangerous.Any(d => cmdLower.Contains(d));
    }

    private static bool IsSafeCommand(string command)
    {
        var safeCommands = new[]
        {
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
        };

        var cmdLower = command.ToLowerInvariant().Trim();
        return safeCommands.Any(s => cmdLower.StartsWith(s));
    }
}
