using System.ClientModel;
using System.Runtime.CompilerServices;
using Dawning.Agents.Abstractions.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI;
using OpenAI.Chat;

namespace Dawning.Agents.OpenAI;

/// <summary>
/// OpenAI API 提供者实现
/// </summary>
public class OpenAIProvider : OpenAIProviderBase
{
    /// <inheritdoc />
    public override string Name => "OpenAI";

    /// <inheritdoc />
    protected override string ModelIdentifier { get; }

    /// <summary>
    /// 创建 OpenAI 提供者
    /// </summary>
    /// <param name="apiKey">OpenAI API Key</param>
    /// <param name="model">模型名称，默认 gpt-4o</param>
    /// <param name="logger">日志记录器</param>
    public OpenAIProvider(
        string apiKey,
        string model = "gpt-4o",
        ILogger<OpenAIProvider>? logger = null
    )
        : base(
            CreateChatClient(apiKey, model),
            logger ?? NullLogger<OpenAIProvider>.Instance
        )
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
