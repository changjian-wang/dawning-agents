using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Observability;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Observability;
using Dawning.Agents.Demo.Helpers;

namespace Dawning.Agents.Demo.Demos;

/// <summary>
/// Observability &amp; Monitoring 演示
/// </summary>
public static class ObservabilityDemos
{
    /// <summary>
    /// 运行 Observability 演示
    /// </summary>
    public static async Task RunObservabilityDemo(ILLMProvider provider)
    {
        ConsoleHelper.PrintSection("Observability & Monitoring 演示");
        Console.WriteLine("演示指标收集、健康检查、分布式追踪等功能\n");

        // 1. 指标收集器演示
        await RunMetricsCollectorDemo();

        // 2. 健康检查演示
        await RunHealthCheckDemo();

        // 3. 追踪模型演示
        RunTracingDemo();

        // 4. 遥测配置说明
        PrintTelemetryConfig();

        ConsoleHelper.PrintSuccess("\nObservability 演示完成！");
    }

    private static async Task RunMetricsCollectorDemo()
    {
        ConsoleHelper.PrintDivider("1. 指标收集器 (MetricsCollector)");

        var collector = new MetricsCollector();

        Console.WriteLine("  记录各种指标...\n");

        // 记录计数器
        collector.IncrementCounter(
            "agent.requests.total",
            tags: new Dictionary<string, string> { ["agent"] = "TestAgent" }
        );
        collector.IncrementCounter(
            "agent.requests.total",
            tags: new Dictionary<string, string> { ["agent"] = "TestAgent" }
        );
        collector.IncrementCounter(
            "agent.requests.total",
            tags: new Dictionary<string, string> { ["agent"] = "TestAgent" }
        );

        // 记录直方图 (响应时间)
        collector.RecordHistogram(
            "agent.response_time_ms",
            120,
            new Dictionary<string, string> { ["agent"] = "TestAgent" }
        );
        collector.RecordHistogram(
            "agent.response_time_ms",
            85,
            new Dictionary<string, string> { ["agent"] = "TestAgent" }
        );
        collector.RecordHistogram(
            "agent.response_time_ms",
            200,
            new Dictionary<string, string> { ["agent"] = "TestAgent" }
        );

        // 设置仪表
        collector.SetGauge("agent.active_instances", 3);
        collector.SetGauge("agent.queue_length", 5);

        // 获取快照
        var snapshot = collector.GetSnapshot();

        Console.WriteLine("  📊 指标快照:");
        Console.WriteLine($"    时间戳: {snapshot.Timestamp:HH:mm:ss}");
        Console.WriteLine($"    计数器数量: {snapshot.Counters.Count}");
        Console.WriteLine($"    直方图数量: {snapshot.Histograms.Count}");
        Console.WriteLine($"    仪表数量: {snapshot.Gauges.Count}");

        // 显示具体值
        var requestCount = collector.GetCounter(
            "agent.requests.total",
            new Dictionary<string, string> { ["agent"] = "TestAgent" }
        );
        var activeInstances = collector.GetGauge("agent.active_instances");

        Console.WriteLine($"\n    agent.requests.total: {requestCount}");
        Console.WriteLine($"    agent.active_instances: {activeInstances}");

        Console.WriteLine();
        await Task.CompletedTask;
    }

    private static async Task RunHealthCheckDemo()
    {
        ConsoleHelper.PrintDivider("2. 健康检查 (HealthCheck)");

        Console.WriteLine("  AgentHealthCheck 检查 Agent 系统健康状态:\n");

        // 模拟健康检查结果
        var healthyScenario = new
        {
            Status = "Healthy",
            SuccessRate = 0.98,
            AvgResponseTime = 150,
            ErrorRate = 0.02,
        };

        var degradedScenario = new
        {
            Status = "Degraded",
            SuccessRate = 0.85,
            AvgResponseTime = 800,
            ErrorRate = 0.15,
        };

        Console.WriteLine("  场景 1: 健康状态");
        Console.WriteLine($"    ✅ 状态: {healthyScenario.Status}");
        Console.WriteLine($"    成功率: {healthyScenario.SuccessRate:P0}");
        Console.WriteLine($"    平均响应: {healthyScenario.AvgResponseTime}ms");
        Console.WriteLine($"    错误率: {healthyScenario.ErrorRate:P0}");

        Console.WriteLine("\n  场景 2: 降级状态");
        Console.WriteLine($"    ⚠️ 状态: {degradedScenario.Status}");
        Console.WriteLine($"    成功率: {degradedScenario.SuccessRate:P0}");
        Console.WriteLine($"    平均响应: {degradedScenario.AvgResponseTime}ms");
        Console.WriteLine($"    错误率: {degradedScenario.ErrorRate:P0}");

        Console.WriteLine("\n  健康状态枚举:");
        Console.WriteLine("    Healthy   - 所有指标正常");
        Console.WriteLine("    Degraded  - 部分指标异常，服务可用");
        Console.WriteLine("    Unhealthy - 关键指标异常，服务不可用");

        Console.WriteLine();
        await Task.CompletedTask;
    }

    private static void RunTracingDemo()
    {
        ConsoleHelper.PrintDivider("3. 分布式追踪 (Tracing)");

        Console.WriteLine("  TraceContext 和 SpanInfo 用于追踪请求流程:\n");

        // 模拟追踪
        var traceId = Guid.NewGuid().ToString("N")[..16];
        Console.WriteLine($"  Trace ID: {traceId}");
        Console.WriteLine("  操作: AgentRequest\n");

        var spans = new[]
        {
            (Name: "ValidateInput", Duration: 5, Parent: (string?)null),
            (Name: "ProcessRequest", Duration: 120, Parent: (string?)null),
            (Name: "CallTool", Duration: 45, Parent: "ProcessRequest"),
            (Name: "LLMInference", Duration: 65, Parent: "ProcessRequest"),
            (Name: "GenerateResponse", Duration: 10, Parent: (string?)null),
        };

        Console.WriteLine("  📍 Spans:");
        foreach (var span in spans)
        {
            var indent = span.Parent != null ? "      " : "    ";
            Console.WriteLine($"{indent}[{span.Name}] {span.Duration}ms");
        }

        var totalDuration = spans.Where(s => s.Parent == null).Sum(s => s.Duration);
        Console.WriteLine($"\n  Trace 总耗时: {totalDuration}ms");
        Console.WriteLine();
    }

    private static void PrintTelemetryConfig()
    {
        ConsoleHelper.PrintDivider("4. 遥测配置说明");

        Console.WriteLine(
            """
              TelemetryConfig 配置选项:

              {
                "Telemetry": {
                  "EnableMetrics": true,
                  "EnableTracing": true,
                  "EnableLogging": true,
                  "MetricsExporter": "Console",  // Console, Prometheus, OTLP
                  "TracingExporter": "Console",  // Console, Jaeger, OTLP
                  "SamplingRate": 1.0            // 0.0 - 1.0
                }
              }

              DI 注册:
              services.AddObservability(configuration);
              services.AddAgentHealthCheck();

              ObservableAgent 自动收集:
              - 请求计数和成功率
              - 响应时间直方图
              - Token 使用量
              - 工具调用统计

            """
        );
    }
}
