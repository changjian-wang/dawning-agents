using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Chroma;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Dawning.Agents.Tests.Chroma;

/// <summary>
/// ChromaOptions 单元测试
/// </summary>
public class ChromaOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new ChromaOptions();

        options.Host.Should().Be("localhost");
        options.Port.Should().Be(8000);
        options.CollectionName.Should().Be("documents");
        options.Tenant.Should().Be("default_tenant");
        options.Database.Should().Be("default_database");
        options.UseHttps.Should().BeFalse();
        options.ApiKey.Should().BeNull();
        options.TimeoutSeconds.Should().Be(30);
        options.VectorDimension.Should().Be(1536);
        options.DistanceMetric.Should().Be(ChromaDistanceMetric.Cosine);
    }

    [Fact]
    public void BaseUrl_ReturnsCorrectUrl_Http()
    {
        var options = new ChromaOptions
        {
            Host = "localhost",
            Port = 8000,
            UseHttps = false,
        };

        options.BaseUrl.Should().Be("http://localhost:8000");
    }

    [Fact]
    public void BaseUrl_ReturnsCorrectUrl_Https()
    {
        var options = new ChromaOptions
        {
            Host = "chroma.example.com",
            Port = 443,
            UseHttps = true,
        };

        options.BaseUrl.Should().Be("https://chroma.example.com:443");
    }

    [Fact]
    public void Validate_ThrowsOnEmptyHost()
    {
        var options = new ChromaOptions { Host = "" };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Host*");
    }

    [Fact]
    public void Validate_ThrowsOnInvalidPort()
    {
        var options = new ChromaOptions { Port = 0 };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Port*");
    }

    [Fact]
    public void Validate_ThrowsOnEmptyCollectionName()
    {
        var options = new ChromaOptions { CollectionName = "" };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*CollectionName*");
    }

    [Fact]
    public void Validate_ThrowsOnInvalidVectorDimension()
    {
        var options = new ChromaOptions { VectorDimension = 0 };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*VectorDimension*");
    }

    [Fact]
    public void Validate_PassesWithValidOptions()
    {
        var options = new ChromaOptions
        {
            Host = "localhost",
            Port = 8000,
            CollectionName = "test",
            VectorDimension = 1536,
        };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }
}

/// <summary>
/// ChromaVectorStore 单元测试
/// </summary>
public class ChromaVectorStoreTests
{
    private static ChromaVectorStore CreateStore(
        HttpMessageHandler handler,
        Action<ChromaOptions>? configureOptions = null
    )
    {
        var options = new ChromaOptions();
        configureOptions?.Invoke(options);

        var httpClient = new HttpClient(handler);
        return new ChromaVectorStore(
            httpClient,
            Options.Create(options)
        );
    }

    private static Mock<HttpMessageHandler> CreateMockHandler()
    {
        return new Mock<HttpMessageHandler>(MockBehavior.Strict);
    }

    [Fact]
    public void Constructor_SetsNameCorrectly()
    {
        var handler = CreateMockHandler();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

        var store = CreateStore(handler.Object);

        store.Name.Should().Be("Chroma");
    }

    [Fact]
    public void Constructor_InitializesCountToZero()
    {
        var handler = CreateMockHandler();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

        var store = CreateStore(handler.Object);

        store.Count.Should().Be(0);
    }

    [Fact]
    public void Constructor_ThrowsOnNullHttpClient()
    {
        var options = Options.Create(new ChromaOptions());

        var act = () => new ChromaVectorStore(null!, options);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ThrowsOnNullOptions()
    {
        var httpClient = new HttpClient();

        var act = () => new ChromaVectorStore(httpClient, null!);

        act.Should().Throw<ArgumentNullException>();
    }
}

/// <summary>
/// Chroma DI 扩展测试
/// </summary>
public class ChromaServiceCollectionExtensionsTests
{
    [Fact]
    public void AddChromaVectorStore_WithConfiguration_RegistersServices()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Chroma:Host"] = "localhost",
                ["Chroma:Port"] = "8000",
                ["Chroma:CollectionName"] = "test",
            })
            .Build();

        services.AddChromaVectorStore(configuration);

        var descriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(IVectorStore)
        );
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddChromaVectorStore_WithOptions_RegistersServices()
    {
        var services = new ServiceCollection();

        services.AddChromaVectorStore(options =>
        {
            options.Host = "localhost";
            options.Port = 8000;
            options.CollectionName = "test";
        });

        var descriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(IVectorStore)
        );
        descriptor.Should().NotBeNull();
    }
}
