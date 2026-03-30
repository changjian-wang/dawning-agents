# Dawning.Agents 示例

本目录包含 Dawning.Agents 框架的示例项目，按功能主题组织。

## 项目结构

```
samples/
├── Dawning.Agents.Samples.Common/        # 共享基础设施
├── Dawning.Agents.Samples.GettingStarted/# 入门示例
├── Dawning.Agents.Samples.Memory/        # 记忆系统示例
├── Dawning.Agents.Samples.RAG/           # RAG/向量检索示例
└── Dawning.Agents.Samples.Enterprise/    # 企业级功能示例
```

## 快速开始

### 1. 准备配置

复制配置模板并填入你的配置：

```bash
cd samples/Dawning.Agents.Samples.GettingStarted
cp appsettings.example.yml appsettings.yml
# 编辑 appsettings.yml 填入你的配置
```

或者使用环境变量（无需创建配置文件）：

```powershell
# PowerShell
$env:LLM__ProviderType = "Ollama"
$env:LLM__Model = "qwen2.5:0.5b"
$env:LLM__Endpoint = "http://localhost:11434"
dotnet run
```

### 2. 运行示例

```bash
# 入门示例
cd samples/Dawning.Agents.Samples.GettingStarted
dotnet run

# Memory 示例
cd samples/Dawning.Agents.Samples.Memory
dotnet run

# RAG 示例
cd samples/Dawning.Agents.Samples.RAG
dotnet run

# Enterprise 示例
cd samples/Dawning.Agents.Samples.Enterprise
dotnet run
```

## 示例说明

### GettingStarted - 入门示例

适合新手快速上手，包含：

- **HelloAgent** - 你的第一个 Agent
- **SimpleChat** - 基础 LLM 对话
- **ToolUsage** - 工具调用演示

```bash
cd Dawning.Agents.Samples.GettingStarted
dotnet run
```

### Memory - 记忆系统

展示 5 种记忆策略：

- **BufferMemory** - 完整历史
- **WindowMemory** - 滑动窗口
- **SummaryMemory** - 自动摘要
- **AdaptiveMemory** - 自适应策略
- **VectorMemory** - 语义检索

```bash
cd Dawning.Agents.Samples.Memory
dotnet run
```

### RAG - 检索增强生成

向量存储和语义缓存：

- **VectorStore** - 文档向量化存储
- **SemanticCache** - 语义相似度缓存
- **KnowledgeBase** - 知识库问答

```bash
cd Dawning.Agents.Samples.RAG
dotnet run
```

### Enterprise - 企业级功能

生产环境必备：

- **Safety** - 安全护栏、内容过滤
- **HumanLoop** - 人机协作、审批流程
- **Scaling** - 熔断器、负载均衡
- **Orchestration** - 多 Agent 编排

```bash
cd Dawning.Agents.Samples.Enterprise
dotnet run
```

## LLM 配置选项

### Ollama（本地，推荐入门）

```yaml
# appsettings.yml
LLM:
  ProviderType: Ollama
  Model: qwen2.5:0.5b
  Endpoint: http://localhost:11434
```

**前置条件：**

```bash
# 安装 Ollama
# macOS: brew install ollama
# Windows: 从 https://ollama.ai 下载

# 拉取模型
ollama pull qwen2.5:0.5b
```

### OpenAI

```yaml
# appsettings.yml
LLM:
  ProviderType: OpenAI
  Model: gpt-4o-mini
  ApiKey: sk-your-api-key
```

### Azure OpenAI

```yaml
# appsettings.yml
LLM:
  ProviderType: AzureOpenAI
  Model: your-deployment-name
  Endpoint: https://your-resource.openai.azure.com/
  ApiKey: your-api-key
```

## 环境变量覆盖

无需修改配置文件，通过环境变量快速切换：

```powershell
# PowerShell
$env:LLM__ProviderType = "Ollama"
$env:LLM__Model = "qwen2.5:0.5b"
$env:LLM__Endpoint = "http://localhost:11434"
dotnet run
```

```bash
# Bash/Zsh
export LLM__ProviderType=Ollama
export LLM__Model=qwen2.5:0.5b
export LLM__Endpoint=http://localhost:11434
dotnet run
```

## 推荐模型

| 提供者 | 模型 | 说明 |
|--------|------|------|
| Ollama | `qwen2.5:0.5b` | 轻量级，适合测试 |
| Ollama | `qwen2.5:7b` | 平衡性能和质量 |
| Ollama | `llama3.2:3b` | Meta 开源模型 |
| OpenAI | `gpt-4o-mini` | 性价比高 |
| OpenAI | `gpt-4o` | 最强大 |

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

### appsettings.yml 被 gitignore

这是正常的，`appsettings.yml` 包含 API Key 等敏感信息，不应提交到 Git。

使用 `appsettings.example.yml` 作为模板创建你自己的配置。

## 更多资源

- [快速入门指南](../docs/QUICKSTART.md)
- [API 参考文档](../docs/API_REFERENCE.md)
- [完整文档](../docs/index.md)
