using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Pinecone;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Tests.RAG;

/// <summary>
/// PineconeVectorStore 单元测试
/// </summary>
public class PineconeVectorStoreTests
{
    [Fact]
    public void Constructor_WithValidOptions_CreatesStore()
    {
        // Arrange
        var options = Options.Create(
            new PineconeOptions
            {
                ApiKey = "test-api-key",
                IndexName = "test-index",
                VectorSize = 1536,
            }
        );

        // Act
        var store = new PineconeVectorStore(options, NullLogger<PineconeVectorStore>.Instance);

        // Assert
        store.Should().NotBeNull();
        store.Name.Should().Be("Pinecone");
        store.Count.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PineconeVectorStore(null!, null);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public void Constructor_WithMissingApiKey_ThrowsOnValidation()
    {
        // Arrange
        var options = Options.Create(
            new PineconeOptions
            {
                ApiKey = "", // 无效
                IndexName = "test",
                VectorSize = 1536,
            }
        );

        // Act
        var act = () => new PineconeVectorStore(options, null);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*ApiKey*");
    }

    [Fact]
    public async Task AddAsync_WithNullChunk_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(
            new PineconeOptions
            {
                ApiKey = "test-api-key",
                IndexName = "test",
                VectorSize = 1536,
            }
        );
        var store = new PineconeVectorStore(options, null);

        // Act
        var act = async () => await store.AddAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("chunk");
    }

    [Fact]
    public async Task AddAsync_WithMissingEmbedding_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(
            new PineconeOptions
            {
                ApiKey = "test-api-key",
                IndexName = "test",
                VectorSize = 1536,
            }
        );
        var store = new PineconeVectorStore(options, null);
        var chunk = new DocumentChunk
        {
            Id = "test-1",
            Content = "Test content",
            Embedding = null,
        };

        // Act
        var act = async () => await store.AddAsync(chunk);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*embedding*");
    }

    [Fact]
    public async Task SearchAsync_WithNullEmbedding_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(
            new PineconeOptions
            {
                ApiKey = "test-api-key",
                IndexName = "test",
                VectorSize = 1536,
            }
        );
        var store = new PineconeVectorStore(options, null);

        // Act
        var act = async () => await store.SearchAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("queryEmbedding");
    }

    [Fact]
    public async Task SearchAsync_WithEmptyEmbedding_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(
            new PineconeOptions
            {
                ApiKey = "test-api-key",
                IndexName = "test",
                VectorSize = 1536,
            }
        );
        var store = new PineconeVectorStore(options, null);

        // Act
        var act = async () => await store.SearchAsync(Array.Empty<float>());

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*empty*");
    }

    [Fact]
    public async Task DeleteAsync_WithNullOrEmptyId_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(
            new PineconeOptions
            {
                ApiKey = "test-api-key",
                IndexName = "test",
                VectorSize = 1536,
            }
        );
        var store = new PineconeVectorStore(options, null);

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.DeleteAsync(null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.DeleteAsync(""));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.DeleteAsync("   "));
    }

    [Fact]
    public async Task DeleteByDocumentIdAsync_WithNullOrEmptyId_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(
            new PineconeOptions
            {
                ApiKey = "test-api-key",
                IndexName = "test",
                VectorSize = 1536,
            }
        );
        var store = new PineconeVectorStore(options, null);

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.DeleteByDocumentIdAsync(null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.DeleteByDocumentIdAsync(""));
    }

    [Fact]
    public async Task GetAsync_WithNullOrEmptyId_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(
            new PineconeOptions
            {
                ApiKey = "test-api-key",
                IndexName = "test",
                VectorSize = 1536,
            }
        );
        var store = new PineconeVectorStore(options, null);

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.GetAsync(null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.GetAsync(""));
    }

    [Fact]
    public async Task AddBatchAsync_WithNullChunks_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(
            new PineconeOptions
            {
                ApiKey = "test-api-key",
                IndexName = "test",
                VectorSize = 1536,
            }
        );
        var store = new PineconeVectorStore(options, null);

        // Act
        var act = async () => await store.AddBatchAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("chunks");
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        var options = Options.Create(
            new PineconeOptions
            {
                ApiKey = "test-api-key",
                IndexName = "test",
                VectorSize = 1536,
            }
        );
        var store = new PineconeVectorStore(options, null);

        // Act
        await store.DisposeAsync();
        var act = async () => await store.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }
}

