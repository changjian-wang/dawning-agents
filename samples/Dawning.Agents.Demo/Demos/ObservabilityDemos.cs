using System.Diagnostics;
using System.Net.Sockets;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Core.Observability;
using Dawning.Agents.Demo.Helpers;

namespace Dawning.Agents.Demo.Demos;

/// <summary>
/// Observability &amp; Monitoring 演示 - 使用真实 LLM 调用收集数据
/// </summary>
public static class ObservabilityDemos
{
    /// <summary>
    /// 运行 Observability 演示
    /// </summary>
    public static async Task RunObservabilityDemo(ILLMProvider provider)
    {
        ConsoleHelper.PrintSection("Observability & Monitoring 演示");
        Console.WriteLine("使用真实 LLM 调用演示指标收集、健康检查、分布式追踪\n");

        // 1. 指标收集器演示 - 真实 LLM 调用
        await RunMetricsCollectorDemo(provider);

        // 2. 健康检查演示 - 真实服务检测
        await RunHealthCheckDemo(provider);

        // 3. 追踪模型演示 - 真实调用链
        await RunTracingDemo(provider);

        // 4. 遥测配置说明
        PrintTelemetryConfig();

        ConsoleHelper.PrintSuccess("\nObservability 演示完成！");
    }

    private static async Task RunMetricsCollectorDemo(ILLMProvider provider)
    {
        ConsoleHelper.PrintDivider("1. 指标收集器 (MetricsCollector) - 真实数据");

        var collector = new MetricsCollector();
        var tags = new Dictionary<string, string> { ["provider"] = "Ollama" };

        Console.WriteLine("  发送 3 次真实 LLM 请求并收集指标...\n");

        var prompts = new[] { "说一个字", "1+1=?", "今天星期几？只回答数字" };

        var responseTimes = new List<long>();

        foreach (var prompt in prompts)
        {
            Console.Write($"    请求: \"{prompt}\" ... ");

            var sw = Stopwatch.StartNew();
            try
            {
                var messages = new List<ChatMessage> { new("user", prompt) };
                var response = await provider.ChatAsync(messages);
                sw.Stop();

                collector.IncrementCounter("llm.requests.total", 1, tags);
                collector.IncrementCounter("llm.requests.success", 1, tags);
                collector.RecordHistogram("llm.response_time_ms", sw.ElapsedMilliseconds, tags);
                responseTimes.Add(sw.ElapsedMilliseconds);

                var shortResponse =
                    response.Content.Length > 20
                        ? response.Content[..20] + "..."
                        : response.Content;
                Console.WriteLine($"✅ {sw.ElapsedMilliseconds}ms - \"{shortResponse.Trim()}\"");
            }
            catch (Exception ex)
            {
                sw.Stop();
                collector.IncrementCounter("llm.requests.total", 1, tags);
                collector.IncrementCounter("llm.requests.failed", 1, tags);
                Console.WriteLine($"❌ 失败: {ex.Message}");
            }
        }

        // 设置仪表
        collector.SetGauge("llm.active_connections", 1);

        // 获取快照
        var snapshot = collector.GetSnapshot();

        Console.WriteLine("\n  📊 指标快照:");
        Console.WriteLine($"    时间戳: {snapshot.Timestamp:HH:mm:ss}");

        var totalRequests = collector.GetCounter("llm.requests.total", tags) ?? 0;
        var successRequests = collector.GetCounter("llm.requests.success", tags) ?? 0;

        Console.WriteLine($"    总请求数: {totalRequests}");
        Console.WriteLine($"    成功请求: {successRequests}");
        Console.WriteLine(
            $"    成功率: {(totalRequests > 0 ? (double)successRequests / totalRequests : 0):P0}"
        );

        if (responseTimes.Count > 0)
        {
            Console.WriteLine($"    平均响应时间: {responseTimes.Average():F0}ms");
            Console.WriteLine($"    最快响应: {responseTimes.Min()}ms");
            Console.WriteLine($"    最慢响应: {responseTimes.Max()}ms");
        }

        Console.WriteLine();
    }

