using System.Diagnostics;
using System.Text;
using Dawning.Agents.Abstractions.Tools;

namespace Dawning.Agents.Core.Tools.BuiltIn;

/// <summary>
/// CSharpier 代码格式化工具 - 提供 C# 代码格式化能力
/// </summary>
/// <remarks>
/// <para>使用 CSharpier 进行代码格式化，确保代码风格一致性</para>
/// <para>支持格式化单个文件、目录或整个项目</para>
/// <para>
/// 需要先安装 CSharpier:
/// <code>dotnet tool install -g csharpier</code>
/// </para>
/// </remarks>
public class CSharpierTool
{
    private readonly CSharpierToolOptions _options;

    /// <summary>
    /// 创建 CSharpier 工具
    /// </summary>
    /// <param name="options">工具配置选项</param>
    public CSharpierTool(CSharpierToolOptions? options = null)
    {
        _options = options ?? new CSharpierToolOptions();
    }

    /// <summary>
    /// 格式化单个 C# 文件
    /// </summary>
    [FunctionTool(
        "使用 CSharpier 格式化单个 C# 文件，确保代码风格一致",
        RequiresConfirmation = false,
        RiskLevel = ToolRiskLevel.Low,
        Category = "CodeFormat"
    )]
    public async Task<ToolResult> FormatFile(
        [ToolParameter("要格式化的 C# 文件路径")] string filePath,
        [ToolParameter("是否只检查不修改（默认 false）")] bool checkOnly = false,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return ToolResult.Fail("文件路径不能为空");
            }

            if (!File.Exists(filePath))
            {
                return ToolResult.Fail($"文件不存在: {filePath}");
            }

            if (!filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                return ToolResult.Fail("只能格式化 .cs 文件");
            }

            var args = checkOnly ? $"--check \"{filePath}\"" : $"\"{filePath}\"";
            var result = await RunCSharpierAsync(args, cancellationToken);

            if (result.ExitCode == 0)
            {
                return checkOnly
                    ? ToolResult.Ok($"文件格式检查通过: {filePath}")
                    : ToolResult.Ok($"文件格式化成功: {filePath}");
            }
            else if (checkOnly && result.ExitCode == 1)
            {
                return ToolResult.Fail($"文件需要格式化: {filePath}\n{result.Output}");
            }
            else
            {
                return ToolResult.Fail($"格式化失败: {result.Output}");
            }
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"格式化出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 格式化目录中的所有 C# 文件
    /// </summary>
    [FunctionTool(
        "使用 CSharpier 格式化目录中的所有 C# 文件",
        RequiresConfirmation = false,
        RiskLevel = ToolRiskLevel.Low,
        Category = "CodeFormat"
    )]
    public async Task<ToolResult> FormatDirectory(
        [ToolParameter("要格式化的目录路径")] string directoryPath,
        [ToolParameter("是否只检查不修改（默认 false）")] bool checkOnly = false,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return ToolResult.Fail("目录路径不能为空");
            }

            if (!Directory.Exists(directoryPath))
            {
                return ToolResult.Fail($"目录不存在: {directoryPath}");
            }

            var args = checkOnly ? $"--check \"{directoryPath}\"" : $"\"{directoryPath}\"";
            var result = await RunCSharpierAsync(args, cancellationToken);

            if (result.ExitCode == 0)
            {
                return checkOnly
                    ? ToolResult.Ok($"目录格式检查通过: {directoryPath}")
                    : ToolResult.Ok($"目录格式化成功: {directoryPath}");
            }
            else if (checkOnly && result.ExitCode == 1)
            {
                return ToolResult.Fail($"目录中有文件需要格式化:\n{result.Output}");
            }
            else
            {
                return ToolResult.Fail($"格式化失败: {result.Output}");
            }
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"格式化出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 格式化代码字符串
    /// </summary>
    [FunctionTool(
        "使用 CSharpier 格式化 C# 代码字符串，返回格式化后的代码",
        RequiresConfirmation = false,
        RiskLevel = ToolRiskLevel.Low,
        Category = "CodeFormat"
    )]
    public async Task<ToolResult> FormatCode(
        [ToolParameter("要格式化的 C# 代码")] string code,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return ToolResult.Fail("代码不能为空");
            }

