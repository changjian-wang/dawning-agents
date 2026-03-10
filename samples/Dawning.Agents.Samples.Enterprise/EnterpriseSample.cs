using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.HumanLoop;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Safety;
using Dawning.Agents.Abstractions.Scaling;
using Dawning.Agents.Core;
using Dawning.Agents.Core.HumanLoop;
using Dawning.Agents.Core.Safety;
using Dawning.Agents.Core.Scaling;
using Dawning.Agents.Core.Tools;
using Dawning.Agents.Samples.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dawning.Agents.Samples.Enterprise;

/// <summary>
/// 企业级功能示例
/// </summary>
public class EnterpriseSample : SampleBase
{
    protected override string SampleName => "Enterprise Features";

    protected override void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        // 注册核心工具
        services.AddCoreTools();

        // 注册 Function Calling Agent (推荐)
        services.AddFunctionCallingAgent(options =>
        {
            options.Name = "EnterpriseAgent";
            options.Instructions = "你是一个企业级 AI 助手。";
            options.MaxSteps = 5;
        });

        // 注册人机协作
        services.AddHumanLoop();
        services.AddSingleton<IHumanInteractionHandler, ConsoleApprovalHandler>();

        // 注册安全护栏
        services.AddSafetyGuardrails(configuration);
    }

    protected override async Task ExecuteAsync()
    {
        ConsoleHelper.PrintTitle("选择企业级功能演示");
        Console.WriteLine("  [1] 安全护栏 - 长度限制");
        Console.WriteLine("  [2] 人机协作 - 审批工作流");
        Console.WriteLine("  [3] 弹性扩展 - 熔断器、负载均衡");
        Console.WriteLine("  [4] 多 Agent 编排 - 顺序/并行");
        Console.WriteLine("  [A] 运行全部");
        Console.WriteLine();
        Console.Write("请选择 (1-4/A): ");

        var choice = Console.ReadLine()?.Trim().ToUpperInvariant();

        switch (choice)
        {
            case "1":
                await RunSafetyDemoAsync();
                break;
            case "2":
                await RunHumanLoopDemoAsync();
                break;
            case "3":
                await RunScalingDemoAsync();
                break;
            case "4":
                await RunOrchestrationDemoAsync();
                break;
            case "A":
                await RunSafetyDemoAsync();
                ConsoleHelper.WaitForKey();
                await RunHumanLoopDemoAsync();
                ConsoleHelper.WaitForKey();
                await RunScalingDemoAsync();
                ConsoleHelper.WaitForKey();
                await RunOrchestrationDemoAsync();
                break;
            default:
                await RunSafetyDemoAsync();
                break;
        }
    }

    /// <summary>
    /// 安全护栏演示
    /// </summary>
    private async Task RunSafetyDemoAsync()
    {
        ConsoleHelper.PrintTitle("安全护栏演示");
        ConsoleHelper.PrintInfo("展示长度限制和护栏管道");
        Console.WriteLine();

        // 使用通过 DI 注册的护栏管道
        var pipeline = GetService<IGuardrailPipeline>();

        // 添加基本护栏（只使用不需要 IOptions 的）
        pipeline.AddInputGuardrail(new MaxLengthGuardrail(1000));

        // 测试输入
        var testInputs = new[]
        {
            ("请帮我分析这段代码的性能问题", "正常请求"),
            (new string('x', 1500), "超长输入 (1500 字符)"),
            ("正常的技术问题", "安全请求"),
        };

        ConsoleHelper.PrintStep(1, "输入验证");
        Console.WriteLine();

        foreach (var (input, desc) in testInputs)
        {
            var displayInput = input.Length > 50 ? input[..50] + "..." : input;
            ConsoleHelper.PrintInfo($"测试: {desc}");
            ConsoleHelper.PrintDim($"  输入: {displayInput}");

            var result = await pipeline.CheckInputAsync(input);

            if (result.Passed)
            {
                ConsoleHelper.PrintSuccess("  ✓ 通过");
            }
            else
            {
                ConsoleHelper.PrintError($"  ✗ 拒绝: {result.Message}");
            }

            if (result.Issues.Count > 0)
            {
                ConsoleHelper.PrintDim($"  问题: {result.Issues.Count} 个");
            }
            Console.WriteLine();
        }
    }

    /// <summary>
    /// 人机协作演示
    /// </summary>
    private async Task RunHumanLoopDemoAsync()
    {
        ConsoleHelper.PrintTitle("人机协作演示");
        ConsoleHelper.PrintInfo("展示审批工作流");
        Console.WriteLine();

        var handler = GetService<IHumanInteractionHandler>();

        // 模拟需要审批的操作
        var operations = new[]
        {
            ("delete_file", "删除重要配置文件 config.json"),
            ("send_email", "向客户发送营销邮件"),
            ("deploy", "部署到生产环境"),
        };

        ConsoleHelper.PrintStep(1, "审批工作流");
        Console.WriteLine();

        foreach (var (action, description) in operations)
        {
            ConsoleHelper.PrintInfo($"操作: {description}");

            var request = new ConfirmationRequest
            {
                Action = action,
                Description = description,
                Timeout = TimeSpan.FromMinutes(5),
            };

            var result = await handler.RequestConfirmationAsync(request);

            var confirmed = result.SelectedOption == "yes" || result.SelectedOption == "confirm";
            if (confirmed)
            {
                ConsoleHelper.PrintSuccess(
                    $"  ✓ 已确认 (确认人: {result.RespondedBy ?? "Console User"})"
                );
            }
            else
            {
                ConsoleHelper.PrintWarning($"  ✗ 已拒绝 (原因: {result.Reason ?? "用户取消"})");
            }
            Console.WriteLine();
        }
    }

    /// <summary>
    /// 弹性扩展演示
    /// </summary>
    private async Task RunScalingDemoAsync()
    {
        ConsoleHelper.PrintTitle("弹性扩展演示");
        ConsoleHelper.PrintInfo("展示熔断器和负载均衡");
        Console.WriteLine();

        // 熔断器演示
        ConsoleHelper.PrintStep(1, "熔断器");
        Console.WriteLine();

        var circuitBreaker = new CircuitBreaker(
            failureThreshold: 3,
            resetTimeout: TimeSpan.FromSeconds(5)
        );
        var random = new Random();

        for (int i = 1; i <= 10; i++)
        {
            try
            {
                await circuitBreaker.ExecuteAsync(async () =>
                {
                    // 模拟 50% 失败率
                    if (random.NextDouble() < 0.5)
                    {
                        throw new Exception("模拟失败");
                    }
                    await Task.Delay(10);
                    return "成功";
                });

                ConsoleHelper.PrintSuccess($"  请求 {i}: 成功");
            }
            catch (CircuitBreakerOpenException)
            {
                ConsoleHelper.PrintError($"  请求 {i}: 熔断器开启，拒绝请求");
            }
            catch (Exception ex)
            {
                ConsoleHelper.PrintWarning($"  请求 {i}: 失败 - {ex.Message}");
            }
        }

        Console.WriteLine();
        ConsoleHelper.PrintInfo($"熔断器状态: {circuitBreaker.State}");

        // 负载均衡演示
        Console.WriteLine();
        ConsoleHelper.PrintStep(2, "负载均衡");
        Console.WriteLine();

        var loadBalancer = new AgentLoadBalancer();

        // 注册实例
        loadBalancer.RegisterInstance(
            new AgentInstance
            {
                Id = "agent-1",
                Endpoint = "http://localhost:8001",
                ActiveRequests = 5,
            }
        );
        loadBalancer.RegisterInstance(
            new AgentInstance
            {
                Id = "agent-2",
                Endpoint = "http://localhost:8002",
                ActiveRequests = 3,
            }
        );
        loadBalancer.RegisterInstance(
            new AgentInstance
            {
                Id = "agent-3",
                Endpoint = "http://localhost:8003",
                ActiveRequests = 8,
            }
        );

        ConsoleHelper.PrintInfo("已注册实例:");
        foreach (var instance in loadBalancer.GetAllInstances())
        {
            ConsoleHelper.PrintDim($"  {instance.Id}: 活跃请求 {instance.ActiveRequests}");
        }

        Console.WriteLine();
        ConsoleHelper.PrintInfo("负载均衡选择 (最小负载优先):");
        for (int i = 1; i <= 5; i++)
        {
            var selected = loadBalancer.GetLeastLoadedInstance();
            if (selected != null)
            {
                selected.IncrementActiveRequests();
                ConsoleHelper.PrintDim(
                    $"  请求 {i} → {selected.Id} (活跃请求: {selected.ActiveRequests})"
                );
            }
        }
    }

    /// <summary>
    /// 多 Agent 编排演示
    /// </summary>
    private async Task RunOrchestrationDemoAsync()
    {
        ConsoleHelper.PrintTitle("多 Agent 编排演示");
        ConsoleHelper.PrintInfo("展示顺序和并行编排");
        Console.WriteLine();

        var provider = GetService<ILLMProvider>();

        // 创建专家 Agent（模拟）
        var experts = new Dictionary<string, string>
        {
            ["技术专家"] = "你是技术专家，专注于代码和架构问题",
            ["产品专家"] = "你是产品专家，专注于用户体验和需求分析",
            ["安全专家"] = "你是安全专家，专注于安全漏洞和合规性",
        };

        // 顺序编排演示
        ConsoleHelper.PrintStep(1, "顺序编排 (Pipeline)");
        Console.WriteLine();

        var question = "如何设计一个安全的用户认证系统？";
        ConsoleHelper.PrintInfo($"问题: {question}");
        Console.WriteLine();

        foreach (var (role, instruction) in experts)
        {
            ConsoleHelper.PrintDim($"[{role}] 分析中...");

            var messages = new List<ChatMessage>
            {
                new("system", instruction),
                new("user", $"请从你的专业角度分析: {question}"),
            };

            var response = await provider.ChatAsync(messages);
            var preview =
                response.Content?.Length > 100 ? response.Content[..100] + "..." : response.Content;

            ConsoleHelper.PrintColored($"  {preview}", ConsoleColor.Cyan);
            Console.WriteLine();
        }

        // 并行编排演示
        Console.WriteLine();
        ConsoleHelper.PrintStep(2, "并行编排 (Fan-out)");
        Console.WriteLine();

        ConsoleHelper.PrintInfo("同时咨询所有专家...");

        var tasks = experts.Select(async kv =>
        {
            var messages = new List<ChatMessage>
            {
                new("system", kv.Value),
                new("user", $"用一句话回答: {question}"),
            };
            var response = await provider.ChatAsync(messages);
            return (Role: kv.Key, Response: response.Content);
        });

        var results = await Task.WhenAll(tasks);

        Console.WriteLine();
        foreach (var (role, response) in results)
        {
            ConsoleHelper.PrintColored($"[{role}] {response}", ConsoleColor.Green);
        }
    }
}

