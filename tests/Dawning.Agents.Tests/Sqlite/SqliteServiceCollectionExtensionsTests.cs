using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Core.Memory;
using Dawning.Agents.Sqlite;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dawning.Agents.Tests.Sqlite;

/// <summary>
/// Unit tests for SqliteServiceCollectionExtensions.
/// </summary>
public class SqliteServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSqliteMemory_WithConfiguration_RegistersServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Dawning:Agents:Memory:Sqlite:ConnectionString"] = "Data Source=:memory:",
                }
            )
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<ITokenCounter>(new SimpleTokenCounter());
        services.AddSqliteMemory(configuration);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var memory = scope.ServiceProvider.GetService<IConversationMemory>();
        memory.Should().NotBeNull();
        memory.Should().BeOfType<SqliteConversationMemory>();
        memory!.SessionId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void AddSqliteMemory_WithDelegate_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITokenCounter>(new SimpleTokenCounter());
        services.AddSqliteMemory(options =>
        {
            options.ConnectionString = "Data Source=:memory:";
        });

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var memory = scope.ServiceProvider.GetService<IConversationMemory>();
        memory.Should().NotBeNull();
        memory.Should().BeOfType<SqliteConversationMemory>();
    }

    [Fact]
    public void AddSqliteMemory_NullConfiguration_ThrowsException()
    {
        var services = new ServiceCollection();
        var action = () => services.AddSqliteMemory((IConfiguration)null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddSqliteMemory_NullDelegate_ThrowsException()
    {
        var services = new ServiceCollection();
        var action = () => services.AddSqliteMemory((Action<SqliteMemoryOptions>)null!);
        action.Should().Throw<ArgumentNullException>();
    }
}