/// <summary>
/// PineconeOptions 配置测试
/// </summary>
public class PineconeOptionsTests
{
    [Fact]
    public void Validate_WithValidOptions_DoesNotThrow()
    {
        // Arrange
        var options = new PineconeOptions
        {
            ApiKey = "test-api-key",
            IndexName = "test-index",
            VectorSize = 1536,
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithInvalidApiKey_ThrowsInvalidOperationException(string? apiKey)
    {
        // Arrange
        var options = new PineconeOptions
        {
            ApiKey = apiKey!,
            IndexName = "test",
            VectorSize = 1536,
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*ApiKey*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithInvalidIndexName_ThrowsInvalidOperationException(string? indexName)
    {
        // Arrange
        var options = new PineconeOptions
        {
            ApiKey = "test-api-key",
            IndexName = indexName!,
            VectorSize = 1536,
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*IndexName*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidVectorSize_ThrowsInvalidOperationException(int vectorSize)
    {
        // Arrange
        var options = new PineconeOptions
        {
            ApiKey = "test-api-key",
            IndexName = "test",
            VectorSize = vectorSize,
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*VectorSize*");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("random")]
    public void Validate_WithInvalidMetric_ThrowsInvalidOperationException(string metric)
    {
        // Arrange
        var options = new PineconeOptions
        {
            ApiKey = "test-api-key",
            IndexName = "test",
            VectorSize = 1536,
            Metric = metric,
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*Metric*");
    }

    [Theory]
    [InlineData("cosine")]
    [InlineData("dotproduct")]
    [InlineData("euclidean")]
    [InlineData("COSINE")]
    [InlineData("DotProduct")]
    public void Validate_WithValidMetric_DoesNotThrow(string metric)
    {
        // Arrange
        var options = new PineconeOptions
        {
            ApiKey = "test-api-key",
            IndexName = "test",
            VectorSize = 1536,
            Metric = metric,
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new PineconeOptions();

        // Assert
        options.ApiKey.Should().BeEmpty();
        options.IndexName.Should().Be("documents");
        options.Namespace.Should().BeNull();
        options.VectorSize.Should().Be(1536);
        options.Metric.Should().Be("cosine");
        options.AutoCreateIndex.Should().BeFalse();
        options.Cloud.Should().Be("aws");
        options.Region.Should().Be("us-east-1");
    }

    [Fact]
    public void SectionName_IsCorrect()
    {
        PineconeOptions.SectionName.Should().Be("Pinecone");
    }
}

/// <summary>
/// Pinecone DI 扩展测试
/// </summary>
public class PineconeServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPineconeVectorStore_WithConfiguration_RegistersServices()
    {
        // Arrange
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Pinecone:ApiKey"] = "test-api-key",
                    ["Pinecone:IndexName"] = "test-index",
                    ["Pinecone:VectorSize"] = "384",
                }
            )
            .Build();

        // Act
        services.AddPineconeVectorStore(config);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IVectorStore));
        descriptor.Should().NotBeNull();
        descriptor!
            .Lifetime.Should()
            .Be(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton);
        descriptor.ImplementationType.Should().Be(typeof(PineconeVectorStore));
    }

    [Fact]
    public void AddPineconeVectorStore_WithOptions_RegistersServices()
    {
        // Arrange
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        // Act
        services.AddPineconeVectorStore(options =>
        {
            options.ApiKey = "test-api-key";
            options.IndexName = "my-index";
            options.VectorSize = 768;
        });

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IVectorStore));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddPineconeVectorStore_WithApiKey_RegistersServices()
    {
        // Arrange
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        // Act
        services.AddPineconeVectorStore("test-api-key", "my-index", "my-namespace", 1536);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IVectorStore));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddPineconeServerless_RegistersWithAutoCreate()
    {
        // Arrange
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        // Act
        services.AddPineconeServerless(
            "test-api-key",
            "my-index",
            vectorSize: 1536,
            cloud: "aws",
            region: "us-east-1"
        );

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IVectorStore));
        descriptor.Should().NotBeNull();
    }
}
