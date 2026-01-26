using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Scaling;
using Dawning.Agents.Demo.Helpers;

namespace Dawning.Agents.Demo.Demos;

/// <summary>
/// Scaling &amp; Deployment 演示
/// </summary>
public static class ScalingDemos
{
    /// <summary>
    /// 运行 Scaling 演示
    /// </summary>
    public static async Task RunScalingDemo(ILLMProvider provider)
    {
        ConsoleHelper.PrintSection("Scaling & Deployment 演示");
        Console.WriteLine("演示请求队列、负载均衡、熔断器、自动扩缩容等功能\n");

        // 1. 请求队列演示
        await RunRequestQueueDemo();

        // 2. 负载均衡器演示
        await RunLoadBalancerDemo();

        // 3. 熔断器演示
        await RunCircuitBreakerDemo();

        // 4. 自动扩缩容演示
        await RunAutoScalerDemo();

        // 5. 生产部署配置说明
        PrintDeploymentConfig();

        ConsoleHelper.PrintSuccess("\nScaling 演示完成！");
    }

    private static async Task RunRequestQueueDemo()
    {
        ConsoleHelper.PrintDivider("1. 请求队列 (AgentRequestQueue)");

        Console.WriteLine("  基于 Channel<T> 的有界队列实现:\n");

        // 模拟队列操作
        var queueCapacity = 100;
        var currentCount = 0;

        Console.WriteLine($"  队列容量: {queueCapacity}");
        Console.WriteLine($"  当前长度: {currentCount}");

        // 模拟入队
        Console.WriteLine("\n  模拟入队操作:");
        for (int i = 1; i <= 3; i++)
        {
            currentCount++;
            Console.WriteLine($"    ✅ 请求 {i} 已入队 (队列: {currentCount}/{queueCapacity})");
        }

        // 模拟出队
        Console.WriteLine("\n  模拟出队处理:");
        currentCount--;
        Console.WriteLine($"    处理请求 1 (队列: {currentCount}/{queueCapacity})");

        Console.WriteLine("\n  队列特性:");
        Console.WriteLine("    - 有界队列防止内存溢出");
        Console.WriteLine("    - 背压机制：队列满时阻塞生产者");
        Console.WriteLine("    - 支持优雅关闭");

        Console.WriteLine();
        await Task.CompletedTask;
    }

    private static async Task RunLoadBalancerDemo()
    {
        ConsoleHelper.PrintDivider("2. 负载均衡器 (AgentLoadBalancer)");

        Console.WriteLine("  支持 Round-Robin 和最少负载策略:\n");

        // 模拟实例
        var instances = new[]
        {
            new
            {
                Id = "instance-1",
                Endpoint = "http://localhost:8001",
                Load = 5,
                Healthy = true,
            },
            new
            {
                Id = "instance-2",
                Endpoint = "http://localhost:8002",
                Load = 2,
                Healthy = true,
            },
            new
            {
                Id = "instance-3",
                Endpoint = "http://localhost:8003",
                Load = 8,
                Healthy = false,
            },
        };

        Console.WriteLine("  已注册实例:");
        foreach (var inst in instances)
        {
            var status = inst.Healthy ? "🟢" : "🔴";
            Console.WriteLine($"    {status} {inst.Id}: 负载={inst.Load}, {inst.Endpoint}");
        }

        Console.WriteLine("\n  Round-Robin 选择 (跳过不健康实例):");
        var rrSequence = new[] { "instance-1", "instance-2", "instance-1" };
        for (int i = 0; i < 3; i++)
        {
            Console.WriteLine($"    第 {i + 1} 次: {rrSequence[i]}");
        }

        Console.WriteLine("\n  最少负载选择:");
        var leastLoaded = instances.Where(i => i.Healthy).OrderBy(i => i.Load).First();
        Console.WriteLine($"    选中: {leastLoaded.Id} (负载={leastLoaded.Load})");

        Console.WriteLine();
        await Task.CompletedTask;
    }

