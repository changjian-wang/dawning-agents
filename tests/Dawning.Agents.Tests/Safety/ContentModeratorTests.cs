using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Safety;
using Dawning.Agents.Core.Safety;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Dawning.Agents.Tests.Safety;

/// <summary>
/// ContentModerator 单元测试
/// </summary>
public class ContentModeratorTests
{
    private readonly Mock<ILLMProvider> _mockLLM;
    private readonly Mock<ILogger<ContentModerator>> _mockLogger;

    public ContentModeratorTests()
    {
        _mockLLM = new Mock<ILLMProvider>();
        _mockLogger = new Mock<ILogger<ContentModerator>>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLLMProvider_ThrowsArgumentNullException()
    {
        var act = () => new ContentModerator(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("llmProvider");
    }

    [Fact]
    public void Constructor_WithValidLLMProvider_Succeeds()
    {
        var act = () => new ContentModerator(_mockLLM.Object);

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithOptions_SetsCorrectly()
    {
        var options = new ContentModeratorOptions { Enabled = false };
        var moderator = new ContentModerator(_mockLLM.Object, options);

        moderator.IsEnabled.Should().BeFalse();
    }

    #endregion

    #region Properties Tests

    [Fact]
    public void Name_ReturnsContentModerator()
    {
        var moderator = new ContentModerator(_mockLLM.Object);

        moderator.Name.Should().Be("ContentModerator");
    }

    [Fact]
    public void Description_ReturnsNonEmpty()
    {
        var moderator = new ContentModerator(_mockLLM.Object);

        moderator.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void IsEnabled_DefaultsToTrue()
    {
        var moderator = new ContentModerator(_mockLLM.Object);

        moderator.IsEnabled.Should().BeTrue();
    }

    #endregion

    #region CheckAsync Tests - Disabled

    [Fact]
    public async Task CheckAsync_WhenDisabled_ReturnsPass()
    {
        var options = new ContentModeratorOptions { Enabled = false };
        var moderator = new ContentModerator(_mockLLM.Object, options);

        var result = await moderator.CheckAsync("Some content");

        result.Passed.Should().BeTrue();
        _mockLLM.Verify(
            x =>
                x.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task CheckAsync_WithEmptyContent_ReturnsPass()
    {
        var moderator = new ContentModerator(_mockLLM.Object);

        var result = await moderator.CheckAsync("");

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_WithWhitespaceContent_ReturnsPass()
    {
        var moderator = new ContentModerator(_mockLLM.Object);

        var result = await moderator.CheckAsync("   ");

        result.Passed.Should().BeTrue();
    }

    #endregion

    #region CheckAsync Tests - Allowed Content

    [Fact]
    public async Task CheckAsync_AllowedContent_ReturnsPass()
    {
        SetupLLMResponse("""{"allowed": true, "categories": [], "reason": null}""");
        var moderator = new ContentModerator(_mockLLM.Object);

        var result = await moderator.CheckAsync("Hello, how are you?");

        result.Passed.Should().BeTrue();
    }

    #endregion

    #region CheckAsync Tests - Disallowed Content

    [Fact]
    public async Task CheckAsync_DisallowedContent_ReturnsFail()
    {
        SetupLLMResponse(
            """{"allowed": false, "categories": ["暴力内容"], "reason": "包含暴力描述"}"""
        );
        var moderator = new ContentModerator(_mockLLM.Object);

        var result = await moderator.CheckAsync("Violent content here");

        result.Passed.Should().BeFalse();
        result.Message.Should().Contain("暴力描述");
    }

    [Fact]
    public async Task CheckAsync_DisallowedContent_IncludesIssues()
    {
        SetupLLMResponse(
            """{"allowed": false, "categories": ["暴力内容", "仇恨言论"], "reason": "违规"}"""
        );
        var moderator = new ContentModerator(_mockLLM.Object);

        var result = await moderator.CheckAsync("Bad content");

        result.Issues.Should().HaveCount(2);
        result.Issues.Should().Contain(i => i.Description == "暴力内容");
        result.Issues.Should().Contain(i => i.Description == "仇恨言论");
    }

    #endregion

    #region CheckAsync Tests - Error Handling

    [Fact]
    public async Task CheckAsync_OnException_WithFailOpen_ReturnsPass()
    {
        _mockLLM
            .Setup(x =>
                x.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new Exception("API Error"));

        var options = new ContentModeratorOptions { FailOpenOnError = true };
        var moderator = new ContentModerator(_mockLLM.Object, options, _mockLogger.Object);

        var result = await moderator.CheckAsync("Some content");

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_OnException_WithFailClose_ReturnsFail()
    {
        _mockLLM
            .Setup(x =>
                x.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new Exception("API Error"));

        var options = new ContentModeratorOptions { FailOpenOnError = false };
        var moderator = new ContentModerator(_mockLLM.Object, options, _mockLogger.Object);

        var result = await moderator.CheckAsync("Some content");

        result.Passed.Should().BeFalse();
        result.Message.Should().Contain("异常");
    }

    #endregion

    #region CheckAsync Tests - Content Truncation

    [Fact]
    public async Task CheckAsync_LongContent_IsTruncated()
    {
        SetupLLMResponse("""{"allowed": true, "categories": [], "reason": null}""");
        var options = new ContentModeratorOptions { MaxContentToCheck = 100 };
        var moderator = new ContentModerator(_mockLLM.Object, options);

        var longContent = new string('a', 200);
        await moderator.CheckAsync(longContent);

        // Verify the content sent to LLM is truncated
        _mockLLM.Verify(
            x =>
                x.ChatAsync(
                    It.Is<IEnumerable<ChatMessage>>(msgs =>
                        msgs.First().Content.Contains("...")
                    ),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    #endregion

    #region CheckAsync Tests - JSON Parsing Edge Cases

    [Fact]
    public async Task CheckAsync_MalformedJson_HandlesGracefully()
    {
        SetupLLMResponse("This is not JSON");
        var moderator = new ContentModerator(_mockLLM.Object, null, _mockLogger.Object);

        var result = await moderator.CheckAsync("Some content");

        // Should default to pass when parsing fails and no clear disallow signal
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_JsonWithFalseAllowed_ReturnsFail()
    {
        SetupLLMResponse("""Some text before {"allowed": false} and after""");
        var moderator = new ContentModerator(_mockLLM.Object, null, _mockLogger.Object);

        var result = await moderator.CheckAsync("Bad content");

        result.Passed.Should().BeFalse();
    }

    #endregion

    #region ContentModeratorOptions Tests

    [Fact]
    public void Options_DefaultCategories_AreNotEmpty()
    {
        var options = new ContentModeratorOptions();

        options.Categories.Should().NotBeEmpty();
        options.Categories.Should().Contain("暴力内容");
    }

    [Fact]
    public void Options_DefaultMaxContentToCheck_IsReasonable()
    {
        var options = new ContentModeratorOptions();

        options.MaxContentToCheck.Should().BeGreaterThan(0);
        options.MaxContentToCheck.Should().BeLessThanOrEqualTo(10000);
    }

    [Fact]
    public void Options_DefaultFailOpenOnError_IsFalse()
    {
        var options = new ContentModeratorOptions();

        options.FailOpenOnError.Should().BeFalse();
    }

    #endregion

    private void SetupLLMResponse(string response)
    {
        _mockLLM
            .Setup(x =>
                x.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ChatCompletionResponse { Content = response });
    }
}
