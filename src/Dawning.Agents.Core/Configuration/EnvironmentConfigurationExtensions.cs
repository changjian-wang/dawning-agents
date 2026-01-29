using Microsoft.Extensions.Configuration;

namespace Dawning.Agents.Core.Configuration;

/// <summary>
/// 环境变量配置扩展
/// </summary>
public static class EnvironmentConfigurationExtensions
{
    /// <summary>
    /// 添加 .env 文件配置支持
    /// </summary>
    /// <param name="builder">配置构建器</param>
    /// <param name="envFilePath">.env 文件路径（默认为当前目录的 .env）</param>
    /// <param name="optional">是否可选</param>
    /// <returns>配置构建器</returns>
    public static IConfigurationBuilder AddEnvFile(
        this IConfigurationBuilder builder,
        string? envFilePath = null,
        bool optional = true
    )
    {
        var path = envFilePath ?? Path.Combine(Directory.GetCurrentDirectory(), ".env");

        if (!optional && !File.Exists(path))
        {
            throw new FileNotFoundException($".env 文件不存在: {path}", path);
        }

        if (File.Exists(path))
        {
            var envVars = ParseEnvFile(path);
            builder.AddInMemoryCollection(envVars);
        }

        return builder;
    }

    /// <summary>
    /// 添加多个 .env 文件配置支持（按环境）
    /// </summary>
    /// <param name="builder">配置构建器</param>
    /// <param name="environment">环境名称（如 Development, Production）</param>
    /// <returns>配置构建器</returns>
    public static IConfigurationBuilder AddEnvFiles(
        this IConfigurationBuilder builder,
        string? environment = null
    )
    {
        var baseDir = Directory.GetCurrentDirectory();

        // 1. 加载基础 .env 文件
        builder.AddEnvFile(Path.Combine(baseDir, ".env"), optional: true);

        // 2. 加载本地 .env.local 文件（一般不提交到版本控制）
        builder.AddEnvFile(Path.Combine(baseDir, ".env.local"), optional: true);

        // 3. 加载环境特定的 .env 文件
        if (!string.IsNullOrEmpty(environment))
        {
            var envSpecificPath = Path.Combine(baseDir, $".env.{environment.ToLowerInvariant()}");
            builder.AddEnvFile(envSpecificPath, optional: true);

            // 4. 加载环境特定的本地 .env 文件
            var envSpecificLocalPath = Path.Combine(
                baseDir,
                $".env.{environment.ToLowerInvariant()}.local"
            );
            builder.AddEnvFile(envSpecificLocalPath, optional: true);
        }

        return builder;
    }

    /// <summary>
    /// 解析 .env 文件
    /// </summary>
    private static Dictionary<string, string?> ParseEnvFile(string path)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var lines = File.ReadAllLines(path);

        foreach (var line in lines)
        {
            // 跳过空行和注释
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith('#'))
            {
                continue;
            }

            // 解析 KEY=VALUE 格式
            var separatorIndex = trimmedLine.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = trimmedLine[..separatorIndex].Trim();
            var value = trimmedLine[(separatorIndex + 1)..].Trim();

            // 处理引号
            if (
                (value.StartsWith('"') && value.EndsWith('"'))
                || (value.StartsWith('\'') && value.EndsWith('\''))
            )
            {
                value = value[1..^1];
            }

            // 处理转义字符
            value = ProcessEscapeSequences(value);

            // 处理嵌套配置键（用 __ 代替 :）
            key = key.Replace("__", ":");

            result[key] = value;
        }

        return result;
    }

    /// <summary>
    /// 处理转义字符
    /// </summary>
    private static string ProcessEscapeSequences(string value)
    {
        return value
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t")
            .Replace("\\\"", "\"")
            .Replace("\\'", "'")
            .Replace("\\\\", "\\");
    }
}
