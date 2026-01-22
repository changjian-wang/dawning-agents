namespace Dawning.Agents.Tests.Configuration;

using Dawning.Agents.Abstractions.Configuration;
using FluentAssertions;

public class ConfigurationModelsTests
{
    [Fact]
    public void AgentDeploymentOptions_DefaultValues_AreCorrect()
    {
        var options = new AgentDeploymentOptions();

        options.Name.Should().Be("DefaultAgent");
        options.MaxIterations.Should().Be(10);
        options.MaxTokensPerRequest.Should().Be(4000);
        options.RequestTimeout.Should().Be(TimeSpan.FromMinutes(5));
        options.EnableSafetyGuardrails.Should().BeTrue();
    }

    [Fact]
    public void AgentDeploymentOptions_Validate_SucceedsWithValidConfig()
    {
        var options = new AgentDeploymentOptions
        {
            Name = "TestAgent",
            MaxIterations = 5,
            MaxTokensPerRequest = 2000,
            RequestTimeout = TimeSpan.FromMinutes(3),
        };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("", "Agent Name is required")]
    [InlineData(null, "Agent Name is required")]
    public void AgentDeploymentOptions_Validate_ThrowsOnInvalidName(string? name, string expectedMessage)
    {
        var options = new AgentDeploymentOptions { Name = name! };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage(expectedMessage);
    }

    [Fact]
    public void AgentDeploymentOptions_Validate_ThrowsOnInvalidMaxIterations()
    {
        var options = new AgentDeploymentOptions { MaxIterations = 0 };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("MaxIterations must be at least 1");
    }

    [Fact]
    public void LLMDeploymentOptions_DefaultValues_AreCorrect()
    {
        var options = new LLMDeploymentOptions();

        options.Provider.Should().Be("OpenAI");
        options.Model.Should().Be("gpt-4");
        options.Temperature.Should().Be(0.7);
        options.MaxRetries.Should().Be(3);
        options.RetryDelay.Should().Be(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void LLMDeploymentOptions_Validate_SucceedsWithValidConfig()
    {
        var options = new LLMDeploymentOptions
        {
            Provider = "Azure",
            Model = "gpt-4o",
            Temperature = 0.5,
            MaxRetries = 5,
        };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(2.1)]
    public void LLMDeploymentOptions_Validate_ThrowsOnInvalidTemperature(double temperature)
    {
        var options = new LLMDeploymentOptions { Temperature = temperature };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("Temperature must be between 0 and 2");
    }

    [Fact]
    public void CacheOptions_DefaultValues_AreCorrect()
    {
        var options = new CacheOptions();

        options.Enabled.Should().BeTrue();
        options.Provider.Should().Be("Memory");
        options.DefaultExpiration.Should().Be(TimeSpan.FromHours(1));
        options.MaxCacheSize.Should().Be(10000);
    }

    [Fact]
    public void CacheOptions_Validate_SucceedsWithValidConfig()
    {
        var options = new CacheOptions
        {
            Enabled = true,
            Provider = "Redis",
            ConnectionString = "localhost:6379",
            DefaultExpiration = TimeSpan.FromMinutes(30),
            MaxCacheSize = 5000,
        };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void CacheOptions_Validate_ThrowsOnInvalidExpiration()
    {
        var options = new CacheOptions { DefaultExpiration = TimeSpan.Zero };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("DefaultExpiration must be positive");
    }
}
