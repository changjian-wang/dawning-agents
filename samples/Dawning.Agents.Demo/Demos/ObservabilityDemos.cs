using Dawning.Agents.Abstractions.Observability;
using Dawning.Agents.Core.Observability;
using Dawning.Agents.Demo.Helpers;

namespace Dawning.Agents.Demo.Demos;

/// <summary>
/// Week 11: 可观测性演示
/// </summary>
public static class ObservabilityDemos
{
    /// <summary>
    /// 可观测性演示
    /// </summary>
    public static async Task RunObservabilityDemo()
    {
        ConsoleHelper.PrintDivider("📊 可观测性 (Observability) 演示");

        Console.WriteLine("\n可观测性三大支柱：");
        Console.WriteLine("  • Logging: 结构化日志");
        Console.WriteLine("  • Metrics: 性能指标");
        Console.WriteLine("  • Tracing: 分布式追踪\n");

        // ====================================================================
        // 1. 遥测配置演示
        // ====================================================================
        ConsoleHelper.PrintDivider("1️⃣ 遥测配置 (Telemetry Config)");
        Console.WriteLine("场景：配置遥测选项\n");

        var config = new TelemetryConfig
        {
            ServiceName = "Dawning.Agents.Demo",
            ServiceVersion = "1.0.0",
            EnableLogging = true,
            EnableMetrics = true,
            EnableTracing = true,
            TraceSampleRate = 0.1, // 采样 10%
            MinLogLevel = TelemetryLogLevel.Information,
            OtlpEndpoint = "http://localhost:4317",
        };

        Console.WriteLine("📋 遥测配置:");
        Console.WriteLine($"  服务名称: {config.ServiceName}");
        Console.WriteLine($"  服务版本: {config.ServiceVersion}");
        Console.WriteLine($"  启用日志: {config.EnableLogging}");
        Console.WriteLine($"  启用指标: {config.EnableMetrics}");
        Console.WriteLine($"  启用追踪: {config.EnableTracing}");
        Console.WriteLine($"  追踪采样率: {config.TraceSampleRate:P0}");
        Console.WriteLine($"  最低日志级别: {config.MinLogLevel}");
        Console.WriteLine($"  OTLP 端点: {config.OtlpEndpoint}");

        // ====================================================================
        // 2. 指标收集演示
        // ====================================================================
        ConsoleHelper.PrintDivider("2️⃣ 指标收集 (Metrics)");
        Console.WriteLine("场景：收集 Agent 运行时指标\n");

        var metrics = new MetricsCollector();

        // 模拟记录指标
        Console.WriteLine("模拟 Agent 请求...\n");

        for (var i = 0; i < 10; i++)
        {
            var latency = Random.Shared.Next(50, 500);
            var success = Random.Shared.NextDouble() > 0.1; // 90% 成功率

            // 使用实际的 API
            metrics.IncrementCounter("agent.requests.total", 1, new Dictionary<string, string> { ["agent"] = "DemoAgent" });
            metrics.RecordHistogram("agent.request.duration", latency, new Dictionary<string, string> { ["agent"] = "DemoAgent" });

            if (!success)
            {
                metrics.IncrementCounter("agent.errors.total", 1, new Dictionary<string, string> { ["agent"] = "DemoAgent" });
            }

            Console.Write(".");
            await Task.Delay(100);
        }

        Console.WriteLine("\n");

        // 获取指标快照
        var snapshot = metrics.GetSnapshot();

        Console.WriteLine("📈 指标快照:");
        Console.WriteLine($"  采集时间: {snapshot.Timestamp:HH:mm:ss}");
        Console.WriteLine($"  计数器数量: {snapshot.Counters.Count}");
        Console.WriteLine($"  直方图数量: {snapshot.Histograms.Count}");

        foreach (var counter in snapshot.Counters)
        {
            Console.WriteLine($"  • {counter.Name}: {counter.Value}");
        }

        foreach (var histogram in snapshot.Histograms)
        {
            Console.WriteLine($"  • {histogram.Name}: 计数={histogram.Count}, P50={histogram.P50:F1}ms, P95={histogram.P95:F1}ms");
        }

        // ====================================================================
        // 3. 分布式追踪演示
        // ====================================================================
        ConsoleHelper.PrintDivider("3️⃣ 分布式追踪 (Tracing)");
        Console.WriteLine("场景：追踪请求在多个组件间的流转\n");

        var tracer = new DistributedTracer(config);

        Console.WriteLine("📍 追踪链路:");

        using (var rootSpan = tracer.StartSpan("Agent.Run", SpanKind.Server))
        {
            rootSpan.SetAttribute("user.id", "user-123");
            rootSpan.SetAttribute("input.length", 256);
            Console.WriteLine($"  [Root] Agent.Run");

            await Task.Delay(50);

            using (var llmSpan = tracer.StartSpan("LLM.Chat", SpanKind.Client))
            {
                llmSpan.SetAttribute("model", "qwen2.5:0.5b");
                llmSpan.SetAttribute("max_tokens", 1024);
                Console.WriteLine($"    [Child] LLM.Chat");

                await Task.Delay(200);

                llmSpan.SetAttribute("tokens.input", 150);
                llmSpan.SetAttribute("tokens.output", 80);
            }

            using (var toolSpan = tracer.StartSpan("Tool.Execute", SpanKind.Internal))
            {
                toolSpan.SetAttribute("tool.name", "Calculator");
                Console.WriteLine($"    [Child] Tool.Execute");

                await Task.Delay(30);
            }

            rootSpan.SetAttribute("result.success", true);
        }

        Console.WriteLine("\n✅ 追踪完成");

        // ====================================================================
        // 4. 健康检查演示
        // ====================================================================
        ConsoleHelper.PrintDivider("4️⃣ 健康检查 (Health Check)");
        Console.WriteLine("场景：检查 Agent 系统健康状态\n");

        var healthResult = new HealthCheckResult
        {
            Status = HealthStatus.Healthy,
            Timestamp = DateTime.UtcNow,
            Components =
            [
                new ComponentHealth
                {
                    Name = "LLMProvider",
                    Status = HealthStatus.Healthy,
                    Message = "Ollama 连接正常",
                },
                new ComponentHealth
                {
                    Name = "ToolRegistry",
                    Status = HealthStatus.Healthy,
                    Message = "64 个工具已注册",
                },
                new ComponentHealth
                {
                    Name = "Memory",
                    Status = HealthStatus.Healthy,
                    Message = "内存使用正常 (256MB/1GB)",
                },
                new ComponentHealth
                {
                    Name = "VectorStore",
                    Status = HealthStatus.Degraded,
                    Message = "索引重建中 (85%)",
                },
            ],
        };

        Console.WriteLine($"🏥 系统状态: {GetHealthIcon(healthResult.Status)} {healthResult.Status}");
        Console.WriteLine($"   检查时间: {healthResult.Timestamp:HH:mm:ss}");
        Console.WriteLine("\n   组件状态:");

        foreach (var component in healthResult.Components)
        {
            var icon = GetHealthIcon(component.Status);
            Console.WriteLine($"     {icon} {component.Name}: {component.Message}");
        }

        // ====================================================================
        // 5. Agent 遥测演示
        // ====================================================================
        ConsoleHelper.PrintDivider("5️⃣ Agent 遥测 (Agent Telemetry)");
        Console.WriteLine("场景：收集 Agent 执行遥测数据\n");

        using var telemetry = new AgentTelemetry(config);

        // 模拟多个 Agent 的遥测数据
        var agents = new[] { "TriageAgent", "TechExpert", "LegalExpert" };

        Console.WriteLine("模拟 Agent 执行...");

        foreach (var agentName in agents)
        {
            for (var i = 0; i < 3; i++)
            {
                var durationMs = Random.Shared.Next(100, 2000);
                var tokensUsed = Random.Shared.Next(100, 800);
                var success = Random.Shared.NextDouble() > 0.1;

                // 使用实际 API 记录请求
                telemetry.RecordRequest(agentName, success, durationMs, tokensUsed);

                Console.Write(".");
            }
        }

        Console.WriteLine("\n");

        Console.WriteLine("📊 Agent 遥测数据已收集");
        Console.WriteLine("  • 指标通过 .NET Meter API 发布到 OTLP 端点");
        Console.WriteLine("  • 追踪通过 ActivitySource 发布到追踪后端");
        Console.WriteLine("  • 可通过 Prometheus/Grafana/Jaeger 等工具查看");

        ConsoleHelper.PrintDivider("演示结束");
        Console.WriteLine("\n可观测性让您全面了解 Agent 系统的运行状态，");
        Console.WriteLine("快速定位问题并优化性能。\n");
    }

    private static string GetHealthIcon(HealthStatus state) => state switch
    {
        HealthStatus.Healthy => "✅",
        HealthStatus.Degraded => "⚠️",
        HealthStatus.Unhealthy => "❌",
        _ => "❓",
    };
}