    private static async Task RunCircuitBreakerDemo()
    {
        ConsoleHelper.PrintDivider("3. 熔断器 (CircuitBreaker)");

        Console.WriteLine("  状态机: Closed → Open → HalfOpen\n");

        Console.WriteLine("  配置:");
        Console.WriteLine("    失败阈值: 3 次");
        Console.WriteLine("    重置超时: 30 秒\n");

        // 模拟状态变化
        var states = new[]
        {
            (Action: "成功调用", State: "Closed", Icon: "🟢"),
            (Action: "失败 1", State: "Closed", Icon: "🟢"),
            (Action: "失败 2", State: "Closed", Icon: "🟢"),
            (Action: "失败 3 (触发熔断)", State: "Open", Icon: "🔴"),
            (Action: "尝试调用", State: "Open (拒绝)", Icon: "🔴"),
            (Action: "等待 30 秒...", State: "HalfOpen", Icon: "🟡"),
            (Action: "探测成功", State: "Closed", Icon: "🟢"),
        };

        Console.WriteLine("  状态变化模拟:");
        foreach (var s in states)
        {
            Console.WriteLine($"    {s.Icon} {s.Action, -20} → {s.State}");
        }

        Console.WriteLine("\n  熔断器用途:");
        Console.WriteLine("    - 防止级联故障");
        Console.WriteLine("    - 快速失败，避免资源耗尽");
        Console.WriteLine("    - 自动恢复检测");

        Console.WriteLine();
        await Task.CompletedTask;
    }

    private static async Task RunAutoScalerDemo()
    {
        ConsoleHelper.PrintDivider("4. 自动扩缩容 (AgentAutoScaler)");

        Console.WriteLine("  基于指标的自动扩缩容决策:\n");

        Console.WriteLine("  配置:");
        Console.WriteLine("    最小实例: 1");
        Console.WriteLine("    最大实例: 10");
        Console.WriteLine("    目标 CPU: 70%");
        Console.WriteLine("    扩容冷却: 60 秒");
        Console.WriteLine("    缩容冷却: 300 秒\n");

        // 模拟不同场景
        var scenarios = new[]
        {
            (Cpu: 30, Queue: 2, Current: 3, Action: "⬇️ ScaleDown", Reason: "CPU 使用率低于阈值"),
            (Cpu: 65, Queue: 5, Current: 2, Action: "➡️ None", Reason: "指标在正常范围"),
            (Cpu: 85, Queue: 20, Current: 2, Action: "⬆️ ScaleUp", Reason: "CPU 超过目标值"),
            (Cpu: 95, Queue: 50, Current: 4, Action: "⬆️ ScaleUp", Reason: "队列积压严重"),
        };

        Console.WriteLine("  决策模拟:");
        foreach (var s in scenarios)
        {
            Console.WriteLine($"    CPU={s.Cpu}%, 队列={s.Queue}, 实例={s.Current}");
            Console.WriteLine($"      {s.Action}: {s.Reason}");
            Console.WriteLine();
        }

        await Task.CompletedTask;
    }

    private static void PrintDeploymentConfig()
    {
        ConsoleHelper.PrintDivider("5. 生产部署配置");

        Console.WriteLine(
            """
              ScalingOptions 配置:

              {
                "Scaling": {
                  "MinInstances": 2,
                  "MaxInstances": 10,
                  "TargetCpuPercent": 70,
                  "TargetMemoryPercent": 80,
                  "ScaleUpCooldownSeconds": 60,
                  "ScaleDownCooldownSeconds": 300,
                  "QueueCapacity": 1000,
                  "WorkerCount": 0  // 0 = ProcessorCount * 2
                }
              }

              DI 注册:
              services.AddScaling(configuration);
              services.AddCircuitBreaker();
              services.AddProductionDeployment(configuration);

              生产部署包含:
              - 请求队列 + 工作线程池
              - 负载均衡器
              - 熔断器
              - 自动扩缩容器
              - 健康检查端点

            """
        );
    }
}
