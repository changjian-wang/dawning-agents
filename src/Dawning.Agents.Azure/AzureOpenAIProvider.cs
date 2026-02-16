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
/// Azure OpenAI / Azure AI Foundry 提供者实现
/// 支持 Azure OpenAI Service 和 Azure AI Foundry 部署的模型
/// </summary>
public class AzureOpenAIProvider : OpenAIProviderBase
{
    /// <inheritdoc />
    public override string Name => "AzureOpenAI";

    /// <inheritdoc />
    protected override string ModelIdentifier { get; }

    /// <summary>
    /// 创建 Azure OpenAI 提供者
    /// </summary>
    /// <param name="endpoint">Azure OpenAI 端点，如 https://your-resource.openai.azure.com/</param>
    /// <param name="apiKey">Azure OpenAI API Key</param>
    /// <param name="deploymentName">模型部署名称</param>
    /// <param name="logger">日志记录器</param>
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
    /// 使用 Azure AD 身份验证创建 Azure OpenAI 提供者
    /// </summary>
    /// <param name="endpoint">Azure OpenAI 端点</param>
    /// <param name="credential">Azure 凭据（如 DefaultAzureCredential）</param>
    /// <param name="deploymentName">模型部署名称</param>
    /// <param name="logger">日志记录器</param>
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

        var client = new AzureOpenAIClient(
            new Uri(endpoint),
            new AzureKeyCredential(apiKey)
        );
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

        var client = new AzureOpenAIClient(new Uri(endpoint), credential);
        return client.GetChatClient(deploymentName);
    }
}
