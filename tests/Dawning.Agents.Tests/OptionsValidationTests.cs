using Dawning.Agents.Abstractions;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Abstractions.Resilience;
using Dawning.Agents.Abstractions.Safety;
using Dawning.Agents.Core;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Tests;

public class OptionsValidationTests
{
    // ── ValidatableOptionsValidator ──

    [Fact]
    public void Validator_ValidOptions_ShouldSucceed()
    {
        var validator = new ValidatableOptionsValidator<TestValidOptions>();
        var options = new TestValidOptions { Value = "ok" };

        var result = validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validator_InvalidOptions_ShouldFail()
    {
        var validator = new ValidatableOptionsValidator<TestValidOptions>();
        var options = new TestValidOptions { Value = "" };

        var result = validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("Value is required");
    }

    // ── AddValidatedOptions with IConfiguration ──

    [Fact]
    public void AddValidatedOptions_WithConfiguration_ShouldResolve()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Test:Value"] = "hello" }
            )
            .Build();

        var services = new ServiceCollection();
        services.AddValidatedOptions<TestValidOptions>(config, "Test");
        var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptions<TestValidOptions>>();
        options.Value.Value.Should().Be("hello");
    }

    [Fact]
    public void AddValidatedOptions_InvalidConfig_ShouldThrowOnStart()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Test:Value"] = "" }
            )
            .Build();

        var services = new ServiceCollection();
        services.AddValidatedOptions<TestValidOptions>(config, "Test");
        var sp = services.BuildServiceProvider();

        var act = () => sp.GetRequiredService<IOptions<TestValidOptions>>().Value;
        act.Should().Throw<OptionsValidationException>();
    }

    // ── AddValidatedOptions with Action ──

    [Fact]
    public void AddValidatedOptions_WithAction_ShouldResolve()
    {
        var services = new ServiceCollection();
        services.AddValidatedOptions<TestValidOptions>(o => o.Value = "configured");
        var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptions<TestValidOptions>>();
        options.Value.Value.Should().Be("configured");
    }

    // ── IValidatableOptions interface on real Options classes ──

    [Fact]
    public void LLMOptions_ShouldImplementIValidatableOptions()
    {
        var options = new LLMOptions();
        options.Should().BeAssignableTo<IValidatableOptions>();
    }

    [Fact]
    public void MemoryOptions_ShouldImplementIValidatableOptions()
    {
        var options = new MemoryOptions();
        options.Should().BeAssignableTo<IValidatableOptions>();
    }

    [Fact]
    public void ResilienceOptions_ShouldImplementIValidatableOptions()
    {
        var options = new ResilienceOptions();
        options.Should().BeAssignableTo<IValidatableOptions>();
    }

    [Fact]
    public void SafetyOptions_ShouldImplementIValidatableOptions()
    {
        var options = new SafetyOptions();
        options.Should().BeAssignableTo<IValidatableOptions>();
    }

    [Fact]
    public void RateLimitOptions_ShouldImplementIValidatableOptions()
    {
        var options = new RateLimitOptions();
        options.Should().BeAssignableTo<IValidatableOptions>();
    }

    // ── Validate() correctness on real Options ──

    [Fact]
    public void MemoryOptions_DefaultValues_ShouldBeValid()
    {
        var options = new MemoryOptions();

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void ResilienceOptions_DefaultValues_ShouldBeValid()
    {
        var options = new ResilienceOptions();

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void RateLimitOptions_DefaultValues_ShouldBeValid()
    {
        var options = new RateLimitOptions();

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void SafetyOptions_DefaultValues_ShouldBeValid()
    {
        var options = new SafetyOptions();

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    // ── Test helper ──

    private class TestValidOptions : IValidatableOptions
    {
        public string Value { get; set; } = "default";

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Value))
            {
                throw new InvalidOperationException("Value is required");
            }
        }
    }
}
