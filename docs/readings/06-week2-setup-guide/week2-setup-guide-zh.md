# Week 2: 开发环境搭建指南

> Week 2 学习资料：环境搭建与项目初始化
> 目标：为 Agent 开发搭建完整的 .NET 开发环境

---

## Day 1-2: 环境搭建

### 1. 安装 .NET 8.0 SDK

#### Windows

```powershell
# 方式 1：使用 winget
winget install Microsoft.DotNet.SDK.8

# 方式 2：从官网下载
# https://dotnet.microsoft.com/download/dotnet/8.0

# 验证安装
dotnet --version
# 预期输出：8.0.x
```

#### 验证安装

```powershell
# 检查 SDK 版本
dotnet --list-sdks

# 检查运行时版本
dotnet --list-runtimes

# 测试创建简单项目
dotnet new console -n HelloWorld
cd HelloWorld
dotnet run
```

### 2. 安装 Visual Studio 2022 / VS Code

#### Visual Studio 2022（推荐用于 C#）

```powershell
# 使用 winget
winget install Microsoft.VisualStudio.2022.Community

# 需要的工作负载：
# - .NET 桌面开发
# - ASP.NET 和 Web 开发
```

**VS 2022 必备扩展：**
- GitHub Copilot
- ReSharper（可选，付费）
- CodeMaid

#### VS Code（轻量级替代方案）

```powershell
# 安装 VS Code
winget install Microsoft.VisualStudioCode

# 安装 C# 扩展
code --install-extension ms-dotnettools.csharp
code --install-extension ms-dotnettools.csdevkit
```

**推荐的 VS Code 扩展：**

| 扩展 | 用途 |
|------|------|
| C# Dev Kit | 完整的 C# 开发支持 |
| .NET Install Tool | .NET SDK 管理 |
| GitHub Copilot | AI 代码辅助 |
| REST Client | API 测试 |
| Thunder Client | API 测试 GUI |
| GitLens | Git 可视化 |

### 3. 安装 Python 3.11+（用于参考学习）

```powershell
# 使用 winget
winget install Python.Python.3.11

# 验证安装
python --version
pip --version

# 安装学习用的包
pip install langchain openai autogen-agentchat
```

### 4. 配置 Git 环境

```powershell
# 安装 Git
winget install Git.Git

# 配置用户信息
git config --global user.name "你的名字"
git config --global user.email "your.email@example.com"

# 配置默认分支名
git config --global init.defaultBranch main

# 配置行尾处理（Windows）
git config --global core.autocrlf true

# 验证配置
git config --list
```

### 5. 获取 API Key

#### OpenAI API Key

1. 访问 https://platform.openai.com/
2. 注册 / 登录
3. 进入 API Keys 页面
4. 创建新的 secret key
5. 安全保存（绝不要提交到 git！）

#### Azure OpenAI（企业选项）

1. 创建 Azure 订阅
2. 申请 Azure OpenAI 访问权限
3. 创建 Azure OpenAI 资源
4. 部署模型（gpt-4, gpt-35-turbo）
5. 获取 endpoint 和 API key

#### 安全存储 API Key

```powershell
# 方式 1：环境变量（会话级）
$env:OPENAI_API_KEY = "sk-..."
$env:AZURE_OPENAI_API_KEY = "..."
$env:AZURE_OPENAI_ENDPOINT = "https://your-resource.openai.azure.com/"

# 方式 2：用户密钥（开发用）
dotnet user-secrets init
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
dotnet user-secrets set "AzureOpenAI:ApiKey" "..."
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://..."
```

---

## Day 3-4: 项目初始化

### 1. 创建解决方案结构

