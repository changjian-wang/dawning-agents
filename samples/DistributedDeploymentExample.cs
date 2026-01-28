// Dawning.Agents 分布式生产部署示例
// 展示如何配置完整的分布式 Agent 系统

using Dawning.Agents.Abstractions.Discovery;
using Dawning.Agents.Abstractions.Security;
using Dawning.Agents.Core.Discovery;
using Dawning.Agents.Core.Health;
using Dawning.Agents.Core.LLM;
using Dawning.Agents.Core.Memory;
using Dawning.Agents.Core.Observability;
using Dawning.Agents.Core.Scaling;
using Dawning.Agents.Core.Security;
using Dawning.Agents.Core.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

Console.WriteLine("=== Dawning.Agents 分布式生产部署示例 ===\n");

// 1. 构建配置
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.production.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// 2. 构建服务容器
var services = new ServiceCollection();

// 日志
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

// 配置
services.AddSingleton<IConfiguration>(configuration);

// --- 核心服务 ---
// LLM 提供者
services.AddLLMProvider(configuration);

// 内存系统
services.AddMemory(configuration);

// 工具系统
services.AddBuiltInTools();

// --- 分布式基础设施 ---
// 服务发现
services.AddServiceDiscovery(configuration);

// 分布式负载均衡
services.AddDistributedLoadBalancer(configuration);

// 健康检查
services.AddAgentHealthChecks();

// --- 可观测性 ---
// OpenTelemetry (追踪 + 指标)
services.AddOpenTelemetryObservability(configuration);

// 现有可观测性系统
services.AddObservability(configuration);

// --- 安全 ---
// 认证、授权、审计、速率限制
services.AddAgentSecurity(configuration);

// 3. 构建服务提供者
var serviceProvider = services.BuildServiceProvider();

// 4. 演示各组件
Console.WriteLine("--- 组件状态 ---\n");

// 服务发现
var serviceRegistry = serviceProvider.GetRequiredService<IServiceRegistry>();
Console.WriteLine($"✅ 服务发现: {serviceRegistry.GetType().Name}");

// 注册当前实例
var currentInstance = new ServiceInstance
{
    Id = $"agent-{Environment.MachineName}-{Guid.NewGuid():N}",
    ServiceName = "dawning-agents",
    Host = "localhost",
    Port = 8080,
    Weight = 100,
    HealthCheckUrl = "http://localhost:8080/health/live",
};
await serviceRegistry.RegisterAsync(currentInstance);
Console.WriteLine($"   已注册实例: {currentInstance.Id}");

// 负载均衡器
var loadBalancer = serviceProvider.GetRequiredService<DistributedLoadBalancer>();
Console.WriteLine($"✅ 负载均衡器: {loadBalancer.GetType().Name}");
Console.WriteLine($"   健康实例数: {loadBalancer.HealthyInstanceCount}");

// 安全组件
var authProvider = serviceProvider.GetRequiredService<IAuthenticationProvider>();
var authzProvider = serviceProvider.GetRequiredService<IAuthorizationProvider>();
var auditLog = serviceProvider.GetRequiredService<IAuditLogProvider>();
var rateLimiter = serviceProvider.GetRequiredService<IRateLimiter>();

Console.WriteLine($"✅ 认证提供者: {authProvider.GetType().Name}");
Console.WriteLine($"✅ 授权提供者: {authzProvider.GetType().Name}");
Console.WriteLine($"✅ 审计日志: {auditLog.GetType().Name}");
Console.WriteLine($"✅ 速率限制: {rateLimiter.GetType().Name}");

// 5. 演示认证流程
Console.WriteLine("\n--- 认证演示 ---\n");

var authResult = await authProvider.AuthenticateApiKeyAsync("dev-key-12345");
Console.WriteLine($"API Key 认证: {(authResult.IsAuthenticated ? "✅ 成功" : "❌ 失败")}");
if (authResult.IsAuthenticated)
{
    Console.WriteLine($"   用户: {authResult.UserName}");
    Console.WriteLine($"   角色: {string.Join(", ", authResult.Roles)}");
}

// 6. 演示授权流程
Console.WriteLine("\n--- 授权演示 ---\n");

var toolAuthz = await authzProvider.AuthorizeToolAsync(authResult, "FileSystemTool");
Console.WriteLine($"FileSystemTool 授权: {(toolAuthz.IsAuthorized ? "✅ 允许" : "❌ 拒绝")}");

// 7. 演示速率限制
Console.WriteLine("\n--- 速率限制演示 ---\n");

for (int i = 0; i < 5; i++)
{
    var rateResult = await rateLimiter.CheckAsync("test-user");
    Console.WriteLine(
        $"请求 {i + 1}: {(rateResult.IsAllowed ? "✅ 允许" : "❌ 限制")} (剩余: {rateResult.RemainingRequests})"
    );
}

// 8. 写入审计日志
Console.WriteLine("\n--- 审计日志演示 ---\n");

await auditLog.WriteAsync(
    new AuditLogEntry
    {
        UserId = authResult.UserId,
        UserName = authResult.UserName,
        Action = AuditActions.AgentRequest,
        Resource = "agent",
        ResourceId = "test-agent",
        IsSuccess = true,
        Metadata = new Dictionary<string, object> { ["input"] = "测试请求", ["tool_count"] = 3 },
    }
);

var logs = await auditLog.QueryAsync(new AuditLogQuery { Take = 5 });
Console.WriteLine($"审计日志条目数: {logs.Count}");
foreach (var log in logs)
{
    Console.WriteLine(
        $"   [{log.Timestamp:HH:mm:ss}] {log.Action} by {log.UserName} - {(log.IsSuccess ? "成功" : "失败")}"
    );
}

// 9. 清理
Console.WriteLine("\n--- 清理 ---\n");
await serviceRegistry.DeregisterAsync(currentInstance.Id);
Console.WriteLine("✅ 已注销服务实例");

Console.WriteLine("\n=== 演示完成 ===");
