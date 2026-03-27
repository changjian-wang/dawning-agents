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
/// </summary>
public class OpenAIProvider : OpenAIProviderBase
{
    /// <inheritdoc />
    public override string Name => "OpenAI";

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
        : base(CreateChatClient(apiKey, model), logger ?? NullLogger<OpenAIProvider>.Instance)
    {
        ModelIdentifier = model;
    }

    private static ChatClient CreateChatClient(string apiKey, string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var client = new OpenAIClient(apiKey);
        return client.GetChatClient(model);
    }
}