/// <summary>
/// 控制台审批处理器
/// </summary>
public class ConsoleApprovalHandler : IHumanInteractionHandler
{
    public Task<ConfirmationResponse> RequestConfirmationAsync(
        ConfirmationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        Console.Write($"  确认 [{request.Action}]: {request.Description}? (Y/n): ");
        var input = Console.ReadLine()?.Trim().ToUpperInvariant();

        var confirmed = input != "N";

        return Task.FromResult(
            new ConfirmationResponse
            {
                RequestId = request.Id,
                SelectedOption = confirmed ? "yes" : "no",
                RespondedBy = "Console User",
                Reason = confirmed ? null : "用户拒绝",
                RespondedAt = DateTime.UtcNow,
            }
        );
    }

    public Task<string> RequestInputAsync(
        string prompt,
        string? defaultValue = null,
        CancellationToken cancellationToken = default
    )
    {
        Console.Write($"  {prompt} [{defaultValue}]: ");
        var input = Console.ReadLine();
        return Task.FromResult(string.IsNullOrEmpty(input) ? defaultValue ?? "" : input);
    }

    public Task NotifyAsync(
        string message,
        NotificationLevel level = NotificationLevel.Info,
        CancellationToken cancellationToken = default
    )
    {
        var color = level switch
        {
            NotificationLevel.Warning => ConsoleColor.Yellow,
            NotificationLevel.Error => ConsoleColor.Red,
            _ => ConsoleColor.Cyan,
        };

        ConsoleHelper.PrintColored($"  通知: {message}", color);
        return Task.CompletedTask;
    }

    public Task<EscalationResult> EscalateAsync(
        EscalationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ConsoleHelper.PrintWarning($"  升级请求: {request.Reason}");
        Console.Write("  是否接受升级? (Y/n): ");
        var input = Console.ReadLine()?.Trim().ToUpperInvariant();

        return Task.FromResult(
            new EscalationResult
            {
                RequestId = request.Id,
                Action = input != "N" ? EscalationAction.Resolved : EscalationAction.Skipped,
                Resolution = "已处理",
                ResolvedBy = "Console User",
                ResolvedAt = DateTime.UtcNow,
            }
        );
    }
}
