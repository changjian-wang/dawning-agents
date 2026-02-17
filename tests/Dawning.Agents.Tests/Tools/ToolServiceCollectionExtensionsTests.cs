using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Dawning.Agents.Tests.Tools;

/// <summary>
/// ToolServiceCollectionExtensions 单元测试
/// </summary>
public class ToolServiceCollectionExtensionsTests
{
    #region AddToolRegistry Tests

    [Fact]
    public void AddToolRegistry_RegistersSingleton()
    {
        var services = new ServiceCollection();

        services.AddToolRegistry();
        var provider = services.BuildServiceProvider();

        var registry1 = provider.GetRequiredService<IToolRegistry>();
        var registry2 = provider.GetRequiredService<IToolRegistry>();

        registry1.Should().BeSameAs(registry2);
    }

    [Fact]
    public void AddToolRegistry_MultipleCalls_DoesNotThrow()
    {
        var services = new ServiceCollection();

        services.AddToolRegistry();
        services.AddToolRegistry();

        var act = () => services.BuildServiceProvider().GetRequiredService<IToolRegistry>();
        act.Should().NotThrow();
    }

    [Fact]
    public void AddToolRegistry_RegistersIToolReader()
    {
        var services = new ServiceCollection();
        services.AddToolRegistry();
        var provider = services.BuildServiceProvider();

        var reader = provider.GetRequiredService<IToolReader>();
        var registry = provider.GetRequiredService<IToolRegistry>();

        reader.Should().BeSameAs(registry);
    }

    [Fact]
    public void AddToolRegistry_RegistersIToolRegistrar()
    {
        var services = new ServiceCollection();
        services.AddToolRegistry();
        var provider = services.BuildServiceProvider();

        var registrar = provider.GetRequiredService<IToolRegistrar>();
        var registry = provider.GetRequiredService<IToolRegistry>();

        registrar.Should().BeSameAs(registry);
    }

    #endregion

    #region AddTool Tests

    [Fact]
    public void AddTool_RegistersToolInstance()
    {
        var services = new ServiceCollection();
        var tool = new TestTool("test-tool", "Test description");

        services.AddToolRegistry();
        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IToolRegistry>();

        // 直接注册到 registry
        registry.Register(tool);

        var registeredTool = registry.GetTool("test-tool");

        registeredTool.Should().NotBeNull();
        registeredTool!.Name.Should().Be("test-tool");
    }

    #endregion

    #region AddToolsFrom<T> Tests

    [Fact]
    public void AddToolsFrom_ScansToolClass()
    {
        var services = new ServiceCollection();

        services.AddToolsFrom<TestCalculatorTool>();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();
        var tools = registry.GetAllTools().Where(t => t.Category == "TestCalc").ToList();

        tools.Should().NotBeEmpty();
        tools.Should().Contain(t => t.Name == "Add");
        tools.Should().Contain(t => t.Name == "Multiply");
    }

    #endregion

    #region AddCoreTools Tests

    [Fact]
    public void AddCoreTools_RegistersCoreTools()
    {
        var services = new ServiceCollection();

        services.AddCoreTools();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();
        var tools = registry.GetAllTools().ToList();

        tools.Should().NotBeEmpty();
        tools.Should().Contain(t => t.Name == "read_file");
        tools.Should().Contain(t => t.Name == "write_file");
        tools.Should().Contain(t => t.Name == "edit_file");
        tools.Should().Contain(t => t.Name == "search");
        tools.Should().Contain(t => t.Name == "bash");
    }

    [Fact]
    public void AddCoreTools_DoesNotRegisterCreateTool()
    {
        // create_tool depends on IToolSession (scoped) and is managed by FunctionCallingAgent
        var services = new ServiceCollection();

        services.AddCoreTools();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();
        registry.HasTool("create_tool").Should().BeFalse();
    }

    [Fact]
    public void AddCoreTools_RegistersSandbox()
    {
        var services = new ServiceCollection();

        services.AddCoreTools();
        var provider = services.BuildServiceProvider();

        var sandbox = provider.GetRequiredService<IToolSandbox>();
        sandbox.Should().NotBeNull();
    }

