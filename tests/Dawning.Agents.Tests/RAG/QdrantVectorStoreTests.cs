using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Qdrant;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Dawning.Agents.Tests.RAG;

/// <summary>
/// QdrantVectorStore 单元测试
/// </summary>
public class QdrantVectorStoreTests
{
    [Fact]
    public void Constructor_WithValidOptions_CreatesStore()
    {
        // Arrange
        var options = Options.Create(new QdrantOptions
        {
            Host = "localhost",
            Port = 6334,
            CollectionName = "test-collection",
            VectorSize = 384
        });

        // Act - 注意：这会尝试连接到 Qdrant，在没有 Qdrant 运行时会失败
        // 这里只测试对象创建，不测试实际连接
        var store = new QdrantVectorStore(options, NullLogger<QdrantVectorStore>.Instance);

        // Assert
        store.Should().NotBeNull();
        store.Name.Should().Be("Qdrant");
        store.Count.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new QdrantVectorStore(null!, null);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public void Constructor_WithInvalidOptions_ThrowsOnValidation()
    {
        // Arrange
        var options = Options.Create(new QdrantOptions
        {
            Host = "",  // 无效的 Host
            CollectionName = "test",
            VectorSize = 384
        });

        // Act
        var act = () => new QdrantVectorStore(options, null);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*Host*");
    }

    [Fact]
    public async Task AddAsync_WithNullChunk_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(new QdrantOptions
        {
            Host = "localhost",
            Port = 6334,
            CollectionName = "test",
            VectorSize = 384
        });
        var store = new QdrantVectorStore(options, null);

        // Act
        var act = async () => await store.AddAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("chunk");
    }

    [Fact]
    public async Task AddAsync_WithMissingEmbedding_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(new QdrantOptions
        {
            Host = "localhost",
            Port = 6334,
            CollectionName = "test",
            VectorSize = 384
        });
        var store = new QdrantVectorStore(options, null);
        var chunk = new DocumentChunk
        {
            Id = "test-1",
            Content = "Test content",
            Embedding = null
        };

        // Act
        var act = async () => await store.AddAsync(chunk);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*embedding*");
    }

    [Fact]
    public async Task SearchAsync_WithNullEmbedding_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(new QdrantOptions
        {
            Host = "localhost",
            Port = 6334,
            CollectionName = "test",
            VectorSize = 384
        });
        var store = new QdrantVectorStore(options, null);

        // Act
        var act = async () => await store.SearchAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("queryEmbedding");
    }

    [Fact]
    public async Task SearchAsync_WithEmptyEmbedding_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(new QdrantOptions
        {
            Host = "localhost",
            Port = 6334,
            CollectionName = "test",
            VectorSize = 384
        });
        var store = new QdrantVectorStore(options, null);

        // Act
        var act = async () => await store.SearchAsync(Array.Empty<float>());

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*empty*");
    }

    [Fact]
    public async Task DeleteAsync_WithNullOrEmptyId_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(new QdrantOptions
        {
            Host = "localhost",
            Port = 6334,
            CollectionName = "test",
            VectorSize = 384
        });
        var store = new QdrantVectorStore(options, null);

        // Act & Assert - ArgumentNullException is a subclass of ArgumentException
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.DeleteAsync(null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.DeleteAsync(""));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.DeleteAsync("   "));
    }

    [Fact]
    public async Task DeleteByDocumentIdAsync_WithNullOrEmptyId_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(new QdrantOptions
        {
            Host = "localhost",
            Port = 6334,
            CollectionName = "test",
            VectorSize = 384
        });
        var store = new QdrantVectorStore(options, null);

        // Act & Assert - ArgumentNullException is a subclass of ArgumentException
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.DeleteByDocumentIdAsync(null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.DeleteByDocumentIdAsync(""));
    }

    [Fact]
    public async Task GetAsync_WithNullOrEmptyId_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(new QdrantOptions
        {
            Host = "localhost",
            Port = 6334,
            CollectionName = "test",
            VectorSize = 384
        });
        var store = new QdrantVectorStore(options, null);

        // Act & Assert - ArgumentNullException is a subclass of ArgumentException
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.GetAsync(null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.GetAsync(""));
    }

    [Fact]
    public async Task AddBatchAsync_WithNullChunks_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(new QdrantOptions
        {
            Host = "localhost",
            Port = 6334,
            CollectionName = "test",
            VectorSize = 384
        });
        var store = new QdrantVectorStore(options, null);

        // Act
        var act = async () => await store.AddBatchAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("chunks");
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        var options = Options.Create(new QdrantOptions
        {
            Host = "localhost",
            Port = 6334,
            CollectionName = "test",
            VectorSize = 384
        });
        var store = new QdrantVectorStore(options, null);

        // Act
        await store.DisposeAsync();
        var act = async () => await store.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }
}

