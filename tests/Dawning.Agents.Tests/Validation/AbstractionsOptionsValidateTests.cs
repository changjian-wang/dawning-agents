using Dawning.Agents.Abstractions.Cache;
using Dawning.Agents.Abstractions.Discovery;
using Dawning.Agents.Abstractions.Evaluation;
using Dawning.Agents.Abstractions.HumanLoop;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Multimodal;
using Dawning.Agents.Abstractions.Safety;
using FluentAssertions;

namespace Dawning.Agents.Tests.Validation;

/// <summary>
/// Abstractions 层 Options Validate() 测试
/// </summary>
public class AbstractionsOptionsValidateTests
{
    #region ModelRouterOptions

    [Fact]
    public void ModelRouterOptions_DefaultValues_ShouldBeValid()
    {
        var options = new ModelRouterOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ModelRouterOptions_InvalidHealthCheckInterval_ShouldThrow(int seconds)
    {
        var options = new ModelRouterOptions { HealthCheckIntervalSeconds = seconds };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*HealthCheckInterval*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ModelRouterOptions_InvalidUnhealthyThreshold_ShouldThrow(int threshold)
    {
        var options = new ModelRouterOptions { UnhealthyThreshold = threshold };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*UnhealthyThreshold*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ModelRouterOptions_InvalidRecoveryThreshold_ShouldThrow(int threshold)
    {
        var options = new ModelRouterOptions { RecoveryThreshold = threshold };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*RecoveryThreshold*");
    }

    [Fact]
    public void ModelRouterOptions_NegativeMaxFailover_ShouldThrow()
    {
        var options = new ModelRouterOptions { MaxFailoverRetries = -1 };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*MaxFailover*");
    }

    [Fact]
    public void ModelRouterOptions_ZeroMaxFailover_ShouldNotThrow()
    {
        var options = new ModelRouterOptions { MaxFailoverRetries = 0 };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    #endregion

    #region HumanLoopOptions

    [Fact]
    public void HumanLoopOptions_DefaultValues_ShouldBeValid()
    {
        var options = new HumanLoopOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void HumanLoopOptions_ZeroTimeout_ShouldThrow()
    {
        var options = new HumanLoopOptions { DefaultTimeout = TimeSpan.Zero };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*DefaultTimeout*");
    }

    [Fact]
    public void HumanLoopOptions_NegativeTimeout_ShouldThrow()
    {
        var options = new HumanLoopOptions { DefaultTimeout = TimeSpan.FromMinutes(-1) };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*DefaultTimeout*");
    }

    [Fact]
    public void HumanLoopOptions_NegativeMaxRetries_ShouldThrow()
    {
        var options = new HumanLoopOptions { MaxRetries = -1 };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*MaxRetries*");
    }

    [Fact]
    public void HumanLoopOptions_ZeroMaxRetries_ShouldNotThrow()
    {
        var options = new HumanLoopOptions { MaxRetries = 0 };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    #endregion

    #region AuditOptions

    [Fact]
    public void AuditOptions_DefaultValues_ShouldBeValid()
    {
        var options = new AuditOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AuditOptions_InvalidMaxContentLength_ShouldThrow(int length)
    {
        var options = new AuditOptions { MaxContentLength = length };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*MaxContentLength*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AuditOptions_InvalidMaxInMemoryEntries_ShouldThrow(int entries)
    {
        var options = new AuditOptions { MaxInMemoryEntries = entries };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*MaxInMemoryEntries*");
    }

    #endregion

    #region SemanticCacheOptions

    [Fact]
    public void SemanticCacheOptions_DefaultValues_ShouldBeValid()
    {
        var options = new SemanticCacheOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    public void SemanticCacheOptions_InvalidSimilarityThreshold_ShouldThrow(float threshold)
    {
        var options = new SemanticCacheOptions { SimilarityThreshold = threshold };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*SimilarityThreshold*");
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void SemanticCacheOptions_ValidSimilarityThreshold_ShouldNotThrow(float threshold)
    {
        var options = new SemanticCacheOptions { SimilarityThreshold = threshold };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SemanticCacheOptions_InvalidMaxEntries_ShouldThrow(int entries)
    {
        var options = new SemanticCacheOptions { MaxEntries = entries };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*MaxEntries*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SemanticCacheOptions_InvalidExpirationMinutes_ShouldThrow(int minutes)
    {
        var options = new SemanticCacheOptions { ExpirationMinutes = minutes };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*ExpirationMinutes*");
    }

    #endregion

    #region EvaluationOptions

    [Fact]
    public void EvaluationOptions_DefaultValues_ShouldBeValid()
    {
        var options = new EvaluationOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void EvaluationOptions_InvalidPassThreshold_ShouldThrow(int threshold)
    {
        var options = new EvaluationOptions { PassThreshold = threshold };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*PassThreshold*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void EvaluationOptions_ValidPassThreshold_ShouldNotThrow(int threshold)
    {
        var options = new EvaluationOptions { PassThreshold = threshold };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EvaluationOptions_InvalidMaxConcurrency_ShouldThrow(int concurrency)
    {
        var options = new EvaluationOptions { MaxConcurrency = concurrency };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*MaxConcurrency*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EvaluationOptions_InvalidTestTimeout_ShouldThrow(int timeout)
    {
        var options = new EvaluationOptions { TestTimeoutSeconds = timeout };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*TestTimeout*");
    }

    [Fact]
    public void EvaluationOptions_WithInvalidLLMJudge_ShouldThrow()
    {
        var options = new EvaluationOptions { LLMJudge = new LLMJudgeOptions { Model = "" } };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Model*");
    }

    [Fact]
    public void EvaluationOptions_WithInvalidSemanticSimilarity_ShouldThrow()
    {
        var options = new EvaluationOptions
        {
            SemanticSimilarity = new SemanticSimilarityOptions { Threshold = -1f },
        };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Threshold*");
    }

    #endregion

    #region LLMJudgeOptions

    [Fact]
    public void LLMJudgeOptions_DefaultValues_ShouldBeValid()
    {
        var options = new LLMJudgeOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LLMJudgeOptions_InvalidModel_ShouldThrow(string? model)
    {
        var options = new LLMJudgeOptions { Model = model! };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Model*");
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(2.1f)]
    public void LLMJudgeOptions_InvalidTemperature_ShouldThrow(float temp)
    {
        var options = new LLMJudgeOptions { Temperature = temp };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Temperature*");
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(2f)]
    public void LLMJudgeOptions_ValidTemperature_ShouldNotThrow(float temp)
    {
        var options = new LLMJudgeOptions { Temperature = temp };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    #endregion

    #region SemanticSimilarityOptions

    [Fact]
    public void SemanticSimilarityOptions_DefaultValues_ShouldBeValid()
    {
        var options = new SemanticSimilarityOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    public void SemanticSimilarityOptions_InvalidThreshold_ShouldThrow(float threshold)
    {
        var options = new SemanticSimilarityOptions { Threshold = threshold };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Threshold*");
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void SemanticSimilarityOptions_ValidThreshold_ShouldNotThrow(float threshold)
    {
        var options = new SemanticSimilarityOptions { Threshold = threshold };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    #endregion

    #region TranscriptionOptions

    [Fact]
    public void TranscriptionOptions_DefaultValues_ShouldBeValid()
    {
        var options = new TranscriptionOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void TranscriptionOptions_InvalidTemperature_ShouldThrow(int temp)
    {
        var options = new TranscriptionOptions { Temperature = temp };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Temperature*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void TranscriptionOptions_ValidTemperature_ShouldNotThrow(int temp)
    {
        var options = new TranscriptionOptions { Temperature = temp };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    #endregion

    #region SpeechOptions

    [Fact]
    public void SpeechOptions_DefaultValues_ShouldBeValid()
    {
        var options = new SpeechOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SpeechOptions_InvalidVoice_ShouldThrow(string? voice)
    {
        var options = new SpeechOptions { Voice = voice! };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Voice*");
    }

    [Theory]
    [InlineData(0.24)]
    [InlineData(4.1)]
    [InlineData(0)]
    public void SpeechOptions_InvalidSpeed_ShouldThrow(double speed)
    {
        var options = new SpeechOptions { Speed = speed };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Speed*");
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(1.0)]
    [InlineData(4.0)]
    public void SpeechOptions_ValidSpeed_ShouldNotThrow(double speed)
    {
        var options = new SpeechOptions { Speed = speed };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    #endregion

    #region ServiceRegistryOptions

    [Fact]
    public void ServiceRegistryOptions_DefaultValues_ShouldBeValid()
    {
        var options = new ServiceRegistryOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ServiceRegistryOptions_InvalidHeartbeatInterval_ShouldThrow(int seconds)
    {
        var options = new ServiceRegistryOptions { HeartbeatIntervalSeconds = seconds };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*HeartbeatInterval*");
    }

    [Fact]
    public void ServiceRegistryOptions_ExpireLessThanHeartbeat_ShouldThrow()
    {
        var options = new ServiceRegistryOptions
        {
            HeartbeatIntervalSeconds = 10,
            ServiceExpireSeconds = 10, // equal, should fail
        };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*ServiceExpireSeconds*");
    }

    [Fact]
    public void ServiceRegistryOptions_ExpireGreaterThanHeartbeat_ShouldNotThrow()
    {
        var options = new ServiceRegistryOptions
        {
            HeartbeatIntervalSeconds = 10,
            ServiceExpireSeconds = 11,
        };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    #endregion
}
