namespace Dawning.Agents.Tests.Evaluation;

using Dawning.Agents.Abstractions.Evaluation;
using Dawning.Agents.Core.Evaluation;
using FluentAssertions;
using Xunit;

public class EvaluationTestCaseTests
{
    [Fact]
    public void TestCase_Should_Have_Required_Properties()
    {
        // Arrange & Act
        var testCase = new EvaluationTestCase
        {
            Id = "test-001",
            Name = "Test Case 1",
            Input = "What is 2+2?",
            ExpectedOutput = "4",
            ExpectedKeywords = ["four", "4"],
            ExpectedTools = ["MathTool.Calculate"],
            MaxLatencyMs = 5000,
        };

        // Assert
        testCase.Id.Should().Be("test-001");
        testCase.Name.Should().Be("Test Case 1");
        testCase.Input.Should().Be("What is 2+2?");
        testCase.ExpectedOutput.Should().Be("4");
        testCase.ExpectedKeywords.Should().HaveCount(2);
        testCase.ExpectedTools.Should().HaveCount(1);
        testCase.MaxLatencyMs.Should().Be(5000);
    }
}

public class EvaluationResultTests
{
    [Fact]
    public void Result_Should_Have_Correct_Properties()
    {
        // Arrange & Act
        var result = new EvaluationResult
        {
            TestCaseId = "test-001",
            Passed = true,
            Score = 95.5,
            ActualOutput = "The answer is 4",
            LatencyMs = 1500,
            StepCount = 3,
            ToolsCalled = ["MathTool.Calculate"],
            MetricScores = new Dictionary<string, double>
            {
                ["KeywordMatch"] = 1.0,
                ["Latency"] = 0.9,
            },
        };

        // Assert
        result.TestCaseId.Should().Be("test-001");
        result.Passed.Should().BeTrue();
        result.Score.Should().Be(95.5);
        result.LatencyMs.Should().Be(1500);
        result.StepCount.Should().Be(3);
        result.ToolsCalled.Should().Contain("MathTool.Calculate");
        result.MetricScores.Should().ContainKey("KeywordMatch");
    }
}

public class TokenUsageTests
{
    [Fact]
    public void TotalTokens_Should_Be_Sum_Of_Input_And_Output()
    {
        // Arrange & Act
        var usage = new TokenUsage
        {
            InputTokens = 100,
            OutputTokens = 50,
            EstimatedCost = 0.01m,
        };

        // Assert
        usage.TotalTokens.Should().Be(150);
        usage.EstimatedCost.Should().Be(0.01m);
    }
}

public class EvaluationReportTests
{
    [Fact]
    public void Report_Should_Calculate_Statistics_Correctly()
    {
        // Arrange
        var results = new List<EvaluationResult>
        {
            new()
            {
                TestCaseId = "test-001",
                Passed = true,
                Score = 90,
                LatencyMs = 1000,
            },
            new()
            {
                TestCaseId = "test-002",
                Passed = true,
                Score = 80,
                LatencyMs = 2000,
            },
            new()
            {
                TestCaseId = "test-003",
                Passed = false,
                Score = 50,
                LatencyMs = 3000,
            },
        };

        // Act
        var report = new EvaluationReport { Results = results };

        // Assert
        report.TotalCount.Should().Be(3);
        report.PassedCount.Should().Be(2);
        report.FailedCount.Should().Be(1);
        report.PassRate.Should().BeApproximately(0.666, 0.01);
        report.AverageScore.Should().BeApproximately(73.33, 0.01);
        report.AverageLatencyMs.Should().Be(2000);
    }

    [Fact]
    public void Report_Should_Handle_Empty_Results()
    {
        // Arrange & Act
        var report = new EvaluationReport { Results = [] };

        // Assert
        report.TotalCount.Should().Be(0);
        report.PassedCount.Should().Be(0);
        report.PassRate.Should().Be(0);
        report.AverageScore.Should().Be(0);
    }

