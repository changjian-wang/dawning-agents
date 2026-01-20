namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// 包管理工具配置选项
/// </summary>
/// <remarks>
/// appsettings.json 示例:
/// <code>
/// {
///   "PackageManager": {
///     "AllowWinget": true,
///     "AllowPip": true,
///     "AllowNpm": false,
///     "AllowDotnetTool": true,
///     "WhitelistedPackages": ["git", "nodejs", "python"],
///     "BlacklistedPackages": ["*hack*", "*crack*"],
///     "DefaultTimeoutSeconds": 300,
///     "RequireExplicitApproval": true
///   }
/// }
/// </code>
/// </remarks>
public class PackageManagerOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "PackageManager";

    /// <summary>
    /// 是否允许使用 winget（Windows 包管理器）
    /// </summary>
    public bool AllowWinget { get; set; } = true;

    /// <summary>
    /// 是否允许使用 pip（Python 包管理器）
    /// </summary>
    public bool AllowPip { get; set; } = true;

    /// <summary>
    /// 是否允许使用 npm（Node.js 包管理器）
    /// </summary>
    public bool AllowNpm { get; set; } = true;

    /// <summary>
    /// 是否允许使用 dotnet tool（.NET CLI 工具）
    /// </summary>
    public bool AllowDotnetTool { get; set; } = true;

    /// <summary>
    /// 白名单包列表（支持通配符 *）
    /// </summary>
    /// <remarks>
    /// 如果不为空，则只允许安装白名单中的包
    /// </remarks>
    public List<string> WhitelistedPackages { get; set; } = [];

    /// <summary>
    /// 黑名单包列表（支持通配符 *）
    /// </summary>
    /// <remarks>
    /// 黑名单中的包将被禁止安装
    /// </remarks>
    public List<string> BlacklistedPackages { get; set; } = [];

    /// <summary>
    /// 默认超时时间（秒）
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// 是否需要显式用户审批（除了工具本身的 RequiresConfirmation）
    /// </summary>
    public bool RequireExplicitApproval { get; set; } = true;

    /// <summary>
    /// pip 使用的 Python 可执行文件路径（可选）
    /// </summary>
    public string? PythonExecutable { get; set; }

    /// <summary>
    /// npm 可执行文件路径（可选）
    /// </summary>
    public string? NpmExecutable { get; set; }

    /// <summary>
    /// 验证配置
    /// </summary>
    public void Validate()
    {
        if (DefaultTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("DefaultTimeoutSeconds must be greater than 0");
        }
    }

    /// <summary>
    /// 检查包名是否在白名单中
    /// </summary>
    public bool IsWhitelisted(string packageName)
    {
        if (WhitelistedPackages.Count == 0)
        {
            return true; // 白名单为空时允许所有
        }

        return WhitelistedPackages.Any(pattern => MatchesPattern(packageName, pattern));
    }

    /// <summary>
    /// 检查包名是否在黑名单中
    /// </summary>
    public bool IsBlacklisted(string packageName)
    {
        return BlacklistedPackages.Any(pattern => MatchesPattern(packageName, pattern));
    }

    /// <summary>
    /// 简单的通配符匹配（支持 * 作为任意字符）
    /// </summary>
    private static bool MatchesPattern(string input, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        // 转换为正则表达式
        var regexPattern =
            "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(
            input,
            regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
    }
}
