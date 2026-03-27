using Dawning.Agents.Abstractions;
using Dawning.Agents.Abstractions.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Sqlite;

/// <summary>
/// Dependency injection extension methods for SQLite conversation memory.
/// </summary>
public static class SqliteServiceCollectionExtensions
{
    /// <summary>
    /// Adds SQLite-backed conversation memory using configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSqliteMemory(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<SqliteMemoryOptions>()
            .Bind(configuration.GetSection(SqliteMemoryOptions.SectionName))
            .ValidateOnStart();

        services.TryAddSingleton<
            IValidateOptions<SqliteMemoryOptions>,
            SqliteMemoryOptionsValidator
        >();

        RegisterCoreServices(services);

        return services;
    }

    /// <summary>
    /// Adds SQLite-backed conversation memory using a configuration delegate.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration delegate.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSqliteMemory(
        this IServiceCollection services,
        Action<SqliteMemoryOptions> configure
    )
    {
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<SqliteMemoryOptions>().Configure(configure).ValidateOnStart();

        services.TryAddSingleton<
            IValidateOptions<SqliteMemoryOptions>,
            SqliteMemoryOptionsValidator
        >();

        RegisterCoreServices(services);

        return services;
    }

    private static void RegisterCoreServices(IServiceCollection services)
    {
        services.TryAddSingleton<SqliteDbContext>();
        services.TryAddScoped<IConversationMemory>(sp =>
        {
            var dbContext = sp.GetRequiredService<SqliteDbContext>();
            var tokenCounter = sp.GetRequiredService<ITokenCounter>();
            var logger = sp.GetService<ILogger<SqliteConversationMemory>>();
            return new SqliteConversationMemory(
                dbContext,
                tokenCounter,
                Guid.NewGuid().ToString(),
                logger
            );
        });
    }
}

/// <summary>
/// Options validator for <see cref="SqliteMemoryOptions"/>.
/// </summary>
internal sealed class SqliteMemoryOptionsValidator : IValidateOptions<SqliteMemoryOptions>
{
    public ValidateOptionsResult Validate(string? name, SqliteMemoryOptions options)
    {
        try
        {
            options.Validate();
            return ValidateOptionsResult.Success;
        }
        catch (InvalidOperationException ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }
    }
}
