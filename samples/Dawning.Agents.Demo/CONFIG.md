# Demo 配置说明

复制 `appsettings.example.json` 为 `appsettings.json`，然后根据需要修改配置。

## LLM 配置

| 字段 | 说明 |
|------|------|
| `ProviderType` | 提供者类型：`Ollama`、`OpenAI`、`AzureOpenAI` |
| `Model` | 模型名称或部署名称 |
| `Endpoint` | API 端点（Ollama/Azure OpenAI 需要） |
| `ApiKey` | API 密钥（OpenAI/Azure OpenAI 需要） |

## 配置示例

### Ollama（本地）

```json
{
  "LLM": {
    "ProviderType": "Ollama",
    "Model": "qwen2.5:7b",
    "Endpoint": "http://localhost:11434"
  }
}
```

### OpenAI

```json
{
  "LLM": {
    "ProviderType": "OpenAI",
    "Model": "gpt-4o",
    "ApiKey": "sk-xxx"
  }
}
```

### Azure OpenAI

```json
{
  "LLM": {
    "ProviderType": "AzureOpenAI",
    "Model": "gpt-4.1-mini",
    "Endpoint": "https://<your-resource>.openai.azure.com/",
    "ApiKey": "<your-key>"
  }
}
```

## 环境变量

也可以通过环境变量配置（优先级低于配置文件）：

```bash
# Azure OpenAI
export AZURE_OPENAI_ENDPOINT="https://xxx.openai.azure.com/"
export AZURE_OPENAI_API_KEY="your-key"
export AZURE_OPENAI_DEPLOYMENT="gpt-4.1-mini"

# OpenAI
export OPENAI_API_KEY="sk-xxx"
export OPENAI_MODEL="gpt-4o"
```
