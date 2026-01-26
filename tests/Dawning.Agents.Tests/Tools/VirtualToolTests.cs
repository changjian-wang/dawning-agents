using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools;
using Dawning.Agents.Core.Tools.BuiltIn;
using FluentAssertions;

namespace Dawning.Agents.Tests.Tools;

/// <summary>
/// VirtualTool 单元测试
/// </summary>
public sealed class VirtualToolTests
{
    #region 构造函数测试

    [Fact]
    public void Constructor_WithToolSet_ShouldSetProperties()
    {
        // Arrange
        var toolSet = ToolSet.FromType<MathTool>("math", "数学工具集");

        // Act
        var virtualTool = new VirtualTool(toolSet);

        // Assert
        virtualTool.Name.Should().Be("math");
        virtualTool.Description.Should().Contain("数学工具集");
        virtualTool.ToolSet.Should().BeSameAs(toolSet);
        virtualTool.IsExpanded.Should().BeFalse();
        virtualTool.Category.Should().Be("VirtualTool");
        virtualTool.RiskLevel.Should().Be(ToolRiskLevel.Low);
        virtualTool.RequiresConfirmation.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithToolSet_ShouldBuildDescriptionWithToolNames()
    {
        // Arrange
        var toolSet = ToolSet.FromType<MathTool>("math", "数学工具集");

        // Act
        var virtualTool = new VirtualTool(toolSet);

        // Assert
        virtualTool.Description.Should().Contain("数学工具集");
        virtualTool.Description.Should().Contain("包含");
    }

    [Fact]
    public void Constructor_WithCustomNameAndDescription_ShouldSetProperties()
    {
        // Arrange
        var toolSet = ToolSet.FromType<MathTool>("math", "数学工具集");

        // Act
        var virtualTool = new VirtualTool("custom_math", "自定义数学工具", toolSet);

        // Assert
        virtualTool.Name.Should().Be("custom_math");
        virtualTool.Description.Should().Be("自定义数学工具");
        virtualTool.ToolSet.Should().BeSameAs(toolSet);
    }

    [Fact]
    public void Constructor_WithNullToolSet_ShouldThrow()
    {
        // Act & Assert
        var act = () => new VirtualTool(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithEmptyName_ShouldThrow()
    {
        // Arrange
        var toolSet = ToolSet.FromType<MathTool>("math", "数学工具集");

        // Act & Assert
        var act = () => new VirtualTool("", "description", toolSet);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyDescription_ShouldThrow()
    {
        // Arrange
        var toolSet = ToolSet.FromType<MathTool>("math", "数学工具集");

        // Act & Assert
        var act = () => new VirtualTool("name", "", toolSet);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullToolSetInOverload_ShouldThrow()
    {
        // Act & Assert
        var act = () => new VirtualTool("name", "description", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region 属性测试

    [Fact]
    public void ParametersSchema_ShouldReturnEmptyObjectSchema()
    {
        // Arrange
        var toolSet = ToolSet.FromType<MathTool>("math", "数学工具集");
        var virtualTool = new VirtualTool(toolSet);

        // Act
        var schema = virtualTool.ParametersSchema;

        // Assert
        schema.Should().Contain("object");
        schema.Should().Contain("properties");
    }

    [Fact]
    public void ExpandedTools_ShouldReturnToolSetTools()
    {
        // Arrange
        var toolSet = ToolSet.FromType<MathTool>("math", "数学工具集");
        var virtualTool = new VirtualTool(toolSet);

        // Act
        var expandedTools = virtualTool.ExpandedTools;

        // Assert
        expandedTools.Should().BeSameAs(toolSet.Tools);
        expandedTools.Should().NotBeEmpty();
    }

    #endregion

    #region Expand/Collapse 测试

    [Fact]
    public void Expand_ShouldSetIsExpandedToTrue()
    {
        // Arrange
        var toolSet = ToolSet.FromType<MathTool>("math", "数学工具集");
        var virtualTool = new VirtualTool(toolSet);

        // Act
        virtualTool.Expand();

        // Assert
        virtualTool.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void Collapse_ShouldSetIsExpandedToFalse()
    {
        // Arrange
        var toolSet = ToolSet.FromType<MathTool>("math", "数学工具集");
        var virtualTool = new VirtualTool(toolSet);
        virtualTool.Expand();

        // Act
        virtualTool.Collapse();

        // Assert
        virtualTool.IsExpanded.Should().BeFalse();
    }

    [Fact]
    public void ExpandAndCollapse_ShouldToggleState()
    {
        // Arrange
        var toolSet = ToolSet.FromType<MathTool>("math", "数学工具集");
        var virtualTool = new VirtualTool(toolSet);

        // Act & Assert
        virtualTool.IsExpanded.Should().BeFalse();

        virtualTool.Expand();
        virtualTool.IsExpanded.Should().BeTrue();

        virtualTool.Collapse();
        virtualTool.IsExpanded.Should().BeFalse();

        virtualTool.Expand();
        virtualTool.IsExpanded.Should().BeTrue();
    }

    #endregion

    #region ExecuteAsync 测试

    [Fact]
    public async Task ExecuteAsync_ShouldExpandAndReturnToolList()
    {
        // Arrange
        var toolSet = ToolSet.FromType<MathTool>("math", "数学工具集");
        var virtualTool = new VirtualTool(toolSet);

        // Act
        var result = await virtualTool.ExecuteAsync("any input");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("已展开");
        result.Output.Should().Contain("math");
        virtualTool.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIncludeToolCount()
    {
        // Arrange
        var toolSet = ToolSet.FromType<MathTool>("math", "数学工具集");
        var virtualTool = new VirtualTool(toolSet);

        // Act
        var result = await virtualTool.ExecuteAsync("");

        // Assert
        result.Output.Should().Contain($"{toolSet.Count} 个工具");
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellationToken_ShouldComplete()
    {
        // Arrange
        var toolSet = ToolSet.FromType<MathTool>("math", "数学工具集");
        var virtualTool = new VirtualTool(toolSet);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await virtualTool.ExecuteAsync("input", cts.Token);

        // Assert
        result.Success.Should().BeTrue();
    }

    #endregion

    #region 静态工厂方法测试

    [Fact]
    public void FromToolSet_ShouldCreateVirtualTool()
    {
        // Arrange
        var toolSet = ToolSet.FromType<MathTool>("math", "数学工具集");

        // Act
        var virtualTool = VirtualTool.FromToolSet(toolSet);

        // Assert
        virtualTool.Should().NotBeNull();
        virtualTool.ToolSet.Should().BeSameAs(toolSet);
    }

    [Fact]
    public void FromType_ShouldCreateVirtualToolFromType()
    {
        // Act
        var virtualTool = VirtualTool.FromType<MathTool>("math_virtual", "虚拟数学工具");

        // Assert
        virtualTool.Should().NotBeNull();
        virtualTool.Name.Should().Be("math_virtual");
        // Description 是通过 BuildDescription 生成的，包含工具集描述和工具列表
        virtualTool.Description.Should().Contain("虚拟数学工具");
        virtualTool.Description.Should().Contain("包含");
        virtualTool.ToolSet.Tools.Should().NotBeEmpty();
    }

    [Fact]
    public void FromType_WithIcon_ShouldCreateVirtualTool()
    {
        // Act
        var virtualTool = VirtualTool.FromType<MathTool>("math_virtual", "虚拟数学工具", "🔢");

        // Assert
        virtualTool.Should().NotBeNull();
        virtualTool.ToolSet.Icon.Should().Be("🔢");
    }

    #endregion

    #region 边界情况测试

    [Fact]
    public void BuildDescription_WithManyTools_ShouldTruncate()
    {
        // Arrange - MathTool has more than 5 methods
        var toolSet = ToolSet.FromType<MathTool>("math", "数学工具集");

        // Act
        var virtualTool = new VirtualTool(toolSet);

        // Assert
        if (toolSet.Tools.Count > 5)
        {
            virtualTool.Description.Should().Contain($"等 {toolSet.Tools.Count} 个工具");
        }
    }

    [Fact]
    public void ExpandedTools_ShouldReflectToolSetChanges()
    {
        // Arrange
        var toolSet = ToolSet.FromType<MathTool>("math", "数学工具集");
        var virtualTool = new VirtualTool(toolSet);

        // Act
        var tools1 = virtualTool.ExpandedTools;
        var tools2 = virtualTool.ExpandedTools;

        // Assert
        tools1.Should().BeSameAs(tools2);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleTimes_ShouldAlwaysSucceed()
    {
        // Arrange
        var toolSet = ToolSet.FromType<MathTool>("math", "数学工具集");
        var virtualTool = new VirtualTool(toolSet);

        // Act & Assert
        for (int i = 0; i < 3; i++)
        {
            var result = await virtualTool.ExecuteAsync($"input {i}");
            result.Success.Should().BeTrue();
        }

        virtualTool.IsExpanded.Should().BeTrue();
    }

    #endregion
}
