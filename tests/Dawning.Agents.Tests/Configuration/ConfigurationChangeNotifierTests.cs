using Dawning.Agents.Abstractions.Configuration;
using Dawning.Agents.Core.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Dawning.Agents.Tests.Configuration;

/// <summary>
/// ConfigurationChangeNotifier 测试
/// </summary>
public class ConfigurationChangeNotifierTests
{
    private readonly Mock<IOptionsMonitor<TestOptions>> _optionsMonitorMock;

    public ConfigurationChangeNotifierTests()
    {
        _optionsMonitorMock = new Mock<IOptionsMonitor<TestOptions>>();
    }

    [Fact]
    public void Constructor_WithValidOptionsMonitor_CreatesInstance()
    {
        // Arrange
        _optionsMonitorMock.Setup(x => x.CurrentValue).Returns(new TestOptions { Value = "test" });

        // Act
        var notifier = new ConfigurationChangeNotifier<TestOptions>(
            _optionsMonitorMock.Object,
            NullLogger<ConfigurationChangeNotifier<TestOptions>>.Instance
        );

        // Assert
        notifier.Should().NotBeNull();
        notifier.CurrentValue.Value.Should().Be("test");
    }

    [Fact]
    public void Constructor_WithNullOptionsMonitor_ThrowsArgumentNullException()
    {
        // Act
        Action act = () =>
            new ConfigurationChangeNotifier<TestOptions>(
                null!,
                NullLogger<ConfigurationChangeNotifier<TestOptions>>.Instance
            );

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("optionsMonitor");
    }

    [Fact]
    public void Constructor_WithNullLogger_UsesNullLogger()
    {
        // Arrange
        _optionsMonitorMock.Setup(x => x.CurrentValue).Returns(new TestOptions { Value = "test" });

        // Act
        var notifier = new ConfigurationChangeNotifier<TestOptions>(_optionsMonitorMock.Object);

        // Assert
        notifier.Should().NotBeNull();
    }

    [Fact]
    public void CurrentValue_ReturnsCurrentOptionsValue()
    {
        // Arrange
        var options = new TestOptions { Value = "current" };
        _optionsMonitorMock.Setup(x => x.CurrentValue).Returns(options);

        var notifier = new ConfigurationChangeNotifier<TestOptions>(_optionsMonitorMock.Object);

        // Act
        var result = notifier.CurrentValue;

        // Assert
        result.Should().Be(options);
    }

    [Fact]
    public void ConfigurationChanged_WhenOptionsChange_RaisesEvent()
    {
        // Arrange
        var initialOptions = new TestOptions { Value = "initial" };
        var newOptions = new TestOptions { Value = "new" };

        Action<TestOptions, string?>? capturedCallback = null;

        _optionsMonitorMock.Setup(x => x.CurrentValue).Returns(initialOptions);
        _optionsMonitorMock
            .Setup(x => x.OnChange(It.IsAny<Action<TestOptions, string?>>()))
            .Callback<Action<TestOptions, string?>>(callback => capturedCallback = callback)
            .Returns(Mock.Of<IDisposable>());

        var notifier = new ConfigurationChangeNotifier<TestOptions>(_optionsMonitorMock.Object);

        ConfigurationChangedEventArgs<TestOptions>? receivedEventArgs = null;
        notifier.ConfigurationChanged += (sender, args) => receivedEventArgs = args;

        // Act
        capturedCallback?.Invoke(newOptions, null);

        // Assert
        receivedEventArgs.Should().NotBeNull();
        receivedEventArgs!.NewValue.Should().Be(newOptions);
    }

    [Fact]
    public void ConfigurationChanged_IncludesOldAndNewValues()
    {
        // Arrange
        var initialOptions = new TestOptions { Value = "old" };
        var newOptions = new TestOptions { Value = "new" };

        Action<TestOptions, string?>? capturedCallback = null;

        _optionsMonitorMock.Setup(x => x.CurrentValue).Returns(initialOptions);
        _optionsMonitorMock
            .Setup(x => x.OnChange(It.IsAny<Action<TestOptions, string?>>()))
            .Callback<Action<TestOptions, string?>>(callback => capturedCallback = callback)
            .Returns(Mock.Of<IDisposable>());

        var notifier = new ConfigurationChangeNotifier<TestOptions>(_optionsMonitorMock.Object);

        ConfigurationChangedEventArgs<TestOptions>? receivedEventArgs = null;
        notifier.ConfigurationChanged += (sender, args) => receivedEventArgs = args;

        // Act
        capturedCallback?.Invoke(newOptions, null);

        // Assert
        receivedEventArgs.Should().NotBeNull();
        receivedEventArgs!.OldValue.Should().Be(initialOptions);
        receivedEventArgs.NewValue.Should().Be(newOptions);
    }

