using Dawning.Agents.Abstractions.Scaling;
using Dawning.Agents.Core.Scaling;
using Dawning.Agents.Demo.Helpers;

namespace Dawning.Agents.Demo.Demos;

/// <summary>
/// Week 12: 部署与扩展演示
/// </summary>
public static class ScalingDemos
{
    /// <summary>
    /// 扩展与部署演示
    /// </summary>
    public static async Task RunScalingDemo()
    {
        ConsoleHelper.PrintDivider("🚀 部署与扩展 (Scaling) 演示");

        Console.WriteLine("\n生产级部署组件：");
        Console.WriteLine("  • CircuitBreaker: 熔断器保护");
        Console.WriteLine("  • RequestQueue: 请求队列");
        Console.WriteLine("  • LoadBalancer: 负载均衡");
        Console.WriteLine("  • AutoScaler: 自动扩展\n");

        // ====================================================================
        // 1. 熔断器演示
        // ====================================================================
        ConsoleHelper.PrintDivider("1️⃣ 熔断器 (Circuit Breaker)");
        Console.WriteLine("场景：保护系统免受级联故障影响\n");

        var circuitBreaker = new CircuitBreaker(
            failureThreshold: 3,
            resetTimeout: TimeSpan.FromSeconds(5)
        );

        Console.WriteLine($"配置: 失败阈值=3, 重置超时=5秒");
        Console.WriteLine($"初始状态: {circuitBreaker.State}\n");

        // 模拟请求
        for (var i = 1; i <= 6; i++)
        {
            var shouldFail = i <= 4; // 前4次失败

            try
            {
                var result = await circuitBreaker.ExecuteAsync(async () =>
                {
                    await Task.Delay(50);
                    if (shouldFail)
                    {
                        throw new Exception("模拟服务故障");
                    }
                    return $"请求 {i} 成功";
                });

                ConsoleHelper.PrintSuccess($"  请求 {i}: {result}");
            }
            catch (CircuitBreakerOpenException)
            {
                ConsoleHelper.PrintError($"  请求 {i}: 熔断器打开，请求被拒绝");
            }
            catch (Exception ex)
            {
                ConsoleHelper.PrintWarning($"  请求 {i}: 失败 - {ex.Message}");
            }

            Console.WriteLine($"    状态: {circuitBreaker.State}, 失败计数: {circuitBreaker.FailureCount}");
        }

        // 等待熔断器恢复
        Console.WriteLine("\n等待熔断器恢复 (5秒)...");
        await Task.Delay(5500);

        Console.WriteLine($"恢复后状态: {circuitBreaker.State}");

        // 成功请求将关闭熔断器
        try
        {
            await circuitBreaker.ExecuteAsync(async () =>
            {
                await Task.Delay(50);
                return "恢复成功";
            });
            ConsoleHelper.PrintSuccess($"  恢复请求成功，熔断器状态: {circuitBreaker.State}");
        }
        catch (Exception ex)
        {
            ConsoleHelper.PrintError($"  恢复请求失败: {ex.Message}");
        }

        // ====================================================================
        // 2. 请求队列演示
        // ====================================================================
        ConsoleHelper.PrintDivider("2️⃣ 请求队列 (Request Queue)");
        Console.WriteLine("场景：异步处理请求，削峰填谷\n");

        var queue = new AgentRequestQueue(capacity: 100);

        Console.WriteLine($"队列容量: 100");
        Console.WriteLine($"初始队列长度: {queue.Count}\n");

        // 入队多个请求
        Console.WriteLine("入队 5 个请求...");

        for (var i = 1; i <= 5; i++)
        {
            var workItem = new AgentWorkItem
            {
                Input = $"任务 {i}: 处理数据",
                CompletionSource = new TaskCompletionSource<Dawning.Agents.Abstractions.Agent.AgentResponse>(),
                Priority = i % 2 == 0 ? 1 : 0, // 偶数任务高优先级
            };

            await queue.EnqueueAsync(workItem);
            Console.WriteLine($"  入队: {workItem.Input} (优先级: {workItem.Priority})");
        }

        Console.WriteLine($"\n当前队列长度: {queue.Count}");

        // 模拟出队处理
        Console.WriteLine("\n出队处理...");
        while (queue.Count > 0)
        {
            var item = await queue.DequeueAsync();
            if (item != null)
            {
                ConsoleHelper.PrintSuccess($"  处理: {item.Input}");
                await Task.Delay(100);
            }
        }

        Console.WriteLine($"处理完成，队列长度: {queue.Count}");

        // ====================================================================
        // 3. 负载均衡演示
        // ====================================================================
        ConsoleHelper.PrintDivider("3️⃣ 负载均衡 (Load Balancer)");
        Console.WriteLine("场景：在多个 Agent 实例间分配请求\n");

        var loadBalancer = new AgentLoadBalancer();

        // 使用模拟的 Agent 接口
        var mockAgent = new MockAgent("MockAgent", "用于演示的模拟 Agent");

        // 注册模拟的 Agent 实例
        var instances = new AgentInstance[]
        {
            new() { Id = "agent-1", Agent = mockAgent, Endpoint = "http://localhost:8001", IsHealthy = true, ActiveRequests = 5 },
            new() { Id = "agent-2", Agent = mockAgent, Endpoint = "http://localhost:8002", IsHealthy = true, ActiveRequests = 3 },
            new() { Id = "agent-3", Agent = mockAgent, Endpoint = "http://localhost:8003", IsHealthy = false, ActiveRequests = 0 },
            new() { Id = "agent-4", Agent = mockAgent, Endpoint = "http://localhost:8004", IsHealthy = true, ActiveRequests = 8 },
        };

        foreach (var instance in instances)
        {
            loadBalancer.RegisterInstance(instance);
        }

        Console.WriteLine("已注册实例:");
        foreach (var instance in instances)
        {
            var status = instance.IsHealthy ? "✅ 健康" : "❌ 不健康";
            Console.WriteLine($"  • {instance.Id}: {status}, 活跃请求: {instance.ActiveRequests}");
        }

        // 演示轮询
        Console.WriteLine("\n轮询模式 (Round Robin):");
        for (var i = 0; i < 5; i++)
        {
            var selected = loadBalancer.GetNextInstance();
            Console.WriteLine($"  请求 {i + 1} → {selected?.Id ?? "无可用实例"}");
        }

        // 演示最小负载
        Console.WriteLine("\n最小负载模式 (Least Loaded):");
        for (var i = 0; i < 3; i++)
        {
            var selected = loadBalancer.GetLeastLoadedInstance();
            if (selected != null)
            {
                Console.WriteLine($"  请求 {i + 1} → {selected.Id} (当前负载: {selected.ActiveRequests})");
                selected.ActiveRequests++; // 模拟增加负载
            }
        }

        // ====================================================================
        // 4. 自动扩展演示
        // ====================================================================
        ConsoleHelper.PrintDivider("4️⃣ 自动扩展 (Auto Scaler)");
        Console.WriteLine("场景：根据负载自动调整实例数量\n");

        var scalingOptions = new ScalingOptions
        {
            MinInstances = 2,
            MaxInstances = 10,
            TargetCpuPercent = 70,
            TargetMemoryPercent = 80,
            ScaleUpCooldownSeconds = 60,
            ScaleDownCooldownSeconds = 300,
        };

        Console.WriteLine("扩展配置:");
        Console.WriteLine($"  最小实例: {scalingOptions.MinInstances}");
        Console.WriteLine($"  最大实例: {scalingOptions.MaxInstances}");
        Console.WriteLine($"  目标 CPU: {scalingOptions.TargetCpuPercent}%");
        Console.WriteLine($"  目标内存: {scalingOptions.TargetMemoryPercent}%");
        Console.WriteLine($"  扩容冷却: {scalingOptions.ScaleUpCooldownSeconds}s");
        Console.WriteLine($"  缩容冷却: {scalingOptions.ScaleDownCooldownSeconds}s");

        // 模拟不同负载场景
        var scenarios = new (string Name, double Cpu, double Memory, int Queue)[]
        {
            ("低负载", 30.0, 40.0, 5),
            ("正常负载", 65.0, 70.0, 20),
            ("高负载", 85.0, 75.0, 100),
            ("峰值负载", 95.0, 90.0, 500),
        };

        Console.WriteLine("\n扩展决策模拟:\n");

        var currentInstances = 3;
        foreach (var scenario in scenarios)
        {
            var metrics = new ScalingMetrics
            {
                CpuPercent = scenario.Cpu,
                MemoryPercent = scenario.Memory,
                QueueLength = scenario.Queue,
                ActiveRequests = scenario.Queue / 2,
            };

            var decision = SimulateScalingDecision(metrics, scalingOptions, currentInstances);

            var decisionIcon = decision.Action switch
            {
                ScalingAction.ScaleUp => "⬆️",
                ScalingAction.ScaleDown => "⬇️",
                _ => "➡️",
            };

            Console.WriteLine($"  📊 {scenario.Name}:");
            Console.WriteLine($"     CPU: {scenario.Cpu}%, 内存: {scenario.Memory}%, 队列: {scenario.Queue}");
            Console.WriteLine($"     决策: {decisionIcon} {decision.Action} (当前: {currentInstances} 实例)");

            if (decision.Action != ScalingAction.None)
            {
                var newCount = decision.Action == ScalingAction.ScaleUp
                    ? Math.Min(currentInstances + decision.Delta, scalingOptions.MaxInstances)
                    : Math.Max(currentInstances - decision.Delta, scalingOptions.MinInstances);
                Console.WriteLine($"     目标: {newCount} 实例 ({(decision.Delta > 0 ? "+" : "")}{decision.Delta})");
                currentInstances = newCount;
            }
            Console.WriteLine();
        }

        ConsoleHelper.PrintDivider("演示结束");
        Console.WriteLine("\n部署与扩展组件帮助您构建高可用、可扩展的 Agent 系统，");
        Console.WriteLine("从容应对生产环境的各种挑战。\n");
    }