    [Fact]
    public void AddCoreTools_RegistersToolStore()
    {
        var services = new ServiceCollection();

        services.AddCoreTools();
        var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IToolStore>();
        store.Should().NotBeNull();
    }

    [Fact]
    public void AddCoreTools_RegistersToolSession()
    {
        var services = new ServiceCollection();

        services.AddCoreTools();
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<IToolSession>();
        session.Should().NotBeNull();
    }

    [Fact]
    public void AddCoreTools_WithOptions_ConfiguresSandbox()
    {
        var services = new ServiceCollection();

        services.AddCoreTools(options =>
        {
            options.Timeout = TimeSpan.FromSeconds(60);
            options.WorkingDirectory = "/tmp";
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<ToolSandboxOptions>();

        options.Timeout.Should().Be(TimeSpan.FromSeconds(60));
        options.WorkingDirectory.Should().Be("/tmp");
    }

    #endregion

    #region AddToolApprovalHandler Tests

    [Fact]
    public void AddToolApprovalHandler_RegistersHandler()
    {
        var services = new ServiceCollection();

        services.AddToolApprovalHandler();
        var provider = services.BuildServiceProvider();

        var handler = provider.GetRequiredService<IToolApprovalHandler>();

        handler.Should().NotBeNull();
    }

    [Theory]
    [InlineData(ApprovalStrategy.AlwaysApprove)]
    [InlineData(ApprovalStrategy.AlwaysDeny)]
    [InlineData(ApprovalStrategy.RiskBased)]
    public void AddToolApprovalHandler_WithStrategy_RegistersCorrectHandler(
        ApprovalStrategy strategy
    )
    {
        var services = new ServiceCollection();

        services.AddToolApprovalHandler(strategy);
        var provider = services.BuildServiceProvider();

        var handler = provider.GetRequiredService<IToolApprovalHandler>();
        handler.Should().NotBeNull();
    }

    #endregion

    #region EnsureToolsRegistered Tests

    [Fact]
    public void EnsureToolsRegistered_TriggersRegistration()
    {
        var services = new ServiceCollection();
        services.AddToolsFrom<TestCalculatorTool>();

        var provider = services.BuildServiceProvider();

        // 注册前获取 registry
        var registry = provider.GetRequiredService<IToolRegistry>();
        var toolsBefore = registry.GetAllTools().ToList();

        // 触发注册
        provider.EnsureToolsRegistered();

        var toolsAfter = registry.GetAllTools().ToList();

        toolsAfter.Should().NotBeEmpty();
    }

    [Fact]
    public void EnsureToolsRegistered_CalledMultipleTimes_DoesNotDuplicate()
    {
        var services = new ServiceCollection();
        services.AddToolsFrom<TestCalculatorTool>();

        var provider = services.BuildServiceProvider();

        provider.EnsureToolsRegistered();
        var countFirst = provider.GetRequiredService<IToolRegistry>().GetAllTools().Count();

        provider.EnsureToolsRegistered();
        var countSecond = provider.GetRequiredService<IToolRegistry>().GetAllTools().Count();

        countFirst.Should().Be(countSecond);
    }

    #endregion

    #region Test Helpers

    private class TestTool : ITool
    {
        public TestTool(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public string Name { get; }
        public string Description { get; }
        public string ParametersSchema => "{}";
        public bool RequiresConfirmation => false;
        public ToolRiskLevel RiskLevel => ToolRiskLevel.Low;
        public string? Category => "Test";

        public Task<ToolResult> ExecuteAsync(string input, CancellationToken ct = default)
        {
            return Task.FromResult(
                new ToolResult { Success = true, Output = $"Executed with: {input}" }
            );
        }
    }

    /// <summary>
    /// 测试用计算器工具
    /// </summary>
    public class TestCalculatorTool
    {
        [FunctionTool("Add two numbers", Category = "TestCalc")]
        public string Add(
            [ToolParameter("First number")] double a,
            [ToolParameter("Second number")] double b
        ) => (a + b).ToString();

        [FunctionTool("Multiply two numbers", Category = "TestCalc")]
        public string Multiply(
            [ToolParameter("First number")] double a,
            [ToolParameter("Second number")] double b
        ) => (a * b).ToString();
    }

    #endregion
}
