using System.Text;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Core.LLM;
using Dawning.Agents.Core.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dawning.Agents.Samples.Common;

/// <summary>
/// Sample 基类，提供通用的初始化逻辑
/// </summary>
public abstract class SampleBase
{
    protected IHost Host { get; private set; } = null!;
    protected IServiceProvider Services => Host.Services;

    /// <summary>
    /// Sample 名称
    /// </summary>
    protected abstract string SampleName { get; }

    /// <summary>
    /// 运行 Sample
    /// </summary>
    public async Task RunAsync(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        ConsoleHelper.PrintBanner(SampleName);

        try
        {
            // 构建 Host
            var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);

            // 加载 YAML 配置（优先级高于默认 JSON）
            builder.Configuration.AddYamlFile(
                "appsettings.yml",
                optional: true,
                reloadOnChange: true
            );
            builder.Configuration.AddYamlFile(
                $"appsettings.{builder.Environment.EnvironmentName}.yml",
                optional: true,
                reloadOnChange: true
            );

            // 注册基础服务
            builder.Services.AddLLMProvider(builder.Configuration);

            // 子类配置额外服务
            ConfigureServices(builder.Services, builder.Configuration);

            Host = builder.Build();

            // 确保工具已注册
            Services.EnsureToolsRegistered();

            // 验证 LLM Provider
            var provider = Services.GetService<ILLMProvider>();
            if (provider != null)
            {
                ConsoleHelper.PrintSuccess($"已创建 {provider.Name} 提供者");
            }

            // 运行示例
            await ExecuteAsync();
        }
        catch (Exception ex)
        {
            ConsoleHelper.PrintError($"运行失败: {ex.Message}");
            if (ex.InnerException != null)
            {
                ConsoleHelper.PrintDim($"  内部错误: {ex.InnerException.Message}");
            }
        }
        finally
        {
            ConsoleHelper.WaitForKey("按任意键退出...");
        }
    }

    /// <summary>
    /// 配置服务（子类重写）
    /// </summary>
    protected abstract void ConfigureServices(
        IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration
    );

    /// <summary>
    /// 执行示例逻辑（子类重写）
    /// </summary>
    protected abstract Task ExecuteAsync();

    /// <summary>
    /// 获取服务
    /// </summary>
    protected T GetService<T>()
        where T : notnull => Services.GetRequiredService<T>();

    /// <summary>
    /// 尝试获取服务
    /// </summary>
    protected T? TryGetService<T>()
        where T : class => Services.GetService<T>();
}
