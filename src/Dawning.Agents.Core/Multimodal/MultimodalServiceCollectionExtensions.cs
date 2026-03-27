using System.Web;
using Dawning.Agents.Abstractions.Multimodal;
using Dawning.Agents.Core.Multimodal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core;

/// <summary>
/// Multimodal service DI extensions.
/// </summary>
public static class MultimodalServiceCollectionExtensions
{
    /// <summary>
    /// Adds the OpenAI Vision provider.
    /// </summary>
    public static IServiceCollection AddOpenAIVision(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var section = configuration.GetSection("OpenAI");
        var apiKey =
            section["ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey is not configured");
        var baseUrl = section["BaseUrl"] ?? "https://api.openai.com/v1";
        var model = section["VisionModel"] ?? "gpt-4o";

        return services.AddOpenAIVision(apiKey, baseUrl, model);
    }

    /// <summary>
    /// Adds the OpenAI Vision provider.
    /// </summary>
    public static IServiceCollection AddOpenAIVision(
        this IServiceCollection services,
        string apiKey,
        string? baseUrl = null,
        string? model = null
    )
    {
        services.TryAddSingleton<IVisionProvider>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("OpenAIVision");
            var logger =
                sp.GetService<Microsoft.Extensions.Logging.ILogger<OpenAIVisionProvider>>();

            return new OpenAIVisionProvider(
                httpClient,
                apiKey,
                baseUrl ?? "https://api.openai.com/v1",
                model ?? "gpt-4o",
                logger
            );
        });

        return services;
    }

    /// <summary>
    /// Adds the Azure OpenAI Vision provider.
    /// </summary>
    public static IServiceCollection AddAzureOpenAIVision(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var section = configuration.GetSection("AzureOpenAI");
        var apiKey =
            section["ApiKey"]
            ?? throw new InvalidOperationException("AzureOpenAI:ApiKey is not configured");
        var endpoint =
            section["Endpoint"]
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is not configured");
        var deployment = section["VisionDeployment"] ?? "gpt-4o";
        var apiVersion = section["ApiVersion"] ?? "2024-02-15-preview";

        // Azure OpenAI uses a different URL format
        var baseUrl = $"{endpoint.TrimEnd('/')}/openai/deployments/{deployment}";

        return services.AddAzureOpenAIVision(apiKey, baseUrl, apiVersion);
    }

    /// <summary>
    /// Adds the Azure OpenAI Vision provider.
    /// </summary>
    public static IServiceCollection AddAzureOpenAIVision(
        this IServiceCollection services,
        string apiKey,
        string endpoint,
        string apiVersion = "2024-02-15-preview"
    )
    {
        services
            .AddHttpClient("AzureOpenAIVision")
            .ConfigureHttpClient(client => client.DefaultRequestHeaders.Add("api-key", apiKey))
            .AddHttpMessageHandler(() => new AzureApiVersionHandler(apiVersion));

        services.TryAddSingleton<IVisionProvider>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("AzureOpenAIVision");
            var logger =
                sp.GetService<Microsoft.Extensions.Logging.ILogger<OpenAIVisionProvider>>();

            return new OpenAIVisionProvider(
                httpClient,
                apiKey: "",
                baseUrl: endpoint,
                defaultModel: "",
                logger: logger
            );
        });

        return services;
    }

    /// <summary>
    /// Adds a custom vision provider.
    /// </summary>
    public static IServiceCollection AddVisionProvider<T>(this IServiceCollection services)
        where T : class, IVisionProvider
    {
        services.TryAddSingleton<IVisionProvider, T>();
        return services;
    }

    #region Audio Transcription (Speech-to-Text)

    /// <summary>
    /// Adds the OpenAI Whisper audio transcription provider.
    /// </summary>
    public static IServiceCollection AddOpenAIWhisper(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var section = configuration.GetSection("OpenAI");
        var apiKey =
            section["ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey is not configured");
        var baseUrl = section["BaseUrl"] ?? "https://api.openai.com/v1";
        var model = section["WhisperModel"] ?? "whisper-1";

        return services.AddOpenAIWhisper(apiKey, baseUrl, model);
    }

    /// <summary>
    /// Adds the OpenAI Whisper audio transcription provider.
    /// </summary>
    public static IServiceCollection AddOpenAIWhisper(
        this IServiceCollection services,
        string apiKey,
        string? baseUrl = null,
        string? model = null
    )
    {
        services.TryAddSingleton<IAudioTranscriptionProvider>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("OpenAIWhisper");
            var logger =
                sp.GetService<Microsoft.Extensions.Logging.ILogger<OpenAIWhisperProvider>>();

            return new OpenAIWhisperProvider(
                httpClient,
                httpClientFactory,
                apiKey,
                baseUrl ?? "https://api.openai.com/v1",
                model ?? "whisper-1",
                logger
            );
        });

        return services;
    }

    /// <summary>
    /// Adds the Azure OpenAI Whisper audio transcription provider.
    /// </summary>
    public static IServiceCollection AddAzureOpenAIWhisper(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var section = configuration.GetSection("AzureOpenAI");
        var apiKey =
            section["ApiKey"]
            ?? throw new InvalidOperationException("AzureOpenAI:ApiKey is not configured");
        var endpoint =
            section["Endpoint"]
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is not configured");
        var deployment = section["WhisperDeployment"] ?? "whisper";
        var apiVersion = section["ApiVersion"] ?? "2024-02-15-preview";

        var baseUrl = $"{endpoint.TrimEnd('/')}/openai/deployments/{deployment}";

        return services.AddAzureOpenAIWhisper(apiKey, baseUrl, apiVersion);
    }

    /// <summary>
    /// Adds the Azure OpenAI Whisper audio transcription provider.
    /// </summary>
    public static IServiceCollection AddAzureOpenAIWhisper(
        this IServiceCollection services,
        string apiKey,
        string endpoint,
        string apiVersion = "2024-02-15-preview"
    )
    {
        services
            .AddHttpClient("AzureOpenAIWhisper")
            .ConfigureHttpClient(client => client.DefaultRequestHeaders.Add("api-key", apiKey))
            .AddHttpMessageHandler(() => new AzureApiVersionHandler(apiVersion));

        services.TryAddSingleton<IAudioTranscriptionProvider>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("AzureOpenAIWhisper");
            var logger =
                sp.GetService<Microsoft.Extensions.Logging.ILogger<OpenAIWhisperProvider>>();

            return new OpenAIWhisperProvider(
                httpClient,
                httpClientFactory,
                apiKey: "",
                baseUrl: endpoint,
                defaultModel: "",
                logger: logger
            );
        });

        return services;
    }

    /// <summary>
    /// Adds a custom audio transcription provider.
    /// </summary>
    public static IServiceCollection AddAudioTranscriptionProvider<T>(
        this IServiceCollection services
    )
        where T : class, IAudioTranscriptionProvider
    {
        services.TryAddSingleton<IAudioTranscriptionProvider, T>();
        return services;
    }

    #endregion

    #region Text-to-Speech

    /// <summary>
    /// Adds the OpenAI TTS text-to-speech provider.
    /// </summary>
    public static IServiceCollection AddOpenAITTS(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var section = configuration.GetSection("OpenAI");
        var apiKey =
            section["ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey is not configured");
        var baseUrl = section["BaseUrl"] ?? "https://api.openai.com/v1";
        var model = section["TTSModel"] ?? "tts-1";

        return services.AddOpenAITTS(apiKey, baseUrl, model);
    }

    /// <summary>
    /// Adds the OpenAI TTS text-to-speech provider.
    /// </summary>
    public static IServiceCollection AddOpenAITTS(
        this IServiceCollection services,
        string apiKey,
        string? baseUrl = null,
        string? model = null
    )
    {
        services.TryAddSingleton<ITextToSpeechProvider>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("OpenAITTS");
            var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<OpenAITTSProvider>>();

            return new OpenAITTSProvider(
                httpClient,
                apiKey,
                baseUrl ?? "https://api.openai.com/v1",
                model ?? "tts-1",
                logger
            );
        });

        return services;
    }

    /// <summary>
    /// Adds a custom text-to-speech provider.
    /// </summary>
    public static IServiceCollection AddTextToSpeechProvider<T>(this IServiceCollection services)
        where T : class, ITextToSpeechProvider
    {
        services.TryAddSingleton<ITextToSpeechProvider, T>();
        return services;
    }

    #endregion

    #region Combined Audio Services

    /// <summary>
    /// Adds all OpenAI audio services (Whisper + TTS).
    /// </summary>
    public static IServiceCollection AddOpenAIAudio(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddOpenAIWhisper(configuration);
        services.AddOpenAITTS(configuration);
        return services;
    }

    /// <summary>
    /// Adds all OpenAI audio services (Whisper + TTS).
    /// </summary>
    public static IServiceCollection AddOpenAIAudio(
        this IServiceCollection services,
        string apiKey,
        string? baseUrl = null
    )
    {
        services.AddOpenAIWhisper(apiKey, baseUrl);
        services.AddOpenAITTS(apiKey, baseUrl);
        return services;
    }

    /// <summary>
    /// Adds all OpenAI multimodal services (Vision + Whisper + TTS).
    /// </summary>
    public static IServiceCollection AddOpenAIMultimodal(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddOpenAIVision(configuration);
        services.AddOpenAIWhisper(configuration);
        services.AddOpenAITTS(configuration);
        return services;
    }

    /// <summary>
    /// Adds all OpenAI multimodal services (Vision + Whisper + TTS).
    /// </summary>
    public static IServiceCollection AddOpenAIMultimodal(
        this IServiceCollection services,
        string apiKey,
        string? baseUrl = null
    )
    {
        services.AddOpenAIVision(apiKey, baseUrl);
        services.AddOpenAIWhisper(apiKey, baseUrl);
        services.AddOpenAITTS(apiKey, baseUrl);
        return services;
    }

    #endregion

    /// <summary>
    /// Appends the api-version query parameter for Azure OpenAI requests.
    /// </summary>
    private sealed class AzureApiVersionHandler(string apiVersion) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            if (request.RequestUri is not null)
            {
                var uriBuilder = new UriBuilder(request.RequestUri);
                var query = HttpUtility.ParseQueryString(uriBuilder.Query);
                query["api-version"] = apiVersion;
                uriBuilder.Query = query.ToString();
                request.RequestUri = uriBuilder.Uri;
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
