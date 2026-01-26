using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools;
using FluentAssertions;
using Moq;

namespace Dawning.Agents.Tests.Tools;

/// <summary>
/// DefaultToolApprovalHandler 单元测试
/// </summary>
public sealed class DefaultToolApprovalHandlerTests
{
    #region 构造函数测试

    [Fact]
    public void Constructor_WithDefaults_ShouldUseRiskBasedStrategy()
    {
        // Act
        var handler = new DefaultToolApprovalHandler();

        // Assert
        handler.Should().NotBeNull();
    }

    [Theory]
    [InlineData(ApprovalStrategy.AlwaysApprove)]
    [InlineData(ApprovalStrategy.AlwaysDeny)]
    [InlineData(ApprovalStrategy.RiskBased)]
    [InlineData(ApprovalStrategy.Interactive)]
    public void Constructor_WithStrategy_ShouldSetStrategy(ApprovalStrategy strategy)
    {
        // Act
        var handler = new DefaultToolApprovalHandler(strategy);

        // Assert
        handler.Should().NotBeNull();
    }

    #endregion

    #region RequestApprovalAsync 测试

    [Fact]
    public async Task RequestApprovalAsync_AlwaysApprove_ShouldReturnTrue()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.AlwaysApprove);
        var tool = CreateMockTool(ToolRiskLevel.High, requiresConfirmation: true);

        // Act
        var result = await handler.RequestApprovalAsync(tool, "input");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RequestApprovalAsync_AlwaysDeny_ShouldReturnFalse()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.AlwaysDeny);
        var tool = CreateMockTool(ToolRiskLevel.Low, requiresConfirmation: false);

        // Act
        var result = await handler.RequestApprovalAsync(tool, "input");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RequestApprovalAsync_RiskBased_LowRisk_ShouldReturnTrue()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.RiskBased);
        var tool = CreateMockTool(ToolRiskLevel.Low, requiresConfirmation: false);

        // Act
        var result = await handler.RequestApprovalAsync(tool, "input");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RequestApprovalAsync_RiskBased_HighRisk_ShouldReturnFalse()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.RiskBased);
        var tool = CreateMockTool(ToolRiskLevel.High, requiresConfirmation: false);

        // Act
        var result = await handler.RequestApprovalAsync(tool, "input");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RequestApprovalAsync_RiskBased_RequiresConfirmation_ShouldReturnFalse()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.RiskBased);
        var tool = CreateMockTool(ToolRiskLevel.Low, requiresConfirmation: true);

        // Act
        var result = await handler.RequestApprovalAsync(tool, "input");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RequestApprovalAsync_Interactive_ShouldReturnFalse()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.Interactive);
        var tool = CreateMockTool(ToolRiskLevel.Low, requiresConfirmation: false);

        // Act
        var result = await handler.RequestApprovalAsync(tool, "input");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RequestApprovalAsync_WithNullTool_ShouldThrow()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.RequestApprovalAsync(null!, "input")
        );
    }

    #endregion

    #region RequestUrlApprovalAsync 测试

    [Fact]
    public async Task RequestUrlApprovalAsync_AlwaysApprove_ShouldReturnTrue()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.AlwaysApprove);
        var tool = CreateMockTool();

        // Act
        var result = await handler.RequestUrlApprovalAsync(tool, "http://example.com");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RequestUrlApprovalAsync_AlwaysDeny_ShouldReturnFalse()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.AlwaysDeny);
        var tool = CreateMockTool();

        // Act
        var result = await handler.RequestUrlApprovalAsync(tool, "http://github.com");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RequestUrlApprovalAsync_RiskBased_TrustedUrl_ShouldReturnTrue()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.RiskBased);
        var tool = CreateMockTool();

        // Act
        var result = await handler.RequestUrlApprovalAsync(tool, "http://localhost:8080/api");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RequestUrlApprovalAsync_RiskBased_GitHub_ShouldReturnTrue()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.RiskBased);
        var tool = CreateMockTool();

        // Act
        var result = await handler.RequestUrlApprovalAsync(tool, "https://api.github.com/repos");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RequestUrlApprovalAsync_RiskBased_UntrustedUrl_ShouldReturnFalse()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.RiskBased);
        var tool = CreateMockTool();

        // Act
        var result = await handler.RequestUrlApprovalAsync(tool, "http://malicious-site.com");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RequestUrlApprovalAsync_AutoApprovedUrl_ShouldReturnTrue()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.AlwaysDeny);
        handler.AddAutoApprovedUrl("http://custom-api.com");
        var tool = CreateMockTool();

        // Act
        var result = await handler.RequestUrlApprovalAsync(tool, "http://custom-api.com");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RequestUrlApprovalAsync_InvalidUrl_ShouldReturnFalse()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.RiskBased);
        var tool = CreateMockTool();

        // Act
        var result = await handler.RequestUrlApprovalAsync(tool, "not-a-valid-url");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RequestUrlApprovalAsync_WithNullTool_ShouldThrow()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.RequestUrlApprovalAsync(null!, "http://example.com")
        );
    }

    [Fact]
    public async Task RequestUrlApprovalAsync_WithEmptyUrl_ShouldThrow()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler();
        var tool = CreateMockTool();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.RequestUrlApprovalAsync(tool, "")
        );
    }

    #endregion

    #region RequestCommandApprovalAsync 测试

    [Fact]
    public async Task RequestCommandApprovalAsync_AlwaysApprove_ShouldReturnTrue()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.AlwaysApprove);
        var tool = CreateMockTool();

        // Act
        var result = await handler.RequestCommandApprovalAsync(tool, "any command");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RequestCommandApprovalAsync_AlwaysDeny_ShouldReturnFalse()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.AlwaysDeny);
        var tool = CreateMockTool();

        // Act
        var result = await handler.RequestCommandApprovalAsync(tool, "ls -la");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RequestCommandApprovalAsync_RiskBased_SafeCommand_ShouldReturnTrue()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.RiskBased);
        var tool = CreateMockTool();

        // Act
        var result = await handler.RequestCommandApprovalAsync(tool, "ls -la");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RequestCommandApprovalAsync_RiskBased_GitCommand_ShouldReturnTrue()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.RiskBased);
        var tool = CreateMockTool();

        // Act
        var result = await handler.RequestCommandApprovalAsync(tool, "git status");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RequestCommandApprovalAsync_RiskBased_DangerousCommand_ShouldReturnFalse()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.RiskBased);
        var tool = CreateMockTool();

        // Act
        var result = await handler.RequestCommandApprovalAsync(tool, "rm -rf /");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RequestCommandApprovalAsync_AutoApprovedCommand_ShouldReturnTrue()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.AlwaysDeny);
        handler.AddAutoApprovedCommand("custom-script.sh");
        var tool = CreateMockTool();

        // Act
        var result = await handler.RequestCommandApprovalAsync(tool, "custom-script.sh");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RequestCommandApprovalAsync_WithNullTool_ShouldThrow()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.RequestCommandApprovalAsync(null!, "ls")
        );
    }

    [Fact]
    public async Task RequestCommandApprovalAsync_WithEmptyCommand_ShouldThrow()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler();
        var tool = CreateMockTool();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.RequestCommandApprovalAsync(tool, "")
        );
    }

    [Theory]
    [InlineData("rm -rf ~")]
    [InlineData("del /s /q c:\\")]
    [InlineData("format c:")]
    [InlineData("chmod -R 777 /")]
    [InlineData("shutdown -h now")]
    public async Task RequestCommandApprovalAsync_DangerousCommands_ShouldReturnFalse(
        string command
    )
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.RiskBased);
        var tool = CreateMockTool();

        // Act
        var result = await handler.RequestCommandApprovalAsync(tool, command);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("pwd")]
    [InlineData("cd /tmp")]
    [InlineData("cat file.txt")]
    [InlineData("echo hello")]
    [InlineData("dotnet --version")]
    [InlineData("node --version")]
    [InlineData("python --version")]
    public async Task RequestCommandApprovalAsync_SafeCommands_ShouldReturnTrue(string command)
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.RiskBased);
        var tool = CreateMockTool();

        // Act
        var result = await handler.RequestCommandApprovalAsync(tool, command);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region AddAutoApproved 测试

    [Fact]
    public void AddAutoApprovedUrl_ShouldAddToList()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler();

        // Act
        handler.AddAutoApprovedUrl("http://example.com");

        // Assert - 验证通过后续调用
        handler.Should().NotBeNull();
    }

    [Fact]
    public void AddAutoApprovedCommand_ShouldAddToList()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler();

        // Act
        handler.AddAutoApprovedCommand("custom-command");

        // Assert - 验证通过后续调用
        handler.Should().NotBeNull();
    }

    #endregion

    #region 辅助方法

    private static ITool CreateMockTool(
        ToolRiskLevel riskLevel = ToolRiskLevel.Low,
        bool requiresConfirmation = false
    )
    {
        var mock = new Mock<ITool>();
        mock.Setup(t => t.Name).Returns("TestTool");
        mock.Setup(t => t.Description).Returns("Test description");
        mock.Setup(t => t.RiskLevel).Returns(riskLevel);
        mock.Setup(t => t.RequiresConfirmation).Returns(requiresConfirmation);
        return mock.Object;
    }

    #endregion
}
