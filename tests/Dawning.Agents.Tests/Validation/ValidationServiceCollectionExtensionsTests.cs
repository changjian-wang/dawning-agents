using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Core.Validation;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Tests.Validation;

public class ValidationServiceCollectionExtensionsTests
{
    [Fact]
    public void AddValidation_RegistersAllBuiltInValidators()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddValidation();
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<IValidator<LLMOptions>>().Should().NotBeNull();
        provider.GetService<IValidator<AgentOptions>>().Should().NotBeNull();
    }

    [Fact]
    public void AddOptionsValidation_WithFluentValidator_ValidatesOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddValidation();
        services.AddOptionsValidation<AgentOptions>();
        services.Configure<AgentOptions>(o =>
        {
            o.Name = ""; // Invalid
            o.MaxSteps = -1; // Invalid
        });

        var provider = services.BuildServiceProvider();

        // Act
        var validateOptions = provider.GetService<IValidateOptions<AgentOptions>>();
        var options = new AgentOptions { Name = "", MaxSteps = -1 };
        var result = validateOptions?.Validate(null, options);

        // Assert
        result.Should().NotBeNull();
        result!.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Name");
        result.FailureMessage.Should().Contain("MaxSteps");
    }

    [Fact]
    public void AddOptionsValidation_WithValidOptions_PassesValidation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddValidation();
        services.AddOptionsValidation<AgentOptions>();

        var provider = services.BuildServiceProvider();

        // Act
        var validateOptions = provider.GetService<IValidateOptions<AgentOptions>>();
        var options = new AgentOptions
        {
            Name = "TestAgent",
            Instructions = "Test instructions",
            MaxSteps = 10,
        };
        var result = validateOptions?.Validate(null, options);

        // Assert
        result.Should().NotBeNull();
        result!.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validators_AreSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddValidation();
        var provider = services.BuildServiceProvider();

        // Act
        var validator1 = provider.GetService<IValidator<LLMOptions>>();
        var validator2 = provider.GetService<IValidator<LLMOptions>>();

        // Assert
        validator1.Should().BeSameAs(validator2);
    }
}
