using System.ClientModel;
using System.Runtime.CompilerServices;
using Dawning.Agents.Abstractions.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI;
using OpenAI.Chat;

namespace Dawning.Agents.OpenAI;

/// <summary>
/// OpenAI API provider implementation.
/// Also serves as the base for OpenAI-compatible providers (DeepSeek, Zhipu, Moonshot, etc.)
/// when an endpoint is specified.
/// </summary>
public class OpenAIProvider : OpenAIProviderBase
{
    private readonly string _providerName;

    /// <inheritdoc />
    public override string Name => _providerName;

    /// <inheritdoc />
    protected override string ModelIdentifier { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIProvider"/> class.
    /// </summary>
    /// <param name="apiKey">The OpenAI API key.</param>
    /// <param name="model">The model name. Defaults to <c>gpt-4o</c>.</param>
    /// <param name="logger">The logger instance.</param>
    public OpenAIProvider(
        string apiKey,
        string model = "gpt-4o",
        ILogger<OpenAIProvider>? logger = null
    )
        : base(CreateChatClient(apiKey, model, null), logger ?? NullLogger<OpenAIProvider>.Instance)
    {
        _providerName = "OpenAI";
        ModelIdentifier = model;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIProvider"/> class with a custom endpoint.
    /// Use this constructor for OpenAI-compatible providers such as DeepSeek, Zhipu, Moonshot, etc.
    /// </summary>
    /// <param name="apiKey">The API key.</param>
    /// <param name="model">The model name (e.g. <c>deepseek-chat</c>).</param>
    /// <param name="endpoint">The base URL of the OpenAI-compatible API (e.g. <c>https://api.deepseek.com</c>).</param>
    /// <param name="providerName">A display name for logging. Defaults to <c>OpenAICompatible</c>.</param>
    /// <param name="logger">The logger instance.</param>
    public OpenAIProvider(
        string apiKey,
        string model,
        string endpoint,
        string providerName = "OpenAICompatible",
        ILogger<OpenAIProvider>? logger = null
    )
        : base(
            CreateChatClient(apiKey, model, endpoint),
            logger ?? NullLogger<OpenAIProvider>.Instance
        )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        _providerName = providerName;
        ModelIdentifier = model;
    }

    private static ChatClient CreateChatClient(string apiKey, string model, string? endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        OpenAIClient client;
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };
            client = new OpenAIClient(new ApiKeyCredential(apiKey), options);
        }
        else
        {
            client = new OpenAIClient(apiKey);
        }

        return client.GetChatClient(model);
    }
}
