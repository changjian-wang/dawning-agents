using System.Net;
using System.Net.Http.Json;
using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Weaviate;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace Dawning.Agents.Tests.Weaviate;

/// <summary>
/// WeaviateOptions unit tests
/// </summary>
public class WeaviateOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new WeaviateOptions();

        options.Host.Should().Be("localhost");
        options.Port.Should().Be(8080);
        options.GrpcPort.Should().Be(50051);
        options.ClassName.Should().Be("Document");
        options.Scheme.Should().Be("http");
        options.ApiKey.Should().BeNull();
        options.TimeoutSeconds.Should().Be(30);
        options.VectorDimension.Should().Be(1536);
        options.DistanceMetric.Should().Be(WeaviateDistanceMetric.Cosine);
        options.VectorIndexType.Should().Be(WeaviateVectorIndexType.Hnsw);
    }

    [Fact]
    public void BaseUrl_ReturnsCorrectUrl_Http()
    {
        var options = new WeaviateOptions
        {
            Host = "localhost",
            Port = 8080,
            Scheme = "http",
        };

        options.BaseUrl.Should().Be("http://localhost:8080");
    }

    [Fact]
    public void BaseUrl_ReturnsCorrectUrl_Https()
    {
        var options = new WeaviateOptions
        {
            Host = "weaviate.example.com",
            Port = 443,
            Scheme = "https",
        };

        options.BaseUrl.Should().Be("https://weaviate.example.com:443");
    }

    [Fact]
    public void Validate_ThrowsOnEmptyHost()
    {
        var options = new WeaviateOptions { Host = "" };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Host*");
    }

    [Fact]
    public void Validate_ThrowsOnInvalidPort()
    {
        var options = new WeaviateOptions { Port = 0 };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Port*");
    }

    [Fact]
    public void Validate_ThrowsOnEmptyClassName()
    {
        var options = new WeaviateOptions { ClassName = "" };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*ClassName*");
    }

    [Fact]
    public void Validate_ThrowsOnInvalidVectorDimension()
    {
        var options = new WeaviateOptions { VectorDimension = 0 };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*VectorDimension*");
    }

    [Fact]
    public void Validate_PassesWithValidOptions()
    {
        var options = new WeaviateOptions
        {
            Host = "localhost",
            Port = 8080,
            ClassName = "test",
            VectorDimension = 1536,
        };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }
}

/// <summary>
/// WeaviateVectorStore unit tests
/// </summary>
public class WeaviateVectorStoreTests
{
    private static WeaviateVectorStore CreateStore(
        HttpMessageHandler handler,
        Action<WeaviateOptions>? configureOptions = null
    )
    {
        var options = new WeaviateOptions();
        configureOptions?.Invoke(options);

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080") };
        return new WeaviateVectorStore(httpClient, Options.Create(options));
    }

    private static Mock<HttpMessageHandler> CreateMockHandler()
    {
        return new Mock<HttpMessageHandler>(MockBehavior.Strict);
    }

    [Fact]
    public void Constructor_SetsNameCorrectly()
    {
        var handler = CreateMockHandler();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

        var store = CreateStore(handler.Object);

        store.Name.Should().Be("Weaviate");
    }

    [Fact]
    public void Constructor_InitializesCountToZero()
    {
        var handler = CreateMockHandler();
        handler
            .Protected()
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
        var options = Options.Create(new WeaviateOptions());

        var act = () => new WeaviateVectorStore(null!, options);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ThrowsOnNullOptions()
    {
        var httpClient = new HttpClient();

        var act = () => new WeaviateVectorStore(httpClient, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SearchAsync_WhenClassPayloadIsNotArray_ShouldReturnEmpty()
    {
        var handler = CreateMockHandler();

        handler
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK))
            .ReturnsAsync(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(
                        new
                        {
                            data = new
                            {
                                get = new Dictionary<string, object>
                                {
                                    ["Document"] = new { unexpected = true },
                                },
                            },
                        }
                    ),
                }
            );

        var store = CreateStore(handler.Object);

        var results = await store.SearchAsync(new[] { 0.1f, 0.2f }, topK: 1);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_WhenCertaintyIsNotNumber_ShouldUseZeroScore()
    {
        var handler = CreateMockHandler();

        handler
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK))
            .ReturnsAsync(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(
                        new
                        {
                            data = new
                            {
                                get = new Dictionary<string, object>
                                {
                                    ["Document"] = new[]
                                    {
                                        new
                                        {
                                            _additional = new
                                            {
                                                id = "chunk-1",
                                                certainty = "high",
                                            },
                                            content = "content",
                                            documentId = "doc-1",
                                            chunkIndex = 0,
                                            metadata = "{}",
                                        },
                                    },
                                },
                            },
                        }
                    ),
                }
            );

        var store = CreateStore(handler.Object);

        var results = await store.SearchAsync(new[] { 0.1f, 0.2f }, topK: 1, minScore: 0f);

        results.Should().HaveCount(1);
        results[0].Score.Should().Be(0f);
        results[0].Chunk.Id.Should().Be("chunk-1");
    }
}

/// <summary>
/// Weaviate DI extension tests
/// </summary>
public class WeaviateServiceCollectionExtensionsTests
{
    [Fact]
    public void AddWeaviateVectorStore_WithConfiguration_RegistersServices()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Weaviate:Host"] = "localhost",
                    ["Weaviate:Port"] = "8080",
                    ["Weaviate:ClassName"] = "test",
                }
            )
            .Build();

        services.AddWeaviateVectorStore(configuration);

        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IVectorStore));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddWeaviateVectorStore_WithOptions_RegistersServices()
    {
        var services = new ServiceCollection();

        services.AddWeaviateVectorStore(options =>
        {
            options.Host = "localhost";
            options.Port = 8080;
            options.ClassName = "test";
        });

        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IVectorStore));
        descriptor.Should().NotBeNull();
    }
}

/// <summary>
/// WeaviateDistanceMetric enum tests
/// </summary>
public class WeaviateDistanceMetricTests
{
    [Theory]
    [InlineData(WeaviateDistanceMetric.Cosine)]
    [InlineData(WeaviateDistanceMetric.Dot)]
    [InlineData(WeaviateDistanceMetric.L2Squared)]
    [InlineData(WeaviateDistanceMetric.Hamming)]
    [InlineData(WeaviateDistanceMetric.Manhattan)]
    public void DistanceMetric_HasExpectedValues(WeaviateDistanceMetric metric)
    {
        metric.Should().BeDefined();
    }
}

/// <summary>
/// WeaviateVectorIndexType enum tests
/// </summary>
public class WeaviateVectorIndexTypeTests
{
    [Theory]
    [InlineData(WeaviateVectorIndexType.Hnsw)]
    [InlineData(WeaviateVectorIndexType.Flat)]
    [InlineData(WeaviateVectorIndexType.Dynamic)]
    public void VectorIndexType_HasExpectedValues(WeaviateVectorIndexType indexType)
    {
        indexType.Should().BeDefined();
    }
}
