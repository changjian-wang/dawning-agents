using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools;
using Dawning.Agents.Core.Tools.BuiltIn;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Dawning.Agents.Tests.Tools.BuiltIn;

/// <summary>
/// BuiltInToolExtensions 单元测试
/// </summary>
public sealed class BuiltInToolExtensionsTests
{
    #region AddBuiltInTools 测试

    [Fact]
    public void AddBuiltInTools_ShouldRegisterBasicTools()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBuiltInTools();

        // Assert
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();
        var registry = provider.GetRequiredService<IToolRegistry>();

        // 验证基础工具已注册
        var tools = registry.GetAllTools();
        tools.Should().NotBeEmpty();
    }

    [Fact]
    public void AddBuiltInTools_ShouldNotIncludeHighRiskTools()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBuiltInTools();

        // Assert
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();
        var registry = provider.GetRequiredService<IToolRegistry>();
        var tools = registry.GetAllTools();

        // 验证不包含高风险工具（FileSystem、Git、Process）
        tools.Should().NotContain(t => t.Category == "FileSystem");
        tools.Should().NotContain(t => t.Category == "Git");
        tools.Should().NotContain(t => t.Category == "Process");
    }

    #endregion

    #region AddAllBuiltInTools 测试

    [Fact]
    public void AddAllBuiltInTools_ShouldRegisterAllTools()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAllBuiltInTools();

        // Assert
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();
        var registry = provider.GetRequiredService<IToolRegistry>();
        var tools = registry.GetAllTools();

        // 验证包含所有类别的工具
        tools.Should().NotBeEmpty();
        tools.Should().Contain(t => t.Category == "FileSystem");
        tools.Should().Contain(t => t.Category == "Git");
        tools.Should().Contain(t => t.Category == "Process");
    }

    [Fact]
    public void AddAllBuiltInTools_ShouldMarkHighRiskToolsCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAllBuiltInTools();

        // Assert
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();
        var registry = provider.GetRequiredService<IToolRegistry>();
        var tools = registry.GetAllTools();

        // 验证高风险工具标记正确
        var deleteTools = tools.Where(t => t.Name.ToLower().Contains("delete")).ToList();
        if (deleteTools.Count > 0)
        {
            deleteTools.Should().OnlyContain(t => t.RiskLevel >= ToolRiskLevel.Medium);
        }
    }

    #endregion

    #region 单独分类添加测试

    [Fact]
    public void AddDateTimeTools_ShouldRegisterDateTimeTools()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDateTimeTools();

        // Assert
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();
        var registry = provider.GetRequiredService<IToolRegistry>();
        var tools = registry.GetAllTools();
        tools.Should().NotBeEmpty();
    }

    [Fact]
    public void AddMathTools_ShouldRegisterMathTools()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMathTools();

        // Assert
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();
        var registry = provider.GetRequiredService<IToolRegistry>();
        var tools = registry.GetAllTools();
        tools.Should().NotBeEmpty();
    }

    [Fact]
    public void AddJsonTools_ShouldRegisterJsonTools()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddJsonTools();

        // Assert
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();
        var registry = provider.GetRequiredService<IToolRegistry>();
        var tools = registry.GetAllTools();
        tools.Should().NotBeEmpty();
    }

    [Fact]
    public void AddUtilityTools_ShouldRegisterUtilityTools()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddUtilityTools();

        // Assert
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();
        var registry = provider.GetRequiredService<IToolRegistry>();
        var tools = registry.GetAllTools();
        tools.Should().NotBeEmpty();
    }

    [Fact]
    public void AddFileSystemTools_ShouldRegisterFileSystemTools()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddFileSystemTools();

        // Assert
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();
        var registry = provider.GetRequiredService<IToolRegistry>();
        var tools = registry.GetAllTools();
        tools.Should().NotBeEmpty();
        tools.Should().Contain(t => t.Category == "FileSystem");
    }

    [Fact]
    public void AddGitTools_ShouldRegisterGitTools()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddGitTools();

        // Assert
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();
        var registry = provider.GetRequiredService<IToolRegistry>();
        var tools = registry.GetAllTools();
        tools.Should().NotBeEmpty();
        tools.Should().Contain(t => t.Category == "Git");
    }

    [Fact]
    public void AddProcessTools_ShouldRegisterProcessTools()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddProcessTools();

        // Assert
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();
        var registry = provider.GetRequiredService<IToolRegistry>();
        var tools = registry.GetAllTools();
        tools.Should().NotBeEmpty();
        tools.Should().Contain(t => t.Category == "Process");
    }

    #endregion

    #region 链式调用测试

    [Fact]
    public void MultipleAdd_ShouldChainCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDateTimeTools().AddMathTools().AddJsonTools();

        // Assert
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();
        var registry = provider.GetRequiredService<IToolRegistry>();
        var tools = registry.GetAllTools();

        // 应该包含多种工具
        tools.Count.Should().BeGreaterThan(5);
    }

    [Fact]
    public void DuplicateAdd_ShouldNotDuplicateTools()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMathTools();
        services.AddMathTools();

        // Assert
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();
        var registry = provider.GetRequiredService<IToolRegistry>();
        var tools = registry.GetAllTools();

        // 不应该有重复的工具
        var distinct = tools.Select(t => t.Name).Distinct().Count();
        distinct.Should().Be(tools.Count);
    }

    #endregion
}
