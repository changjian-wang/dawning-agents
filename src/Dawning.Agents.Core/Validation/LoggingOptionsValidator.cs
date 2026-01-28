using Dawning.Agents.Abstractions.Logging;
using FluentValidation;

namespace Dawning.Agents.Core.Validation;

/// <summary>
/// 日志配置选项验证器
/// </summary>
public class LoggingOptionsValidator : AbstractValidator<LoggingOptions>
{
    private static readonly string[] ValidLogLevels =
    [
        "Verbose",
        "Debug",
        "Information",
        "Warning",
        "Error",
        "Fatal",
    ];

    public LoggingOptionsValidator()
    {
        RuleFor(x => x.MinimumLevel)
            .NotEmpty()
            .WithMessage("MinimumLevel 不能为空")
            .Must(BeValidLogLevel)
            .WithMessage($"MinimumLevel 必须是有效的日志级别: {string.Join(", ", ValidLogLevels)}");

        When(
            x => x.EnableFile,
            () =>
            {
                RuleFor(x => x.FilePath)
                    .NotEmpty()
                    .WithMessage("启用文件日志时 FilePath 不能为空");
            }
        );

        RuleFor(x => x.RetainedFileCount)
            .GreaterThan(0)
            .WithMessage("RetainedFileCount 必须大于 0")
            .LessThanOrEqualTo(365)
            .WithMessage("RetainedFileCount 不能超过 365");

        RuleFor(x => x.OutputTemplate)
            .NotEmpty()
            .WithMessage("OutputTemplate 不能为空");

        RuleForEach(x => x.Override)
            .Must(kvp => BeValidLogLevel(kvp.Value))
            .WithMessage("Override 中的日志级别必须有效");

        // Elasticsearch 配置验证
        When(
            x => x.Elasticsearch?.Enabled == true,
            () =>
            {
                RuleFor(x => x.Elasticsearch!.NodeUris)
                    .NotEmpty()
                    .WithMessage("Elasticsearch 需要配置至少一个节点地址");

                RuleForEach(x => x.Elasticsearch!.NodeUris)
                    .Must(BeValidUrl)
                    .WithMessage("Elasticsearch 节点地址必须是有效的 URL");

                RuleFor(x => x.Elasticsearch!.BatchSize)
                    .GreaterThan(0)
                    .WithMessage("BatchSize 必须大于 0")
                    .LessThanOrEqualTo(1000)
                    .WithMessage("BatchSize 不能超过 1000");

                RuleFor(x => x.Elasticsearch!.BatchIntervalSeconds)
                    .GreaterThan(0)
                    .WithMessage("BatchIntervalSeconds 必须大于 0");
            }
        );

        // Seq 配置验证
        When(
            x => x.Seq?.Enabled == true,
            () =>
            {
                RuleFor(x => x.Seq!.ServerUrl)
                    .NotEmpty()
                    .WithMessage("Seq 需要配置服务器地址")
                    .Must(BeValidUrl)
                    .WithMessage("Seq ServerUrl 必须是有效的 URL");

                RuleFor(x => x.Seq!.BatchIntervalSeconds)
                    .GreaterThan(0)
                    .WithMessage("BatchIntervalSeconds 必须大于 0");
            }
        );
    }

    private static bool BeValidLogLevel(string level)
    {
        return ValidLogLevels.Contains(level, StringComparer.OrdinalIgnoreCase);
    }

    private static bool BeValidUrl(string? url)
    {
        return !string.IsNullOrWhiteSpace(url)
            && Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
