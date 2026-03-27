using System.ClientModel;
using System.Runtime.CompilerServices;
using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI.Chat;

namespace Dawning.Agents.Azure;

/// <summary>
/// Azure OpenAI / Azure AI Foundry provider implementation.
/// Supports models deployed on Azure OpenAI Service and Azure AI Foundry.
/// </summary>
public class AzureOpenAIProvider : OpenAIProviderBase
{
    /// <inheritdoc />
    public override string Name => "AzureOpenAI";

    /// <inheritdoc />
    protected override string ModelIdentifier { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureOpenAIProvider"/> class.
    /// </summary>
    /// <param name="endpoint">The Azure OpenAI endpoint, e.g. <c>https://your-resource.openai.azure.com/</c>.</param>
    /// <param name="apiKey">The Azure OpenAI API key.</param>
    /// <param name="deploymentName">The model deployment name.</param>
    /// <param name="logger">The logger instance.</param>
    public AzureOpenAIProvider(
        string endpoint,
        string apiKey,
        string deploymentName,
        ILogger<AzureOpenAIProvider>? logger = null
    )
        : base(
            CreateChatClient(endpoint, apiKey, deploymentName),
            logger ?? NullLogger<AzureOpenAIProvider>.Instance
        )
    {
        ModelIdentifier = deploymentName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureOpenAIProvider"/> class using Azure AD authentication.
    /// </summary>
    /// <param name="endpoint">The Azure OpenAI endpoint URL.</param>
    /// <param name="credential">The Azure credential (e.g. DefaultAzureCredential).</param>
    /// <param name="deploymentName">The model deployment name.</param>
    /// <param name="logger">The logger instance.</param>
    public AzureOpenAIProvider(
        string endpoint,
        TokenCredential credential,
        string deploymentName,
        ILogger<AzureOpenAIProvider>? logger = null
    )
        : base(
            CreateChatClient(endpoint, credential, deploymentName),
            logger ?? NullLogger<AzureOpenAIProvider>.Instance
        )
    {
        ModelIdentifier = deploymentName;
    }

    private static ChatClient CreateChatClient(
        string endpoint,
        string apiKey,
        string deploymentName
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentName);
        ValidateEndpoint(endpoint);

        var client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        return client.GetChatClient(deploymentName);
    }

    private static ChatClient CreateChatClient(
        string endpoint,
        TokenCredential credential,
        string deploymentName
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentName);
        ValidateEndpoint(endpoint);

        var client = new AzureOpenAIClient(new Uri(endpoint), credential);
        return client.GetChatClient(deploymentName);
    }

    private static void ValidateEndpoint(string endpoint)
    {
        if (
            !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
        )
        {
            throw new ArgumentException(
                "Azure OpenAI endpoint must be a valid HTTPS URL",
                nameof(endpoint)
            );
        }
    }
}
