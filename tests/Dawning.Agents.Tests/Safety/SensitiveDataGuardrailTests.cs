using Dawning.Agents.Abstractions.Safety;
using Dawning.Agents.Core.Safety;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Tests.Safety;

public class SensitiveDataGuardrailTests
{
    private static IOptions<SafetyOptions> CreateOptions(
        bool enableDetection = true,
        bool autoMask = true,
        GuardrailFailureBehavior behavior = GuardrailFailureBehavior.WarnAndContinue
    )
    {
        return Options.Create(
            new SafetyOptions
            {
                EnableSensitiveDataDetection = enableDetection,
                AutoMaskSensitiveData = autoMask,
                FailureBehavior = behavior,
            }
        );
    }

    [Fact]
    public async Task CheckAsync_WhenDisabled_ShouldPass()
    {
        // Arrange
        var options = CreateOptions(enableDetection: false);
        var guardrail = new SensitiveDataGuardrail(options);

        // Act
        var result = await guardrail.CheckAsync("test@email.com");

        // Assert
        result.Passed.Should().BeTrue();
        result.ProcessedContent.Should().Be("test@email.com");
    }

    [Fact]
    public async Task CheckAsync_WithNoSensitiveData_ShouldPass()
    {
        // Arrange
        var options = CreateOptions();
        var guardrail = new SensitiveDataGuardrail(options);

        // Act
        var result = await guardrail.CheckAsync("Hello World");

        // Assert
        result.Passed.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckAsync_WithEmail_ShouldDetect()
    {
        // Arrange
        var options = CreateOptions();
        var guardrail = new SensitiveDataGuardrail(options);

        // Act
        var result = await guardrail.CheckAsync("Contact: test@example.com");

        // Assert
        result.Passed.Should().BeTrue();
        result.Issues.Should().HaveCount(1);
        result.Issues[0].Type.Should().Be("Email");
    }

    [Fact]
    public async Task CheckAsync_WithEmail_ShouldMask()
    {
        // Arrange
        var options = CreateOptions(autoMask: true);
        var guardrail = new SensitiveDataGuardrail(options);

        // Act
        var result = await guardrail.CheckAsync("Contact: test@example.com");

        // Assert
        result.ProcessedContent.Should().Contain("te**************");
    }

    [Fact]
    public async Task CheckAsync_WithPhone_ShouldDetect()
    {
        // Arrange
        var options = CreateOptions();
        var guardrail = new SensitiveDataGuardrail(options);

        // Act
        var result = await guardrail.CheckAsync("手机号: 13812345678");

        // Assert
        result.Passed.Should().BeTrue();
        result.Issues.Should().HaveCount(1);
        result.Issues[0].Type.Should().Be("Phone");
    }

    [Fact]
    public async Task CheckAsync_WithPhone_ShouldMaskCorrectly()
    {
        // Arrange
        var options = CreateOptions(autoMask: true);
        var guardrail = new SensitiveDataGuardrail(options);

        // Act
        var result = await guardrail.CheckAsync("手机号: 13812345678");

        // Assert
        // Phone pattern: KeepFirst=3, KeepLast=4 -> 138****5678
        result.ProcessedContent.Should().Contain("138****5678");
    }

    [Fact]
    public async Task CheckAsync_WithIDCard_ShouldDetect()
    {
        // Arrange
        var options = CreateOptions();
        var guardrail = new SensitiveDataGuardrail(options);

        // Act
        var result = await guardrail.CheckAsync("身份证: 110101199001011234");

        // Assert
        result.Issues.Should().Contain(i => i.Type == "IDCard");
    }

    [Fact]
    public async Task CheckAsync_WithMultipleSensitiveData_ShouldDetectAll()
    {
        // Arrange
        var options = CreateOptions();
        var guardrail = new SensitiveDataGuardrail(options);
        var content = "联系方式: test@example.com, 手机: 13812345678";

        // Act
        var result = await guardrail.CheckAsync(content);

        // Assert
        result.Issues.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task CheckAsync_WithBlockAndReport_ShouldFail()
    {
        // Arrange
        var options = CreateOptions(behavior: GuardrailFailureBehavior.BlockAndReport);
        var guardrail = new SensitiveDataGuardrail(options);

        // Act
        var result = await guardrail.CheckAsync("Email: test@example.com");

        // Assert
        result.Passed.Should().BeFalse();
        result.TriggeredBy.Should().Be("SensitiveDataGuardrail");
    }

    [Fact]
    public void Name_ShouldBeSensitiveDataGuardrail()
    {
        // Arrange
        var options = CreateOptions();
        var guardrail = new SensitiveDataGuardrail(options);

        // Assert
        guardrail.Name.Should().Be("SensitiveDataGuardrail");
    }

    [Fact]
    public void IsEnabled_ShouldReflectOptions()
    {
        // Arrange
        var enabledOptions = CreateOptions(enableDetection: true);
        var disabledOptions = CreateOptions(enableDetection: false);

        // Act
        var enabledGuardrail = new SensitiveDataGuardrail(enabledOptions);
        var disabledGuardrail = new SensitiveDataGuardrail(disabledOptions);

        // Assert
        enabledGuardrail.IsEnabled.Should().BeTrue();
        disabledGuardrail.IsEnabled.Should().BeFalse();
    }
}
