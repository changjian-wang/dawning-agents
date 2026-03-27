using Dawning.Agents.Abstractions.LLM;
using FluentValidation;

namespace Dawning.Agents.Core.Validation;

/// <summary>
/// Validator for <see cref="LLMOptions"/>.
/// </summary>
public class LLMOptionsValidator : AbstractValidator<LLMOptions>
{
    public LLMOptionsValidator()
    {
        RuleFor(x => x.Model).NotEmpty().WithMessage("Model must not be empty.");

        When(
            x => x.ProviderType == LLMProviderType.OpenAI,
            () =>
            {
                RuleFor(x => x.ApiKey)
                    .NotEmpty()
                    .WithMessage("ApiKey is required for the OpenAI provider.");
            }
        );

        When(
            x => x.ProviderType == LLMProviderType.AzureOpenAI,
            () =>
            {
                RuleFor(x => x.Endpoint)
                    .NotEmpty()
                    .WithMessage("Endpoint is required for the Azure OpenAI provider.")
                    .Must(BeValidUrl)
                    .WithMessage("Endpoint must be a valid URL.");

                RuleFor(x => x.ApiKey)
                    .NotEmpty()
                    .WithMessage("ApiKey is required for the Azure OpenAI provider.");
            }
        );

        When(
            x => x.ProviderType == LLMProviderType.Ollama,
            () =>
            {
                RuleFor(x => x.Endpoint)
                    .NotEmpty()
                    .WithMessage("Endpoint is required for the Ollama provider.")
                    .Must(BeValidUrl)
                    .WithMessage("Endpoint must be a valid URL.");
            }
        );

        When(
            x => x.ProviderType == LLMProviderType.OpenAICompatible,
            () =>
            {
                RuleFor(x => x.ApiKey)
                    .NotEmpty()
                    .WithMessage("ApiKey is required for the OpenAI-compatible provider.");

                RuleFor(x => x.Endpoint)
                    .NotEmpty()
                    .WithMessage("Endpoint is required for the OpenAI-compatible provider.")
                    .Must(BeValidUrl)
                    .WithMessage("Endpoint must be a valid URL.");
            }
        );
    }

    private static bool BeValidUrl(string? url)
    {
        return !string.IsNullOrWhiteSpace(url)
            && Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
