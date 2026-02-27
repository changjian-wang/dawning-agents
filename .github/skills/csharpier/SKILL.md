---
description: "CSharpier code formatting rules and conventions for Dawning.Agents. Trigger: format, 格式化, csharpier, code style, 代码风格, formatting, 排版"
---

# CSharpier Formatting Skill

## 目标

定义 CSharpier 代码格式化规则，所有 C# 代码必须遵循这些规范。

## 触发条件

- **关键词**：format, 格式化, csharpier, code style, 代码风格, formatting, 排版
- **文件模式**：`*.cs`, `.csharpierrc`, `.editorconfig`
- **用户意图**：格式化代码、检查代码风格、了解格式化规则

## 编排

- **前置**：`run-tests`（测试通过后格式化）
- **后续**：`git-workflow`（格式化后提交）

---

## Core Rules

### 1. Long Parameter Lists — One Per Line

```csharp
// ✅ Correct
public MyService(
    ILLMProvider llmProvider,
    IOptions<MyOptions> options,
    ILogger<MyService>? logger = null
)

// ❌ Wrong — single line too long
public MyService(ILLMProvider llmProvider, IOptions<MyOptions> options, ILogger<MyService>? logger = null)
```

### 2. Collection Initializers — Elements on Separate Lines

```csharp
// ✅ Correct — trailing comma
var messages = new List<ChatMessage>
{
    new("system", systemPrompt),
    new("user", userInput),
};
```

### 3. Method Chaining — Each Call on Its Own Line

```csharp
var result = items
    .Where(x => x.IsActive)
    .OrderBy(x => x.Name)
    .Select(x => x.ToDto())
    .ToList();
```

### 4. If Statements — Always Use Braces

```csharp
// ✅ Always use braces
if (condition)
{
    DoSomething();
}
```

### 5. Long Method Calls — Arguments on Separate Lines

```csharp
await _llmProvider.ChatAsync(
    messages,
    new LLMOptions { Temperature = 0.7f },
    cancellationToken
);
```

### 6-10. Additional Rules

- **Lambda**: multi-line for complex bodies
- **String Interpolation**: break long strings
- **Switch Expressions**: cases on separate lines
- **Attributes**: one per line when multiple
- **Trailing Commas**: always include in multi-line collections

## Running CSharpier

```bash
# Format all
~/.dotnet/tools/csharpier format .

# Check only
~/.dotnet/tools/csharpier --check src/

# Install
dotnet tool install -g csharpier
```

## 验收场景

- **输入**："格式化 src/ 目录下的代码"
- **预期**：agent 运行 `~/.dotnet/tools/csharpier format .`，报告格式化结果
- **上次验证**：2026-02-27
