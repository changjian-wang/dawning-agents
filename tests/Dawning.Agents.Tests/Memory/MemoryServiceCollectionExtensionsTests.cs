using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Core.Memory;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Dawning.Agents.Tests.Memory;

/// <summary>
/// MemoryServiceCollectionExtensions 单元测试
/// </summary>
public class MemoryServiceCollectionExtensionsTests
{
    #region AddBufferMemory Tests

    [Fact]
    public void AddBufferMemory_RegistersBufferMemory()
    {
        var services = new ServiceCollection();

        services.AddBufferMemory();
        var provider = services.BuildServiceProvider();

        var memory = provider.GetRequiredService<IConversationMemory>();

        memory.Should().NotBeNull();
        memory.Should().BeOfType<BufferMemory>();
    }

    [Fact]
    public void AddBufferMemory_WithCustomParams_RegistersCorrectly()
    {
        var services = new ServiceCollection();

        services.AddBufferMemory("gpt-3.5-turbo", 4096);
        var provider = services.BuildServiceProvider();

        var tokenCounter = provider.GetRequiredService<ITokenCounter>();
        var memory = provider.GetRequiredService<IConversationMemory>();

        tokenCounter.ModelName.Should().Be("gpt-3.5-turbo");
        tokenCounter.MaxContextTokens.Should().Be(4096);
        memory.Should().BeOfType<BufferMemory>();
    }

    #endregion

    #region AddWindowMemory Tests

    [Fact]
    public void AddWindowMemory_RegistersWindowMemory()
    {
        var services = new ServiceCollection();

        services.AddWindowMemory();
        var provider = services.BuildServiceProvider();

        var memory = provider.GetRequiredService<IConversationMemory>();

        memory.Should().NotBeNull();
        memory.Should().BeOfType<WindowMemory>();
    }

    [Fact]
    public void AddWindowMemory_WithCustomWindowSize_RegistersCorrectly()
    {
        var services = new ServiceCollection();

        services.AddWindowMemory(windowSize: 5);
        var provider = services.BuildServiceProvider();

        var memory = provider.GetRequiredService<IConversationMemory>();

        memory.Should().BeOfType<WindowMemory>();
    }

    #endregion

    #region AddSummaryMemory Tests

    [Fact]
    public void AddSummaryMemory_WithLLMProvider_RegistersSummaryMemory()
    {
        var services = new ServiceCollection();
        var mockLLM = new Mock<ILLMProvider>();

        services.AddSingleton(mockLLM.Object);
        services.AddSummaryMemory();
        var provider = services.BuildServiceProvider();

        var memory = provider.GetRequiredService<IConversationMemory>();

        memory.Should().NotBeNull();
        memory.Should().BeOfType<SummaryMemory>();
    }

    #endregion

    #region AddTokenCounter Tests

    [Fact]
    public void AddTokenCounter_RegistersSimpleTokenCounter()
    {
        var services = new ServiceCollection();

        services.AddTokenCounter();
        var provider = services.BuildServiceProvider();

        var counter = provider.GetRequiredService<ITokenCounter>();

        counter.Should().NotBeNull();
        counter.Should().BeOfType<SimpleTokenCounter>();
    }

    [Fact]
    public void AddTokenCounter_WithCustomParams_RegistersCorrectly()
    {
        var services = new ServiceCollection();

        services.AddTokenCounter("llama3", 128000);
        var provider = services.BuildServiceProvider();

        var counter = provider.GetRequiredService<ITokenCounter>();

        counter.ModelName.Should().Be("llama3");
        counter.MaxContextTokens.Should().Be(128000);
    }

    #endregion

    #region AddMemory (Configuration-based) Tests

    [Fact]
    public void AddMemory_WithBufferConfig_RegistersBufferMemory()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Memory:Type"] = "Buffer",
                    ["Memory:ModelName"] = "gpt-4",
                    ["Memory:MaxContextTokens"] = "8192",
                }
            )
            .Build();

        services.AddMemory(config);
        var provider = services.BuildServiceProvider();

        var memory = provider.GetRequiredService<IConversationMemory>();

        memory.Should().BeOfType<BufferMemory>();
    }

    [Fact]
    public void AddMemory_WithWindowConfig_RegistersWindowMemory()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Memory:Type"] = "Window",
                    ["Memory:WindowSize"] = "10",
                }
            )
            .Build();

        services.AddMemory(config);
        var provider = services.BuildServiceProvider();

        var memory = provider.GetRequiredService<IConversationMemory>();

        memory.Should().BeOfType<WindowMemory>();
    }

    [Fact]
    public void AddMemory_WithSummaryConfig_RegistersSummaryMemory()
    {
        var services = new ServiceCollection();
        var mockLLM = new Mock<ILLMProvider>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Memory:Type"] = "Summary",
                    ["Memory:MaxRecentMessages"] = "5",
                    ["Memory:SummaryThreshold"] = "10",
                }
            )
            .Build();

        services.AddSingleton(mockLLM.Object);
        services.AddMemory(config);
        var provider = services.BuildServiceProvider();

        var memory = provider.GetRequiredService<IConversationMemory>();

        memory.Should().BeOfType<SummaryMemory>();
    }

    [Fact]
    public void AddMemory_WithEmptyConfig_DefaultsToBufferMemory()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();

        services.AddMemory(config);
        var provider = services.BuildServiceProvider();

        var memory = provider.GetRequiredService<IConversationMemory>();

        memory.Should().BeOfType<BufferMemory>();
    }

    #endregion

    #region Singleton/Scoped Behavior Tests

    [Fact]
    public void TokenCounter_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddBufferMemory();
        var provider = services.BuildServiceProvider();

        var counter1 = provider.GetRequiredService<ITokenCounter>();
        var counter2 = provider.GetRequiredService<ITokenCounter>();

        counter1.Should().BeSameAs(counter2);
    }

    [Fact]
    public void Memory_IsScoped()
    {
        var services = new ServiceCollection();
        services.AddBufferMemory();
        var provider = services.BuildServiceProvider();

        IConversationMemory memory1, memory2;
        using (var scope1 = provider.CreateScope())
        {
            memory1 = scope1.ServiceProvider.GetRequiredService<IConversationMemory>();
        }
        using (var scope2 = provider.CreateScope())
        {
            memory2 = scope2.ServiceProvider.GetRequiredService<IConversationMemory>();
        }

        memory1.Should().NotBeSameAs(memory2);
    }

    #endregion
}