```powershell
# 进入项目文件夹
cd C:\github

# 克隆仓库（如果还没有）
git clone https://github.com/changjian-wang/dawning-agents.git
cd dawning-agents

# 创建解决方案
dotnet new sln -n DawningAgents

# 创建核心类库项目
dotnet new classlib -n DawningAgents.Core -o src/DawningAgents.Core
dotnet sln add src/DawningAgents.Core/DawningAgents.Core.csproj

# 创建测试项目
dotnet new xunit -n DawningAgents.Tests -o tests/DawningAgents.Tests
dotnet sln add tests/DawningAgents.Tests/DawningAgents.Tests.csproj

# 添加项目引用
dotnet add tests/DawningAgents.Tests/DawningAgents.Tests.csproj reference src/DawningAgents.Core/DawningAgents.Core.csproj

# 构建验证
dotnet build
```

### 2. 配置 NuGet 包

#### 核心项目包

```powershell
cd src/DawningAgents.Core

# OpenAI SDK
dotnet add package Azure.AI.OpenAI --version 2.0.0
dotnet add package OpenAI --version 2.0.0

# Semantic Kernel（可选，参考用）
dotnet add package Microsoft.SemanticKernel --version 1.25.0

# JSON 处理
dotnet add package System.Text.Json

# 日志
dotnet add package Microsoft.Extensions.Logging.Abstractions

# HTTP
dotnet add package Microsoft.Extensions.Http
```

#### 测试项目包

```powershell
cd tests/DawningAgents.Tests

# 测试框架
dotnet add package xunit
dotnet add package xunit.runner.visualstudio
dotnet add package Microsoft.NET.Test.Sdk
dotnet add package Moq
dotnet add package FluentAssertions
```

### 3. 设置代码规范

#### 创建 .editorconfig

在根目录创建 `.editorconfig`：

```ini
# dawning-agents 的 EditorConfig
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = crlf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.cs]
# C# 特定设置
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = true:suggestion

# 命名空间偏好
csharp_style_namespace_declarations = file_scoped:warning

# 表达式主体成员
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
csharp_style_expression_bodied_properties = true:suggestion

# 模式匹配
csharp_style_pattern_matching_over_is_with_cast_check = true:suggestion
csharp_style_pattern_matching_over_as_with_null_check = true:suggestion

# 空检查
csharp_style_throw_expression = true:suggestion
csharp_style_conditional_delegate_call = true:suggestion

# 代码块偏好
csharp_prefer_braces = true:warning

# Using 指令
dotnet_sort_system_directives_first = true
dotnet_separate_import_directive_groups = false

# 命名约定
dotnet_naming_rule.public_members_should_be_pascal_case.severity = warning
dotnet_naming_rule.public_members_should_be_pascal_case.symbols = public_symbols
dotnet_naming_rule.public_members_should_be_pascal_case.style = pascal_case

dotnet_naming_symbols.public_symbols.applicable_kinds = property,method,field,event,delegate
dotnet_naming_symbols.public_symbols.applicable_accessibilities = public

dotnet_naming_style.pascal_case.capitalization = pascal_case

# 私有字段使用下划线前缀
dotnet_naming_rule.private_fields_should_be_camel_case.severity = warning
dotnet_naming_rule.private_fields_should_be_camel_case.symbols = private_fields
dotnet_naming_rule.private_fields_should_be_camel_case.style = camel_case_underscore

dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private

dotnet_naming_style.camel_case_underscore.required_prefix = _
dotnet_naming_style.camel_case_underscore.capitalization = camel_case

[*.md]
trim_trailing_whitespace = false

[*.{json,yml,yaml}]
indent_size = 2
```

#### 创建 Directory.Build.props

在根目录创建 `Directory.Build.props`：

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>

  <PropertyGroup>
    <Authors>changjian-wang</Authors>
    <Company>dawning-agents</Company>
    <RepositoryUrl>https://github.com/changjian-wang/dawning-agents</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
  </PropertyGroup>
</Project>
```

### 4. 配置 GitHub Actions CI/CD

创建 `.github/workflows/build.yml`：

```yaml
name: Build and Test

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --no-restore --configuration Release

    - name: Test
      run: dotnet test --no-build --configuration Release --verbosity normal --collect:"XPlat Code Coverage"

    - name: Upload coverage reports
      uses: codecov/codecov-action@v3
      with:
        files: '**/coverage.cobertura.xml'
        fail_ci_if_error: false
