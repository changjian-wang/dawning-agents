namespace DawningAgents.Abstractions.LLM;

/// <summary>
/// LLM 提供者类型
/// </summary>
public enum LLMProviderType
{
    /// <summary>本地 Ollama 模型</summary>
    Ollama,

    /// <summary>OpenAI API</summary>
    OpenAI,

    /// <summary>Azure OpenAI / Azure AI Foundry</summary>
    AzureOpenAI
}

/// <summary>
/// LLM 配置
/// </summary>
public record LLMConfiguration
{
    /// <summary>提供者类型</summary>
    public LLMProviderType ProviderType { get; init; } = LLMProviderType.Ollama;

    /// <summary>模型名称或部署名称</summary>
    public string Model { get; init; } = "deepseek-coder:6.7b";

    /// <summary>API Key（OpenAI/Azure OpenAI）</summary>
    public string? ApiKey { get; init; }

    /// <summary>端点 URL（Ollama/Azure OpenAI）</summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// 从环境变量创建配置
    /// </summary>
    public static LLMConfiguration FromEnvironment()
    {
        // 优先检查 Azure OpenAI
        var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var azureApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        var azureDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");

        if (!string.IsNullOrEmpty(azureEndpoint) && !string.IsNullOrEmpty(azureApiKey))
        {
            return new LLMConfiguration
            {
                ProviderType = LLMProviderType.AzureOpenAI,
                Endpoint = azureEndpoint,
                ApiKey = azureApiKey,
                Model = azureDeployment ?? "gpt-4o"
            };
        }

        // 检查 OpenAI
        var openaiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var openaiModel = Environment.GetEnvironmentVariable("OPENAI_MODEL");

        if (!string.IsNullOrEmpty(openaiApiKey))
        {
            return new LLMConfiguration
            {
                ProviderType = LLMProviderType.OpenAI,
                ApiKey = openaiApiKey,
                Model = openaiModel ?? "gpt-4o"
            };
        }

        // 默认使用 Ollama
        var ollamaEndpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT");
        var ollamaModel = Environment.GetEnvironmentVariable("OLLAMA_MODEL");

        return new LLMConfiguration
        {
            ProviderType = LLMProviderType.Ollama,
            Endpoint = ollamaEndpoint ?? "http://localhost:11434",
            Model = ollamaModel ?? "deepseek-coder:6.7b"
        };
    }
}
