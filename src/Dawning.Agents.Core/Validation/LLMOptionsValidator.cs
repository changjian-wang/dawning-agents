using Dawning.Agents.Abstractions.LLM;
using FluentValidation;

namespace Dawning.Agents.Core.Validation;

/// <summary>
/// LLM 配置选项验证器
/// </summary>
public class LLMOptionsValidator : AbstractValidator<LLMOptions>
{
    public LLMOptionsValidator()
    {
        RuleFor(x => x.Model).NotEmpty().WithMessage("Model 不能为空");

        When(
            x => x.ProviderType == LLMProviderType.OpenAI,
            () =>
            {
                RuleFor(x => x.ApiKey).NotEmpty().WithMessage("OpenAI 需要配置 ApiKey");
            }
        );

        When(
            x => x.ProviderType == LLMProviderType.AzureOpenAI,
            () =>
            {
                RuleFor(x => x.Endpoint)
                    .NotEmpty()
                    .WithMessage("Azure OpenAI 需要配置 Endpoint")
                    .Must(BeValidUrl)
                    .WithMessage("Endpoint 必须是有效的 URL");

                RuleFor(x => x.ApiKey).NotEmpty().WithMessage("Azure OpenAI 需要配置 ApiKey");
            }
        );

        When(
            x => x.ProviderType == LLMProviderType.Ollama,
            () =>
            {
                RuleFor(x => x.Endpoint)
                    .Must(BeValidUrlOrEmpty)
                    .WithMessage("Endpoint 必须是有效的 URL");
            }
        );
    }

    private static bool BeValidUrl(string? url)
    {
        return !string.IsNullOrWhiteSpace(url)
            && Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static bool BeValidUrlOrEmpty(string? url)
    {
        return string.IsNullOrWhiteSpace(url) || BeValidUrl(url);
    }
}
