using Dawning.Agents.Sqlite;
using FluentAssertions;

namespace Dawning.Agents.Tests.Sqlite;

/// <summary>
/// Unit tests for SqliteMemoryOptions.
/// </summary>
public class SqliteMemoryOptionsTests
{
    [Fact]
    public void Defaults_AreReasonable()
    {
        var options = new SqliteMemoryOptions();

        options.ConnectionString.Should().Be("Data Source=dawning_agents.db");
        options.AutoCreateSchema.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidOptions_DoesNotThrow()
    {
        var options = new SqliteMemoryOptions { ConnectionString = "Data Source=test.db" };

        var action = () => options.Validate();

        action.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyConnectionString_Throws(string? connectionString)
    {
        var options = new SqliteMemoryOptions { ConnectionString = connectionString! };

        var action = () => options.Validate();

        action.Should().Throw<InvalidOperationException>();
    }
}
