using Dawning.Agents.Abstractions.Safety;
using Dawning.Agents.Core.Safety;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Dawning.Agents.Tests.Safety;

public class GuardrailPipelineTests
{
    [Fact]
    public void Constructor_ShouldInitializeEmptyLists()
    {
        // Act
        var pipeline = new GuardrailPipeline();

        // Assert
        pipeline.InputGuardrails.Should().BeEmpty();
        pipeline.OutputGuardrails.Should().BeEmpty();
    }

    [Fact]
    public void AddInputGuardrail_ShouldAddGuardrail()
    {
        // Arrange
        var pipeline = new GuardrailPipeline();
        var guardrail = new MaxLengthGuardrail(100);

        // Act
        pipeline.AddInputGuardrail(guardrail);

        // Assert
        pipeline.InputGuardrails.Should().HaveCount(1);
        pipeline.InputGuardrails[0].Should().Be(guardrail);
    }

    [Fact]
    public void AddOutputGuardrail_ShouldAddGuardrail()
    {
        // Arrange
        var pipeline = new GuardrailPipeline();
        var guardrail = new MaxLengthGuardrail(100, isInputGuardrail: false);

        // Act
        pipeline.AddOutputGuardrail(guardrail);

        // Assert
        pipeline.OutputGuardrails.Should().HaveCount(1);
    }

    [Fact]
    public void AddGuardrail_ShouldReturnPipelineForChaining()
    {
        // Arrange
        var pipeline = new GuardrailPipeline();
        var guardrail1 = new MaxLengthGuardrail(100);
        var guardrail2 = new MaxLengthGuardrail(200);

        // Act
        var result = pipeline.AddInputGuardrail(guardrail1).AddInputGuardrail(guardrail2);

        // Assert
        result.Should().Be(pipeline);
        pipeline.InputGuardrails.Should().HaveCount(2);
    }

    [Fact]
    public void AddInputGuardrail_WithNull_ShouldThrow()
    {
        // Arrange
        var pipeline = new GuardrailPipeline();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => pipeline.AddInputGuardrail(null!));
    }

    [Fact]
    public async Task CheckInputAsync_WithNoGuardrails_ShouldPass()
    {
        // Arrange
        var pipeline = new GuardrailPipeline();

        // Act
        var result = await pipeline.CheckInputAsync("test content");

        // Assert
        result.Passed.Should().BeTrue();
        result.ProcessedContent.Should().Be("test content");
    }

    [Fact]
    public async Task CheckInputAsync_WithPassingGuardrails_ShouldPass()
    {
        // Arrange
        var pipeline = new GuardrailPipeline();
        pipeline.AddInputGuardrail(new MaxLengthGuardrail(100));

        // Act
        var result = await pipeline.CheckInputAsync("short content");

        // Assert
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckInputAsync_WithFailingGuardrail_ShouldFail()
    {
        // Arrange
        var pipeline = new GuardrailPipeline();
        pipeline.AddInputGuardrail(new MaxLengthGuardrail(10));

        // Act
        var result = await pipeline.CheckInputAsync("this is a very long content");

        // Assert
        result.Passed.Should().BeFalse();
        result.TriggeredBy.Should().Be("MaxInputLength");
    }

    [Fact]
    public async Task CheckInputAsync_ShouldStopAtFirstFailure()
    {
        // Arrange
        var pipeline = new GuardrailPipeline();
        var mockGuardrail = new Mock<IInputGuardrail>();
        mockGuardrail.Setup(g => g.IsEnabled).Returns(true);
        mockGuardrail.Setup(g => g.Name).Returns("MockGuardrail");

        pipeline.AddInputGuardrail(new MaxLengthGuardrail(10)); // Will fail
        pipeline.AddInputGuardrail(mockGuardrail.Object); // Should not be called

        // Act
        var result = await pipeline.CheckInputAsync("this is a very long content");

        // Assert
        result.Passed.Should().BeFalse();
        mockGuardrail.Verify(
            g => g.CheckAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task CheckInputAsync_ShouldSkipDisabledGuardrails()
    {
        // Arrange
        var pipeline = new GuardrailPipeline();
        var mockGuardrail = new Mock<IInputGuardrail>();
        mockGuardrail.Setup(g => g.IsEnabled).Returns(false);
        mockGuardrail.Setup(g => g.Name).Returns("DisabledGuardrail");

        pipeline.AddInputGuardrail(mockGuardrail.Object);

        // Act
        var result = await pipeline.CheckInputAsync("test");

        // Assert
        result.Passed.Should().BeTrue();
        mockGuardrail.Verify(
            g => g.CheckAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task CheckInputAsync_ShouldPassProcessedContentToNextGuardrail()
    {
        // Arrange
        var pipeline = new GuardrailPipeline();

        // Use SensitiveDataGuardrail that modifies content
        var options = Options.Create(
            new SafetyOptions
            {
                EnableSensitiveDataDetection = true,
                AutoMaskSensitiveData = true,
                FailureBehavior = GuardrailFailureBehavior.WarnAndContinue,
            }
        );

        pipeline.AddInputGuardrail(new SensitiveDataGuardrail(options));
        pipeline.AddInputGuardrail(new MaxLengthGuardrail(1000));

        // Act
        var result = await pipeline.CheckInputAsync("Email: test@example.com");

        // Assert
        result.Passed.Should().BeTrue();
        result.ProcessedContent.Should().Contain("te**************"); // Masked email
    }

    [Fact]
    public async Task CheckInputAsync_ShouldCollectAllIssues()
    {
        // Arrange
        var pipeline = new GuardrailPipeline();
        var options = Options.Create(
            new SafetyOptions
            {
                EnableSensitiveDataDetection = true,
                AutoMaskSensitiveData = true,
                FailureBehavior = GuardrailFailureBehavior.WarnAndContinue,
            }
        );

        pipeline.AddInputGuardrail(new SensitiveDataGuardrail(options));

        // Act
        var result = await pipeline.CheckInputAsync("Email: a@b.com, Phone: 13812345678");

        // Assert
        result.Passed.Should().BeTrue();
        result.Issues.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task CheckOutputAsync_ShouldWorkSameAsInput()
    {
        // Arrange
        var pipeline = new GuardrailPipeline();
        pipeline.AddOutputGuardrail(new MaxLengthGuardrail(10, isInputGuardrail: false));

        // Act
        var result = await pipeline.CheckOutputAsync("this is a very long output");

        // Assert
        result.Passed.Should().BeFalse();
        result.TriggeredBy.Should().Be("MaxOutputLength");
    }

    [Fact]
    public async Task CheckAsync_ShouldSupportCancellation()
    {
        // Arrange
        var pipeline = new GuardrailPipeline();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        pipeline.AddInputGuardrail(new MaxLengthGuardrail(100));

        // Act & Assert - Should throw OperationCanceledException when token is cancelled
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            pipeline.CheckInputAsync("test", cts.Token)
        );
    }
}
