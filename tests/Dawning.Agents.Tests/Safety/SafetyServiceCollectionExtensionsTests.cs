using Dawning.Agents.Abstractions.Safety;
using Dawning.Agents.Core.Safety;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Tests.Safety;

public class SafetyServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSafetyGuardrails_ShouldRegisterGuardrailPipeline()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSafetyGuardrails();
        var provider = services.BuildServiceProvider();

        // Assert
        var pipeline = provider.GetRequiredService<IGuardrailPipeline>();
        pipeline.Should().NotBeNull();
        pipeline.Should().BeOfType<GuardrailPipeline>();
    }

    [Fact]
    public void AddSafetyGuardrails_ShouldConfigureDefaultGuardrails()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSafetyGuardrails(options =>
        {
            options.EnableSensitiveDataDetection = true;
            options.EnableContentFilter = false;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var pipeline = provider.GetRequiredService<IGuardrailPipeline>();
        pipeline.InputGuardrails.Should().HaveCountGreaterThanOrEqualTo(1);
        pipeline.OutputGuardrails.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void AddSafetyGuardrails_WithConfigure_ShouldApplyConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSafetyGuardrails(options =>
        {
            options.MaxInputLength = 500;
            options.MaxOutputLength = 1000;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetRequiredService<IOptions<SafetyOptions>>().Value;
        options.MaxInputLength.Should().Be(500);
        options.MaxOutputLength.Should().Be(1000);
    }

    [Fact]
    public void AddSafetyGuardrails_ShouldRegisterIndividualGuardrails()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSafetyGuardrails();
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetRequiredService<MaxLengthGuardrail>().Should().NotBeNull();
        provider.GetRequiredService<SensitiveDataGuardrail>().Should().NotBeNull();
        provider.GetRequiredService<ContentFilterGuardrail>().Should().NotBeNull();
        provider.GetRequiredService<UrlDomainGuardrail>().Should().NotBeNull();
    }

    [Fact]
    public void AddSafetyGuardrails_WithBlockedKeywords_ShouldIncludeContentFilter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSafetyGuardrails(options =>
        {
            options.EnableContentFilter = true;
            options.BlockedKeywords = ["bad", "evil"];
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var pipeline = provider.GetRequiredService<IGuardrailPipeline>();
        pipeline.InputGuardrails.Should().Contain(g => g.Name == "ContentFilterGuardrail");
    }

    [Fact]
    public void AddCustomGuardrailPipeline_ShouldAllowCustomConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddCustomGuardrailPipeline(
            (pipeline, sp) =>
            {
                pipeline.AddInputGuardrail(new MaxLengthGuardrail(500));
            }
        );
        var provider = services.BuildServiceProvider();

        // Assert
        var pipeline = provider.GetRequiredService<IGuardrailPipeline>();
        pipeline.InputGuardrails.Should().HaveCount(1);
        pipeline.InputGuardrails[0].Name.Should().Be("MaxInputLength");
    }

    [Fact]
    public void AddSafetyGuardrails_ShouldBeSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSafetyGuardrails();
        var provider = services.BuildServiceProvider();

        // Act
        var pipeline1 = provider.GetRequiredService<IGuardrailPipeline>();
        var pipeline2 = provider.GetRequiredService<IGuardrailPipeline>();

        // Assert
        pipeline1.Should().BeSameAs(pipeline2);
    }
}
