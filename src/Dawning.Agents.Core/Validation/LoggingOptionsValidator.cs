using Dawning.Agents.Abstractions.Logging;
using FluentValidation;

namespace Dawning.Agents.Core.Validation;

/// <summary>
/// Validator for <see cref="LoggingOptions"/>.
/// </summary>
public class LoggingOptionsValidator : AbstractValidator<LoggingOptions>
{
    private static readonly string[] s_validLogLevels =
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
            .WithMessage("MinimumLevel must not be empty.")
            .Must(BeValidLogLevel)
            .WithMessage(
                $"MinimumLevel must be a valid log level: {string.Join(", ", s_validLogLevels)}"
            );

        When(
            x => x.EnableFile,
            () =>
            {
                RuleFor(x => x.FilePath).NotEmpty().WithMessage("FilePath must not be empty when file logging is enabled.");
            }
        );

        RuleFor(x => x.RetainedFileCount)
            .GreaterThan(0)
            .WithMessage("RetainedFileCount must be greater than 0.")
            .LessThanOrEqualTo(365)
            .WithMessage("RetainedFileCount must not exceed 365.");

        RuleFor(x => x.OutputTemplate).NotEmpty().WithMessage("OutputTemplate must not be empty.");

        RuleForEach(x => x.Override)
            .Must(kvp => BeValidLogLevel(kvp.Value))
            .WithMessage("Log level in Override must be valid.");

        // Elasticsearch configuration validation
        When(
            x => x.Elasticsearch?.Enabled == true,
            () =>
            {
                RuleFor(x => x.Elasticsearch!.NodeUris)
                    .NotEmpty()
                    .WithMessage("Elasticsearch requires at least one node URI.");

                RuleForEach(x => x.Elasticsearch!.NodeUris)
                    .Must(BeValidUrl)
                    .WithMessage("Elasticsearch node URI must be a valid URL.");

                RuleFor(x => x.Elasticsearch!.BatchSize)
                    .GreaterThan(0)
                    .WithMessage("BatchSize must be greater than 0.")
                    .LessThanOrEqualTo(1000)
                    .WithMessage("BatchSize must not exceed 1000.");

                RuleFor(x => x.Elasticsearch!.BatchIntervalSeconds)
                    .GreaterThan(0)
                    .WithMessage("BatchIntervalSeconds must be greater than 0.");
            }
        );

        // Seq configuration validation
        When(
            x => x.Seq?.Enabled == true,
            () =>
            {
                RuleFor(x => x.Seq!.ServerUrl)
                    .NotEmpty()
                    .WithMessage("Seq requires a server URL.")
                    .Must(BeValidUrl)
                    .WithMessage("Seq ServerUrl must be a valid URL.");

                RuleFor(x => x.Seq!.BatchIntervalSeconds)
                    .GreaterThan(0)
                    .WithMessage("BatchIntervalSeconds must be greater than 0.");
            }
        );
    }

    private static bool BeValidLogLevel(string level)
    {
        return s_validLogLevels.Contains(level, StringComparer.OrdinalIgnoreCase);
    }

    private static bool BeValidUrl(string? url)
    {
        return !string.IsNullOrWhiteSpace(url)
            && Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
