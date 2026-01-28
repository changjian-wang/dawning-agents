using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Abstractions.Orchestration;
using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Abstractions.Resilience;
using Dawning.Agents.Abstractions.Safety;
using Dawning.Agents.Core.Validation;
using FluentAssertions;

namespace Dawning.Agents.Tests.Validation;

public class OptionsValidatorTests
{
    #region MemoryOptionsValidator Tests

    [Fact]
    public void MemoryOptions_ValidConfig_PassesValidation()
    {
        var validator = new MemoryOptionsValidator();
        var options = new MemoryOptions
        {
            Type = MemoryType.Window,
            WindowSize = 10,
            MaxRecentMessages = 6,
            SummaryThreshold = 10,
            ModelName = "gpt-4",
            MaxContextTokens = 8192,
        };

        var result = validator.Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void MemoryOptions_InvalidWindowSize_FailsValidation()
    {
        var validator = new MemoryOptionsValidator();
        var options = new MemoryOptions { WindowSize = 0 };

        var result = validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "WindowSize");
    }

    [Fact]
    public void MemoryOptions_SummaryThresholdLessThanMaxRecentMessages_FailsValidation()
    {
        var validator = new MemoryOptionsValidator();
        var options = new MemoryOptions { MaxRecentMessages = 10, SummaryThreshold = 5 };

        var result = validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SummaryThreshold");
    }

    #endregion

    #region RAGOptionsValidator Tests

    [Fact]
    public void RAGOptions_ValidConfig_PassesValidation()
    {
        var validator = new RAGOptionsValidator();
        var options = new RAGOptions
        {
            ChunkSize = 500,
            ChunkOverlap = 50,
            TopK = 5,
            MinScore = 0.5f,
            EmbeddingModel = "text-embedding-3-small",
        };

        var result = validator.Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RAGOptions_ChunkOverlapGreaterThanChunkSize_FailsValidation()
    {
        var validator = new RAGOptionsValidator();
        var options = new RAGOptions { ChunkSize = 100, ChunkOverlap = 150 };

        var result = validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ChunkOverlap");
    }

    [Fact]
    public void RAGOptions_InvalidMinScore_FailsValidation()
    {
        var validator = new RAGOptionsValidator();
        var options = new RAGOptions { MinScore = 1.5f };

        var result = validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MinScore");
    }

    #endregion

    #region SafetyOptionsValidator Tests

    [Fact]
    public void SafetyOptions_ValidConfig_PassesValidation()
    {
        var validator = new SafetyOptionsValidator();
        var options = new SafetyOptions
        {
            MaxInputLength = 10000,
            MaxOutputLength = 50000,
            EnableSensitiveDataDetection = true,
        };

        var result = validator.Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void SafetyOptions_InvalidMaxInputLength_FailsValidation()
    {
        var validator = new SafetyOptionsValidator();
        var options = new SafetyOptions { MaxInputLength = 0 };

        var result = validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MaxInputLength");
    }

    [Fact]
    public void SafetyOptions_InvalidRegexPattern_FailsValidation()
    {
        var validator = new SafetyOptionsValidator();
        var options = new SafetyOptions
        {
            SensitivePatterns =
            [
                new SensitivePattern { Name = "Invalid", Pattern = "[invalid(" },
            ],
        };

        var result = validator.Validate(options);

        result.IsValid.Should().BeFalse();
    }

    #endregion

    #region OrchestratorOptionsValidator Tests

    [Fact]
    public void OrchestratorOptions_ValidConfig_PassesValidation()
    {
        var validator = new OrchestratorOptionsValidator();
        var options = new OrchestratorOptions
        {
            MaxConcurrency = 5,
            TimeoutSeconds = 300,
            AgentTimeoutSeconds = 60,
        };

        var result = validator.Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void OrchestratorOptions_AgentTimeoutGreaterThanTotal_FailsValidation()
    {
        var validator = new OrchestratorOptionsValidator();
        var options = new OrchestratorOptions { TimeoutSeconds = 60, AgentTimeoutSeconds = 120 };

        var result = validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AgentTimeoutSeconds");
    }

    #endregion

    #region BulkheadOptionsValidator Tests

    [Fact]
    public void BulkheadOptions_ValidConfig_PassesValidation()
    {
        var validator = new BulkheadOptionsValidator();
        var options = new BulkheadOptions
        {
            Enabled = true,
            MaxConcurrency = 10,
            MaxQueuedActions = 20,
        };

        var result = validator.Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void BulkheadOptions_InvalidMaxConcurrency_FailsValidation()
    {
        var validator = new BulkheadOptionsValidator();
        var options = new BulkheadOptions { MaxConcurrency = 0 };

        var result = validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MaxConcurrency");
    }

    [Fact]
    public void BulkheadOptions_ExcessiveMaxConcurrency_FailsValidation()
    {
        var validator = new BulkheadOptionsValidator();
        var options = new BulkheadOptions { MaxConcurrency = 2000 };

        var result = validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MaxConcurrency");
    }

    #endregion
}
