using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools;
using Dawning.Agents.Core.Tools.BuiltIn;
using FluentAssertions;
using Xunit;

namespace Dawning.Agents.Tests.Tools;

/// <summary>
/// ToolSet 和 VirtualTool 测试
/// </summary>
public class ToolSetTests
{
    [Fact]
    public void ToolSet_ShouldCreateWithTools()
    {
        // Arrange
        var registry = new ToolRegistry();
        registry.RegisterToolsFromType<MathTool>();
        var tools = registry.GetAllTools();

        // Act
        var toolSet = new ToolSet("math", "数学计算工具集", tools);

        // Assert
        toolSet.Name.Should().Be("math");
        toolSet.Description.Should().Be("数学计算工具集");
        toolSet.Count.Should().BeGreaterThan(0);
        toolSet.Tools.Should().NotBeEmpty();
    }

    [Fact]
    public void ToolSet_GetTool_ShouldReturnTool()
    {
        // Arrange
        var registry = new ToolRegistry();
        registry.RegisterToolsFromType<MathTool>();
        var toolSet = new ToolSet("math", "数学计算工具集", registry.GetAllTools());

        // Act
        var tool = toolSet.GetTool("Calculate");

        // Assert
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("Calculate");
    }

    [Fact]
    public void ToolSet_Contains_ShouldReturnTrue()
    {
        // Arrange
        var registry = new ToolRegistry();
        registry.RegisterToolsFromType<MathTool>();
        var toolSet = new ToolSet("math", "数学计算工具集", registry.GetAllTools());

        // Act & Assert
        toolSet.Contains("Calculate").Should().BeTrue();
        toolSet.Contains("NonExistent").Should().BeFalse();
    }

    [Fact]
    public void ToolSet_FromType_ShouldCreateFromToolClass()
    {
        // Act
        var toolSet = ToolSet.FromType<DateTimeTool>("datetime", "日期时间工具集");

        // Assert
        toolSet.Name.Should().Be("datetime");
        toolSet.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void VirtualTool_ShouldWrapToolSet()
    {
        // Arrange
        var toolSet = ToolSet.FromType<MathTool>("math", "数学计算工具集");

        // Act
        var virtualTool = new VirtualTool(toolSet);

        // Assert
        virtualTool.Name.Should().Be("math");
        virtualTool.ToolSet.Should().BeSameAs(toolSet);
        virtualTool.IsExpanded.Should().BeFalse();
        virtualTool.ExpandedTools.Should().NotBeEmpty();
        virtualTool.Category.Should().Be("VirtualTool");
        virtualTool.RiskLevel.Should().Be(ToolRiskLevel.Low);
    }

    [Fact]
    public void VirtualTool_Expand_ShouldSetIsExpandedTrue()
    {
        // Arrange
        var toolSet = ToolSet.FromType<MathTool>("math", "数学计算工具集");
        var virtualTool = new VirtualTool(toolSet);

        // Act
        virtualTool.Expand();

        // Assert
        virtualTool.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void VirtualTool_Collapse_ShouldSetIsExpandedFalse()
    {
        // Arrange
        var toolSet = ToolSet.FromType<MathTool>("math", "数学计算工具集");
        var virtualTool = new VirtualTool(toolSet);
        virtualTool.Expand();

        // Act
        virtualTool.Collapse();

        // Assert
        virtualTool.IsExpanded.Should().BeFalse();
    }

    [Fact]
    public async Task VirtualTool_Execute_ShouldExpandAndReturnToolList()
    {
        // Arrange
        var toolSet = ToolSet.FromType<MathTool>("math", "数学计算工具集");
        var virtualTool = new VirtualTool(toolSet);

        // Act
        var result = await virtualTool.ExecuteAsync("{}");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("math");
        result.Output.Should().Contain("工具");
        virtualTool.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void VirtualTool_FromType_ShouldCreateDirectly()
    {
        // Act
        var virtualTool = VirtualTool.FromType<DateTimeTool>("datetime", "日期时间工具");

        // Assert
        virtualTool.Name.Should().Be("datetime");
        virtualTool.ExpandedTools.Should().NotBeEmpty();
    }

    [Fact]
    public void ToolRegistry_RegisterToolSet_ShouldRegisterAllTools()
    {
        // Arrange
        var registry = new ToolRegistry();
        var toolSet = ToolSet.FromType<MathTool>("math", "数学计算工具集");

        // Act
        registry.RegisterToolSet(toolSet);

        // Assert
        registry.GetAllToolSets().Should().ContainSingle();
        registry.GetToolSet("math").Should().NotBeNull();
        registry.GetAllTools().Should().NotBeEmpty();
    }

    [Fact]
    public void ToolRegistry_RegisterVirtualTool_ShouldRegisterBoth()
    {
        // Arrange
        var registry = new ToolRegistry();
        var virtualTool = VirtualTool.FromType<MathTool>("math", "数学计算工具集");

        // Act
        registry.RegisterVirtualTool(virtualTool);

        // Assert
        registry.GetVirtualTools().Should().ContainSingle();
        registry.GetAllToolSets().Should().ContainSingle();
        registry.HasTool("math").Should().BeTrue(); // 虚拟工具本身也被注册
    }

    [Fact]
    public void ToolRegistry_GetToolsByCategory_ShouldReturnFilteredTools()
    {
        // Arrange
        var registry = new ToolRegistry();
        registry.RegisterToolsFromType<MathTool>();
        registry.RegisterToolsFromType<DateTimeTool>();

        // Act
        var mathTools = registry.GetToolsByCategory("Math");
        var dateTimeTools = registry.GetToolsByCategory("DateTime");

        // Assert
        mathTools.Should().NotBeEmpty();
        mathTools.Should().OnlyContain(t => t.Category == "Math");

        dateTimeTools.Should().NotBeEmpty();
        dateTimeTools.Should().OnlyContain(t => t.Category == "DateTime");
    }

    [Fact]
    public void ToolRegistry_GetCategories_ShouldReturnAllCategories()
    {
        // Arrange
        var registry = new ToolRegistry();
        registry.RegisterToolsFromType<MathTool>();
        registry.RegisterToolsFromType<DateTimeTool>();

        // Act
        var categories = registry.GetCategories();

        // Assert
        categories.Should().Contain("Math");
        categories.Should().Contain("DateTime");
    }
}
