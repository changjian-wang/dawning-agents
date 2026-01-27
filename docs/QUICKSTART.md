# 🚀 Dawning.Agents 快速入门

> 5 分钟内运行你的第一个 Agent

---

## 📋 前置条件

- .NET 10.0 SDK
- Ollama (本地 LLM)

### 安装 Ollama

```bash
# Windows (winget)
winget install Ollama.Ollama

# macOS
brew install ollama

# Linux
curl -fsSL https://ollama.com/install.sh | sh
```

### 启动 Ollama 并下载模型

```bash
# 启动服务
ollama serve

# 下载模型 (另一个终端)
ollama pull qwen2.5:0.5b
```

---

## 🎯 最简示例

### 1. 创建项目

```bash
dotnet new console -n MyAgent
cd MyAgent
dotnet add package Dawning.Agents.Core
```

### 2. 配置 appsettings.json

```json
{
  "LLM": {
    "ProviderType": "Ollama",
    "Model": "qwen2.5:0.5b",
    "Endpoint": "http://localhost:11434"
  }
}
```

### 3. 编写 Program.cs

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Core.LLM;
using Dawning.Agents.Core.Agent;
using Dawning.Agents.Core.Tools;

var builder = Host.CreateApplicationBuilder(args);

// 添加配置
builder.Configuration.AddJsonFile("appsettings.json");

// 注册服务
builder.Services.AddLLMProvider(builder.Configuration);
builder.Services.AddBuiltInTools();
builder.Services.AddReActAgent(options =>
{
    options.Name = "Assistant";
    options.Instructions = "你是一个智能助手，可以帮助用户完成各种任务。";
});

var host = builder.Build();
var agent = host.Services.GetRequiredService<IAgent>();

// 运行 Agent
Console.WriteLine("🤖 Agent 已启动，输入 'exit' 退出\n");

while (true)
{
    Console.Write("You: ");
    var input = Console.ReadLine();
    
    if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "exit")
        break;
    
    var response = await agent.RunAsync(input);
    Console.WriteLine($"\nAgent: {response.FinalAnswer}\n");
}
```

### 4. 运行

```bash
dotnet run
```

---

## 📖 进阶示例

### 使用 Memory

```csharp
builder.Services.AddWindowMemory(windowSize: 10);
```

### 使用自定义工具

```csharp
public class MyTools
{
    [FunctionTool("获取当前天气")]
    public string GetWeather(string city)
    {
        return $"{city}的天气: 晴天, 25°C";
    }
}

// 注册
builder.Services.AddToolsFromType<MyTools>();
```

### 使用多 Agent

```csharp
var orchestrator = new SequentialOrchestrator("Pipeline")
    .AddAgent(extractorAgent)
    .AddAgent(analyzerAgent);

var result = await orchestrator.ExecuteAsync("分析这份报告");
```

### 启用安全护栏

```csharp
builder.Services.AddSafetyGuardrails(options =>
{
    options.EnableContentFilter = true;
    options.EnableSensitiveDataFilter = true;
});
```

---

## 🎮 运行 Demo

项目包含完整的示例程序：

```bash
cd samples/Dawning.Agents.Demo
dotnet run
```

### 可用选项

| 选项 | 说明 |
|------|------|
| `--chat` | 简单聊天 |
| `--agent` | ReAct Agent |
| `--stream` | 流式输出 |
| `-i` | 交互式对话 |
| `-m` | Memory 演示 |
| `-o` | 多 Agent 编排 |
| `-hf` | Handoff 协作 |
| `-hl` | 人机协作 |
| `-ob` | 可观测性 |
| `-sc` | 扩展部署 |

---

## 📚 下一步

1. [API 参考](API_REFERENCE.md) - 了解所有接口和类
2. [学习资源](LEARNING_RESOURCES.md) - 深入学习 Agent 原理
3. [企业路线图](ENTERPRISE_ROADMAP.md) - 了解企业级特性规划

---

## ❓ 常见问题

### Ollama 连接失败

```
确保 Ollama 服务正在运行:
ollama serve
```

### 模型响应慢

```
尝试更小的模型:
ollama pull qwen2.5:0.5b  # 397MB, ~13秒
```

### 内存不足

```
减少上下文窗口:
{
  "LLM": { "MaxTokens": 512 }
}
```

---

> 📌 **获取帮助**: 查看 [CHANGELOG.md](../CHANGELOG.md) 了解最新变更