```

---

## Day 5-7: LLM API 调用实践

### 1. 创建 LLM Provider 接口

创建 `src/DawningAgents.Core/LLM/ILLMProvider.cs`：

```csharp
namespace DawningAgents.Core.LLM;

/// <summary>
/// 表示对话中的一条消息
/// </summary>
public record ChatMessage(string Role, string Content);

/// <summary>
/// 聊天完成请求的选项
/// </summary>
public record ChatCompletionOptions
{
    public float Temperature { get; init; } = 0.7f;
    public int MaxTokens { get; init; } = 1000;
    public string? SystemPrompt { get; init; }
}

/// <summary>
/// 聊天完成请求的响应
/// </summary>
public record ChatCompletionResponse
{
    public required string Content { get; init; }
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public int TotalTokens => PromptTokens + CompletionTokens;
    public string? FinishReason { get; init; }
}

/// <summary>
/// LLM 提供者接口（OpenAI、Azure OpenAI 等）
/// </summary>
public interface ILLMProvider
{
    /// <summary>
    /// 提供者名称（如 "OpenAI"、"AzureOpenAI"）
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 发送聊天完成请求
    /// </summary>
    Task<ChatCompletionResponse> ChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式聊天完成响应
    /// </summary>
    IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

### 2. 实现 OpenAI Provider

创建 `src/DawningAgents.Core/LLM/OpenAIProvider.cs`：

```csharp
using System.ClientModel;
using System.Runtime.CompilerServices;
using OpenAI;
using OpenAI.Chat;

namespace DawningAgents.Core.LLM;

/// <summary>
/// OpenAI API 提供者实现
/// </summary>
public class OpenAIProvider : ILLMProvider
{
    private readonly ChatClient _chatClient;
    private readonly string _model;

    public string Name => "OpenAI";

    public OpenAIProvider(string apiKey, string model = "gpt-4o")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var client = new OpenAIClient(apiKey);
        _chatClient = client.GetChatClient(model);
        _model = model;
    }

    public async Task<ChatCompletionResponse> ChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ChatCompletionOptions();

        var chatMessages = BuildMessages(messages, options.SystemPrompt);
        var requestOptions = BuildRequestOptions(options);

        var response = await _chatClient.CompleteChatAsync(
            chatMessages,
            requestOptions,
            cancellationToken);

        var completion = response.Value;

        return new ChatCompletionResponse
        {
            Content = completion.Content[0].Text ?? string.Empty,
            PromptTokens = completion.Usage.InputTokenCount,
            CompletionTokens = completion.Usage.OutputTokenCount,
            FinishReason = completion.FinishReason.ToString()
        };
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new ChatCompletionOptions();

        var chatMessages = BuildMessages(messages, options.SystemPrompt);
        var requestOptions = BuildRequestOptions(options);

        await foreach (var update in _chatClient.CompleteChatStreamingAsync(
            chatMessages,
            requestOptions,
            cancellationToken))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    yield return part.Text;
                }
            }
        }
    }

    private static List<OpenAI.Chat.ChatMessage> BuildMessages(
        IEnumerable<ChatMessage> messages,
        string? systemPrompt)
    {
        var result = new List<OpenAI.Chat.ChatMessage>();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            result.Add(new SystemChatMessage(systemPrompt));
        }

        foreach (var msg in messages)
        {
            result.Add(msg.Role.ToLowerInvariant() switch
            {
                "user" => new UserChatMessage(msg.Content),
                "assistant" => new AssistantChatMessage(msg.Content),
                "system" => new SystemChatMessage(msg.Content),
                _ => throw new ArgumentException($"未知角色: {msg.Role}")
            });
        }

        return result;
    }

    private static ChatCompletionOptions BuildRequestOptions(ChatCompletionOptions options)
    {
        return new ChatCompletionOptions
        {
            Temperature = options.Temperature,
            MaxOutputTokenCount = options.MaxTokens
        };
    }
}
```

### 3. 实现 Azure OpenAI Provider

创建 `src/DawningAgents.Core/LLM/AzureOpenAIProvider.cs`：

```csharp
using System.ClientModel;
using System.Runtime.CompilerServices;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace DawningAgents.Core.LLM;

/// <summary>
/// Azure OpenAI API 提供者实现
/// </summary>
public class AzureOpenAIProvider : ILLMProvider
{
    private readonly ChatClient _chatClient;
    private readonly string _deploymentName;

    public string Name => "AzureOpenAI";

    public AzureOpenAIProvider(string endpoint, string apiKey, string deploymentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentName);

        var client = new AzureOpenAIClient(
            new Uri(endpoint),
            new AzureKeyCredential(apiKey));

        _chatClient = client.GetChatClient(deploymentName);
        _deploymentName = deploymentName;
    }

    public async Task<ChatCompletionResponse> ChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ChatCompletionOptions();

        var chatMessages = BuildMessages(messages, options.SystemPrompt);
        var requestOptions = BuildRequestOptions(options);

        var response = await _chatClient.CompleteChatAsync(
            chatMessages,
            requestOptions,
            cancellationToken);

        var completion = response.Value;

        return new ChatCompletionResponse
        {
            Content = completion.Content[0].Text ?? string.Empty,
            PromptTokens = completion.Usage.InputTokenCount,
            CompletionTokens = completion.Usage.OutputTokenCount,
            FinishReason = completion.FinishReason.ToString()
        };
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new ChatCompletionOptions();

        var chatMessages = BuildMessages(messages, options.SystemPrompt);
        var requestOptions = BuildRequestOptions(options);

        await foreach (var update in _chatClient.CompleteChatStreamingAsync(
            chatMessages,
            requestOptions,
            cancellationToken))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    yield return part.Text;
                }
            }
        }
    }

    private static List<OpenAI.Chat.ChatMessage> BuildMessages(
        IEnumerable<ChatMessage> messages,
        string? systemPrompt)
    {
        var result = new List<OpenAI.Chat.ChatMessage>();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            result.Add(new SystemChatMessage(systemPrompt));
        }

        foreach (var msg in messages)
        {
            result.Add(msg.Role.ToLowerInvariant() switch
            {
                "user" => new UserChatMessage(msg.Content),
                "assistant" => new AssistantChatMessage(msg.Content),
                "system" => new SystemChatMessage(msg.Content),
                _ => throw new ArgumentException($"未知角色: {msg.Role}")
            });
        }

        return result;
    }

    private static ChatCompletionOptions BuildRequestOptions(ChatCompletionOptions options)
    {
        return new ChatCompletionOptions
        {
            Temperature = options.Temperature,
            MaxOutputTokenCount = options.MaxTokens
        };
    }
}
```

### 4. 创建简单控制台演示

创建 `src/DawningAgents.Demo/Program.cs`：

```csharp
using DawningAgents.Core.LLM;

Console.WriteLine("=== DawningAgents LLM 演示 ===\n");

// 从环境变量获取 API key
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("请设置 OPENAI_API_KEY 环境变量");
    return;
}

// 创建提供者
ILLMProvider provider = new OpenAIProvider(apiKey, "gpt-4o-mini");

// 简单聊天
Console.WriteLine("1. 简单聊天：");
var response = await provider.ChatAsync(
    [new ChatMessage("user", "用两句话解释什么是 AI Agent？")],
    new ChatCompletionOptions { MaxTokens = 100 });

Console.WriteLine($"回复：{response.Content}");
Console.WriteLine($"Token 数：{response.TotalTokens}\n");

// 流式聊天
Console.WriteLine("2. 流式聊天：");
Console.Write("回复：");

await foreach (var chunk in provider.ChatStreamAsync(
    [new ChatMessage("user", "慢慢从 1 数到 5。")],
    new ChatCompletionOptions { MaxTokens = 100 }))
{
    Console.Write(chunk);
}

Console.WriteLine("\n\n3. 对话：");

var messages = new List<ChatMessage>();
var systemPrompt = "你是一个名叫 Dawn 的 AI 助手。回答要简洁。";

while (true)
{
    Console.Write("\n你：");
    var input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "quit")
        break;

    messages.Add(new ChatMessage("user", input));

    var result = await provider.ChatAsync(
        messages,
        new ChatCompletionOptions { SystemPrompt = systemPrompt });

    Console.WriteLine($"Dawn：{result.Content}");

    messages.Add(new ChatMessage("assistant", result.Content));
}

Console.WriteLine("\n再见！");
```

### 5. 创建单元测试

创建 `tests/DawningAgents.Tests/LLM/OpenAIProviderTests.cs`：

```csharp
using DawningAgents.Core.LLM;
using FluentAssertions;

namespace DawningAgents.Tests.LLM;

public class OpenAIProviderTests
{
    [Fact]
    public void Constructor_WithNullApiKey_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => new OpenAIProvider(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyApiKey_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => new OpenAIProvider("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Name_ReturnsOpenAI()
    {
        // Arrange
        var provider = new OpenAIProvider("fake-key");

        // Act & Assert
        provider.Name.Should().Be("OpenAI");
    }

    [Fact]
    public void ChatCompletionOptions_HasDefaultValues()
    {
        // Arrange
        var options = new ChatCompletionOptions();

        // Assert
        options.Temperature.Should().Be(0.7f);
        options.MaxTokens.Should().Be(1000);
        options.SystemPrompt.Should().BeNull();
    }

    [Fact]
    public void ChatMessage_RecordEquality()
    {
        // Arrange
        var msg1 = new ChatMessage("user", "Hello");
        var msg2 = new ChatMessage("user", "Hello");

        // Assert
        msg1.Should().Be(msg2);
    }

    [Fact]
    public void ChatCompletionResponse_CalculatesTotalTokens()
    {
        // Arrange
        var response = new ChatCompletionResponse
        {
            Content = "测试",
            PromptTokens = 10,
            CompletionTokens = 20
        };

        // Assert
        response.TotalTokens.Should().Be(30);
    }
}
```

---

## 最终项目结构

Week 2 结束后，项目结构应该是：

```
dawning-agents/
├── .github/
│   └── workflows/
│       └── build.yml
├── src/
│   ├── DawningAgents.Core/
│   │   ├── LLM/
│   │   │   ├── ILLMProvider.cs
│   │   │   ├── OpenAIProvider.cs
│   │   │   └── AzureOpenAIProvider.cs
│   │   └── DawningAgents.Core.csproj
│   └── DawningAgents.Demo/
│       ├── Program.cs
│       └── DawningAgents.Demo.csproj
├── tests/
│   └── DawningAgents.Tests/
│       ├── LLM/
│       │   └── OpenAIProviderTests.cs
│       └── DawningAgents.Tests.csproj
├── docs/
│   └── readings/
│       └── ...
├── .editorconfig
├── .gitignore
├── Directory.Build.props
├── DawningAgents.sln
├── LEARNING_PLAN.md
└── LICENSE
```

---

## 验证清单

- [ ] .NET 8.0 SDK 已安装并验证
- [ ] IDE（VS 2022 或 VS Code）已配置扩展
- [ ] Git 已配置用户信息
- [ ] API key 已获取并安全存储
- [ ] 解决方案结构已创建
- [ ] NuGet 包已添加
- [ ] 代码规范（.editorconfig）已配置
- [ ] CI/CD 流水线已配置
- [ ] LLM 提供者已实现
- [ ] 演示控制台应用可运行
- [ ] 单元测试通过

---

## 下一步

完成 Week 2 后，你将拥有：
- 可用的开发环境
- 基本的项目结构
- LLM 提供者抽象
- CI/CD 流水线

Week 3 将专注于实现核心 Agent 循环和 ReAct 模式！
