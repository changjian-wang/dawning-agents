using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools;
using Dawning.Agents.Core.Tools.BuiltIn;
using FluentAssertions;
using Xunit;

namespace Dawning.Agents.Tests.Tools;

/// <summary>
/// ToolApprovalHandler 测试
/// </summary>
public class ToolApprovalHandlerTests
{
    /// <summary>
    /// 创建测试用 Mock 工具
    /// </summary>
    private static ITool CreateMockTool(
        string name,
        ToolRiskLevel riskLevel = ToolRiskLevel.Low,
        bool requiresConfirmation = false
    )
    {
        return new MockTool(name, riskLevel, requiresConfirmation);
    }

    /// <summary>
    /// 简单的 Mock 工具实现用于测试
    /// </summary>
    private class MockTool : ITool
    {
        public string Name { get; }
        public string Description => $"Mock tool: {Name}";
        public string ParametersSchema => "{}";
        public bool RequiresConfirmation { get; }
        public ToolRiskLevel RiskLevel { get; }
        public string? Category => "Test";

        public MockTool(string name, ToolRiskLevel riskLevel, bool requiresConfirmation)
        {
            Name = name;
            RiskLevel = riskLevel;
            RequiresConfirmation = requiresConfirmation;
        }

        public Task<ToolResult> ExecuteAsync(string input, CancellationToken ct = default)
        {
            return Task.FromResult(new ToolResult { Output = "Mock result" });
        }
    }

    [Fact]
    public async Task RequestApprovalAsync_AlwaysApprove_ShouldReturnTrue()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.AlwaysApprove);
        var tool = CreateMockTool("TestTool", ToolRiskLevel.Low);

        // Act
        var approved = await handler.RequestApprovalAsync(tool, """{"a": 1, "b": 2}""");

        // Assert
        approved.Should().BeTrue();
    }

    [Fact]
    public async Task RequestApprovalAsync_AlwaysDeny_ShouldReturnFalse()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.AlwaysDeny);
        var tool = CreateMockTool("TestTool", ToolRiskLevel.Low);

        // Act
        var approved = await handler.RequestApprovalAsync(tool, """{"a": 1, "b": 2}""");

        // Assert
        approved.Should().BeFalse();
    }

    [Fact]
    public async Task RequestApprovalAsync_RiskBased_LowRisk_ShouldApprove()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.RiskBased);
        var tool = CreateMockTool("TestTool", ToolRiskLevel.Low);

        // Act
        var approved = await handler.RequestApprovalAsync(tool, """{"a": 1, "b": 2}""");

        // Assert
        approved.Should().BeTrue();
        tool.RiskLevel.Should().Be(ToolRiskLevel.Low);
    }

    [Fact]
    public async Task RequestApprovalAsync_RiskBased_HighRisk_ShouldDeny()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.RiskBased);
        var tool = CreateMockTool("RunCommand", ToolRiskLevel.High);

        // Act
        var approved = await handler.RequestApprovalAsync(tool, """{"command": "ls"}""");

        // Assert
        approved.Should().BeFalse();
        tool.RiskLevel.Should().Be(ToolRiskLevel.High);
    }

    [Fact]
    public async Task RequestApprovalAsync_Interactive_ShouldDenyByDefault()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.Interactive);
        var tool = CreateMockTool("TestTool", ToolRiskLevel.Low);

        // Act
        var approved = await handler.RequestApprovalAsync(tool, """{"a": 1, "b": 2}""");

        // Assert
        approved.Should().BeFalse(); // 交互模式默认拒绝，需要 UI 实现
    }

    [Fact]
    public async Task RequestUrlApprovalAsync_Localhost_ShouldApprove()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.RiskBased);
        var tool = CreateMockTool("HttpGet", ToolRiskLevel.Medium);

        // Act
        var approved = await handler.RequestUrlApprovalAsync(
            tool,
            "http://localhost:8080/api/test"
        );

        // Assert
        approved.Should().BeTrue();
    }

    [Fact]
    public async Task RequestUrlApprovalAsync_TrustedDomain_ShouldApprove()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.RiskBased);
        var tool = CreateMockTool("HttpGet", ToolRiskLevel.Medium);

        // Act
        var approved = await handler.RequestUrlApprovalAsync(
            tool,
            "https://api.github.com/repos/test"
        );

        // Assert
        approved.Should().BeTrue();
    }

    [Fact]
    public async Task RequestUrlApprovalAsync_UntrustedDomain_ShouldDeny()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.RiskBased);
        var tool = CreateMockTool("HttpGet", ToolRiskLevel.Medium);

        // Act
        var approved = await handler.RequestUrlApprovalAsync(
            tool,
            "https://unknown-site.example.com/api"
        );

        // Assert
        approved.Should().BeFalse();
    }

    [Fact]
    public async Task RequestCommandApprovalAsync_SafeCommand_ShouldApprove()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.RiskBased);
        var tool = CreateMockTool("RunCommand", ToolRiskLevel.High);

        // Act
        var approved = await handler.RequestCommandApprovalAsync(tool, "git status");

        // Assert
        approved.Should().BeTrue();
    }

    [Fact]
    public async Task RequestCommandApprovalAsync_DangerousCommand_ShouldDeny()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.AlwaysApprove);
        var tool = CreateMockTool("RunCommand", ToolRiskLevel.High);

        // Act
        var approved = await handler.RequestCommandApprovalAsync(tool, "rm -rf /");

        // Assert
        approved.Should().BeFalse(); // 即使 AlwaysApprove，危险命令也应被拒绝
    }

    [Fact]
    public async Task AddAutoApprovedUrl_ShouldBypassCheck()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.AlwaysDeny);
        handler.AddAutoApprovedUrl("https://my-api.example.com/endpoint");
        var tool = CreateMockTool("HttpGet", ToolRiskLevel.Medium);

        // Act
        var approved = await handler.RequestUrlApprovalAsync(
            tool,
            "https://my-api.example.com/endpoint"
        );

        // Assert
        approved.Should().BeTrue();
    }

    [Fact]
    public async Task AddAutoApprovedCommand_ShouldBypassCheck()
    {
        // Arrange
        var handler = new DefaultToolApprovalHandler(ApprovalStrategy.AlwaysDeny);
        handler.AddAutoApprovedCommand("npm install");
        var tool = CreateMockTool("RunCommand", ToolRiskLevel.High);

        // Act
        var approved = await handler.RequestCommandApprovalAsync(tool, "npm install");

        // Assert
        approved.Should().BeTrue();
    }
}
