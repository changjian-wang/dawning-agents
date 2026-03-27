using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Safety;
using Dawning.Agents.Core.Safety;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Dawning.Agents.Tests.Safety;

public class SafeAgentTests
{
    private readonly Mock<IAgent> _mockAgent;
    private readonly Mock<IGuardrailPipeline> _mockPipeline;
    private readonly Mock<IRateLimiter> _mockRateLimiter;
    private readonly Mock<IAuditLogger> _mockAuditLogger;

    public SafeAgentTests()
    {
        _mockAgent = new Mock<IAgent>();
        _mockPipeline = new Mock<IGuardrailPipeline>();
        _mockRateLimiter = new Mock<IRateLimiter>();
        _mockAuditLogger = new Mock<IAuditLogger>();

        // Default setups
        _mockAgent.Setup(a => a.Name).Returns("TestAgent");
        _mockAgent.Setup(a => a.Instructions).Returns("Test instructions");

        _mockPipeline
            .Setup(p => p.CheckInputAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string input, CancellationToken _) => GuardrailResult.Pass(input));

        _mockPipeline
            .Setup(p => p.CheckOutputAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string output, CancellationToken _) => GuardrailResult.Pass(output));

        _mockRateLimiter
            .Setup(r => r.TryAcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RateLimitResult.Allow(10, DateTimeOffset.UtcNow.AddMinutes(1)));

        _mockAuditLogger
            .Setup(l => l.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private SafeAgent CreateSafeAgent(
        IGuardrailPipeline? pipeline = null,
        IRateLimiter? rateLimiter = null,
        ITokenRateLimiter? tokenRateLimiter = null,
        IAuditLogger? auditLogger = null
    )
    {
        return new SafeAgent(
            _mockAgent.Object,
            pipeline ?? _mockPipeline.Object,
            rateLimiter ?? _mockRateLimiter.Object,
            tokenRateLimiter,
            auditLogger ?? _mockAuditLogger.Object,
            NullLogger<SafeAgent>.Instance
        );
    }

    [Fact]
    public void Properties_ShouldDelegateToInnerAgent()
    {
        // Arrange
        var safeAgent = CreateSafeAgent();

        // Assert
        safeAgent.Name.Should().Be("TestAgent");
        safeAgent.Instructions.Should().Be("Test instructions");
    }

    [Fact]
    public async Task RunAsync_ShouldCallInnerAgent_WhenAllChecksPass()
    {
        // Arrange
        var expectedResponse = AgentResponse.Successful("Hello back!", [], TimeSpan.FromSeconds(1));
        _mockAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var safeAgent = CreateSafeAgent();

        // Act
        var response = await safeAgent.RunAsync("Hello", "user123");

        // Assert
        response.FinalAnswer.Should().Be("Hello back!");
        _mockAgent.Verify(
            a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task RunAsync_ShouldBlockRequest_WhenRateLimitExceeded()
    {
        // Arrange
        _mockRateLimiter
            .Setup(r => r.TryAcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                RateLimitResult.Deny(TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow.AddMinutes(1))
            );

        var safeAgent = CreateSafeAgent();

        // Act
        var response = await safeAgent.RunAsync("Hello", "user123");

        // Assert
        response.Success.Should().BeFalse();
        response.Error.Should().Contain("Rate limited");
        _mockAgent.Verify(
            a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task RunAsync_ShouldBlockRequest_WhenInputGuardrailFails()
    {
        // Arrange
        var failResult = GuardrailResult.Fail("Input too long", "MaxLengthGuardrail");
        _mockPipeline
            .Setup(p => p.CheckInputAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failResult);

        var safeAgent = CreateSafeAgent();

        // Act
        var response = await safeAgent.RunAsync("Very long input...", "user123");

        // Assert
        response.Success.Should().BeFalse();
        response.Error.Should().Contain("Input failed safety check");
        _mockAgent.Verify(
            a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task RunAsync_ShouldUseProcessedInput_WhenGuardrailModifiesInput()
    {
        // Arrange
        var processedResult = GuardrailResult.PassWithContent("[Redacted]Email: ***");
        _mockPipeline
            .Setup(p => p.CheckInputAsync("Email: test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(processedResult);

        var expectedResponse = AgentResponse.Successful("Processed", [], TimeSpan.FromSeconds(1));
        _mockAgent
            .Setup(a =>
                a.RunAsync(
                    It.Is<AgentContext>(c => c.UserInput == "[Redacted]Email: ***"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expectedResponse);

        var safeAgent = CreateSafeAgent();

        // Act
        await safeAgent.RunAsync("Email: test@example.com", "user123");

        // Assert
        _mockAgent.Verify(
            a =>
                a.RunAsync(
                    It.Is<AgentContext>(c => c.UserInput == "[Redacted]Email: ***"),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task RunAsync_ShouldBlockResponse_WhenOutputGuardrailFails()
    {
        // Arrange
        var agentResponse = AgentResponse.Successful(
            "Sensitive output with credit card",
            [],
            TimeSpan.FromSeconds(1)
        );
        _mockAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentResponse);

        var failResult = GuardrailResult.Fail("Sensitive data detected", "SensitiveDataGuardrail");
        _mockPipeline
            .Setup(p =>
                p.CheckOutputAsync(
                    "Sensitive output with credit card",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(failResult);

        var safeAgent = CreateSafeAgent();

        // Act
        var response = await safeAgent.RunAsync("Hello", "user123");

        // Assert
        response.Success.Should().BeFalse();
        response.Error.Should().Contain("Output failed safety check");
    }

    [Fact]
    public async Task RunAsync_ShouldUseProcessedOutput_WhenGuardrailModifiesOutput()
    {
        // Arrange
        var agentResponse = AgentResponse.Successful(
            "Phone: 13812345678",
            [],
            TimeSpan.FromSeconds(1)
        );
        _mockAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentResponse);

        var processedResult = GuardrailResult.PassWithContent("Phone: ***");
        _mockPipeline
            .Setup(p => p.CheckOutputAsync("Phone: 13812345678", It.IsAny<CancellationToken>()))
            .ReturnsAsync(processedResult);

        var safeAgent = CreateSafeAgent();

        // Act
        var response = await safeAgent.RunAsync("Tell me the phone number", "user123");

        // Assert
        response.FinalAnswer.Should().Be("Phone: ***");
    }

    [Fact]
    public async Task RunAsync_ShouldLogAuditEvents()
    {
        // Arrange
        var expectedResponse = AgentResponse.Successful("Response", [], TimeSpan.FromSeconds(1));
        _mockAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var safeAgent = CreateSafeAgent();

        // Act
        await safeAgent.RunAsync("Hello", "user123");

        // Assert - Should log start and complete
        _mockAuditLogger.Verify(
            l =>
                l.LogAsync(
                    It.Is<AuditEntry>(e => e.EventType == AuditEventType.AgentRunStart),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        _mockAuditLogger.Verify(
            l =>
                l.LogAsync(
                    It.Is<AuditEntry>(e => e.EventType == AuditEventType.AgentRunEnd),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task RunAsync_ShouldLogGuardrailTrigger_WhenBlocked()
    {
        // Arrange
        var failResult = GuardrailResult.Fail("Blocked reason", "TestGuardrail");
        _mockPipeline
            .Setup(p => p.CheckInputAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failResult);

        var safeAgent = CreateSafeAgent();

        // Act
        await safeAgent.RunAsync("Bad input", "user123");

        // Assert
        _mockAuditLogger.Verify(
            l =>
                l.LogAsync(
                    It.Is<AuditEntry>(e => e.EventType == AuditEventType.GuardrailTriggered),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task RunAsync_ShouldLogRateLimitExceeded_WhenBlocked()
    {
        // Arrange
        _mockRateLimiter
            .Setup(r => r.TryAcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                RateLimitResult.Deny(TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow.AddMinutes(1))
            );

        var safeAgent = CreateSafeAgent();

        // Act
        await safeAgent.RunAsync("Hello", "user123");

        // Assert
        _mockAuditLogger.Verify(
            l =>
                l.LogAsync(
                    It.Is<AuditEntry>(e => e.EventType == AuditEventType.RateLimited),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task RunAsync_ShouldWorkWithoutOptionalDependencies()
    {
        // Arrange
        var expectedResponse = AgentResponse.Successful("Hello!", [], TimeSpan.FromSeconds(1));
        _mockAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Create SafeAgent without optional dependencies (null pipelines, etc.)
        var safeAgent = new SafeAgent(
            _mockAgent.Object,
            guardrailPipeline: null,
            rateLimiter: null,
            auditLogger: null
        );

        // Act
        var response = await safeAgent.RunAsync("Hello", "user123");

        // Assert
        response.FinalAnswer.Should().Be("Hello!");
    }

    [Fact]
    public async Task RunAsync_ShouldHandleAgentException()
    {
        // Arrange
        _mockAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Agent error"));

        var safeAgent = CreateSafeAgent();

        // Act
        var response = await safeAgent.RunAsync("Hello", "user123");

        // Assert
        response.Success.Should().BeFalse();
        response.Error.Should().Contain("An error occurred during execution");
    }

    [Fact]
    public async Task RunAsync_ShouldLogError_WhenAgentThrows()
    {
        // Arrange
        _mockAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Agent error"));

        var safeAgent = CreateSafeAgent();

        // Act
        await safeAgent.RunAsync("Hello", "user123");

        // Assert
        _mockAuditLogger.Verify(
            l =>
                l.LogAsync(
                    It.Is<AuditEntry>(e => e.EventType == AuditEventType.Error),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task RunAsync_ShouldBlockRequest_WhenTokenBudgetExhausted()
    {
        // Arrange
        var mockTokenLimiter = new Mock<ITokenRateLimiter>();
        mockTokenLimiter.Setup(t => t.HasBudget(It.IsAny<string>())).Returns(false);
        mockTokenLimiter.Setup(t => t.GetUsedTokens(It.IsAny<string>())).Returns(100000);

        var expectedResponse = AgentResponse.Successful("Hello!", [], TimeSpan.FromSeconds(1));
        _mockAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var safeAgent = CreateSafeAgent(tokenRateLimiter: mockTokenLimiter.Object);

        // Act
        var response = await safeAgent.RunAsync("Hello", "user123");

        // Assert
        response.Success.Should().BeFalse();
        response.Error.Should().Contain("Token budget exhausted");
        _mockAgent.Verify(
            a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task RunAsync_ShouldProceed_WhenTokenBudgetAvailable()
    {
        // Arrange
        var mockTokenLimiter = new Mock<ITokenRateLimiter>();
        mockTokenLimiter.Setup(t => t.HasBudget(It.IsAny<string>())).Returns(true);

        var expectedResponse = AgentResponse.Successful("Hello!", [], TimeSpan.FromSeconds(1));
        _mockAgent
            .Setup(a => a.RunAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var safeAgent = CreateSafeAgent(tokenRateLimiter: mockTokenLimiter.Object);

        // Act
        var response = await safeAgent.RunAsync("Hello", "user123");

        // Assert
        response.Success.Should().BeTrue();
        response.FinalAnswer.Should().Be("Hello!");
    }

    [Fact]
    public async Task RunAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockAuditLogger
            .Setup(l => l.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Returns(
                (AuditEntry _, CancellationToken ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                }
            );

        var safeAgent = CreateSafeAgent();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            safeAgent.RunAsync("Hello", "user123", cts.Token)
        );
    }
}
