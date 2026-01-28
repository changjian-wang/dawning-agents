using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools;
using Dawning.Agents.Core.Tools.BuiltIn;
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
    public void AddToolsFrom_ScansMathTool()
    {
        var services = new ServiceCollection();

        services.AddToolsFrom<MathTool>();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();
        var tools = registry.GetAllTools().Where(t => t.Category == "Math").ToList();

        tools.Should().NotBeEmpty();
        tools.Should().Contain(t => t.Name == "Calculate");
        tools.Should().Contain(t => t.Name == "BasicMath");
    }

    [Fact]
    public void AddToolsFrom_ScansDateTimeTool()
    {
        var services = new ServiceCollection();

        services.AddToolsFrom<DateTimeTool>();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();
        var tools = registry.GetAllTools().Where(t => t.Category == "DateTime").ToList();

        tools.Should().NotBeEmpty();
        tools.Should().Contain(t => t.Name == "GetCurrentDateTime");
    }

    #endregion

    #region AddToolSet Tests

    [Fact]
    public void AddToolSet_RegistersToolSet()
    {
        var services = new ServiceCollection();
        var toolSet = ToolSet.FromType<MathTool>("math", "Math tools");

        services.AddToolSet(toolSet);
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();
        var retrievedSet = registry.GetToolSet("math");

        retrievedSet.Should().NotBeNull();
        retrievedSet!.Name.Should().Be("math");
    }

    [Fact]
    public void AddToolSetFrom_CreatesAndRegistersToolSet()
    {
        var services = new ServiceCollection();

        services.AddToolSetFrom<JsonTool>("json", "JSON tools");
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();
        var toolSet = registry.GetToolSet("json");

        toolSet.Should().NotBeNull();
        toolSet!.Tools.Should().NotBeEmpty();
    }

    #endregion

    #region AddVirtualTool Tests

    [Fact]
    public void AddVirtualTool_RegistersVirtualTool()
    {
        var services = new ServiceCollection();
        var toolSet = ToolSet.FromType<UtilityTool>("utility", "Utility tools");
        var virtualTool = new VirtualTool(toolSet);

        services.AddVirtualTool(virtualTool);
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();
        var tool = registry.GetTool("utility");

        tool.Should().NotBeNull();
        tool.Should().BeAssignableTo<IVirtualTool>();
    }

    [Fact]
    public void AddVirtualToolFrom_CreatesAndRegistersVirtualTool()
    {
        var services = new ServiceCollection();

        services.AddVirtualToolFrom<MathTool>("math-virtual", "Virtual math tools");
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();
        var tool = registry.GetTool("math-virtual");

        tool.Should().NotBeNull();
        tool.Should().BeAssignableTo<IVirtualTool>();
    }

    #endregion

    #region AddToolSelector Tests

    [Fact]
    public void AddToolSelector_RegistersSelector()
    {
        var services = new ServiceCollection();

        services.AddToolSelector();
        var provider = services.BuildServiceProvider();

        var selector = provider.GetRequiredService<IToolSelector>();

        selector.Should().NotBeNull();
        selector.Should().BeOfType<DefaultToolSelector>();
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

    #region BuiltIn Tool Extensions Tests

    [Fact]
    public void AddBuiltInTools_RegistersSafeTools()
    {
        var services = new ServiceCollection();

        services.AddBuiltInTools();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();
        var tools = registry.GetAllTools().ToList();

        tools.Should().NotBeEmpty();
        tools.Should().Contain(t => t.Category == "DateTime");
        tools.Should().Contain(t => t.Category == "Math");
        tools.Should().Contain(t => t.Category == "Json");
        tools.Should().Contain(t => t.Category == "Utility");
    }

    [Fact]
    public void AddAllBuiltInTools_RegistersAllTools()
    {
        var services = new ServiceCollection();

        services.AddAllBuiltInTools();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();
        var tools = registry.GetAllTools().ToList();

        // 应该包含所有类别，包括高风险的
        tools.Should().Contain(t => t.Category == "FileSystem");
        tools.Should().Contain(t => t.Category == "Process");
        tools.Should().Contain(t => t.Category == "Git");
    }

    [Fact]
    public void AddFileSystemTools_RegistersFileTools()
    {
        var services = new ServiceCollection();

        services.AddFileSystemTools();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();
        var tools = registry.GetAllTools().Where(t => t.Category == "FileSystem").ToList();

        tools.Should().NotBeEmpty();
        tools.Should().Contain(t => t.Name == "ReadFile");
        tools.Should().Contain(t => t.Name == "WriteFile");
    }

    [Fact]
    public void AddProcessTools_RegistersProcessTools()
    {
        var services = new ServiceCollection();

        services.AddProcessTools();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();
        var tools = registry.GetAllTools().Where(t => t.Category == "Process").ToList();

        tools.Should().NotBeEmpty();
        tools.Should().Contain(t => t.Name == "RunCommand");
    }

    [Fact]
    public void AddGitTools_RegistersGitTools()
    {
        var services = new ServiceCollection();

        services.AddGitTools();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();
        var tools = registry.GetAllTools().Where(t => t.Category == "Git").ToList();

        tools.Should().NotBeEmpty();
    }

    #endregion

    #region EnsureToolsRegistered Tests

    [Fact]
    public void EnsureToolsRegistered_TriggersRegistration()
    {
        var services = new ServiceCollection();
        services.AddToolsFrom<MathTool>();

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
        services.AddToolsFrom<MathTool>();

        var provider = services.BuildServiceProvider();

        provider.EnsureToolsRegistered();
        var countFirst = provider.GetRequiredService<IToolRegistry>().GetAllTools().Count();

        provider.EnsureToolsRegistered();
        var countSecond = provider.GetRequiredService<IToolRegistry>().GetAllTools().Count();

        countFirst.Should().Be(countSecond);
    }

    #endregion

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
}
