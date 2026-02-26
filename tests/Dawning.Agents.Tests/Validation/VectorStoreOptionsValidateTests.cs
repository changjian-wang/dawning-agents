using Dawning.Agents.Chroma;
using Dawning.Agents.Pinecone;
using Dawning.Agents.Qdrant;
using Dawning.Agents.Weaviate;
using FluentAssertions;

namespace Dawning.Agents.Tests.Validation;

/// <summary>
/// 向量存储 Options Validate() 测试
/// </summary>
public class VectorStoreOptionsValidateTests
{
    #region ChromaOptions

    [Fact]
    public void ChromaOptions_DefaultValues_ShouldBeValid()
    {
        var options = new ChromaOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ChromaOptions_InvalidHost_ShouldThrow(string? host)
    {
        var options = new ChromaOptions { Host = host! };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Host*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void ChromaOptions_InvalidPort_ShouldThrow(int port)
    {
        var options = new ChromaOptions { Port = port };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Port*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ChromaOptions_InvalidCollectionName_ShouldThrow(string? name)
    {
        var options = new ChromaOptions { CollectionName = name! };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*CollectionName*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ChromaOptions_InvalidVectorDimension_ShouldThrow(int dim)
    {
        var options = new ChromaOptions { VectorDimension = dim };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*VectorDimension*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ChromaOptions_InvalidTimeoutSeconds_ShouldThrow(int timeout)
    {
        var options = new ChromaOptions { TimeoutSeconds = timeout };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*TimeoutSeconds*");
    }

    #endregion

    #region WeaviateOptions

    [Fact]
    public void WeaviateOptions_DefaultValues_ShouldBeValid()
    {
        var options = new WeaviateOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WeaviateOptions_InvalidHost_ShouldThrow(string? host)
    {
        var options = new WeaviateOptions { Host = host! };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Host*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void WeaviateOptions_InvalidPort_ShouldThrow(int port)
    {
        var options = new WeaviateOptions { Port = port };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Port*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WeaviateOptions_InvalidClassName_ShouldThrow(string? name)
    {
        var options = new WeaviateOptions { ClassName = name! };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*ClassName*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WeaviateOptions_InvalidVectorDimension_ShouldThrow(int dim)
    {
        var options = new WeaviateOptions { VectorDimension = dim };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*VectorDimension*");
    }

    [Theory]
    [InlineData("ftp")]
    [InlineData("")]
    [InlineData("tcp")]
    public void WeaviateOptions_InvalidScheme_ShouldThrow(string scheme)
    {
        var options = new WeaviateOptions { Scheme = scheme };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Scheme*");
    }

    [Theory]
    [InlineData("http")]
    [InlineData("https")]
    public void WeaviateOptions_ValidScheme_ShouldNotThrow(string scheme)
    {
        var options = new WeaviateOptions { Scheme = scheme };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WeaviateOptions_InvalidTimeoutSeconds_ShouldThrow(int timeout)
    {
        var options = new WeaviateOptions { TimeoutSeconds = timeout };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*TimeoutSeconds*");
    }

    #endregion

    #region QdrantOptions

    [Fact]
    public void QdrantOptions_DefaultValues_ShouldBeValid()
    {
        var options = new QdrantOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void QdrantOptions_InvalidHost_ShouldThrow(string? host)
    {
        var options = new QdrantOptions { Host = host! };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Host*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void QdrantOptions_InvalidPort_ShouldThrow(int port)
    {
        var options = new QdrantOptions { Port = port };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Port*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void QdrantOptions_InvalidCollectionName_ShouldThrow(string? name)
    {
        var options = new QdrantOptions { CollectionName = name! };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*CollectionName*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void QdrantOptions_InvalidVectorSize_ShouldThrow(int size)
    {
        var options = new QdrantOptions { VectorSize = size };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*VectorSize*");
    }

    #endregion

    #region PineconeOptions

    [Fact]
    public void PineconeOptions_ValidConfig_ShouldNotThrow()
    {
        var options = new PineconeOptions { ApiKey = "test-key" };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PineconeOptions_InvalidApiKey_ShouldThrow(string? key)
    {
        var options = new PineconeOptions { ApiKey = key! };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*ApiKey*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PineconeOptions_InvalidIndexName_ShouldThrow(string? name)
    {
        var options = new PineconeOptions { ApiKey = "key", IndexName = name! };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*IndexName*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PineconeOptions_InvalidVectorSize_ShouldThrow(int size)
    {
        var options = new PineconeOptions { ApiKey = "key", VectorSize = size };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*VectorSize*");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("COSINE")] // case: Metric.ToLowerInvariant() is used, so this should work
    public void PineconeOptions_InvalidMetric_ShouldThrow(string metric)
    {
        var options = new PineconeOptions { ApiKey = "key", Metric = metric };
        var act = () => options.Validate();

        if (metric.Equals("cosine", StringComparison.OrdinalIgnoreCase))
        {
            act.Should().NotThrow();
        }
        else
        {
            act.Should().Throw<InvalidOperationException>().WithMessage("*Metric*");
        }
    }

    [Theory]
    [InlineData("cosine")]
    [InlineData("dotproduct")]
    [InlineData("euclidean")]
    public void PineconeOptions_ValidMetric_ShouldNotThrow(string metric)
    {
        var options = new PineconeOptions { ApiKey = "key", Metric = metric };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    #endregion
}
