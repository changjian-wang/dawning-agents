using System.Text.Json;
using Dawning.Agents.Abstractions.Evaluation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Evaluation;

/// <summary>
/// Evaluation dataset — loads, serializes, and manages test case collections.
/// </summary>
public sealed class EvaluationDataset
{
    private readonly List<EvaluationTestCase> _testCases;

    /// <summary>
    /// Dataset name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// All test cases in the dataset.
    /// </summary>
    public IReadOnlyList<EvaluationTestCase> TestCases => _testCases;

    /// <summary>
    /// Creates an evaluation dataset.
    /// </summary>
    /// <param name="name">Dataset name.</param>
    /// <param name="testCases">Test cases.</param>
    public EvaluationDataset(string name, IEnumerable<EvaluationTestCase> testCases)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(testCases);
        Name = name;
        _testCases = testCases.ToList();
    }

    /// <summary>
    /// Filters test cases by tags.
    /// </summary>
    /// <param name="tags">Tags to filter by (any match).</param>
    public IReadOnlyList<EvaluationTestCase> FilterByTags(IEnumerable<string> tags)
    {
        var tagSet = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
        return _testCases.Where(tc => tc.Tags?.Any(t => tagSet.Contains(t)) == true).ToList();
    }

    /// <summary>
    /// Loads a dataset from a JSON file.
    /// </summary>
    /// <param name="filePath">Path to JSON file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<EvaluationDataset> LoadFromFileAsync(
        string filePath,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await using var stream = File.OpenRead(filePath);
        var data = await JsonSerializer
            .DeserializeAsync<DatasetJson>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (data is null)
        {
            throw new InvalidOperationException($"Failed to deserialize dataset from {filePath}");
        }

        return new EvaluationDataset(
            data.Name ?? Path.GetFileNameWithoutExtension(filePath),
            data.TestCases
        );
    }

    /// <summary>
    /// Saves the dataset to a JSON file.
    /// </summary>
    /// <param name="filePath">Output file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveToFileAsync(
        string filePath,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var data = new DatasetJson { Name = Name, TestCases = _testCases };
        await using var stream = File.Create(filePath);
        await JsonSerializer
            .SerializeAsync(stream, data, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private sealed class DatasetJson
    {
        public string? Name { get; set; }
        public List<EvaluationTestCase> TestCases { get; set; } = [];
    }
}

/// <summary>
/// Evaluation runner — executes a dataset against an evaluator and produces comparison reports.
/// </summary>
public sealed class EvaluationRunner
{
    private readonly IAgentEvaluator _evaluator;
    private readonly ILogger<EvaluationRunner> _logger;

    /// <summary>
    /// Creates an evaluation runner.
    /// </summary>
    /// <param name="evaluator">The agent evaluator.</param>
    /// <param name="logger">The logger.</param>
    public EvaluationRunner(IAgentEvaluator evaluator, ILogger<EvaluationRunner>? logger = null)
    {
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _logger = logger ?? NullLogger<EvaluationRunner>.Instance;
    }

    /// <summary>
    /// Runs a dataset evaluation and returns the report.
    /// </summary>
    /// <param name="dataset">Evaluation dataset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<EvaluationReport> RunAsync(
        EvaluationDataset dataset,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(dataset);

        _logger.LogInformation(
            "Starting evaluation of dataset '{Name}' with {Count} test cases",
            dataset.Name,
            dataset.TestCases.Count
        );

        var report = await _evaluator
            .EvaluateBatchAsync(dataset.TestCases, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Evaluation complete: {Passed}/{Total} passed ({Rate:P0}), avg score: {Score:F1}",
            report.PassedCount,
            report.TotalCount,
            report.PassRate,
            report.AverageScore
        );

        return report;
    }

    /// <summary>
    /// Compares two evaluation reports and outputs the differences.
    /// </summary>
    /// <param name="baseline">Baseline (previous) report.</param>
    /// <param name="current">Current report.</param>
    public static EvaluationComparison Compare(EvaluationReport baseline, EvaluationReport current)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);

        return new EvaluationComparison
        {
            BaselineId = baseline.Id,
            CurrentId = current.Id,
            PassRateDelta = current.PassRate - baseline.PassRate,
            AverageScoreDelta = current.AverageScore - baseline.AverageScore,
            LatencyDelta = current.AverageLatencyMs - baseline.AverageLatencyMs,
            BaselinePassRate = baseline.PassRate,
            CurrentPassRate = current.PassRate,
            Regressions = FindRegressions(baseline, current),
            Improvements = FindImprovements(baseline, current),
        };
    }

    private static IReadOnlyList<string> FindRegressions(
        EvaluationReport baseline,
        EvaluationReport current
    )
    {
        var baselineResults = baseline.Results.ToDictionary(
            r => r.TestCaseId,
            StringComparer.OrdinalIgnoreCase
        );
        var regressions = new List<string>();

        foreach (var result in current.Results)
        {
            if (
                baselineResults.TryGetValue(result.TestCaseId, out var baselineResult)
                && baselineResult.Passed
                && !result.Passed
            )
            {
                regressions.Add(result.TestCaseId);
            }
        }

        return regressions;
    }

    private static IReadOnlyList<string> FindImprovements(
        EvaluationReport baseline,
        EvaluationReport current
    )
    {
        var baselineResults = baseline.Results.ToDictionary(
            r => r.TestCaseId,
            StringComparer.OrdinalIgnoreCase
        );
        var improvements = new List<string>();

        foreach (var result in current.Results)
        {
            if (
                baselineResults.TryGetValue(result.TestCaseId, out var baselineResult)
                && !baselineResult.Passed
                && result.Passed
            )
            {
                improvements.Add(result.TestCaseId);
            }
        }

        return improvements;
    }
}

/// <summary>
/// Evaluation comparison between two reports.
/// </summary>
public record EvaluationComparison
{
    /// <summary>
    /// Baseline report ID.
    /// </summary>
    public required string BaselineId { get; init; }

    /// <summary>
    /// Current report ID.
    /// </summary>
    public required string CurrentId { get; init; }

    /// <summary>
    /// Pass rate delta (positive = improvement).
    /// </summary>
    public double PassRateDelta { get; init; }

    /// <summary>
    /// Average score delta (positive = improvement).
    /// </summary>
    public double AverageScoreDelta { get; init; }

    /// <summary>
    /// Latency delta in ms (negative = improvement).
    /// </summary>
    public double LatencyDelta { get; init; }

    /// <summary>
    /// Baseline pass rate.
    /// </summary>
    public double BaselinePassRate { get; init; }

    /// <summary>
    /// Current pass rate.
    /// </summary>
    public double CurrentPassRate { get; init; }

    /// <summary>
    /// Test cases that regressed (passed → failed).
    /// </summary>
    public IReadOnlyList<string> Regressions { get; init; } = [];

    /// <summary>
    /// Test cases that improved (failed → passed).
    /// </summary>
    public IReadOnlyList<string> Improvements { get; init; } = [];

    /// <summary>
    /// Whether the current report is a regression (pass rate dropped more than 5%).
    /// </summary>
    public bool IsRegression => PassRateDelta < -0.05;
}