/// <summary>
/// QdrantOptions 配置测试
/// </summary>
public class QdrantOptionsTests
{
    [Fact]
    public void Validate_WithValidOptions_DoesNotThrow()
    {
        // Arrange
        var options = new QdrantOptions
        {
            Host = "localhost",
            Port = 6334,
            CollectionName = "my-collection",
            VectorSize = 1536
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
    public void Validate_WithInvalidHost_ThrowsInvalidOperationException(string? host)
    {
        // Arrange
        var options = new QdrantOptions
        {
            Host = host!,
            Port = 6334,
            CollectionName = "test",
            VectorSize = 384
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*Host*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Validate_WithInvalidPort_ThrowsInvalidOperationException(int port)
    {
        // Arrange
        var options = new QdrantOptions
        {
            Host = "localhost",
            Port = port,
            CollectionName = "test",
            VectorSize = 384
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*Port*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithInvalidCollectionName_ThrowsInvalidOperationException(string? name)
    {
        // Arrange
        var options = new QdrantOptions
        {
            Host = "localhost",
            Port = 6334,
            CollectionName = name!,
            VectorSize = 384
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*CollectionName*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidVectorSize_ThrowsInvalidOperationException(int vectorSize)
    {
        // Arrange
        var options = new QdrantOptions
        {
            Host = "localhost",
            Port = 6334,
            CollectionName = "test",
            VectorSize = vectorSize
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*VectorSize*");
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new QdrantOptions();

        // Assert
        options.Host.Should().Be("localhost");
        options.Port.Should().Be(6334);
        options.CollectionName.Should().Be("documents");
        options.VectorSize.Should().Be(1536);
        options.UseTls.Should().BeFalse();
        options.ApiKey.Should().BeNull();
    }

    [Fact]
    public void SectionName_IsCorrect()
    {
        QdrantOptions.SectionName.Should().Be("Qdrant");
    }
}

/// <summary>
/// Qdrant DI 扩展测试
/// </summary>
public class QdrantServiceCollectionExtensionsTests
{
    [Fact]
    public void AddQdrantVectorStore_WithConfiguration_RegistersServices()
    {
        // Arrange
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Qdrant:Host"] = "localhost",
                ["Qdrant:Port"] = "6334",
                ["Qdrant:CollectionName"] = "test-collection",
                ["Qdrant:VectorSize"] = "384"
            })
            .Build();

        // Act
        services.AddQdrantVectorStore(config);

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IVectorStore));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton);
        descriptor.ImplementationType.Should().Be(typeof(QdrantVectorStore));
    }

    [Fact]
    public void AddQdrantVectorStore_WithOptions_RegistersServices()
    {
        // Arrange
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        // Act
        services.AddQdrantVectorStore(options =>
        {
            options.Host = "my-qdrant";
            options.Port = 6334;
            options.CollectionName = "my-collection";
            options.VectorSize = 768;
        });

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IVectorStore));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddQdrantCloud_RegistersWithApiKey()
    {
        // Arrange
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        // Act
        services.AddQdrantCloud("my-cluster.qdrant.cloud", "test-api-key", "my-collection");

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IVectorStore));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddQdrantCloud_WithDefaultParameters_RegistersServices()
    {
        // Arrange
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        // Act
        services.AddQdrantCloud("my-cluster.qdrant.cloud", "test-api-key");

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IVectorStore));
        descriptor.Should().NotBeNull();
    }
}
