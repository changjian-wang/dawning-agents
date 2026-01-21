using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools;
using Dawning.Agents.Core.Tools.BuiltIn;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Dawning.Agents.Tests.Tools;

/// <summary>
/// PackageManagerTool 单元测试
/// </summary>
public class PackageManagerToolTests
{
    #region PackageManagerOptions Tests

    [Fact]
    public void PackageManagerOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new PackageManagerOptions();

        // Assert
        options.AllowWinget.Should().BeTrue();
        options.AllowPip.Should().BeTrue();
        options.AllowNpm.Should().BeTrue();
        options.AllowDotnetTool.Should().BeTrue();
        options.DefaultTimeoutSeconds.Should().Be(300);
        options.RequireExplicitApproval.Should().BeTrue();
        options.WhitelistedPackages.Should().BeEmpty();
        options.BlacklistedPackages.Should().BeEmpty();
    }

    [Theory]
    [InlineData("git", "git", true)]
    [InlineData("Git", "git", true)]
    [InlineData("nodejs", "node*", true)]
    [InlineData("python3", "*python*", true)]
    [InlineData("random-package", "git", false)]
    public void PackageManagerOptions_IsWhitelisted_ShouldMatchPattern(
        string packageName,
        string pattern,
        bool expected
    )
    {
        // Arrange
        var options = new PackageManagerOptions { WhitelistedPackages = [pattern] };

        // Act
        var result = options.IsWhitelisted(packageName);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void PackageManagerOptions_IsWhitelisted_EmptyList_ShouldAllowAll()
    {
        // Arrange
        var options = new PackageManagerOptions { WhitelistedPackages = [] };

        // Act & Assert
        options.IsWhitelisted("any-package").Should().BeTrue();
        options.IsWhitelisted("another-package").Should().BeTrue();
    }

    [Theory]
    [InlineData("hack-tool", "*hack*", true)]
    [InlineData("some-crack", "*crack*", true)]
    [InlineData("normal-package", "*hack*", false)]
    public void PackageManagerOptions_IsBlacklisted_ShouldMatchPattern(
        string packageName,
        string pattern,
        bool expected
    )
    {
        // Arrange
        var options = new PackageManagerOptions { BlacklistedPackages = [pattern] };

        // Act
        var result = options.IsBlacklisted(packageName);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void PackageManagerOptions_Validate_InvalidTimeout_ShouldThrow()
    {
        // Arrange
        var options = new PackageManagerOptions { DefaultTimeoutSeconds = 0 };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*DefaultTimeoutSeconds*");
    }

    #endregion

    #region DI Registration Tests

    [Fact]
    public void AddPackageManagerTools_ShouldRegisterTools()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPackageManagerTools();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();

        // Assert
        var tools = registry.GetToolsByCategory("PackageManager").ToList();
        tools.Should().NotBeEmpty();
        tools
            .Should()
            .Contain(t =>
                t.Name.Contains("Winget")
                || t.Name.Contains("Pip")
                || t.Name.Contains("Npm")
                || t.Name.Contains("DotnetTool")
            );
    }

    [Fact]
    public void AddPackageManagerTools_WithConfiguration_ShouldApplyOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPackageManagerTools(options =>
        {
            options.AllowWinget = false;
            options.AllowPip = false;
            options.DefaultTimeoutSeconds = 600;
        });
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<PackageManagerOptions>();

        // Assert
        options.AllowWinget.Should().BeFalse();
        options.AllowPip.Should().BeFalse();
        options.DefaultTimeoutSeconds.Should().Be(600);
    }

    [Fact]
    public void PackageManagerTools_InstallMethods_ShouldRequireConfirmation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPackageManagerTools();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();

        // Act
        var installTools = registry
            .GetToolsByCategory("PackageManager")
            .Where(t =>
                t.Name.Contains("Install")
                || t.Name.Contains("Uninstall")
                || t.Name.Contains("Update")
            )
            .ToList();

        // Assert
        installTools.Should().NotBeEmpty("应该存在安装/卸载工具");
        installTools
            .Should()
            .OnlyContain(
                t => t.RequiresConfirmation && t.RiskLevel == ToolRiskLevel.High,
                "所有安装/卸载操作都应该是高风险并需要确认"
            );
    }

    [Fact]
    public void PackageManagerTools_SearchAndListMethods_ShouldBeLowRisk()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPackageManagerTools();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();

        // Act
        var readOnlyTools = registry
            .GetToolsByCategory("PackageManager")
            .Where(t =>
                t.Name.Contains("Search")
                || t.Name.Contains("List")
                || t.Name.Contains("Show")
                || t.Name.Contains("View")
            )
            .ToList();

        // Assert
        readOnlyTools.Should().NotBeEmpty("应该存在只读工具");
        readOnlyTools
            .Should()
            .OnlyContain(
                t => t.RiskLevel == ToolRiskLevel.Low && !t.RequiresConfirmation,
                "所有只读操作都应该是低风险且不需要确认"
            );
    }

    #endregion

    #region Tool Validation Tests

    [Fact]
    public async Task WingetSearch_EmptyQuery_ShouldFail()
    {
        // Arrange
        var tool = new PackageManagerTool();

        // Act
        var result = await tool.WingetSearch("");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("不能为空");
    }

    [Fact]
    public async Task WingetInstall_BlacklistedPackage_ShouldFail()
    {
        // Arrange
        var options = new PackageManagerOptions { BlacklistedPackages = ["*hack*", "*malware*"] };
        var tool = new PackageManagerTool(options);

        // Act
        var result = await tool.WingetInstall("some-hack-tool");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("黑名单");
    }

    [Fact]
    public async Task WingetInstall_NotInWhitelist_ShouldFail()
    {
        // Arrange
        var options = new PackageManagerOptions
        {
            WhitelistedPackages = ["Git.Git", "Microsoft.*"],
        };
        var tool = new PackageManagerTool(options);

        // Act
        var result = await tool.WingetInstall("RandomPackage.App");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("白名单");
    }

    [Fact]
    public async Task PipInstall_DisabledPip_ShouldFail()
    {
        // Arrange
        var options = new PackageManagerOptions { AllowPip = false };
        var tool = new PackageManagerTool(options);

        // Act
        var result = await tool.PipInstall("requests");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("已被配置禁用");
    }

    [Fact]
    public async Task NpmInstall_DisabledNpm_ShouldFail()
    {
        // Arrange
        var options = new PackageManagerOptions { AllowNpm = false };
        var tool = new PackageManagerTool(options);

        // Act
        var result = await tool.NpmInstall("lodash");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("已被配置禁用");
    }

    [Fact]
    public async Task DotnetToolInstall_DisabledDotnetTool_ShouldFail()
    {
        // Arrange
        var options = new PackageManagerOptions { AllowDotnetTool = false };
        var tool = new PackageManagerTool(options);

        // Act
        var result = await tool.DotnetToolInstall("dotnet-ef");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("已被配置禁用");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PipList_ShouldNotRequireValidation()
    {
        // Arrange - PipList 是只读操作，应该不受白名单/黑名单限制
        var options = new PackageManagerOptions
        {
            WhitelistedPackages = ["specific-package"], // 严格白名单
        };
        var tool = new PackageManagerTool(options);

        // Act - 这不是安装操作，应该可以执行（即使命令可能因环境问题失败）
        // 我们只验证它不会因为白名单验证而失败
        var result = await tool.PipList();

        // Assert - 要么成功，要么因为其他原因失败（如找不到 python），但不应因验证失败
        result.Error.Should().NotContain("白名单");
        result.Error.Should().NotContain("黑名单");
    }

    #endregion

    #region Tool Count Tests

    [Fact]
    public void PackageManagerTool_ShouldHaveExpectedMethodCount()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPackageManagerTools();
        var provider = services.BuildServiceProvider();
        provider.EnsureToolsRegistered();

        var registry = provider.GetRequiredService<IToolRegistry>();

        // Act
        var tools = registry.GetToolsByCategory("PackageManager").ToList();

        // Assert
        // Winget: Search, Show, Install, Uninstall, List (5)
        // Pip: List, Show, Install, Uninstall (4)
        // Npm: Search, View, Install, Uninstall, List (5)
        // DotnetTool: Search, Install, Uninstall, List, Update (5)
        // Total: 19
        tools.Count.Should().Be(19, "PackageManagerTool 应该有 19 个方法");
    }

    #endregion
}
