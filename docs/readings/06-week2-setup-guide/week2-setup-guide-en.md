# Week 2: Development Environment Setup Guide

> Week 2 Learning Material: Environment Setup & Project Initialization
> Target: Set up complete .NET development environment for Agent development

---

## Day 1-2: Environment Setup

### 1. Install .NET 8.0 SDK

#### Windows

```powershell
# Option 1: Using winget
winget install Microsoft.DotNet.SDK.8

# Option 2: Download from official website
# https://dotnet.microsoft.com/download/dotnet/8.0

# Verify installation
dotnet --version
# Expected: 8.0.x
```

#### Verify Installation

```powershell
# Check SDK version
dotnet --list-sdks

# Check runtime version
dotnet --list-runtimes

# Test with a simple project
dotnet new console -n HelloWorld
cd HelloWorld
dotnet run
```

### 2. Install Visual Studio 2022 / VS Code

#### Visual Studio 2022 (Recommended for C#)

```powershell
# Using winget
winget install Microsoft.VisualStudio.2022.Community

# Required Workloads:
# - .NET desktop development
# - ASP.NET and web development
```

**Essential Extensions for VS 2022:**
- GitHub Copilot
- ReSharper (optional, paid)
- CodeMaid

#### VS Code (Lightweight Alternative)

```powershell
# Install VS Code
winget install Microsoft.VisualStudioCode

# Install C# extension
code --install-extension ms-dotnettools.csharp
code --install-extension ms-dotnettools.csdevkit
```

**Recommended VS Code Extensions:**

| Extension | Purpose |
|-----------|---------|
| C# Dev Kit | Full C# development support |
| .NET Install Tool | .NET SDK management |
| GitHub Copilot | AI code assistance |
| REST Client | API testing |
| Thunder Client | API testing GUI |
| GitLens | Git visualization |

### 3. Install Python 3.11+ (For Reference Learning)

```powershell
# Using winget
winget install Python.Python.3.11

# Verify installation
python --version
pip --version

# Install useful packages for learning
pip install langchain langgraph openai openai-agents

# Microsoft Agent Framework (optional, for reference)
pip install agent-framework
```

### 4. Configure Git Environment

```powershell
# Install Git
winget install Git.Git

# Configure user
git config --global user.name "Your Name"
git config --global user.email "your.email@example.com"

# Configure default branch name
git config --global init.defaultBranch main

# Configure line endings (Windows)
git config --global core.autocrlf true

# Verify configuration
git config --list
```

### 5. Get API Keys

#### OpenAI API Key

1. Go to https://platform.openai.com/
2. Sign up / Log in
3. Navigate to API Keys section
4. Create new secret key
5. Store securely (never commit to git!)

#### Azure OpenAI (Enterprise Option)

1. Create Azure subscription
2. Request Azure OpenAI access
3. Create Azure OpenAI resource
4. Deploy models (gpt-4, gpt-35-turbo)
5. Get endpoint and API key

#### Store API Keys Securely

```powershell
# Option 1: Environment variables (session)
$env:OPENAI_API_KEY = "sk-..."
$env:AZURE_OPENAI_API_KEY = "..."
$env:AZURE_OPENAI_ENDPOINT = "https://your-resource.openai.azure.com/"

# Option 2: User secrets (for development)
dotnet user-secrets init
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
dotnet user-secrets set "AzureOpenAI:ApiKey" "..."
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://..."
```

---

## Day 3-4: Project Initialization

### 1. Create Solution Structure

```powershell
# Navigate to your projects folder
cd C:\github

# Clone the repository (if not already done)
git clone https://github.com/changjian-wang/dawning-agents.git
cd dawning-agents

# Create solution
dotnet new sln -n DawningAgents

# Create core library project
dotnet new classlib -n DawningAgents.Core -o src/DawningAgents.Core
dotnet sln add src/DawningAgents.Core/DawningAgents.Core.csproj

# Create test project
dotnet new xunit -n DawningAgents.Tests -o tests/DawningAgents.Tests
dotnet sln add tests/DawningAgents.Tests/DawningAgents.Tests.csproj

# Add reference from tests to core
dotnet add tests/DawningAgents.Tests/DawningAgents.Tests.csproj reference src/DawningAgents.Core/DawningAgents.Core.csproj

# Build to verify
dotnet build
```

### 2. Configure NuGet Packages

#### Core Project Packages

```powershell
cd src/DawningAgents.Core

# OpenAI SDK
dotnet add package Azure.AI.OpenAI --version 2.0.0
dotnet add package OpenAI --version 2.0.0

# Microsoft Agent Framework (optional, for reference)
dotnet add package Microsoft.Agents.AI --prerelease

# JSON handling
dotnet add package System.Text.Json

# Logging
dotnet add package Microsoft.Extensions.Logging.Abstractions

# HTTP
dotnet add package Microsoft.Extensions.Http
```

#### Test Project Packages

```powershell
cd tests/DawningAgents.Tests

# Testing frameworks
dotnet add package xunit
dotnet add package xunit.runner.visualstudio
dotnet add package Microsoft.NET.Test.Sdk
dotnet add package Moq
dotnet add package FluentAssertions
```

### 3. Setup Code Standards

#### Create .editorconfig

Create `.editorconfig` in the root folder:

