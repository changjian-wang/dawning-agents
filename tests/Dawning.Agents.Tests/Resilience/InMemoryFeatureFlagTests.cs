using Dawning.Agents.Abstractions.Resilience;
using Dawning.Agents.Core.Resilience;
using FluentAssertions;

namespace Dawning.Agents.Tests.Resilience;

/// <summary>
/// InMemoryFeatureFlag tests.
/// </summary>
public sealed class InMemoryFeatureFlagTests
{
    private readonly InMemoryFeatureFlag _flag = new();

    [Fact]
    public async Task IsEnabledAsync_UnknownFlag_ReturnsFalse()
    {
        var result = await _flag.IsEnabledAsync("unknown");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_DisabledFlag_ReturnsFalse()
    {
        _flag.SetFlag(new FeatureFlagDefinition { Name = "feature1", Enabled = false });

        var result = await _flag.IsEnabledAsync("feature1");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_Enabled100Percent_ReturnsTrue()
    {
        _flag.SetFlag(
            new FeatureFlagDefinition
            {
                Name = "feature1",
                Enabled = true,
                RolloutPercentage = 100,
            }
        );

        var result = await _flag.IsEnabledAsync("feature1");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_Enabled0Percent_ReturnsFalse()
    {
        _flag.SetFlag(
            new FeatureFlagDefinition
            {
                Name = "feature1",
                Enabled = true,
                RolloutPercentage = 0,
            }
        );

        var result = await _flag.IsEnabledAsync("feature1");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_DeterministicForSameContext()
    {
        _flag.SetFlag(
            new FeatureFlagDefinition
            {
                Name = "test",
                Enabled = true,
                RolloutPercentage = 50,
            }
        );

        var result1 = await _flag.IsEnabledAsync("test", "user-123");
        var result2 = await _flag.IsEnabledAsync("test", "user-123");

        result1.Should().Be(result2, "same context should always produce same result");
    }

    [Fact]
    public async Task IsEnabledAsync_50Percent_SplitsTraffic()
    {
        _flag.SetFlag(
            new FeatureFlagDefinition
            {
                Name = "ab_test",
                Enabled = true,
                RolloutPercentage = 50,
            }
        );

        var enabledCount = 0;
        for (var i = 0; i < 100; i++)
        {
            if (await _flag.IsEnabledAsync("ab_test", $"user-{i}"))
            {
                enabledCount++;
            }
        }

        // With 100 users, roughly 50% should be enabled (allow ±20% tolerance)
        enabledCount.Should().BeInRange(30, 70);
    }

    [Fact]
    public async Task IsEnabledAsync_CaseInsensitiveName()
    {
        _flag.SetFlag(
            new FeatureFlagDefinition
            {
                Name = "MyFeature",
                Enabled = true,
                RolloutPercentage = 100,
            }
        );

        var result = await _flag.IsEnabledAsync("myfeature");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SetFlag_OverwritesExistingFlag()
    {
        _flag.SetFlag(new FeatureFlagDefinition { Name = "f1", Enabled = false });
        _flag.SetFlag(
            new FeatureFlagDefinition
            {
                Name = "f1",
                Enabled = true,
                RolloutPercentage = 100,
            }
        );

        var result = await _flag.IsEnabledAsync("f1");
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void SetFlag_InvalidRolloutPercentage_ShouldThrow(int percentage)
    {
        var act = () =>
            _flag.SetFlag(
                new FeatureFlagDefinition
                {
                    Name = "bad",
                    Enabled = true,
                    RolloutPercentage = percentage,
                }
            );
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
