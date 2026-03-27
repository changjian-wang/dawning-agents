using Dawning.Agents.Abstractions.Evaluation;
using Dawning.Agents.Core.Evaluation;
using FluentAssertions;
using Moq;

namespace Dawning.Agents.Tests.Evaluation;

/// <summary>
/// EvaluationDataset and EvaluationRunner tests.
/// </summary>
public sealed class EvaluationDatasetTests
{
    private static EvaluationTestCase MakeCase(string id, params string[] tags) =>
        new()
        {
            Id = id,
            Input = $"Input for {id}",
            Tags = tags,
        };

    [Fact]
    public void Constructor_StoresNameAndCases()
    {
        var cases = new[] { MakeCase("t1"), MakeCase("t2") };
        var dataset = new EvaluationDataset("test-ds", cases);

        dataset.Name.Should().Be("test-ds");
        dataset.TestCases.Should().HaveCount(2);
    }

    [Fact]
    public void Constructor_EmptyName_ShouldThrow()
    {
        var act = () => new EvaluationDataset("", [MakeCase("t1")]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NullTestCases_ShouldThrow()
    {
        var act = () => new EvaluationDataset("ds", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FilterByTags_ReturnsOnlyMatching()
    {
        var cases = new[]
        {
            MakeCase("t1", "math", "easy"),
            MakeCase("t2", "science"),
            MakeCase("t3", "math", "hard"),
        };
        var dataset = new EvaluationDataset("ds", cases);

        var filtered = dataset.FilterByTags(["math"]);

        filtered.Should().HaveCount(2);
        filtered.Select(t => t.Id).Should().BeEquivalentTo("t1", "t3");
    }

    [Fact]
    public void FilterByTags_CaseInsensitive()
    {
        var cases = new[] { MakeCase("t1", "Math") };
        var dataset = new EvaluationDataset("ds", cases);

        var filtered = dataset.FilterByTags(["MATH"]);
        filtered.Should().HaveCount(1);
    }

    [Fact]
    public void FilterByTags_NoMatch_ReturnsEmpty()
    {
        var cases = new[] { MakeCase("t1", "math") };
        var dataset = new EvaluationDataset("ds", cases);

        var filtered = dataset.FilterByTags(["physics"]);
        filtered.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var cases = new[]
        {
            new EvaluationTestCase
            {
                Id = "tc1",
                Input = "What is 2+2?",
                ExpectedOutput = "4",
                Tags = ["math"],
            },
        };
        var dataset = new EvaluationDataset("round-trip", cases);

        var tempFile = Path.Combine(Path.GetTempPath(), $"eval-test-{Guid.NewGuid():N}.json");
        try
        {
            await dataset.SaveToFileAsync(tempFile);
            var loaded = await EvaluationDataset.LoadFromFileAsync(tempFile);

            loaded.Name.Should().Be("round-trip");
            loaded.TestCases.Should().HaveCount(1);
            loaded.TestCases[0].Id.Should().Be("tc1");
            loaded.TestCases[0].Input.Should().Be("What is 2+2?");
            loaded.TestCases[0].ExpectedOutput.Should().Be("4");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}

/// <summary>
/// EvaluationRunner tests.
/// </summary>
public sealed class EvaluationRunnerTests
{
    [Fact]
    public async Task RunAsync_CallsEvaluatorBatch()
    {
        var cases = new[]
        {
            new EvaluationTestCase { Id = "t1", Input = "hello" },
        };
        var dataset = new EvaluationDataset("ds", cases);

        var expectedReport = new EvaluationReport
        {
            Results =
            [
                new EvaluationResult
                {
                    TestCaseId = "t1",
                    Passed = true,
                    Score = 90,
                },
            ],
        };

        var evaluator = new Mock<IAgentEvaluator>();
        evaluator
            .Setup(e =>
                e.EvaluateBatchAsync(
                    It.IsAny<IEnumerable<EvaluationTestCase>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expectedReport);

        var runner = new EvaluationRunner(evaluator.Object);
        var report = await runner.RunAsync(dataset);

        report.PassedCount.Should().Be(1);
        report.TotalCount.Should().Be(1);
    }

    [Fact]
    public void Compare_DetectsRegressions()
    {
        var baseline = new EvaluationReport
        {
            Results =
            [
                new EvaluationResult
                {
                    TestCaseId = "t1",
                    Passed = true,
                    Score = 90,
                },
                new EvaluationResult
                {
                    TestCaseId = "t2",
                    Passed = true,
                    Score = 80,
                },
            ],
        };
        var current = new EvaluationReport
        {
            Results =
            [
                new EvaluationResult
                {
                    TestCaseId = "t1",
                    Passed = false,
                    Score = 30,
                },
                new EvaluationResult
                {
                    TestCaseId = "t2",
                    Passed = true,
                    Score = 85,
                },
            ],
        };

        var comparison = EvaluationRunner.Compare(baseline, current);

        comparison.Regressions.Should().Contain("t1");
        comparison.Improvements.Should().BeEmpty();
    }

    [Fact]
    public void Compare_DetectsImprovements()
    {
        var baseline = new EvaluationReport
        {
            Results =
            [
                new EvaluationResult
                {
                    TestCaseId = "t1",
                    Passed = false,
                    Score = 30,
                },
            ],
        };
        var current = new EvaluationReport
        {
            Results =
            [
                new EvaluationResult
                {
                    TestCaseId = "t1",
                    Passed = true,
                    Score = 90,
                },
            ],
        };

        var comparison = EvaluationRunner.Compare(baseline, current);

        comparison.Improvements.Should().Contain("t1");
        comparison.Regressions.Should().BeEmpty();
    }

    [Fact]
    public void Compare_IsRegression_WhenPassRateDropsMoreThan5Percent()
    {
        var baseline = new EvaluationReport
        {
            Results =
            [
                new EvaluationResult { TestCaseId = "t1", Passed = true },
                new EvaluationResult { TestCaseId = "t2", Passed = true },
                new EvaluationResult { TestCaseId = "t3", Passed = true },
                new EvaluationResult { TestCaseId = "t4", Passed = true },
            ],
        };
        var current = new EvaluationReport
        {
            Results =
            [
                new EvaluationResult { TestCaseId = "t1", Passed = true },
                new EvaluationResult { TestCaseId = "t2", Passed = false },
                new EvaluationResult { TestCaseId = "t3", Passed = false },
                new EvaluationResult { TestCaseId = "t4", Passed = true },
            ],
        };

        var comparison = EvaluationRunner.Compare(baseline, current);

        // 100% → 50% = -50% delta, IsRegression should be true
        comparison.IsRegression.Should().BeTrue();
        comparison.PassRateDelta.Should().BeLessThan(-0.05);
    }

    [Fact]
    public void Compare_NotRegression_WhenPassRateDropsLessThan5Percent()
    {
        var baseline = new EvaluationReport
        {
            Results = Enumerable
                .Range(1, 100)
                .Select(i => new EvaluationResult { TestCaseId = $"t{i}", Passed = true })
                .ToList(),
        };
        var current = new EvaluationReport
        {
            Results = Enumerable
                .Range(1, 100)
                .Select(i => new EvaluationResult
                {
                    TestCaseId = $"t{i}",
                    Passed = i <= 96, // 96% pass rate, only 4% drop
                })
                .ToList(),
        };

        var comparison = EvaluationRunner.Compare(baseline, current);

        comparison.IsRegression.Should().BeFalse();
    }
}
