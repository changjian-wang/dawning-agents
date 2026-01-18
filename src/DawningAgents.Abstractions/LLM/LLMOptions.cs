namespace DawningAgents.Abstractions.LLM;

/// <summary>
/// LLM 配置选项
/// 支持通过 appsettings.json、环境变量、用户机密等方式配置
/// </summary>
/// <remarks>
/// appsettings.json 示例:
/// <code>
/// {
///   "LLM": {
///     "ProviderType": "Ollama",
///     "Model": "deepseek-coder:1.3b",
///     "Endpoint": "http://localhost:11434"
///   }
/// }
/// </code>
///
/// 环境变量示例:
/// - LLM__ProviderType=OpenAI
/// - LLM__Model=gpt-4o
/// - LLM__ApiKey=sk-xxx
///
/// 或使用传统环境变量（向后兼容）:
/// - OPENAI_API_KEY, AZURE_OPENAI_ENDPOINT 等
/// </remarks>
public class LLMOptions
{
    /// <summary>配置节名称</summary>
    public const string SectionName = "LLM";

    /// <summary>提供者类型</summary>
    public LLMProviderType ProviderType { get; set; } = LLMProviderType.Ollama;

    /// <summary>模型名称或部署名称</summary>
    public string Model { get; set; } = "deepseek-coder:1.3b";

    /// <summary>API Key（OpenAI/Azure OpenAI）</summary>
    public string? ApiKey { get; set; }

    /// <summary>端点 URL（Ollama/Azure OpenAI）</summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    public void Validate()
    {
        switch (ProviderType)
        {
            case LLMProviderType.OpenAI:
                if (string.IsNullOrWhiteSpace(ApiKey))
                {
                    throw new InvalidOperationException("OpenAI 需要配置 ApiKey");
                }
                break;

            case LLMProviderType.AzureOpenAI:
                if (string.IsNullOrWhiteSpace(Endpoint))
                {
                    throw new InvalidOperationException("Azure OpenAI 需要配置 Endpoint");
                }
                if (string.IsNullOrWhiteSpace(ApiKey))
                {
                    throw new InvalidOperationException("Azure OpenAI 需要配置 ApiKey");
                }
                break;

            case LLMProviderType.Ollama:
                Endpoint ??= "http://localhost:11434";
                break;
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            throw new InvalidOperationException("必须配置 Model");
        }
    }
}
