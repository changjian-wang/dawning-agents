# Demo 配置说明

## 快速开始

1. 复制 `appsettings.json` 模板（见下方）
2. 根据你的 LLM 提供者修改配置
3. 运行 `dotnet run`

## 配置模板

### Ollama（本地 LLM，推荐）

最简单的方式，无需 API Key：

```json
{
  "LLM": {
    "ProviderType": "Ollama",
    "Model": "qwen2.5:0.5b",
    "Endpoint": "http://localhost:11434"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

**前置条件**：

```bash
# 安装 Ollama
brew install ollama    # macOS
# 或从 https://ollama.ai 下载

# 拉取模型
ollama pull qwen2.5:0.5b
```

### OpenAI

> ⚠️ 需要额外引用 `Dawning.Agents.OpenAI` 包

```json
{
  "LLM": {
    "ProviderType": "OpenAI",
    "Model": "gpt-4o-mini",
    "ApiKey": "sk-your-api-key"
  }
}
```

**代码配置**：

```csharp
// 需要使用 Dawning.Agents.OpenAI 包
services.AddOpenAIProvider("sk-your-api-key", "gpt-4o-mini");
```

### Azure OpenAI

> ⚠️ 需要额外引用 `Dawning.Agents.Azure` 包

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

**代码配置**：

```csharp
// 需要使用 Dawning.Agents.Azure 包
services.AddAzureOpenAIProvider(
    "https://your-resource.openai.azure.com/",
    "your-api-key",
    "your-deployment-name"
);
```

## 环境变量

通过环境变量覆盖配置（无需修改文件）：

```bash
# Bash/Zsh (macOS/Linux)
export LLM__ProviderType=Ollama
export LLM__Model=qwen2.5:0.5b
export LLM__Endpoint=http://localhost:11434
dotnet run

# PowerShell (Windows)
$env:LLM__ProviderType = "Ollama"
$env:LLM__Model = "qwen2.5:0.5b"
$env:LLM__Endpoint = "http://localhost:11434"
dotnet run
```

## 运行模式

Demo 支持 12 种演示模式：

```bash
dotnet run              # 显示交互式菜单

# 命令行参数
dotnet run -- -c        # [1] 简单聊天
dotnet run -- -a        # [2] Agent 演示
dotnet run -- -s        # [3] 流式聊天
dotnet run -- -i        # [4] 交互式对话
dotnet run -- -m        # [5] Memory 系统
dotnet run -- -am       # [6] Agent + Memory
dotnet run -- -pm       # [7] 包管理工具
dotnet run -- -ma       # [8] 多 Agent 编排器
dotnet run -- -ho       # [9] Handoff 协作
dotnet run -- -hl       # [H] 人机协作
dotnet run -- -ob       # [O] 可观测性
dotnet run -- -sc       # [S] 扩展与部署
dotnet run -- --all     # [A] 运行全部 (1-3)
dotnet run -- --help    # 显示帮助
```

## 推荐模型

| 提供者 | 模型 | 说明 |
|--------|------|------|
| Ollama | `qwen2.5:0.5b` | 轻量级，适合测试 |
| Ollama | `qwen2.5:7b` | 平衡性能和质量 |
| Ollama | `llama3.2:3b` | Meta 开源模型 |
| OpenAI | `gpt-4o-mini` | 性价比高 |
| OpenAI | `gpt-4o` | 最强大 |
| Azure | 根据部署名称 | 企业级 |

## 故障排除

### Ollama 连接失败

```bash
# 检查 Ollama 是否运行
curl http://localhost:11434/api/tags

# 如果未运行，启动 Ollama
ollama serve
```

### 模型不存在

```bash
# 列出已安装的模型
ollama list

# 拉取缺失的模型
ollama pull qwen2.5:0.5b
```
