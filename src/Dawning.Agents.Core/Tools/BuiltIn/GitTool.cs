using System.Diagnostics;
using System.Text;
using Dawning.Agents.Abstractions.Tools;

namespace Dawning.Agents.Core.Tools.BuiltIn;

/// <summary>
/// Git 版本控制工具 - 提供 Git 操作能力
/// </summary>
/// <remarks>
/// <para>支持常用的 Git 操作：状态、提交、分支、差异等</para>
/// <para>修改类操作（commit、push、checkout 等）需要用户确认</para>
/// </remarks>
public class GitTool
{
    /// <summary>
    /// 获取 Git 仓库状态
    /// </summary>
    [FunctionTool("获取 Git 仓库的当前状态（git status）。", Category = "Git")]
    public async Task<ToolResult> GitStatus(
        [ToolParameter("Git 仓库目录路径")] string repositoryPath,
        CancellationToken cancellationToken = default
    )
    {
        return await RunGitCommand(repositoryPath, "status", cancellationToken);
    }

    /// <summary>
    /// 获取文件差异
    /// </summary>
    [FunctionTool("获取工作区或暂存区的文件差异（git diff）。", Category = "Git")]
    public async Task<ToolResult> GitDiff(
        [ToolParameter("Git 仓库目录路径")] string repositoryPath,
        [ToolParameter("是否显示暂存区差异")] bool staged = false,
        [ToolParameter("指定文件路径（可选）")] string? filePath = null,
        CancellationToken cancellationToken = default
    )
    {
        var args = "diff";
        if (staged)
        {
            args += " --staged";
        }
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            args += $" -- \"{filePath}\"";
        }

