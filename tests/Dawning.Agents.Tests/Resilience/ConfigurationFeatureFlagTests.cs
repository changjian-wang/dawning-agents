using Dawning.Agents.Abstractions.Resilience;
using Dawning.Agents.Core.Resilience;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Dawning.Agents.Tests.Resilience;

/// <summary>
/// ConfigurationFeatureFlag tests.
/// </summary>
public sealed class ConfigurationFeatureFlagTests
{
    [Fact]
    public async Task IsEnabledAsync_ReadsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["FeatureFlags:NewAgent:Enabled"] = "true",
                    ["FeatureFlags:NewAgent:RolloutPercentage"] = "100",
                }
            )
            .Build();

        var flag = new ConfigurationFeatureFlag(config);

        var result = await flag.IsEnabledAsync("NewAgent");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_MissingFlag_ReturnsFalse()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var flag = new ConfigurationFeatureFlag(config);

        var result = await flag.IsEnabledAsync("NonExistent");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_DisabledFlag_ReturnsFalse()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["FeatureFlags:Feature1:Enabled"] = "false",
                    ["FeatureFlags:Feature1:RolloutPercentage"] = "100",
                }
            )
            .Build();

        var flag = new ConfigurationFeatureFlag(config);

        var result = await flag.IsEnabledAsync("Feature1");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_DefaultRolloutIs100()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["FeatureFlags:Feature1:Enabled"] = "true" }
            )
            .Build();

        var flag = new ConfigurationFeatureFlag(config);

        var result = await flag.IsEnabledAsync("Feature1");
        result.Should().BeTrue();
    }

    [Fact]
    public void Constructor_NullConfiguration_ShouldThrow()
    {
        var act = () => new ConfigurationFeatureFlag(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
