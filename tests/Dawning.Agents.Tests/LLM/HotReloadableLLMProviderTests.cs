using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Core.LLM;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Dawning.Agents.Tests.LLM;

/// <summary>
/// HotReloadableLLMProvider 测试
/// </summary>
public class HotReloadableLLMProviderTests
{
    private readonly Mock<IOptionsMonitor<LLMOptions>> _optionsMonitorMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;

    public HotReloadableLLMProviderTests()
    {
        _optionsMonitorMock = new Mock<IOptionsMonitor<LLMOptions>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
    }

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange
        var options = CreateValidOptions();
        SetupOptionsMonitor(options);
        SetupHttpClientFactory();

        // Act
        var provider = CreateProvider();

        // Assert
        provider.Should().NotBeNull();
        provider.Name.Should().Be("Ollama");
    }

    [Fact]
    public void Constructor_WithNullOptionsMonitor_ThrowsArgumentNullException()
    {
        // Act
        Action act = () =>
            new HotReloadableLLMProvider(null!, _httpClientFactoryMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("optionsMonitor");
    }

    [Fact]
    public void Constructor_WithNullHttpClientFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var options = CreateValidOptions();
        SetupOptionsMonitor(options);

        // Act
        Action act = () =>
            new HotReloadableLLMProvider(_optionsMonitorMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("httpClientFactory");
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_UsesNullLoggerFactory()
    {
        // Arrange
        var options = CreateValidOptions();
        SetupOptionsMonitor(options);
        SetupHttpClientFactory();

        // Act
        var provider = new HotReloadableLLMProvider(
            _optionsMonitorMock.Object,
            _httpClientFactoryMock.Object,
            null
        );

        // Assert
        provider.Should().NotBeNull();
    }

    [Fact]
    public void ConfigurationChanged_WhenOptionsChange_RaisesEvent()
    {
        // Arrange
        var initialOptions = CreateValidOptions();
        var newOptions = CreateValidOptions("qwen2.5:7b");

        Action<LLMOptions, string?>? capturedCallback = null;

        _optionsMonitorMock.Setup(x => x.CurrentValue).Returns(initialOptions);
        _optionsMonitorMock
            .Setup(x => x.OnChange(It.IsAny<Action<LLMOptions, string?>>()))
            .Callback<Action<LLMOptions, string?>>(callback => capturedCallback = callback)
            .Returns(Mock.Of<IDisposable>());

        SetupHttpClientFactory();

        var provider = CreateProvider();

        LLMOptions? receivedOptions = null;
        provider.ConfigurationChanged += (sender, options) => receivedOptions = options;

        // Act
        capturedCallback?.Invoke(newOptions, null);

        // Assert
        receivedOptions.Should().NotBeNull();
        receivedOptions!.Model.Should().Be("qwen2.5:7b");
    }

    [Fact]
    public void ConfigurationChanged_WithInvalidOptions_DoesNotRaiseEvent()
    {
        // Arrange
        var initialOptions = CreateValidOptions();
        var invalidOptions = new LLMOptions
        {
            ProviderType = LLMProviderType.Ollama,
            Model = "", // Invalid - empty model
        };

        Action<LLMOptions, string?>? capturedCallback = null;

        _optionsMonitorMock.Setup(x => x.CurrentValue).Returns(initialOptions);
        _optionsMonitorMock
            .Setup(x => x.OnChange(It.IsAny<Action<LLMOptions, string?>>()))
            .Callback<Action<LLMOptions, string?>>(callback => capturedCallback = callback)
            .Returns(Mock.Of<IDisposable>());

        SetupHttpClientFactory();

        var provider = CreateProvider();

        var eventRaised = false;
        provider.ConfigurationChanged += (sender, options) => eventRaised = true;

        // Act
        capturedCallback?.Invoke(invalidOptions, null);

        // Assert
        eventRaised.Should().BeFalse();
    }

    [Fact]
    public void ConfigurationChanged_WithUnsupportedProviderType_DoesNotRaiseEvent()
    {
        // Arrange
        var initialOptions = CreateValidOptions();
        var unsupportedOptions = new LLMOptions
        {
            ProviderType = LLMProviderType.OpenAI, // Not supported
            Model = "gpt-4",
        };

        Action<LLMOptions, string?>? capturedCallback = null;

        _optionsMonitorMock.Setup(x => x.CurrentValue).Returns(initialOptions);
        _optionsMonitorMock
            .Setup(x => x.OnChange(It.IsAny<Action<LLMOptions, string?>>()))
            .Callback<Action<LLMOptions, string?>>(callback => capturedCallback = callback)
            .Returns(Mock.Of<IDisposable>());

        SetupHttpClientFactory();

        var provider = CreateProvider();

        var eventRaised = false;
        provider.ConfigurationChanged += (sender, options) => eventRaised = true;

        // Act
        capturedCallback?.Invoke(unsupportedOptions, null);

        // Assert
        eventRaised.Should().BeFalse();
    }

    [Fact]
    public void Dispose_DisposesChangeTokenRegistration()
    {
        // Arrange
        var options = CreateValidOptions();
        var disposableMock = new Mock<IDisposable>();

        _optionsMonitorMock.Setup(x => x.CurrentValue).Returns(options);
        _optionsMonitorMock
            .Setup(x => x.OnChange(It.IsAny<Action<LLMOptions, string?>>()))
            .Returns(disposableMock.Object);

        SetupHttpClientFactory();

        var provider = CreateProvider();

        // Act
        provider.Dispose();

        // Assert
        disposableMock.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public void Dispose_MultipleDisposeCalls_OnlyDisposesOnce()
    {
        // Arrange
        var options = CreateValidOptions();
        var disposableMock = new Mock<IDisposable>();

        _optionsMonitorMock.Setup(x => x.CurrentValue).Returns(options);
        _optionsMonitorMock
            .Setup(x => x.OnChange(It.IsAny<Action<LLMOptions, string?>>()))
            .Returns(disposableMock.Object);

        SetupHttpClientFactory();

        var provider = CreateProvider();

        // Act
        provider.Dispose();
        provider.Dispose();

        // Assert
        disposableMock.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public void ChatAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var options = CreateValidOptions();
        SetupOptionsMonitor(options);
        SetupHttpClientFactory();

        var provider = CreateProvider();
        provider.Dispose();

        // Act
        Func<Task> act = async () =>
            await provider.ChatAsync(
                new[] { new ChatMessage("user", "Hello") }
            );

        // Assert
        act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void ConfigurationChanged_AfterDispose_DoesNotRaiseEvent()
    {
        // Arrange
        var initialOptions = CreateValidOptions();
        var newOptions = CreateValidOptions("qwen2.5:7b");

        Action<LLMOptions, string?>? capturedCallback = null;

        _optionsMonitorMock.Setup(x => x.CurrentValue).Returns(initialOptions);
        _optionsMonitorMock
            .Setup(x => x.OnChange(It.IsAny<Action<LLMOptions, string?>>()))
            .Callback<Action<LLMOptions, string?>>(callback => capturedCallback = callback)
            .Returns(Mock.Of<IDisposable>());

        SetupHttpClientFactory();

        var provider = CreateProvider();

        var eventRaised = false;
        provider.ConfigurationChanged += (sender, options) => eventRaised = true;

        // Act
        provider.Dispose();
        capturedCallback?.Invoke(newOptions, null);

        // Assert
        eventRaised.Should().BeFalse();
    }

    [Fact]
    public void Name_ReturnsInnerProviderName()
    {
        // Arrange
        var options = CreateValidOptions();
        SetupOptionsMonitor(options);
        SetupHttpClientFactory();

        var provider = CreateProvider();

        // Act
        var name = provider.Name;

        // Assert
        name.Should().Be("Ollama");
    }

    private static LLMOptions CreateValidOptions(string model = "qwen2.5:0.5b")
    {
        return new LLMOptions
        {
            ProviderType = LLMProviderType.Ollama,
            Model = model,
            Endpoint = "http://localhost:11434",
        };
    }

    private void SetupOptionsMonitor(LLMOptions options)
    {
        _optionsMonitorMock.Setup(x => x.CurrentValue).Returns(options);
        _optionsMonitorMock
            .Setup(x => x.OnChange(It.IsAny<Action<LLMOptions, string?>>()))
            .Returns(Mock.Of<IDisposable>());
    }

    private void SetupHttpClientFactory()
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:11434") };
        _httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);
    }

    private HotReloadableLLMProvider CreateProvider()
    {
        return new HotReloadableLLMProvider(
            _optionsMonitorMock.Object,
            _httpClientFactoryMock.Object,
            NullLoggerFactory.Instance
        );
    }
}
