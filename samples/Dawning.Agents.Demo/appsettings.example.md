# Demo 配置说明

## 快速开始

1. 复制以下配置到 `appsettings.json`
2. 修改对应字段
3. 运行 `dotnet run`

## 配置模板

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
    "Model": "gpt-4o-mini",
    "ApiKey": "sk-your-api-key"
  }
}
```

### Azure OpenAI

```json
{
  "LLM": {
    "ProviderType": "AzureOpenAI",
    "Model": "your-deployment-name",
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-api-key"
  }
}
```

## 环境变量切换

不修改配置文件，通过环境变量临时切换：

```powershell
$env:LLM__ProviderType = "Ollama"
$env:LLM__Model = "phi3:mini"
$env:LLM__Endpoint = "http://localhost:11434"
dotnet run
```

## 命令行选项

```bash
dotnet run                # 运行所有演示
dotnet run -- --agent     # 只运行 Agent 演示
dotnet run -- --chat      # 只运行简单聊天
dotnet run -- -i          # 交互式对话
dotnet run -- --help      # 查看帮助
```
