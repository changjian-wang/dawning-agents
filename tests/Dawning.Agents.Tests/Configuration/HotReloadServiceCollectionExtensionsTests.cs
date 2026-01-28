using Dawning.Agents.Abstractions.Configuration;
using Dawning.Agents.Core.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dawning.Agents.Tests.Configuration;

/// <summary>
/// HotReloadServiceCollectionExtensions 测试
/// </summary>
public class HotReloadServiceCollectionExtensionsTests
{
    [Fact]
    public void AddHotReloadOptions_RegistersOptionsAndNotifier()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Test:Value"] = "test-value",
                    ["Test:Number"] = "42",
                }
            )
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddHotReloadOptions<TestOptions>(configuration, "Test");

        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetRequiredService<IOptions<TestOptions>>();
        options.Value.Value.Should().Be("test-value");
        options.Value.Number.Should().Be(42);

        var notifier = provider.GetRequiredService<IConfigurationChangeNotifier<TestOptions>>();
        notifier.Should().NotBeNull();
        notifier.CurrentValue.Value.Should().Be("test-value");
    }

    [Fact]
    public void AddHotReloadOptions_RegistersOptionsMonitor()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Test:Value"] = "monitor-test" }
            )
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddHotReloadOptions<TestOptions>(configuration, "Test");

        var provider = services.BuildServiceProvider();

        // Assert
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<TestOptions>>();
        optionsMonitor.Should().NotBeNull();
        optionsMonitor.CurrentValue.Value.Should().Be("monitor-test");
    }

    [Fact]
    public void AddHotReloadOptions_WithValidation_ValidatesOptions()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Test:Value"] = "valid", ["Test:Number"] = "10" }
            )
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddHotReloadOptions<TestOptions>(
            configuration,
            "Test",
            options => !string.IsNullOrEmpty(options.Value)
        );

        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetRequiredService<IOptions<TestOptions>>();
        options.Value.Value.Should().Be("valid");
    }

    [Fact]
    public void AddHotReloadOptions_WithValidation_ThrowsOnInvalidOptions()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Test:Value"] = "", // Invalid - empty value
                    ["Test:Number"] = "10",
                }
            )
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddHotReloadOptions<TestOptions>(
            configuration,
            "Test",
            options => !string.IsNullOrEmpty(options.Value)
        );

        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetRequiredService<IOptions<TestOptions>>();
        Action act = () => _ = options.Value;
        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddHotReloadOptions_WithValidationAndMessage_IncludesCustomMessage()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Test:Value"] = "", ["Test:Number"] = "10" }
            )
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddHotReloadOptions<TestOptions>(
            configuration,
            "Test",
            options => !string.IsNullOrEmpty(options.Value),
            "Value cannot be empty"
        );

        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetRequiredService<IOptions<TestOptions>>();
        Action act = () => _ = options.Value;
        act.Should().Throw<OptionsValidationException>().WithMessage("*Value cannot be empty*");
    }

    [Fact]
    public void AddHotReloadOptions_SingletonNotifier_ReturnsSameInstance()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Test:Value"] = "singleton" })
            .Build();

        var services = new ServiceCollection();
        services.AddHotReloadOptions<TestOptions>(configuration, "Test");

        var provider = services.BuildServiceProvider();

        // Act
        var notifier1 = provider.GetRequiredService<IConfigurationChangeNotifier<TestOptions>>();
        var notifier2 = provider.GetRequiredService<IConfigurationChangeNotifier<TestOptions>>();

        // Assert
        notifier1.Should().BeSameAs(notifier2);
    }

    [Fact]
    public void AddHotReloadOptions_WithMissingSectionName_UsesDefaultValues()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddHotReloadOptions<TestOptions>(configuration, "NonExistent");

        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetRequiredService<IOptions<TestOptions>>();
        options.Value.Value.Should().Be("default");
        options.Value.Number.Should().Be(0);
    }

    [Fact]
    public void AddHotReloadOptions_ReturnsSameServiceCollection()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();

        // Act
        var result = services.AddHotReloadOptions<TestOptions>(configuration, "Test");

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddHotReloadOptions_MultipleOptionsTypes_RegistersEachSeparately()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["OptionsA:Value"] = "valueA",
                    ["OptionsB:Name"] = "nameB",
                }
            )
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddHotReloadOptions<TestOptions>(configuration, "OptionsA");
        services.AddHotReloadOptions<OtherOptions>(configuration, "OptionsB");

        var provider = services.BuildServiceProvider();

        // Assert
        var optionsA = provider.GetRequiredService<IOptions<TestOptions>>();
        var optionsB = provider.GetRequiredService<IOptions<OtherOptions>>();

        optionsA.Value.Value.Should().Be("valueA");
        optionsB.Value.Name.Should().Be("nameB");
    }

    /// <summary>
    /// 测试用配置类
    /// </summary>
    public class TestOptions
    {
        public string Value { get; set; } = "default";
        public int Number { get; set; }
    }

    /// <summary>
    /// 另一个测试用配置类
    /// </summary>
    public class OtherOptions
    {
        public string Name { get; set; } = string.Empty;
    }
}