```ini
# EditorConfig for dawning-agents
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = crlf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.cs]
# C# specific settings
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = true:suggestion

# Namespace preferences
csharp_style_namespace_declarations = file_scoped:warning

# Expression-bodied members
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
csharp_style_expression_bodied_properties = true:suggestion

# Pattern matching
csharp_style_pattern_matching_over_is_with_cast_check = true:suggestion
csharp_style_pattern_matching_over_as_with_null_check = true:suggestion

# Null checking
csharp_style_throw_expression = true:suggestion
csharp_style_conditional_delegate_call = true:suggestion

# Code block preferences
csharp_prefer_braces = true:warning

# Using directives
dotnet_sort_system_directives_first = true
dotnet_separate_import_directive_groups = false

# Naming conventions
dotnet_naming_rule.public_members_should_be_pascal_case.severity = warning
dotnet_naming_rule.public_members_should_be_pascal_case.symbols = public_symbols
dotnet_naming_rule.public_members_should_be_pascal_case.style = pascal_case

dotnet_naming_symbols.public_symbols.applicable_kinds = property,method,field,event,delegate
dotnet_naming_symbols.public_symbols.applicable_accessibilities = public

dotnet_naming_style.pascal_case.capitalization = pascal_case

# Private fields with underscore prefix
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

#### Create Directory.Build.props

Create `Directory.Build.props` in the root folder:

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

### 4. Configure GitHub Actions CI/CD

Create `.github/workflows/build.yml`:

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

## Day 5-7: LLM API Integration Practice

### 1. Create LLM Provider Interface

Create `src/DawningAgents.Core/LLM/ILLMProvider.cs`:

```csharp
namespace DawningAgents.Core.LLM;

/// <summary>
/// Represents a message in a conversation
/// </summary>
public record ChatMessage(string Role, string Content);

/// <summary>
/// Options for chat completion requests
/// </summary>
public record ChatCompletionOptions
{
    public float Temperature { get; init; } = 0.7f;
    public int MaxTokens { get; init; } = 1000;
    public string? SystemPrompt { get; init; }
}

/// <summary>
/// Response from a chat completion request
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
/// Interface for LLM providers (OpenAI, Azure OpenAI, etc.)
/// </summary>
public interface ILLMProvider
{
    /// <summary>
    /// Provider name (e.g., "OpenAI", "AzureOpenAI")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Send a chat completion request
    /// </summary>
    Task<ChatCompletionResponse> ChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stream a chat completion response
    /// </summary>
    IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

### 2. Implement OpenAI Provider

Create `src/DawningAgents.Core/LLM/OpenAIProvider.cs`:

```csharp
using System.ClientModel;
using System.Runtime.CompilerServices;
using OpenAI;
using OpenAI.Chat;

namespace DawningAgents.Core.LLM;

/// <summary>
/// OpenAI API provider implementation
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
                _ => throw new ArgumentException($"Unknown role: {msg.Role}")
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

### 3. Implement Azure OpenAI Provider

Create `src/DawningAgents.Core/LLM/AzureOpenAIProvider.cs`:

```csharp
using System.ClientModel;
using System.Runtime.CompilerServices;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace DawningAgents.Core.LLM;

/// <summary>
/// Azure OpenAI API provider implementation
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
                _ => throw new ArgumentException($"Unknown role: {msg.Role}")
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

### 4. Create Simple Console Demo

Create `src/DawningAgents.Demo/Program.cs`:

```csharp
using DawningAgents.Core.LLM;

Console.WriteLine("=== DawningAgents LLM Demo ===\n");

// Get API key from environment
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("Please set OPENAI_API_KEY environment variable");
    return;
}

// Create provider
ILLMProvider provider = new OpenAIProvider(apiKey, "gpt-4o-mini");

// Simple chat
Console.WriteLine("1. Simple Chat:");
var response = await provider.ChatAsync(
    [new ChatMessage("user", "What is an AI Agent in 2 sentences?")],
    new ChatCompletionOptions { MaxTokens = 100 });

Console.WriteLine($"Response: {response.Content}");
Console.WriteLine($"Tokens: {response.TotalTokens}\n");

// Streaming chat
Console.WriteLine("2. Streaming Chat:");
Console.Write("Response: ");

await foreach (var chunk in provider.ChatStreamAsync(
    [new ChatMessage("user", "Count from 1 to 5 slowly.")],
    new ChatCompletionOptions { MaxTokens = 100 }))
{
    Console.Write(chunk);
}

Console.WriteLine("\n\n3. Conversation:");

var messages = new List<ChatMessage>();
var systemPrompt = "You are a helpful AI assistant named Dawn. Be concise.";

while (true)
{
    Console.Write("\nYou: ");
    var input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "quit")
        break;

    messages.Add(new ChatMessage("user", input));

    var result = await provider.ChatAsync(
        messages,
        new ChatCompletionOptions { SystemPrompt = systemPrompt });

    Console.WriteLine($"Dawn: {result.Content}");

    messages.Add(new ChatMessage("assistant", result.Content));
}

Console.WriteLine("\nGoodbye!");
```

### 5. Create Unit Tests

Create `tests/DawningAgents.Tests/LLM/OpenAIProviderTests.cs`:

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
            Content = "Test",
            PromptTokens = 10,
            CompletionTokens = 20
        };

        // Assert
        response.TotalTokens.Should().Be(30);
    }
}
```

---

## Final Project Structure

After Week 2, your project should look like:

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

## Verification Checklist

- [ ] .NET 8.0 SDK installed and verified
- [ ] IDE (VS 2022 or VS Code) configured with extensions
- [ ] Git configured with user info
- [ ] API key obtained and stored securely
- [ ] Solution structure created
- [ ] NuGet packages added
- [ ] Code standards (.editorconfig) in place
- [ ] CI/CD pipeline configured
- [ ] LLM providers implemented
- [ ] Demo console app working
- [ ] Unit tests passing

---

## Next Steps

After completing Week 2, you'll have:
- A working development environment
- A basic project structure
- LLM provider abstractions
- CI/CD pipeline

Week 3 will focus on implementing the core Agent loop and ReAct pattern!
