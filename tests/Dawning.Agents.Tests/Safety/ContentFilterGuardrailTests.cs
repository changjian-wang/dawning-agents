using Dawning.Agents.Abstractions.Safety;
using Dawning.Agents.Core.Safety;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Tests.Safety;

public class ContentFilterGuardrailTests
{
    private static IOptions<SafetyOptions> CreateOptions(
        bool enableFilter = true,
        params string[] blockedKeywords
    )
    {
        return Options.Create(
            new SafetyOptions
            {
                EnableContentFilter = enableFilter,
                BlockedKeywords = [.. blockedKeywords],
            }
        );
    }

    [Fact]
    public async Task CheckAsync_WhenDisabled_ShouldPass()
    {
        // Arrange
        var options = CreateOptions(enableFilter: false, "bad");
        var guardrail = new ContentFilterGuardrail(options);

        // Act
        var result = await guardrail.CheckAsync("This is bad content");

        // Assert
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_WithNoBlockedKeywords_ShouldPass()
    {
        // Arrange
        var options = CreateOptions(enableFilter: true);
        var guardrail = new ContentFilterGuardrail(options);

        // Act
        var result = await guardrail.CheckAsync("Any content");

        // Assert
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_WithCleanContent_ShouldPass()
    {
        // Arrange
        var options = CreateOptions(true, "bad", "evil");
        var guardrail = new ContentFilterGuardrail(options);

        // Act
        var result = await guardrail.CheckAsync("This is good content");

        // Assert
        result.Passed.Should().BeTrue();
        result.ProcessedContent.Should().Be("This is good content");
    }

    [Fact]
    public async Task CheckAsync_WithBlockedKeyword_ShouldFail()
    {
        // Arrange
        var options = CreateOptions(true, "bad");
        var guardrail = new ContentFilterGuardrail(options);

        // Act
        var result = await guardrail.CheckAsync("This is bad content");

        // Assert
        result.Passed.Should().BeFalse();
        result.TriggeredBy.Should().Be("ContentFilterGuardrail");
        result.Issues.Should().HaveCount(1);
        result.Issues[0].Type.Should().Be("BlockedKeyword");
    }

    [Fact]
    public async Task CheckAsync_ShouldBeCaseInsensitive()
    {
        // Arrange
        var options = CreateOptions(true, "bad");
        var guardrail = new ContentFilterGuardrail(options);

        // Act
        var result = await guardrail.CheckAsync("This is BAD content");

        // Assert
        result.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_WithMultipleOccurrences_ShouldDetectAll()
    {
        // Arrange
        var options = CreateOptions(true, "bad");
        var guardrail = new ContentFilterGuardrail(options);

        // Act
        var result = await guardrail.CheckAsync("bad bad bad");

        // Assert
        result.Passed.Should().BeFalse();
        result.Issues.Should().HaveCount(3);
    }

    [Fact]
    public async Task CheckAsync_WithMultipleKeywords_ShouldDetectAll()
    {
        // Arrange
        var options = CreateOptions(true, "bad", "evil");
        var guardrail = new ContentFilterGuardrail(options);

        // Act
        var result = await guardrail.CheckAsync("bad and evil");

        // Assert
        result.Passed.Should().BeFalse();
        result.Issues.Should().HaveCount(2);
    }

    [Fact]
    public void Name_ShouldBeContentFilterGuardrail()
    {
        // Arrange
        var options = CreateOptions(true, "test");
        var guardrail = new ContentFilterGuardrail(options);

        // Assert
        guardrail.Name.Should().Be("ContentFilterGuardrail");
    }

    [Fact]
    public void IsEnabled_ShouldRequireBothFlagAndKeywords()
    {
        // Arrange
        var withKeywords = CreateOptions(true, "test");
        var withoutKeywords = CreateOptions(true);
        var disabled = CreateOptions(false, "test");

        // Act & Assert
        new ContentFilterGuardrail(withKeywords)
            .IsEnabled.Should()
            .BeTrue();
        new ContentFilterGuardrail(withoutKeywords).IsEnabled.Should().BeFalse();
        new ContentFilterGuardrail(disabled).IsEnabled.Should().BeFalse();
    }
}

public class UrlDomainGuardrailTests
{
    private static IOptions<SafetyOptions> CreateOptions(
        string[]? allowed = null,
        string[]? blocked = null
    )
    {
        return Options.Create(
            new SafetyOptions
            {
                AllowedDomains = allowed?.ToList() ?? [],
                BlockedDomains = blocked?.ToList() ?? [],
            }
        );
    }

    [Fact]
    public async Task CheckAsync_WithNoConfiguration_ShouldPass()
    {
        // Arrange
        var options = CreateOptions();
        var guardrail = new UrlDomainGuardrail(options);

        // Act
        var result = await guardrail.CheckAsync("Visit https://example.com");

        // Assert
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_WithAllowedDomain_ShouldPass()
    {
        // Arrange
        var options = CreateOptions(allowed: ["example.com"]);
        var guardrail = new UrlDomainGuardrail(options);

        // Act
        var result = await guardrail.CheckAsync("Visit https://example.com/page");

        // Assert
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_WithSubdomain_ShouldMatchParent()
    {
        // Arrange
        var options = CreateOptions(allowed: ["example.com"]);
        var guardrail = new UrlDomainGuardrail(options);

        // Act
        var result = await guardrail.CheckAsync("Visit https://sub.example.com/page");

        // Assert
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_WithUnallowedDomain_ShouldWarn()
    {
        // Arrange
        var options = CreateOptions(allowed: ["trusted.com"]);
        var guardrail = new UrlDomainGuardrail(options);

        // Act
        var result = await guardrail.CheckAsync("Visit https://untrusted.com");

        // Assert
        result.Passed.Should().BeTrue(); // Warnings don't fail
        result.Issues.Should().HaveCount(1);
        result.Issues[0].Type.Should().Be("UnallowedDomain");
        result.Issues[0].Severity.Should().Be(IssueSeverity.Warning);
    }

    [Fact]
    public async Task CheckAsync_WithBlockedDomain_ShouldFail()
    {
        // Arrange
        var options = CreateOptions(blocked: ["evil.com"]);
        var guardrail = new UrlDomainGuardrail(options);

        // Act
        var result = await guardrail.CheckAsync("Visit https://evil.com");

        // Assert
        result.Passed.Should().BeFalse();
        result.Issues.Should().HaveCount(1);
        result.Issues[0].Type.Should().Be("BlockedDomain");
        result.Issues[0].Severity.Should().Be(IssueSeverity.Error);
    }

    [Fact]
    public async Task CheckAsync_WithNoUrls_ShouldPass()
    {
        // Arrange
        var options = CreateOptions(allowed: ["trusted.com"]);
        var guardrail = new UrlDomainGuardrail(options);

        // Act
        var result = await guardrail.CheckAsync("No URLs here");

        // Assert
        result.Passed.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void IsEnabled_ShouldRequireConfiguration()
    {
        // Arrange
        var withAllowed = CreateOptions(allowed: ["example.com"]);
        var withBlocked = CreateOptions(blocked: ["evil.com"]);
        var withNeither = CreateOptions();

        // Assert
        new UrlDomainGuardrail(withAllowed)
            .IsEnabled.Should()
            .BeTrue();
        new UrlDomainGuardrail(withBlocked).IsEnabled.Should().BeTrue();
        new UrlDomainGuardrail(withNeither).IsEnabled.Should().BeFalse();
    }
}