    [Fact]
    public void ConfigurationChanged_IncludesTimestamp()
    {
        // Arrange
        var initialOptions = new TestOptions { Value = "initial" };
        var newOptions = new TestOptions { Value = "new" };

        Action<TestOptions, string?>? capturedCallback = null;
        var beforeChange = DateTime.UtcNow;

        _optionsMonitorMock.Setup(x => x.CurrentValue).Returns(initialOptions);
        _optionsMonitorMock
            .Setup(x => x.OnChange(It.IsAny<Action<TestOptions, string?>>()))
            .Callback<Action<TestOptions, string?>>(callback => capturedCallback = callback)
            .Returns(Mock.Of<IDisposable>());

        var notifier = new ConfigurationChangeNotifier<TestOptions>(_optionsMonitorMock.Object);

        ConfigurationChangedEventArgs<TestOptions>? receivedEventArgs = null;
        notifier.ConfigurationChanged += (sender, args) => receivedEventArgs = args;

        // Act
        capturedCallback?.Invoke(newOptions, null);
        var afterChange = DateTime.UtcNow;

        // Assert
        receivedEventArgs.Should().NotBeNull();
        receivedEventArgs!.Timestamp.Should().BeOnOrAfter(beforeChange).And.BeOnOrBefore(afterChange);
    }

    [Fact]
    public void Dispose_DisposesChangeTokenRegistration()
    {
        // Arrange
        var disposableMock = new Mock<IDisposable>();

        _optionsMonitorMock.Setup(x => x.CurrentValue).Returns(new TestOptions());
        _optionsMonitorMock
            .Setup(x => x.OnChange(It.IsAny<Action<TestOptions, string?>>()))
            .Returns(disposableMock.Object);

        var notifier = new ConfigurationChangeNotifier<TestOptions>(_optionsMonitorMock.Object);

        // Act
        notifier.Dispose();

        // Assert
        disposableMock.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public void Dispose_MultipleDisposeCalls_OnlyDisposesOnce()
    {
        // Arrange
        var disposableMock = new Mock<IDisposable>();

        _optionsMonitorMock.Setup(x => x.CurrentValue).Returns(new TestOptions());
        _optionsMonitorMock
            .Setup(x => x.OnChange(It.IsAny<Action<TestOptions, string?>>()))
            .Returns(disposableMock.Object);

        var notifier = new ConfigurationChangeNotifier<TestOptions>(_optionsMonitorMock.Object);

        // Act
        notifier.Dispose();
        notifier.Dispose();

        // Assert
        disposableMock.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public void ConfigurationChanged_AfterDispose_DoesNotRaiseEvent()
    {
        // Arrange
        var initialOptions = new TestOptions { Value = "initial" };
        var newOptions = new TestOptions { Value = "new" };

        Action<TestOptions, string?>? capturedCallback = null;

        _optionsMonitorMock.Setup(x => x.CurrentValue).Returns(initialOptions);
        _optionsMonitorMock
            .Setup(x => x.OnChange(It.IsAny<Action<TestOptions, string?>>()))
            .Callback<Action<TestOptions, string?>>(callback => capturedCallback = callback)
            .Returns(Mock.Of<IDisposable>());

        var notifier = new ConfigurationChangeNotifier<TestOptions>(_optionsMonitorMock.Object);

        var eventRaised = false;
        notifier.ConfigurationChanged += (sender, args) => eventRaised = true;

        // Act
        notifier.Dispose();
        capturedCallback?.Invoke(newOptions, null);

        // Assert
        eventRaised.Should().BeFalse();
    }

    [Fact]
    public void MultipleSubscribers_AllReceiveNotification()
    {
        // Arrange
        var initialOptions = new TestOptions { Value = "initial" };
        var newOptions = new TestOptions { Value = "new" };

        Action<TestOptions, string?>? capturedCallback = null;

        _optionsMonitorMock.Setup(x => x.CurrentValue).Returns(initialOptions);
        _optionsMonitorMock
            .Setup(x => x.OnChange(It.IsAny<Action<TestOptions, string?>>()))
            .Callback<Action<TestOptions, string?>>(callback => capturedCallback = callback)
            .Returns(Mock.Of<IDisposable>());

        var notifier = new ConfigurationChangeNotifier<TestOptions>(_optionsMonitorMock.Object);

        var count = 0;
        notifier.ConfigurationChanged += (sender, args) => count++;
        notifier.ConfigurationChanged += (sender, args) => count++;
        notifier.ConfigurationChanged += (sender, args) => count++;

        // Act
        capturedCallback?.Invoke(newOptions, null);

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public void ConfigurationChanged_NameParameter_IsPassedCorrectly()
    {
        // Arrange
        var initialOptions = new TestOptions { Value = "initial" };
        var newOptions = new TestOptions { Value = "new" };
        const string configName = "TestConfig";

        Action<TestOptions, string?>? capturedCallback = null;

        _optionsMonitorMock.Setup(x => x.CurrentValue).Returns(initialOptions);
        _optionsMonitorMock
            .Setup(x => x.OnChange(It.IsAny<Action<TestOptions, string?>>()))
            .Callback<Action<TestOptions, string?>>(callback => capturedCallback = callback)
            .Returns(Mock.Of<IDisposable>());

        var notifier = new ConfigurationChangeNotifier<TestOptions>(_optionsMonitorMock.Object);

        ConfigurationChangedEventArgs<TestOptions>? receivedEventArgs = null;
        notifier.ConfigurationChanged += (sender, args) => receivedEventArgs = args;

        // Act
        capturedCallback?.Invoke(newOptions, configName);

        // Assert
        receivedEventArgs.Should().NotBeNull();
        receivedEventArgs!.Name.Should().Be(configName);
    }

    /// <summary>
    /// 测试用配置类
    /// </summary>
    public class TestOptions
    {
        public string Value { get; set; } = string.Empty;
    }
}
