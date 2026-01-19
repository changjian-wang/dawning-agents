using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools;
using Dawning.Agents.Core.Tools.BuiltIn;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Dawning.Agents.Tests.Tools;

/// <summary>
/// 内置工具测试
/// </summary>
public class BuiltInToolTests
{
    [Fact]
    public void AddAllBuiltInTools_ShouldRegisterAllTools()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAllBuiltInTools();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();

        // Assert
        var tools = registry.GetAllTools().ToList();
        tools.Should().NotBeEmpty();

        // 验证各类工具都已注册
        tools.Should().Contain(t => t.Category == "DateTime");
        tools.Should().Contain(t => t.Category == "Math");
        tools.Should().Contain(t => t.Category == "Json");
        tools.Should().Contain(t => t.Category == "Utility");
        tools.Should().Contain(t => t.Category == "FileSystem");
        tools.Should().Contain(t => t.Category == "Process");
        tools.Should().Contain(t => t.Category == "Git");
    }

    [Fact]
    public void HighRiskTools_ShouldRequireConfirmation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAllBuiltInTools();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();

        // Act
        var highRiskTools = registry
            .GetAllTools()
            .Where(t => t.RiskLevel == ToolRiskLevel.High)
            .ToList();

        // Assert
        highRiskTools.Should().NotBeEmpty("应该存在高风险工具");
        highRiskTools
            .Should()
            .OnlyContain(t => t.RequiresConfirmation, "所有高风险工具都应该需要确认");
    }

    [Fact]
    public void FileSystemTools_ShouldHaveCorrectRiskLevels()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddFileSystemTools();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();

        // Assert - 读取操作应该是低风险
        var readTool = registry.GetTool("ReadFile");
        readTool.Should().NotBeNull();
        readTool!.RiskLevel.Should().Be(ToolRiskLevel.Low);
        readTool.RequiresConfirmation.Should().BeFalse();

        // Assert - 删除操作应该是高风险
        var deleteTool = registry.GetTool("DeleteFile");
        deleteTool.Should().NotBeNull();
        deleteTool!.RiskLevel.Should().Be(ToolRiskLevel.High);
        deleteTool.RequiresConfirmation.Should().BeTrue();
    }

    [Fact]
    public void ProcessTool_RunCommand_ShouldRequireConfirmation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddProcessTools();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();

        // Act
        var runCommandTool = registry.GetTool("RunCommand");

        // Assert
        runCommandTool.Should().NotBeNull();
        runCommandTool!.RequiresConfirmation.Should().BeTrue();
        runCommandTool.RiskLevel.Should().Be(ToolRiskLevel.High);
        runCommandTool.Category.Should().Be("Process");
    }

    [Fact]
    public void GitTools_ShouldHaveCorrectCategories()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddGitTools();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();

        // Act
        var gitTools = registry.GetAllTools().Where(t => t.Category == "Git").ToList();

        // Assert
        gitTools.Should().NotBeEmpty();
        gitTools.Should().OnlyContain(t => t.Category == "Git");

        // 只读操作不需要确认
        var statusTool = registry.GetTool("GitStatus");
        statusTool.Should().NotBeNull();
        statusTool!.RequiresConfirmation.Should().BeFalse();

        // 推送操作需要确认
        var pushTool = registry.GetTool("GitPush");
        pushTool.Should().NotBeNull();
        pushTool!.RequiresConfirmation.Should().BeTrue();
        pushTool.RiskLevel.Should().Be(ToolRiskLevel.High);
    }

    [Fact]
    public void ToolResult_NeedConfirmation_ShouldSetCorrectProperties()
    {
        // Act
        var result = ToolResult.NeedConfirmation("请确认是否删除文件?");

        // Assert
        result.Success.Should().BeFalse();
        result.RequiresConfirmation.Should().BeTrue();
        result.ConfirmationMessage.Should().Be("请确认是否删除文件?");
    }

    [Fact]
    public async Task FileSystemTool_ReadFile_ShouldWork()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddFileSystemTools();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();
        var readTool = registry.GetTool("ReadFile");

        // 创建临时文件
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "Hello, World!");

        try
        {
            // Act
            var result = await readTool!.ExecuteAsync(
                $@"{{""filePath"": ""{tempFile.Replace("\\", "\\\\")}""}}"
            );

            // Assert
            result.Success.Should().BeTrue();
            result.Output.Should().Contain("Hello, World!");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task FileSystemTool_ListDirectory_ShouldWork()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddFileSystemTools();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();
        var listTool = registry.GetTool("ListDirectory");

        // Act
        var result = await listTool!.ExecuteAsync(
            $@"{{""directoryPath"": ""{Path.GetTempPath().Replace("\\", "\\\\")}""}}"
        );

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ProcessTool_ListProcesses_ShouldWork()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddProcessTools();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();
        var listTool = registry.GetTool("ListProcesses");

        // Act
        var result = await listTool!.ExecuteAsync("");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("PID");
    }

    [Fact]
    public async Task ProcessTool_GetEnvironmentVariable_ShouldWork()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddProcessTools();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();
        var envTool = registry.GetTool("GetEnvironmentVariable");

        // Act - 不传参数则列出所有环境变量
        var result = await envTool!.ExecuteAsync("");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("环境变量");
    }

    [Fact]
    public void MethodTool_ShouldExposeNewProperties()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddProcessTools();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();

        // Act
        var tool = registry.GetTool("RunCommand");

        // Assert
        tool.Should().NotBeNull();
        tool.Should().BeAssignableTo<ITool>();

        // 验证新属性
        tool!.RequiresConfirmation.Should().BeTrue();
        tool.RiskLevel.Should().Be(ToolRiskLevel.High);
        tool.Category.Should().Be("Process");
    }
}