            // 创建临时文件
            var tempFile = Path.Combine(Path.GetTempPath(), $"csharpier_{Guid.NewGuid()}.cs");
            try
            {
                await File.WriteAllTextAsync(tempFile, code, cancellationToken);

                var result = await RunCSharpierAsync($"\"{tempFile}\"", cancellationToken);

                if (result.ExitCode == 0)
                {
                    var formattedCode = await File.ReadAllTextAsync(tempFile, cancellationToken);
                    return ToolResult.Ok(formattedCode);
                }
                else
                {
                    return ToolResult.Fail($"格式化失败: {result.Output}");
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"格式化出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 检查 CSharpier 是否已安装
    /// </summary>
    [FunctionTool(
        "检查 CSharpier 是否已安装",
        RequiresConfirmation = false,
        RiskLevel = ToolRiskLevel.Low,
        Category = "CodeFormat"
    )]
    public async Task<ToolResult> CheckInstallation(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await RunCSharpierAsync("--version", cancellationToken);

            if (result.ExitCode == 0)
            {
                return ToolResult.Ok($"CSharpier 已安装: {result.Output.Trim()}");
            }
            else
            {
                return ToolResult.Fail(
                    "CSharpier 未安装。请运行: dotnet tool install -g csharpier"
                );
            }
        }
        catch (Exception ex)
        {
            return ToolResult.Fail(
                $"无法检测 CSharpier: {ex.Message}\n请运行: dotnet tool install -g csharpier"
            );
        }
    }

    /// <summary>
    /// 安装 CSharpier
    /// </summary>
    [FunctionTool(
        "安装 CSharpier 全局工具",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.Medium,
        Category = "CodeFormat"
    )]
    public async Task<ToolResult> Install(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await RunDotnetAsync(
                "tool install -g csharpier",
                cancellationToken
            );

            if (result.ExitCode == 0 || result.Output.Contains("already installed"))
            {
                return ToolResult.Ok("CSharpier 安装成功");
            }
            else
            {
                return ToolResult.Fail($"安装失败: {result.Output}");
            }
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"安装出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取 CSharpier 格式化规则说明
    /// </summary>
    [FunctionTool(
        "获取 CSharpier 格式化规则说明，帮助理解代码风格要求",
        RequiresConfirmation = false,
        RiskLevel = ToolRiskLevel.Low,
        Category = "CodeFormat"
    )]
    public ToolResult GetFormattingRules()
    {
        var rules = """"
            # CSharpier 格式化规则

            ## 1. 长参数列表 - 每个参数独占一行
            ```csharp
            // ✅ 好 - 多参数换行
            public MyService(
                ILLMProvider llmProvider,
                IOptions<MyOptions> options,
                ILogger<MyService>? logger = null
            )
            {
            }

            // ❌ 避免 - 单行过长
            public MyService(ILLMProvider llmProvider, IOptions<MyOptions> options, ILogger<MyService>? logger = null)
            ```

            ## 2. 集合初始化 - 元素换行，尾随逗号
            ```csharp
            // ✅ 好
            var messages = new List<ChatMessage>
            {
                new("system", systemPrompt),
                new("user", userInput),
            };

            // ❌ 避免
            var messages = new List<ChatMessage> { new("system", systemPrompt), new("user", userInput) };
            ```

            ## 3. 方法链 - 每个调用独占一行
            ```csharp
            // ✅ 好
            var result = items
                .Where(x => x.IsActive)
                .Select(x => x.Name)
                .ToList();
            ```

            ## 4. if 语句 - 始终使用大括号
            ```csharp
            // ✅ 好
            if (condition)
            {
                DoSomething();
            }

            // ❌ 避免
            if (condition)
                DoSomething();
            ```

            ## 5. 字符串插值 - 保持可读性
            ```csharp
            // ✅ 好 - 短插值
            var message = $"Hello, {name}!";

            // ✅ 好 - 长插值换行
            var log = $"""
                Processing item {id}:
                  Name: {name}
                  Status: {status}
                """;
            ```

            ## 6. Lambda 表达式
            ```csharp
            // ✅ 好 - 简单 lambda
            items.Where(x => x.IsActive);

            // ✅ 好 - 复杂 lambda 换行
            items.Where(x =>
                x.IsActive &&
                x.CreatedAt > DateTime.UtcNow.AddDays(-7)
            );
            ```

            ## 7. 默认行宽: 100 字符

            ## 配置文件 (.csharpierrc.json)
            ```json
            {
              "printWidth": 100,
              "useTabs": false,
              "tabWidth": 4
            }
            ```
            """";

        return ToolResult.Ok(rules);
    }

    #region Private Helpers

    private async Task<(int ExitCode, string Output)> RunCSharpierAsync(
        string arguments,
        CancellationToken cancellationToken
    )
    {
        return await RunProcessAsync(_options.CSharpierCommand, arguments, cancellationToken);
    }

    private async Task<(int ExitCode, string Output)> RunDotnetAsync(
        string arguments,
        CancellationToken cancellationToken
    )
    {
        return await RunProcessAsync("dotnet", arguments, cancellationToken);
    }

    private async Task<(int ExitCode, string Output)> RunProcessAsync(
        string command,
        string arguments,
        CancellationToken cancellationToken
    )
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = startInfo };

        var output = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                output.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                output.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"命令执行超时（{_options.TimeoutSeconds}秒）");
        }

        return (process.ExitCode, output.ToString());
    }

    #endregion
}

/// <summary>
/// CSharpier 工具配置选项
/// </summary>
public class CSharpierToolOptions
{
    /// <summary>
    /// CSharpier 命令（默认 "dotnet-csharpier"）
    /// </summary>
    public string CSharpierCommand { get; set; } = "dotnet-csharpier";

    /// <summary>
    /// 命令执行超时时间（秒，默认 60）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;
}