    private static async Task RunHealthCheckDemo(ILLMProvider provider)
    {
        ConsoleHelper.PrintDivider("2. 健康检查 (HealthCheck) - 真实服务检测");

        Console.WriteLine("  检查 LLM 服务健康状态...\n");

        var sw = Stopwatch.StartNew();
        var isHealthy = false;
        var responseTime = 0L;
        string? errorMessage = null;

        // 检查 Ollama 服务是否可达
        try
        {
            // 1. 检查 TCP 连接
            Console.Write("    检查 Ollama 服务端口... ");
            using var tcpClient = new TcpClient();
            var connectTask = tcpClient.ConnectAsync("localhost", 11434);
            if (await Task.WhenAny(connectTask, Task.Delay(3000)) == connectTask)
            {
                Console.WriteLine("✅ 端口可达");

                // 2. 发送简单请求测试
                Console.Write("    发送测试请求... ");
                var testSw = Stopwatch.StartNew();
                var messages = new List<ChatMessage> { new("user", "hi") };
                await provider.ChatAsync(messages);
                testSw.Stop();
                responseTime = testSw.ElapsedMilliseconds;
                isHealthy = true;
                Console.WriteLine($"✅ 响应正常 ({responseTime}ms)");
            }
            else
            {
                Console.WriteLine("❌ 连接超时");
                errorMessage = "连接超时";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ {ex.Message}");
            errorMessage = ex.Message;
        }

        sw.Stop();

        // 显示健康检查结果
        Console.WriteLine("\n  📋 健康检查结果:");

        var status = isHealthy ? "Healthy" : "Unhealthy";
        var statusIcon = isHealthy ? "✅" : "❌";

        Console.WriteLine($"    {statusIcon} 状态: {status}");
        Console.WriteLine($"    检查耗时: {sw.ElapsedMilliseconds}ms");

        if (isHealthy)
        {
            Console.WriteLine($"    LLM 响应时间: {responseTime}ms");

            // 基于响应时间评估健康等级
            var healthLevel =
                responseTime < 1000 ? "良好"
                : responseTime < 3000 ? "正常"
                : "较慢";
            Console.WriteLine($"    响应等级: {healthLevel}");
        }
        else
        {
            Console.WriteLine($"    错误信息: {errorMessage}");
        }

        Console.WriteLine("\n  健康状态枚举:");
        Console.WriteLine("    Healthy   - 服务正常响应，延迟在可接受范围");
        Console.WriteLine("    Degraded  - 服务响应较慢，但仍可用");
        Console.WriteLine("    Unhealthy - 服务不可达或响应异常");

        Console.WriteLine();
    }

    private static async Task RunTracingDemo(ILLMProvider provider)
    {
        ConsoleHelper.PrintDivider("3. 分布式追踪 (Tracing) - 真实调用链");

        var traceId = Guid.NewGuid().ToString("N")[..16];
        Console.WriteLine($"  Trace ID: {traceId}");
        Console.WriteLine("  操作: 完整 LLM 调用流程\n");

        var spans = new List<(string Name, long DurationMs, string? Parent)>();

        // Span 1: 输入验证
        var totalSw = Stopwatch.StartNew();
        var spanSw = Stopwatch.StartNew();
        var userInput = "计算 2 + 3 的结果";
        _ = !string.IsNullOrWhiteSpace(userInput); // 验证输入
        spanSw.Stop();
        spans.Add(("ValidateInput", spanSw.ElapsedMilliseconds, null));

        // Span 2: 构建消息
        spanSw.Restart();
        var messages = new List<ChatMessage>
        {
            new("system", "你是一个计算器助手，只返回计算结果数字"),
            new("user", userInput),
        };
        spanSw.Stop();
        spans.Add(("BuildMessages", spanSw.ElapsedMilliseconds, null));

        // Span 3: LLM 推理 (真实调用)
        Console.Write("  执行真实 LLM 调用... ");
        spanSw.Restart();
        string? responseContent = null;
        try
        {
            var response = await provider.ChatAsync(messages);
            responseContent = response.Content;
            spanSw.Stop();
            spans.Add(("LLMInference", spanSw.ElapsedMilliseconds, null));
            Console.WriteLine($"✅ ({spanSw.ElapsedMilliseconds}ms)");
        }
        catch (Exception ex)
        {
            spanSw.Stop();
            spans.Add(("LLMInference", spanSw.ElapsedMilliseconds, null));
            Console.WriteLine($"❌ 失败: {ex.Message}");
        }

        // Span 4: 响应处理
        spanSw.Restart();
        var result = responseContent?.Trim() ?? "N/A";
        spanSw.Stop();
        spans.Add(("ProcessResponse", spanSw.ElapsedMilliseconds, null));

        totalSw.Stop();

        // 显示追踪结果
        Console.WriteLine("\n  📍 Spans (真实耗时):");
        foreach (var span in spans)
        {
            var indent = span.Parent != null ? "      " : "    ";
            var bar = new string('█', Math.Min((int)(span.DurationMs / 10), 50));
            Console.WriteLine($"{indent}[{span.Name}] {span.DurationMs}ms {bar}");
        }

        Console.WriteLine($"\n  Trace 总耗时: {totalSw.ElapsedMilliseconds}ms");
        Console.WriteLine($"  LLM 返回结果: \"{result}\"");

        // 分析耗时占比
        var llmSpan = spans.FirstOrDefault(s => s.Name == "LLMInference");
        if (llmSpan.DurationMs > 0)
        {
            var llmPercent = (double)llmSpan.DurationMs / totalSw.ElapsedMilliseconds * 100;
            Console.WriteLine($"  LLM 推理占比: {llmPercent:F1}%");
        }

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
