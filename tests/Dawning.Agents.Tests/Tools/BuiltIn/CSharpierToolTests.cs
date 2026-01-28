using Dawning.Agents.Core.Tools.BuiltIn;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Dawning.Agents.Tests.Tools.BuiltIn;

/// <summary>
/// CSharpierTool 单元测试
/// </summary>
public class CSharpierToolTests
{
    private readonly CSharpierTool _tool;

    public CSharpierToolTests()
    {
        _tool = new CSharpierTool();
    }

    [Fact]
    public async Task FormatFile_WithEmptyPath_ReturnsFail()
    {
        // Act
        var result = await _tool.FormatFile("");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("不能为空");
    }

    [Fact]
    public async Task FormatFile_WithNonExistentFile_ReturnsFail()
    {
        // Act
        var result = await _tool.FormatFile("non_existent_file.cs");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("不存在");
    }

    [Fact]
    public async Task FormatFile_WithNonCsFile_ReturnsFail()
    {
        // Arrange
        var tempFile = Path.GetTempFileName(); // Creates .tmp file
        try
        {
            // Act
            var result = await _tool.FormatFile(tempFile);

            // Assert
            result.Success.Should().BeFalse();
            result.Error.Should().Contain(".cs");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task FormatDirectory_WithEmptyPath_ReturnsFail()
    {
        // Act
        var result = await _tool.FormatDirectory("");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("不能为空");
    }

    [Fact]
    public async Task FormatDirectory_WithNonExistentDirectory_ReturnsFail()
    {
        // Act
        var result = await _tool.FormatDirectory("non_existent_directory");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("不存在");
    }

    [Fact]
    public async Task FormatCode_WithEmptyCode_ReturnsFail()
    {
        // Act
        var result = await _tool.FormatCode("");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("不能为空");
    }

    [Fact]
    public void GetFormattingRules_ReturnsRules()
    {
        // Act
        var result = _tool.GetFormattingRules();

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("CSharpier");
        result.Output.Should().Contain("参数");
        result.Output.Should().Contain("方法链");
        result.Output.Should().Contain("if 语句");
    }

    [Fact]
    public void Constructor_WithDefaultOptions_UsesDefaults()
    {
        // Act
        var tool = new CSharpierTool();

        // Assert - 通过调用方法间接验证
        var result = tool.GetFormattingRules();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithCustomOptions_UsesCustomOptions()
    {
        // Arrange
        var options = new CSharpierToolOptions
        {
            CSharpierCommand = "custom-csharpier",
            TimeoutSeconds = 120
        };

        // Act
        var tool = new CSharpierTool(options);

        // Assert - 工具创建成功
        var result = tool.GetFormattingRules();
        result.Success.Should().BeTrue();
    }
}

/// <summary>
/// CSharpierToolOptions 测试
/// </summary>
public class CSharpierToolOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new CSharpierToolOptions();

        // Assert
        options.CSharpierCommand.Should().Be("dotnet-csharpier");
        options.TimeoutSeconds.Should().Be(60);
    }

    [Fact]
    public void CanSetCustomValues()
    {
        // Arrange & Act
        var options = new CSharpierToolOptions
        {
            CSharpierCommand = "csharpier",
            TimeoutSeconds = 120
        };

        // Assert
        options.CSharpierCommand.Should().Be("csharpier");
        options.TimeoutSeconds.Should().Be(120);
    }
}

/// <summary>
/// CSharpier DI 扩展测试
/// </summary>
public class CSharpierExtensionsTests
{
    [Fact]
    public void AddCSharpierTools_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCSharpierTools();

        // Assert
        var descriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(CSharpierToolOptions)
        );
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddCSharpierTools_WithOptions_RegistersWithCustomOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCSharpierTools(options =>
        {
            options.TimeoutSeconds = 120;
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<CSharpierToolOptions>();
        options.TimeoutSeconds.Should().Be(120);
    }
}