    private static ScalingDecision SimulateScalingDecision(
        ScalingMetrics metrics,
        ScalingOptions options,
        int currentInstances)
    {
        // 检查是否需要扩容
        if (metrics.CpuPercent > options.TargetCpuPercent ||
            metrics.MemoryPercent > options.TargetMemoryPercent ||
            metrics.QueueLength > currentInstances * 10)
        {
            var cpuRatio = metrics.CpuPercent / options.TargetCpuPercent;
            var memoryRatio = metrics.MemoryPercent / options.TargetMemoryPercent;
            var targetRatio = Math.Max(cpuRatio, memoryRatio);
            var delta = Math.Max(1, (int)Math.Ceiling(currentInstances * (targetRatio - 1)));

            return new ScalingDecision
            {
                Action = ScalingAction.ScaleUp,
                Delta = delta,
                Reason = $"CPU: {metrics.CpuPercent}%, Memory: {metrics.MemoryPercent}%",
            };
        }

        // 检查是否可以缩容
        if (metrics.CpuPercent < options.TargetCpuPercent * 0.5 &&
            metrics.MemoryPercent < options.TargetMemoryPercent * 0.5 &&
            metrics.QueueLength < currentInstances * 2)
        {
            return new ScalingDecision
            {
                Action = ScalingAction.ScaleDown,
                Delta = 1,
                Reason = "低利用率",
            };
        }

        return new ScalingDecision { Action = ScalingAction.None };
    }

    /// <summary>
    /// 用于演示的模拟 Agent
    /// </summary>
    private class MockAgent : Dawning.Agents.Abstractions.Agent.IAgent
    {
        public string Name { get; }
        public string Instructions { get; }

        public MockAgent(string name, string instructions)
        {
            Name = name;
            Instructions = instructions;
        }

        public Task<Dawning.Agents.Abstractions.Agent.AgentResponse> RunAsync(
            string input,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new Dawning.Agents.Abstractions.Agent.AgentResponse
            {
                Success = true,
                FinalAnswer = $"模拟响应: {input}",
            });
        }

        public Task<Dawning.Agents.Abstractions.Agent.AgentResponse> RunAsync(
            Dawning.Agents.Abstractions.Agent.AgentContext context,
            CancellationToken cancellationToken = default)
        {
            return RunAsync(context.UserInput, cancellationToken);
        }
    }
}
