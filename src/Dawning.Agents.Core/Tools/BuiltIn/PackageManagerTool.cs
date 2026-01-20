using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Tools.BuiltIn;

/// <summary>
/// 包管理工具 - 提供跨平台软件包安装能力
/// </summary>
/// <remarks>
/// <para>支持的包管理器：winget (Windows), pip (Python), npm (Node.js), dotnet tool (.NET)</para>
/// <para>所有安装操作都是高风险操作，需要用户确认</para>
/// <para>支持白名单/黑名单机制控制可安装的包</para>
/// </remarks>
public class PackageManagerTool
{
    private readonly PackageManagerOptions _options;
    private readonly ILogger<PackageManagerTool> _logger;

    /// <summary>
    /// 创建包管理工具
    /// </summary>
    public PackageManagerTool(
        PackageManagerOptions? options = null,
        ILogger<PackageManagerTool>? logger = null
    )
    {
        _options = options ?? new PackageManagerOptions();
        _logger = logger ?? NullLogger<PackageManagerTool>.Instance;
    }

    #region Winget (Windows)

    /// <summary>
    /// 使用 winget 搜索 Windows 软件
    /// </summary>
    [FunctionTool(
        "在 Windows 上使用 winget 搜索可安装的软件包。返回匹配的软件列表及其 ID。",
        RiskLevel = ToolRiskLevel.Low,
        Category = "PackageManager"
    )]
    public async Task<ToolResult> WingetSearch(
        [ToolParameter("搜索关键词")] string query,
        [ToolParameter("最大返回数量（默认 10）")] int maxResults = 10,
        CancellationToken cancellationToken = default
    )
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ToolResult.Fail("winget 仅在 Windows 上可用");
        }

        if (!_options.AllowWinget)
        {
            return ToolResult.Fail("winget 已被配置禁用");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return ToolResult.Fail("搜索关键词不能为空");
        }

        _logger.LogDebug("Winget 搜索: {Query}", query);

        var result = await RunCommandAsync(
            "winget",
            $"search \"{query}\" --count {maxResults}",
            cancellationToken
        );

        return result;
    }

    /// <summary>
    /// 使用 winget 获取软件包详细信息
    /// </summary>
    [FunctionTool(
        "获取 winget 软件包的详细信息，包括版本、发布者、描述等。",
        RiskLevel = ToolRiskLevel.Low,
        Category = "PackageManager"
    )]
    public async Task<ToolResult> WingetShow(
        [ToolParameter("软件包 ID（如 Git.Git）")] string packageId,
        CancellationToken cancellationToken = default
    )
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ToolResult.Fail("winget 仅在 Windows 上可用");
        }

        if (!_options.AllowWinget)
        {
            return ToolResult.Fail("winget 已被配置禁用");
        }

        if (string.IsNullOrWhiteSpace(packageId))
        {
            return ToolResult.Fail("软件包 ID 不能为空");
        }

        _logger.LogDebug("Winget 查看详情: {PackageId}", packageId);

        return await RunCommandAsync(
            "winget",
            $"show \"{packageId}\"",
            cancellationToken
        );
    }

    /// <summary>
    /// 使用 winget 安装 Windows 软件（高风险）
    /// </summary>
    [FunctionTool(
        "在 Windows 上使用 winget 安装软件包。这是高风险操作，会修改系统状态，需要用户确认。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.High,
        Category = "PackageManager"
    )]
    public async Task<ToolResult> WingetInstall(
        [ToolParameter("软件包 ID（如 Git.Git）")] string packageId,
        [ToolParameter("指定版本（可选）")] string? version = null,
        [ToolParameter("静默安装（默认 true）")] bool silent = true,
        CancellationToken cancellationToken = default
    )
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ToolResult.Fail("winget 仅在 Windows 上可用");
        }

        if (!_options.AllowWinget)
        {
            return ToolResult.Fail("winget 已被配置禁用");
        }

        var validationResult = ValidatePackage(packageId);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        _logger.LogInformation("Winget 安装: {PackageId} 版本: {Version}", packageId, version ?? "最新");

        var args = $"install \"{packageId}\" --accept-package-agreements --accept-source-agreements";
        if (!string.IsNullOrWhiteSpace(version))
        {
            args += $" --version \"{version}\"";
        }
        if (silent)
        {
            args += " --silent";
        }

        return await RunCommandAsync("winget", args, cancellationToken, _options.DefaultTimeoutSeconds);
    }

    /// <summary>
    /// 使用 winget 卸载 Windows 软件（高风险）
    /// </summary>
    [FunctionTool(
        "在 Windows 上使用 winget 卸载软件包。这是高风险操作，会修改系统状态，需要用户确认。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.High,
        Category = "PackageManager"
    )]
    public async Task<ToolResult> WingetUninstall(
        [ToolParameter("软件包 ID（如 Git.Git）")] string packageId,
        [ToolParameter("静默卸载（默认 true）")] bool silent = true,
        CancellationToken cancellationToken = default
    )
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ToolResult.Fail("winget 仅在 Windows 上可用");
        }

        if (!_options.AllowWinget)
        {
            return ToolResult.Fail("winget 已被配置禁用");
        }

        if (string.IsNullOrWhiteSpace(packageId))
        {
            return ToolResult.Fail("软件包 ID 不能为空");
        }

        _logger.LogInformation("Winget 卸载: {PackageId}", packageId);

        var args = $"uninstall \"{packageId}\"";
        if (silent)
        {
            args += " --silent";
        }

        return await RunCommandAsync("winget", args, cancellationToken, _options.DefaultTimeoutSeconds);
    }

    /// <summary>
    /// 列出已安装的 winget 软件包
    /// </summary>
    [FunctionTool(
        "列出通过 winget 已安装的软件包。",
        RiskLevel = ToolRiskLevel.Low,
        Category = "PackageManager"
    )]
    public async Task<ToolResult> WingetList(
        [ToolParameter("过滤关键词（可选）")] string? filter = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ToolResult.Fail("winget 仅在 Windows 上可用");
        }

        if (!_options.AllowWinget)
        {
            return ToolResult.Fail("winget 已被配置禁用");
        }

        var args = "list";
        if (!string.IsNullOrWhiteSpace(filter))
        {
            args += $" --name \"{filter}\"";
        }

        return await RunCommandAsync("winget", args, cancellationToken);
    }

    #endregion

    #region Pip (Python)

    /// <summary>
    /// 使用 pip 搜索 Python 包（注意：pip search 已被禁用，改用 pypi.org 搜索建议）
    /// </summary>
    [FunctionTool(
        "列出已安装的 Python 包，或搜索特定包是否已安装。注意：pip search 命令已被禁用。",
        RiskLevel = ToolRiskLevel.Low,
        Category = "PackageManager"
    )]
    public async Task<ToolResult> PipList(
        [ToolParameter("过滤关键词（可选）")] string? filter = null,
        [ToolParameter("只显示过时的包")] bool outdated = false,
        CancellationToken cancellationToken = default
    )
    {
        if (!_options.AllowPip)
        {
            return ToolResult.Fail("pip 已被配置禁用");
        }

        var python = _options.PythonExecutable ?? GetDefaultPython();
        var args = "-m pip list";
        if (outdated)
        {
            args += " --outdated";
        }

        var result = await RunCommandAsync(python, args, cancellationToken);

        // 如果有过滤关键词，在结果中过滤
        if (result.Success && !string.IsNullOrWhiteSpace(filter))
        {
            var lines = result.Output.Split('\n')
                .Where(line => line.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (lines.Length > 0)
            {
                return ToolResult.Ok(string.Join("\n", lines));
            }
            return ToolResult.Ok($"未找到包含 '{filter}' 的已安装包");
        }

        return result;
    }

    /// <summary>
    /// 获取 Python 包的详细信息
    /// </summary>
    [FunctionTool(
        "获取已安装 Python 包的详细信息，包括版本、依赖等。",
        RiskLevel = ToolRiskLevel.Low,
        Category = "PackageManager"
    )]
    public async Task<ToolResult> PipShow(
        [ToolParameter("包名（如 requests）")] string packageName,
        CancellationToken cancellationToken = default
    )
    {
        if (!_options.AllowPip)
        {
            return ToolResult.Fail("pip 已被配置禁用");
        }

        if (string.IsNullOrWhiteSpace(packageName))
        {
            return ToolResult.Fail("包名不能为空");
        }

        var python = _options.PythonExecutable ?? GetDefaultPython();
        return await RunCommandAsync(python, $"-m pip show \"{packageName}\"", cancellationToken);
    }

    /// <summary>
    /// 使用 pip 安装 Python 包（高风险）
    /// </summary>
    [FunctionTool(
        "使用 pip 安装 Python 包。这是高风险操作，会修改 Python 环境，需要用户确认。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.High,
        Category = "PackageManager"
    )]
    public async Task<ToolResult> PipInstall(
        [ToolParameter("包名（如 requests 或 requests==2.28.0）")] string packageName,
        [ToolParameter("安装到用户目录（--user）")] bool userInstall = false,
        [ToolParameter("升级已存在的包（--upgrade）")] bool upgrade = false,
        CancellationToken cancellationToken = default
    )
    {
        if (!_options.AllowPip)
        {
            return ToolResult.Fail("pip 已被配置禁用");
        }

        // 提取纯包名（去除版本号）用于验证
        var purePackageName = packageName.Split(['=', '<', '>', '['])[0].Trim();
        var validationResult = ValidatePackage(purePackageName);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        _logger.LogInformation("Pip 安装: {PackageName}", packageName);

        var python = _options.PythonExecutable ?? GetDefaultPython();
        var args = $"-m pip install \"{packageName}\"";
        if (userInstall)
        {
            args += " --user";
        }
        if (upgrade)
        {
            args += " --upgrade";
        }

        return await RunCommandAsync(python, args, cancellationToken, _options.DefaultTimeoutSeconds);
    }

    /// <summary>
    /// 使用 pip 卸载 Python 包（高风险）
    /// </summary>
    [FunctionTool(
        "使用 pip 卸载 Python 包。这是高风险操作，会修改 Python 环境，需要用户确认。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.High,
        Category = "PackageManager"
    )]
    public async Task<ToolResult> PipUninstall(
        [ToolParameter("包名")] string packageName,
        CancellationToken cancellationToken = default
    )
    {
        if (!_options.AllowPip)
        {
            return ToolResult.Fail("pip 已被配置禁用");
        }

        if (string.IsNullOrWhiteSpace(packageName))
        {
            return ToolResult.Fail("包名不能为空");
        }

        _logger.LogInformation("Pip 卸载: {PackageName}", packageName);

        var python = _options.PythonExecutable ?? GetDefaultPython();
        return await RunCommandAsync(
            python,
            $"-m pip uninstall \"{packageName}\" -y",
            cancellationToken,
            _options.DefaultTimeoutSeconds
        );
    }

    #endregion

    #region Npm (Node.js)

    /// <summary>
    /// 使用 npm 搜索 Node.js 包
    /// </summary>
    [FunctionTool(
        "使用 npm 搜索 Node.js 包。返回匹配的包列表。",
        RiskLevel = ToolRiskLevel.Low,
        Category = "PackageManager"
    )]
    public async Task<ToolResult> NpmSearch(
        [ToolParameter("搜索关键词")] string query,
        CancellationToken cancellationToken = default
    )
    {
        if (!_options.AllowNpm)
        {
            return ToolResult.Fail("npm 已被配置禁用");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return ToolResult.Fail("搜索关键词不能为空");
        }

        _logger.LogDebug("Npm 搜索: {Query}", query);

        var npm = _options.NpmExecutable ?? "npm";
        return await RunCommandAsync(npm, $"search \"{query}\" --json", cancellationToken);
    }

    /// <summary>
    /// 获取 npm 包的详细信息
    /// </summary>
    [FunctionTool(
        "获取 npm 包的详细信息，包括版本、依赖、描述等。",
        RiskLevel = ToolRiskLevel.Low,
        Category = "PackageManager"
    )]
    public async Task<ToolResult> NpmView(
        [ToolParameter("包名（如 lodash）")] string packageName,
        CancellationToken cancellationToken = default
    )
    {
        if (!_options.AllowNpm)
        {
            return ToolResult.Fail("npm 已被配置禁用");
        }

        if (string.IsNullOrWhiteSpace(packageName))
        {
            return ToolResult.Fail("包名不能为空");
        }

        var npm = _options.NpmExecutable ?? "npm";
        return await RunCommandAsync(npm, $"view \"{packageName}\"", cancellationToken);
    }

    /// <summary>
    /// 使用 npm 安装 Node.js 包（高风险）
    /// </summary>
    [FunctionTool(
        "使用 npm 安装 Node.js 包。这是高风险操作，会修改 node_modules，需要用户确认。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.High,
        Category = "PackageManager"
    )]
    public async Task<ToolResult> NpmInstall(
        [ToolParameter("包名（如 lodash 或 lodash@4.17.21）")] string packageName,
        [ToolParameter("全局安装（-g）")] bool global = false,
        [ToolParameter("保存为开发依赖（--save-dev）")] bool saveDev = false,
        [ToolParameter("工作目录（可选）")] string? workingDirectory = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!_options.AllowNpm)
        {
            return ToolResult.Fail("npm 已被配置禁用");
        }

        // 提取纯包名用于验证
        var purePackageName = packageName.Split('@')[0].Trim();
        if (purePackageName.StartsWith('@'))
        {
            // Scoped package like @types/node
            var parts = packageName.Split('/');
            if (parts.Length > 1)
            {
                purePackageName = parts[0] + "/" + parts[1].Split('@')[0];
            }
        }

        var validationResult = ValidatePackage(purePackageName);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        _logger.LogInformation("Npm 安装: {PackageName} 全局: {Global}", packageName, global);

        var npm = _options.NpmExecutable ?? "npm";
        var args = $"install \"{packageName}\"";
        if (global)
        {
            args += " -g";
        }
        if (saveDev)
        {
            args += " --save-dev";
        }

        return await RunCommandAsync(
            npm,
            args,
            cancellationToken,
            _options.DefaultTimeoutSeconds,
            workingDirectory
        );
    }

    /// <summary>
    /// 使用 npm 卸载 Node.js 包（高风险）
    /// </summary>
    [FunctionTool(
        "使用 npm 卸载 Node.js 包。这是高风险操作，会修改 node_modules，需要用户确认。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.High,
        Category = "PackageManager"
    )]
    public async Task<ToolResult> NpmUninstall(
        [ToolParameter("包名")] string packageName,
        [ToolParameter("全局卸载（-g）")] bool global = false,
        [ToolParameter("工作目录（可选）")] string? workingDirectory = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!_options.AllowNpm)
        {
            return ToolResult.Fail("npm 已被配置禁用");
        }

        if (string.IsNullOrWhiteSpace(packageName))
        {
            return ToolResult.Fail("包名不能为空");
        }

        _logger.LogInformation("Npm 卸载: {PackageName} 全局: {Global}", packageName, global);

        var npm = _options.NpmExecutable ?? "npm";
        var args = $"uninstall \"{packageName}\"";
        if (global)
        {
            args += " -g";
        }

        return await RunCommandAsync(
            npm,
            args,
            cancellationToken,
            _options.DefaultTimeoutSeconds,
            workingDirectory
        );
    }

    /// <summary>
    /// 列出已安装的 npm 包
    /// </summary>
    [FunctionTool(
        "列出已安装的 npm 包。",
        RiskLevel = ToolRiskLevel.Low,
        Category = "PackageManager"
    )]
    public async Task<ToolResult> NpmList(
        [ToolParameter("全局包（-g）")] bool global = false,
        [ToolParameter("只显示顶层依赖（--depth=0）")] bool topLevelOnly = true,
        [ToolParameter("工作目录（可选）")] string? workingDirectory = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!_options.AllowNpm)
        {
            return ToolResult.Fail("npm 已被配置禁用");
        }

        var npm = _options.NpmExecutable ?? "npm";
        var args = "list";
        if (global)
        {
            args += " -g";
        }
        if (topLevelOnly)
        {
            args += " --depth=0";
        }

        return await RunCommandAsync(npm, args, cancellationToken, 60, workingDirectory);
    }

    #endregion

    #region Dotnet Tool (.NET)

    /// <summary>
    /// 搜索 .NET CLI 工具
    /// </summary>
    [FunctionTool(
        "在 NuGet.org 搜索 .NET CLI 工具。",
        RiskLevel = ToolRiskLevel.Low,
        Category = "PackageManager"
    )]
    public async Task<ToolResult> DotnetToolSearch(
        [ToolParameter("搜索关键词")] string query,
        CancellationToken cancellationToken = default
    )
    {
        if (!_options.AllowDotnetTool)
        {
            return ToolResult.Fail("dotnet tool 已被配置禁用");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return ToolResult.Fail("搜索关键词不能为空");
        }

        _logger.LogDebug("Dotnet tool 搜索: {Query}", query);

        return await RunCommandAsync("dotnet", $"tool search \"{query}\"", cancellationToken);
    }

    /// <summary>
    /// 安装 .NET CLI 工具（高风险）
    /// </summary>
    [FunctionTool(
        "安装 .NET CLI 工具。这是高风险操作，会安装可执行程序，需要用户确认。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.High,
        Category = "PackageManager"
    )]
    public async Task<ToolResult> DotnetToolInstall(
        [ToolParameter("工具包名（如 dotnet-ef）")] string packageName,
        [ToolParameter("全局安装（-g）")] bool global = true,
        [ToolParameter("指定版本（可选）")] string? version = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!_options.AllowDotnetTool)
        {
            return ToolResult.Fail("dotnet tool 已被配置禁用");
        }

        var validationResult = ValidatePackage(packageName);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        _logger.LogInformation("Dotnet tool 安装: {PackageName} 全局: {Global}", packageName, global);

        var args = $"tool install \"{packageName}\"";
        if (global)
        {
            args += " -g";
        }
        if (!string.IsNullOrWhiteSpace(version))
        {
            args += $" --version \"{version}\"";
        }

        return await RunCommandAsync("dotnet", args, cancellationToken, _options.DefaultTimeoutSeconds);
    }

    /// <summary>
    /// 卸载 .NET CLI 工具（高风险）
    /// </summary>
    [FunctionTool(
        "卸载 .NET CLI 工具。这是高风险操作，需要用户确认。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.High,
        Category = "PackageManager"
    )]
    public async Task<ToolResult> DotnetToolUninstall(
        [ToolParameter("工具包名")] string packageName,
        [ToolParameter("全局卸载（-g）")] bool global = true,
        CancellationToken cancellationToken = default
    )
    {
        if (!_options.AllowDotnetTool)
        {
            return ToolResult.Fail("dotnet tool 已被配置禁用");
        }

        if (string.IsNullOrWhiteSpace(packageName))
        {
            return ToolResult.Fail("工具包名不能为空");
        }

        _logger.LogInformation("Dotnet tool 卸载: {PackageName}", packageName);

        var args = $"tool uninstall \"{packageName}\"";
        if (global)
        {
            args += " -g";
        }

        return await RunCommandAsync("dotnet", args, cancellationToken, _options.DefaultTimeoutSeconds);
    }

    /// <summary>
    /// 列出已安装的 .NET CLI 工具
    /// </summary>
    [FunctionTool(
        "列出已安装的 .NET CLI 工具。",
        RiskLevel = ToolRiskLevel.Low,
        Category = "PackageManager"
    )]
    public async Task<ToolResult> DotnetToolList(
        [ToolParameter("全局工具（-g）")] bool global = true,
        CancellationToken cancellationToken = default
    )
    {
        if (!_options.AllowDotnetTool)
        {
            return ToolResult.Fail("dotnet tool 已被配置禁用");
        }

        var args = "tool list";
        if (global)
        {
            args += " -g";
        }

        return await RunCommandAsync("dotnet", args, cancellationToken);
    }

    /// <summary>
    /// 更新 .NET CLI 工具（高风险）
    /// </summary>
    [FunctionTool(
        "更新 .NET CLI 工具到最新版本。这是高风险操作，需要用户确认。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.High,
        Category = "PackageManager"
    )]
    public async Task<ToolResult> DotnetToolUpdate(
        [ToolParameter("工具包名")] string packageName,
        [ToolParameter("全局工具（-g）")] bool global = true,
        CancellationToken cancellationToken = default
    )
    {
        if (!_options.AllowDotnetTool)
        {
            return ToolResult.Fail("dotnet tool 已被配置禁用");
        }

        if (string.IsNullOrWhiteSpace(packageName))
        {
            return ToolResult.Fail("工具包名不能为空");
        }

        _logger.LogInformation("Dotnet tool 更新: {PackageName}", packageName);

        var args = $"tool update \"{packageName}\"";
        if (global)
        {
            args += " -g";
        }

        return await RunCommandAsync("dotnet", args, cancellationToken, _options.DefaultTimeoutSeconds);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 验证包名是否允许安装
    /// </summary>
    private ToolResult ValidatePackage(string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
        {
            return ToolResult.Fail("包名不能为空");
        }

        // 检查黑名单
        if (_options.IsBlacklisted(packageName))
        {
            _logger.LogWarning("包 {PackageName} 在黑名单中，拒绝安装", packageName);
            return ToolResult.Fail($"包 '{packageName}' 在黑名单中，禁止安装");
        }

        // 检查白名单
        if (!_options.IsWhitelisted(packageName))
        {
            _logger.LogWarning("包 {PackageName} 不在白名单中，拒绝安装", packageName);
            return ToolResult.Fail($"包 '{packageName}' 不在白名单中，禁止安装");
        }

        return ToolResult.Ok("验证通过");
    }

    /// <summary>
    /// 获取默认 Python 可执行文件
    /// </summary>
    private static string GetDefaultPython()
    {
        // Windows 上优先使用 python，Linux/Mac 上使用 python3
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "python" : "python3";
    }

    /// <summary>
    /// 执行命令并返回结果
    /// </summary>
    private async Task<ToolResult> RunCommandAsync(
        string command,
        string arguments,
        CancellationToken cancellationToken,
        int timeoutSeconds = 60,
        string? workingDirectory = null
    )
    {
        try
        {
            _logger.LogDebug("执行命令: {Command} {Arguments}", command, arguments);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
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

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // 忽略终止进程失败
                }

                return ToolResult.Fail($"命令执行超时（{timeoutSeconds} 秒）");
            }

            var result = new StringBuilder();
            result.AppendLine($"退出代码: {process.ExitCode}");

            if (outputBuilder.Length > 0)
            {
                result.AppendLine();
                result.AppendLine("--- 输出 ---");
                result.Append(TruncateOutput(outputBuilder.ToString()));
            }

            if (errorBuilder.Length > 0)
            {
                result.AppendLine();
                result.AppendLine("--- 错误/警告 ---");
                result.Append(TruncateOutput(errorBuilder.ToString()));
            }

            return process.ExitCode == 0
                ? ToolResult.Ok(result.ToString().TrimEnd())
                : ToolResult.Fail(result.ToString().TrimEnd());
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception)
        {
            return ToolResult.Fail($"命令 '{command}' 未找到。请确保已安装并添加到 PATH 环境变量。");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行命令失败: {Command} {Arguments}", command, arguments);
            return ToolResult.Fail($"执行命令失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 截断过长的输出
    /// </summary>
    private static string TruncateOutput(string output, int maxLength = 8000)
    {
        if (string.IsNullOrEmpty(output) || output.Length <= maxLength)
        {
            return output;
        }

        return output[..maxLength] + $"\n\n... (输出被截断，总长度 {output.Length} 字符)";
    }

    #endregion
}