    [Fact]
    public void Report_Should_Calculate_Percentiles_Correctly()
    {
        // Arrange
        var results = Enumerable
            .Range(1, 100)
            .Select(i => new EvaluationResult
            {
                TestCaseId = $"test-{i:D3}",
                Passed = true,
                Score = 100,
                LatencyMs = i * 10,
            })
            .ToList();

        var report = new EvaluationReport { Results = results };

        // Assert
        report.P50LatencyMs.Should().BeApproximately(500, 10);
        report.P95LatencyMs.Should().BeApproximately(950, 10);
        report.P99LatencyMs.Should().BeApproximately(990, 10);
    }
}

public class EvaluationOptionsTests
{
    [Fact]
    public void Default_Options_Should_Have_Expected_Values()
    {
        // Arrange & Act
        var options = new EvaluationOptions();

        // Assert
        options.PassThreshold.Should().Be(70);
        options.MaxConcurrency.Should().Be(5);
        options.TestTimeoutSeconds.Should().Be(120);
        options.ContinueOnFailure.Should().BeTrue();
        options.EnabledMetrics.Should().Contain(EvaluationMetric.ContainsKeywords);
    }

    [Fact]
    public void EvaluationOptions_Validate_WithValidConfig_DoesNotThrow()
    {
        var options = new EvaluationOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void EvaluationOptions_Validate_PassThresholdOutOfRange_Throws()
    {
        var options = new EvaluationOptions { PassThreshold = 101 };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*PassThreshold*");
    }

    [Fact]
    public void EvaluationOptions_Validate_NegativePassThreshold_Throws()
    {
        var options = new EvaluationOptions { PassThreshold = -1 };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*PassThreshold*");
    }

    [Fact]
    public void EvaluationOptions_Validate_ZeroMaxConcurrency_Throws()
    {
        var options = new EvaluationOptions { MaxConcurrency = 0 };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*MaxConcurrency*");
    }

    [Fact]
    public void EvaluationOptions_Validate_ZeroTestTimeoutSeconds_Throws()
    {
        var options = new EvaluationOptions { TestTimeoutSeconds = 0 };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*TestTimeoutSeconds*");
    }

    [Fact]
    public void EvaluationOptions_Validate_CascadesToLLMJudge()
    {
        var options = new EvaluationOptions { LLMJudge = new LLMJudgeOptions { Model = "" } };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Model*");
    }

    [Fact]
    public void EvaluationOptions_Validate_CascadesToSemanticSimilarity()
    {
        var options = new EvaluationOptions
        {
            SemanticSimilarity = new SemanticSimilarityOptions { Threshold = 1.5f },
        };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Threshold*");
    }

    [Fact]
    public void LLMJudgeOptions_Validate_WithValidConfig_DoesNotThrow()
    {
        var options = new LLMJudgeOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void LLMJudgeOptions_Validate_EmptyModel_Throws()
    {
        var options = new LLMJudgeOptions { Model = "" };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Model*");
    }

    [Fact]
    public void LLMJudgeOptions_Validate_TemperatureOutOfRange_Throws()
    {
        var options = new LLMJudgeOptions { Temperature = 3.0f };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Temperature*");
    }

    [Fact]
    public void SemanticSimilarityOptions_Validate_WithValidConfig_DoesNotThrow()
    {
        var options = new SemanticSimilarityOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void SemanticSimilarityOptions_Validate_ThresholdOutOfRange_Throws()
    {
        var options = new SemanticSimilarityOptions { Threshold = -0.1f };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Threshold*");
    }
}

public class KeywordMatchEvaluatorTests
{
    private readonly KeywordMatchEvaluator _evaluator = new();

    [Fact]
    public async Task Should_Return_Full_Score_When_All_Keywords_Match()
    {
        // Arrange
        var context = new MetricEvaluationContext
        {
            TestCase = new EvaluationTestCase
            {
                Id = "test",
                Input = "test",
                ExpectedKeywords = ["hello", "world"],
            },
            ActualOutput = "Hello World!",
        };

        // Act
        var score = await _evaluator.EvaluateAsync(context);

        // Assert
        score.Should().Be(1.0);
    }

    [Fact]
    public async Task Should_Return_Partial_Score_When_Some_Keywords_Match()
    {
        // Arrange
        var context = new MetricEvaluationContext
        {
            TestCase = new EvaluationTestCase
            {
                Id = "test",
                Input = "test",
                ExpectedKeywords = ["hello", "world", "foo"],
            },
            ActualOutput = "Hello World!",
        };

        // Act
        var score = await _evaluator.EvaluateAsync(context);

        // Assert
        score.Should().BeApproximately(0.666, 0.01);
    }

    [Fact]
    public async Task Should_Return_Full_Score_When_No_Keywords_Expected()
    {
        // Arrange
        var context = new MetricEvaluationContext
        {
            TestCase = new EvaluationTestCase
            {
                Id = "test",
                Input = "test",
                ExpectedKeywords = null,
            },
            ActualOutput = "Any output",
        };

        // Act
        var score = await _evaluator.EvaluateAsync(context);

        // Assert
        score.Should().Be(1.0);
    }
}

public class ToolCallAccuracyEvaluatorTests
{
    private readonly ToolCallAccuracyEvaluator _evaluator = new();

    [Fact]
    public async Task Should_Return_Full_Score_When_All_Tools_Match()
    {
        // Arrange
        var context = new MetricEvaluationContext
        {
            TestCase = new EvaluationTestCase
            {
                Id = "test",
                Input = "test",
                ExpectedTools = ["ToolA", "ToolB"],
            },
            ToolsCalled = ["ToolA", "ToolB"],
        };

        // Act
        var score = await _evaluator.EvaluateAsync(context);

        // Assert
        score.Should().Be(1.0);
    }

    [Fact]
    public async Task Should_Return_Zero_When_No_Tools_Called_But_Expected()
    {
        // Arrange
        var context = new MetricEvaluationContext
        {
            TestCase = new EvaluationTestCase
            {
                Id = "test",
                Input = "test",
                ExpectedTools = ["ToolA"],
            },
            ToolsCalled = [],
        };

        // Act
        var score = await _evaluator.EvaluateAsync(context);

        // Assert
        score.Should().Be(0.0);
    }

    [Fact]
    public async Task Should_Return_Full_Score_When_No_Tools_Expected()
    {
        // Arrange
        var context = new MetricEvaluationContext
        {
            TestCase = new EvaluationTestCase
            {
                Id = "test",
                Input = "test",
                ExpectedTools = null,
            },
            ToolsCalled = ["ToolA"],
        };

        // Act
        var score = await _evaluator.EvaluateAsync(context);

        // Assert
        score.Should().Be(1.0);
    }
}

public class LatencyEvaluatorTests
{
    private readonly LatencyEvaluator _evaluator = new();

    [Fact]
    public async Task Should_Return_Full_Score_When_Within_Limit()
    {
        // Arrange
        var context = new MetricEvaluationContext
        {
            TestCase = new EvaluationTestCase
            {
                Id = "test",
                Input = "test",
                MaxLatencyMs = 5000,
            },
            LatencyMs = 3000,
        };

        // Act
        var score = await _evaluator.EvaluateAsync(context);

        // Assert
        score.Should().Be(1.0);
    }

    [Fact]
    public async Task Should_Return_Partial_Score_When_Over_Limit()
    {
        // Arrange
        var context = new MetricEvaluationContext
        {
            TestCase = new EvaluationTestCase
            {
                Id = "test",
                Input = "test",
                MaxLatencyMs = 1000,
            },
            LatencyMs = 2000,
        };

        // Act
        var score = await _evaluator.EvaluateAsync(context);

        // Assert
        score.Should().Be(0.5);
    }

    [Fact]
    public async Task Should_Return_Full_Score_When_No_Limit()
    {
        // Arrange
        var context = new MetricEvaluationContext
        {
            TestCase = new EvaluationTestCase
            {
                Id = "test",
                Input = "test",
                MaxLatencyMs = null,
            },
            LatencyMs = 10000,
        };

        // Act
        var score = await _evaluator.EvaluateAsync(context);

        // Assert
        score.Should().Be(1.0);
    }
}

public class ExactMatchEvaluatorTests
{
    private readonly ExactMatchEvaluator _evaluator = new();

    [Fact]
    public async Task Should_Return_Full_Score_When_Exact_Match()
    {
        // Arrange
        var context = new MetricEvaluationContext
        {
            TestCase = new EvaluationTestCase
            {
                Id = "test",
                Input = "test",
                ExpectedOutput = "Hello World",
            },
            ActualOutput = "Hello World",
        };

        // Act
        var score = await _evaluator.EvaluateAsync(context);

        // Assert
        score.Should().Be(1.0);
    }

    [Fact]
    public async Task Should_Ignore_Case_And_Whitespace()
    {
        // Arrange
        var context = new MetricEvaluationContext
        {
            TestCase = new EvaluationTestCase
            {
                Id = "test",
                Input = "test",
                ExpectedOutput = "  hello world  ",
            },
            ActualOutput = "HELLO WORLD",
        };

        // Act
        var score = await _evaluator.EvaluateAsync(context);

        // Assert
        score.Should().Be(1.0);
    }

    [Fact]
    public async Task Should_Return_Zero_When_Not_Match()
    {
        // Arrange
        var context = new MetricEvaluationContext
        {
            TestCase = new EvaluationTestCase
            {
                Id = "test",
                Input = "test",
                ExpectedOutput = "Hello",
            },
            ActualOutput = "World",
        };

        // Act
        var score = await _evaluator.EvaluateAsync(context);

        // Assert
        score.Should().Be(0.0);
    }
}

public class EvaluationReportGeneratorTests
{
    private readonly EvaluationReportGenerator _generator = new();

    [Fact]
    public void GenerateMarkdown_Should_Include_Summary()
    {
        // Arrange
        var report = new EvaluationReport
        {
            Name = "Test Report",
            AgentName = "TestAgent",
            Results =
            [
                new EvaluationResult
                {
                    TestCaseId = "test-001",
                    Passed = true,
                    Score = 90,
                    LatencyMs = 1000,
                },
            ],
        };

        // Act
        var markdown = _generator.GenerateMarkdown(report);

        // Assert
        markdown.Should().Contain("# Evaluation Report: Test Report");
        markdown.Should().Contain("**Agent:** TestAgent");
        markdown.Should().Contain("| Pass Rate |");
        markdown.Should().Contain("test-001");
    }

    [Fact]
    public void GenerateJson_Should_Be_Valid()
    {
        // Arrange
        var report = new EvaluationReport
        {
            Name = "Test Report",
            Results =
            [
                new EvaluationResult
                {
                    TestCaseId = "test-001",
                    Passed = true,
                    Score = 90,
                    LatencyMs = 1000,
                },
            ],
        };

        // Act
        var json = _generator.GenerateJson(report);

        // Assert
        json.Should().Contain("\"name\":");
        json.Should().Contain("\"results\":");
        json.Should().Contain("\"testCaseId\":");
    }
}

public class ABTestResultTests
{
    [Fact]
    public void Winner_Should_Be_Higher_Score_Variant()
    {
        // Arrange
        var result = new ABTestResult
        {
            VariantA = new ABVariant
            {
                Name = "A",
                Report = new EvaluationReport
                {
                    Results =
                    [
                        new EvaluationResult
                        {
                            TestCaseId = "1",
                            Score = 90,
                            Passed = true,
                        },
                    ],
                },
            },
            VariantB = new ABVariant
            {
                Name = "B",
                Report = new EvaluationReport
                {
                    Results =
                    [
                        new EvaluationResult
                        {
                            TestCaseId = "1",
                            Score = 80,
                            Passed = true,
                        },
                    ],
                },
            },
            TestCaseCount = 1,
        };

        // Assert
        result.Winner.Should().NotBeNull();
        result.Winner!.Name.Should().Be("A");
        result.ScoreDifference.Should().Be(10);
    }

    [Fact]
    public void Winner_Should_Be_Null_When_Tie()
    {
        // Arrange
        var result = new ABTestResult
        {
            VariantA = new ABVariant
            {
                Name = "A",
                Report = new EvaluationReport
                {
                    Results =
                    [
                        new EvaluationResult
                        {
                            TestCaseId = "1",
                            Score = 90,
                            Passed = true,
                        },
                    ],
                },
            },
            VariantB = new ABVariant
            {
                Name = "B",
                Report = new EvaluationReport
                {
                    Results =
                    [
                        new EvaluationResult
                        {
                            TestCaseId = "1",
                            Score = 90.5,
                            Passed = true,
                        },
                    ],
                },
            },
            TestCaseCount = 1,
        };

        // Assert
        result.Winner.Should().BeNull(); // 差距小于 1 分
    }
}
