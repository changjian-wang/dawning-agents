using Microsoft.Extensions.Configuration;

namespace Dawning.Agents.Core.Configuration;

/// <summary>
/// Environment variable configuration extensions.
/// </summary>
public static class EnvironmentConfigurationExtensions
{
    /// <summary>
    /// Adds .env file configuration support.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="envFilePath">The .env file path (defaults to .env in the current directory).</param>
    /// <param name="optional">Whether the file is optional.</param>
    /// <returns>The configuration builder.</returns>
    public static IConfigurationBuilder AddEnvFile(
        this IConfigurationBuilder builder,
        string? envFilePath = null,
        bool optional = true
    )
    {
        var path = envFilePath ?? Path.Combine(Directory.GetCurrentDirectory(), ".env");

        if (!optional && !File.Exists(path))
        {
            throw new FileNotFoundException($".env file not found: {path}", path);
        }

        if (File.Exists(path))
        {
            var envVars = ParseEnvFile(path);
            builder.AddInMemoryCollection(envVars);
        }

        return builder;
    }

    /// <summary>
    /// Adds multiple .env file configuration support (per environment).
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="environment">The environment name (e.g. Development, Production).</param>
    /// <returns>The configuration builder.</returns>
    public static IConfigurationBuilder AddEnvFiles(
        this IConfigurationBuilder builder,
        string? environment = null
    )
    {
        var baseDir = Directory.GetCurrentDirectory();

        // 1. Load the base .env file
        builder.AddEnvFile(Path.Combine(baseDir, ".env"), optional: true);

        // 2. Load the local .env.local file (typically not committed to version control)
        builder.AddEnvFile(Path.Combine(baseDir, ".env.local"), optional: true);

        // 3. Load the environment-specific .env file
        if (!string.IsNullOrEmpty(environment))
        {
            var envSpecificPath = Path.Combine(baseDir, $".env.{environment.ToLowerInvariant()}");
            builder.AddEnvFile(envSpecificPath, optional: true);

            // 4. Load the environment-specific local .env file
            var envSpecificLocalPath = Path.Combine(
                baseDir,
                $".env.{environment.ToLowerInvariant()}.local"
            );
            builder.AddEnvFile(envSpecificLocalPath, optional: true);
        }

        return builder;
    }

    /// <summary>
    /// Parses a .env file.
    /// </summary>
    private static Dictionary<string, string?> ParseEnvFile(string path)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var lines = File.ReadAllLines(path);

        foreach (var line in lines)
        {
            // Skip blank lines and comments
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith('#'))
            {
                continue;
            }

            // Parse KEY=VALUE format
            var separatorIndex = trimmedLine.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = trimmedLine[..separatorIndex].Trim();
            var value = trimmedLine[(separatorIndex + 1)..].Trim();

            // Strip quotes
            var quoteChar = '\0';
            if (
                value.Length >= 2
                && (
                    (value.StartsWith('"') && value.EndsWith('"'))
                    || (value.StartsWith('\'') && value.EndsWith('\''))
                )
            )
            {
                quoteChar = value[0];
                value = value[1..^1];
            }

            // Only process escape sequences for double-quoted and unquoted values (single-quoted values are literal)
            if (quoteChar != '\'')
            {
                value = ProcessEscapeSequences(value);
            }

            // Convert nested config keys (__ replaces :)
            key = key.Replace("__", ":");

            result[key] = value;
        }

        return result;
    }

    /// <summary>
    /// Processes escape sequences.
    /// </summary>
    private static string ProcessEscapeSequences(string value)
    {
        // Process \\ first using placeholder to avoid premature matching.
        // e.g. input "\\n" should become "\n" (literal backslash + n),
        // not backslash + newline.
        const string placeholder = "\x00";
        return value
            .Replace("\\\\", placeholder)
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t")
            .Replace("\\\"", "\"")
            .Replace("\\'", "'")
            .Replace(placeholder, "\\");
    }
}