        return await RunGitCommand(repositoryPath, args, cancellationToken);
    }

    /// <summary>
    /// 获取提交历史
    /// </summary>
    [FunctionTool("获取提交历史（git log）。", Category = "Git")]
    public async Task<ToolResult> GitLog(
        [ToolParameter("Git 仓库目录路径")] string repositoryPath,
        [ToolParameter("显示的提交数量")] int count = 10,
        [ToolParameter("是否显示简洁格式")] bool oneline = true,
        CancellationToken cancellationToken = default
    )
    {
        var args = oneline ? $"log --oneline -n {count}" : $"log -n {count}";

        return await RunGitCommand(repositoryPath, args, cancellationToken);
    }

    /// <summary>
    /// 获取当前分支名
    /// </summary>
    [FunctionTool("获取当前所在的分支名称。", Category = "Git")]
    public async Task<ToolResult> GitBranch(
        [ToolParameter("Git 仓库目录路径")] string repositoryPath,
        [ToolParameter("是否显示所有分支（包括远程）")] bool all = false,
        CancellationToken cancellationToken = default
    )
    {
        var args = all ? "branch -a" : "branch";
        return await RunGitCommand(repositoryPath, args, cancellationToken);
    }

    /// <summary>
    /// 添加文件到暂存区（需要确认）
    /// </summary>
    [FunctionTool(
        "将文件添加到暂存区（git add）。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.Low,
        Category = "Git"
    )]
    public async Task<ToolResult> GitAdd(
        [ToolParameter("Git 仓库目录路径")] string repositoryPath,
        [ToolParameter("要添加的文件路径（'.' 表示所有）")] string filePath = ".",
        CancellationToken cancellationToken = default
    )
    {
        return await RunGitCommand(repositoryPath, $"add \"{filePath}\"", cancellationToken);
    }

    /// <summary>
    /// 提交更改（需要确认）
    /// </summary>
    [FunctionTool(
        "提交暂存区的更改（git commit）。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.Medium,
        Category = "Git"
    )]
    public async Task<ToolResult> GitCommit(
        [ToolParameter("Git 仓库目录路径")] string repositoryPath,
        [ToolParameter("提交消息")] string message,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return ToolResult.Fail("提交消息不能为空");
        }

        // 转义消息中的引号
        var escapedMessage = message.Replace("\"", "\\\"");
        return await RunGitCommand(
            repositoryPath,
            $"commit -m \"{escapedMessage}\"",
            cancellationToken
        );
    }

    /// <summary>
    /// 推送到远程仓库（高风险，需要确认）
    /// </summary>
    [FunctionTool(
        "推送本地提交到远程仓库（git push）。这会影响远程仓库。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.High,
        Category = "Git"
    )]
    public async Task<ToolResult> GitPush(
        [ToolParameter("Git 仓库目录路径")] string repositoryPath,
        [ToolParameter("远程名称（默认 origin）")] string remote = "origin",
        [ToolParameter("分支名称（可选，默认当前分支）")] string? branch = null,
        CancellationToken cancellationToken = default
    )
    {
        var args = string.IsNullOrWhiteSpace(branch) ? $"push {remote}" : $"push {remote} {branch}";

        return await RunGitCommand(repositoryPath, args, cancellationToken);
    }

    /// <summary>
    /// 从远程仓库拉取（需要确认）
    /// </summary>
    [FunctionTool(
        "从远程仓库拉取更新（git pull）。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.Medium,
        Category = "Git"
    )]
    public async Task<ToolResult> GitPull(
        [ToolParameter("Git 仓库目录路径")] string repositoryPath,
        [ToolParameter("远程名称（默认 origin）")] string remote = "origin",
        CancellationToken cancellationToken = default
    )
    {
        return await RunGitCommand(repositoryPath, $"pull {remote}", cancellationToken);
    }

    /// <summary>
    /// 切换分支（需要确认）
    /// </summary>
    [FunctionTool(
        "切换到指定分支（git checkout）。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.Medium,
        Category = "Git"
    )]
    public async Task<ToolResult> GitCheckout(
        [ToolParameter("Git 仓库目录路径")] string repositoryPath,
        [ToolParameter("分支名称")] string branch,
        [ToolParameter("是否创建新分支")] bool createNew = false,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(branch))
        {
            return ToolResult.Fail("分支名称不能为空");
        }

        var args = createNew ? $"checkout -b {branch}" : $"checkout {branch}";

        return await RunGitCommand(repositoryPath, args, cancellationToken);
    }

    /// <summary>
    /// 创建新分支（需要确认）
    /// </summary>
    [FunctionTool(
        "创建新分支（git branch）。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.Low,
        Category = "Git"
    )]
    public async Task<ToolResult> GitCreateBranch(
        [ToolParameter("Git 仓库目录路径")] string repositoryPath,
        [ToolParameter("新分支名称")] string branchName,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(branchName))
        {
            return ToolResult.Fail("分支名称不能为空");
        }

        return await RunGitCommand(repositoryPath, $"branch {branchName}", cancellationToken);
    }

    /// <summary>
    /// 删除分支（高风险，需要确认）
    /// </summary>
    [FunctionTool(
        "删除指定分支（git branch -d）。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.High,
        Category = "Git"
    )]
    public async Task<ToolResult> GitDeleteBranch(
        [ToolParameter("Git 仓库目录路径")] string repositoryPath,
        [ToolParameter("要删除的分支名称")] string branchName,
        [ToolParameter("是否强制删除（即使未合并）")] bool force = false,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(branchName))
        {
            return ToolResult.Fail("分支名称不能为空");
        }

        var flag = force ? "-D" : "-d";
        return await RunGitCommand(
            repositoryPath,
            $"branch {flag} {branchName}",
            cancellationToken
        );
    }

    /// <summary>
    /// 获取远程仓库信息
    /// </summary>
    [FunctionTool("获取远程仓库的 URL 信息（git remote -v）。", Category = "Git")]
    public async Task<ToolResult> GitRemote(
        [ToolParameter("Git 仓库目录路径")] string repositoryPath,
        CancellationToken cancellationToken = default
    )
    {
        return await RunGitCommand(repositoryPath, "remote -v", cancellationToken);
    }

    /// <summary>
    /// 暂存当前更改（需要确认）
    /// </summary>
    [FunctionTool(
        "暂存当前工作区的更改（git stash）。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.Medium,
        Category = "Git"
    )]
    public async Task<ToolResult> GitStash(
        [ToolParameter("Git 仓库目录路径")] string repositoryPath,
        [ToolParameter("暂存操作（push/pop/list/drop）")] string action = "push",
        [ToolParameter("暂存消息（仅 push 时使用）")] string? message = null,
        CancellationToken cancellationToken = default
    )
    {
        var args = action.ToLowerInvariant() switch
        {
            "push" when !string.IsNullOrWhiteSpace(message) => $"stash push -m \"{message}\"",
            "push" => "stash push",
            "pop" => "stash pop",
            "list" => "stash list",
            "drop" => "stash drop",
            _ => $"stash {action}",
        };

        return await RunGitCommand(repositoryPath, args, cancellationToken);
    }

    /// <summary>
    /// 撤销工作区更改（高风险，需要确认）
    /// </summary>
    [FunctionTool(
        "撤销工作区的更改（git restore）。这是不可逆操作。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.High,
        Category = "Git"
    )]
    public async Task<ToolResult> GitRestore(
        [ToolParameter("Git 仓库目录路径")] string repositoryPath,
        [ToolParameter("要撤销的文件路径（'.' 表示所有）")] string filePath,
        [ToolParameter("是否撤销暂存区")] bool staged = false,
        CancellationToken cancellationToken = default
    )
    {
        var args = staged ? $"restore --staged \"{filePath}\"" : $"restore \"{filePath}\"";

        return await RunGitCommand(repositoryPath, args, cancellationToken);
    }

    /// <summary>
    /// 重置到指定提交（高风险，需要确认）
    /// </summary>
    [FunctionTool(
        "重置当前分支到指定提交（git reset）。这可能导致数据丢失。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.High,
        Category = "Git"
    )]
    public async Task<ToolResult> GitReset(
        [ToolParameter("Git 仓库目录路径")] string repositoryPath,
        [ToolParameter("提交 ID 或引用（如 HEAD~1）")] string commit,
        [ToolParameter("重置模式（soft/mixed/hard）")] string mode = "mixed",
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(commit))
        {
            return ToolResult.Fail("提交引用不能为空");
        }

        var validModes = new[] { "soft", "mixed", "hard" };
        if (!validModes.Contains(mode.ToLowerInvariant()))
        {
            return ToolResult.Fail($"无效的重置模式: {mode}，可选: soft, mixed, hard");
        }

        return await RunGitCommand(repositoryPath, $"reset --{mode} {commit}", cancellationToken);
    }

    /// <summary>
    /// 获取文件的提交历史
    /// </summary>
    [FunctionTool("获取指定文件的提交历史（git log --follow）。", Category = "Git")]
    public async Task<ToolResult> GitFileHistory(
        [ToolParameter("Git 仓库目录路径")] string repositoryPath,
        [ToolParameter("文件路径")] string filePath,
        [ToolParameter("显示的提交数量")] int count = 10,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return ToolResult.Fail("文件路径不能为空");
        }

        return await RunGitCommand(
            repositoryPath,
            $"log --oneline --follow -n {count} -- \"{filePath}\"",
            cancellationToken
        );
    }

    /// <summary>
    /// 查看文件每行的最后修改信息
    /// </summary>
    [FunctionTool("查看文件每行的最后修改信息（git blame）。", Category = "Git")]
    public async Task<ToolResult> GitBlame(
        [ToolParameter("Git 仓库目录路径")] string repositoryPath,
        [ToolParameter("文件路径")] string filePath,
        [ToolParameter("起始行号（可选）")] int startLine = 0,
        [ToolParameter("结束行号（可选）")] int endLine = 0,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return ToolResult.Fail("文件路径不能为空");
        }

        var args = "blame";
        if (startLine > 0 && endLine > 0)
        {
            args += $" -L {startLine},{endLine}";
        }
        args += $" \"{filePath}\"";

        return await RunGitCommand(repositoryPath, args, cancellationToken);
    }

    /// <summary>
    /// 执行 Git 命令
    /// </summary>
    private async Task<ToolResult> RunGitCommand(
        string repositoryPath,
        string arguments,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(repositoryPath))
            {
                return ToolResult.Fail("仓库路径不能为空");
            }

            if (!Directory.Exists(repositoryPath))
            {
                return ToolResult.Fail($"目录不存在: {repositoryPath}");
            }

            if (!Directory.Exists(Path.Combine(repositoryPath, ".git")))
            {
                return ToolResult.Fail($"不是有效的 Git 仓库: {repositoryPath}");
            }

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = repositoryPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            var output = outputBuilder.ToString().TrimEnd();
            var error = errorBuilder.ToString().TrimEnd();

            if (process.ExitCode == 0)
            {
                return ToolResult.Ok(
                    string.IsNullOrEmpty(output) ? "命令执行成功（无输出）" : output
                );
            }

            return ToolResult.Fail(
                string.IsNullOrEmpty(error)
                    ? $"Git 命令失败（退出代码: {process.ExitCode}）"
                    : error
            );
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"执行 Git 命令失败: {ex.Message}");
        }
    }
}
