using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools;
using Dawning.Agents.Core.Tools.BuiltIn;
using FluentAssertions;
using Xunit;

namespace Dawning.Agents.Tests.Tools;

/// <summary>
/// ToolSelector 测试
/// </summary>
public class ToolSelectorTests
{
    private readonly DefaultToolSelector _selector = new();

    [Fact]
    public async Task SelectToolsAsync_ShouldReturnAllIfUnderLimit()
    {
        // Arrange
        var registry = new ToolRegistry();
        registry.RegisterToolsFromType<MathTool>();
        var tools = registry.GetAllTools();

        // Act
        var selected = await _selector.SelectToolsAsync("calculate", tools, maxTools: 100);

        // Assert
        selected.Count.Should().Be(tools.Count);
    }

    [Fact]
    public async Task SelectToolsAsync_ShouldLimitResults()
    {
        // Arrange
        var registry = new ToolRegistry();
        registry.RegisterToolsFromType<MathTool>();
        registry.RegisterToolsFromType<DateTimeTool>();
        registry.RegisterToolsFromType<JsonTool>();
        var tools = registry.GetAllTools();

        // Act
        var selected = await _selector.SelectToolsAsync("something", tools, maxTools: 5);

        // Assert
        selected.Count.Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public async Task SelectToolsAsync_ShouldPrioritizeMatchingTools()
    {
        // Arrange
        var registry = new ToolRegistry();
        registry.RegisterToolsFromType<MathTool>();
        registry.RegisterToolsFromType<DateTimeTool>();
        var tools = registry.GetAllTools();

        // Act
        var selected = await _selector.SelectToolsAsync(
            "add numbers calculate",
            tools,
            maxTools: 3
        );

        // Assert
        selected.Should().Contain(t => t.Name.Contains("Add", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SelectToolsAsync_ShouldMatchCategory()
    {
        // Arrange
        var registry = new ToolRegistry();
        registry.RegisterToolsFromType<MathTool>();
        registry.RegisterToolsFromType<DateTimeTool>();
        var tools = registry.GetAllTools();

        // Act
        var selected = await _selector.SelectToolsAsync("math calculation", tools, maxTools: 5);

        // Assert
        selected.Should().OnlyContain(t => t.Category == "Math" || t.Category == "DateTime");
        // Math 工具应该排在前面
        selected.First().Category.Should().Be("Math");
    }

    [Fact]
    public async Task SelectToolsAsync_ShouldMatchTimeQuery()
    {
        // Arrange
        var registry = new ToolRegistry();
        registry.RegisterToolsFromType<MathTool>();
        registry.RegisterToolsFromType<DateTimeTool>();
        var tools = registry.GetAllTools();

        // Act
        var selected = await _selector.SelectToolsAsync("what time is it now", tools, maxTools: 3);

        // Assert
        selected.First().Category.Should().Be("DateTime");
    }

    [Fact]
    public async Task SelectToolSetsAsync_ShouldReturnAllIfUnderLimit()
    {
        // Arrange
        var toolSets = new List<IToolSet>
        {
            ToolSet.FromType<MathTool>("math", "数学工具"),
            ToolSet.FromType<DateTimeTool>("datetime", "时间工具"),
        };

        // Act
        var selected = await _selector.SelectToolSetsAsync("query", toolSets, maxToolSets: 10);

        // Assert
        selected.Count.Should().Be(2);
    }

    [Fact]
    public async Task SelectToolSetsAsync_ShouldPrioritizeMatchingToolSets()
    {
        // Arrange
        var toolSets = new List<IToolSet>
        {
            ToolSet.FromType<MathTool>("math", "数学计算工具"),
            ToolSet.FromType<DateTimeTool>("datetime", "日期时间工具"),
            ToolSet.FromType<JsonTool>("json", "JSON 处理工具"),
        };

        // Act
        var selected = await _selector.SelectToolSetsAsync(
            "calculate math",
            toolSets,
            maxToolSets: 2
        );

        // Assert
        selected.First().Name.Should().Be("math");
    }
}
