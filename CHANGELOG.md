# Changelog

本文档记录 dawning-agents 项目的所有重要变更，便于在不同会话中快速恢复上下文。

---

## 🖥️ 快速恢复指南（另一台电脑）

### 环境准备

```bash
# 1. 拉取最新代码
git pull

# 2. 确保 Ollama 运行并有模型
ollama serve  # 如果未运行
ollama pull qwen2.5:0.5b

# 3. 运行测试确认环境正常
cd dawning-agents
dotnet test

# 4. 运行 Demo 验证
cd samples/Dawning.Agents.Demo
dotnet run
```

### 当前配置

```json
// samples/Dawning.Agents.Demo/appsettings.json
{
  "LLM": {
    "ProviderType": "Ollama",
    "Model": "qwen2.5:0.5b",  // 快速推理，ReAct 格式兼容好
    "Endpoint": "http://localhost:11434"
  }
}
```

### 模型选择说明

| 模型 | 大小 | 速度 | ReAct 兼容 | 用途 |
|------|------|------|-----------|------|
| qwen2.5:0.5b | 397MB | ~13秒 | ✅ 好 | Agent 推理（当前使用） |
| qwen2.5:7b | 4.7GB | ~165秒 | ✅ 好 | 复杂推理（质量更高） |
| deepseek-coder | 4GB | ~15秒 | ❌ 差 | 代码生成（Week 5 工具） |

### 当前进度

- ✅ Week 2: LLM Provider 完成
- ✅ Week 3: Agent 核心循环完成（63 测试通过）
- ✅ Week 4: Memory 系统完成（150 测试通过）
- ✅ Week 5: Tools/Skills 系统完成（74 测试通过）
- ✅ Week 5.5: Tool Sets 与 Virtual Tools 完成（106 测试通过）
- ✅ Week 6: PackageManagerTool 完成（176 测试通过）
- 🔜 Week 6.5: RAG 集成

### 下一步任务

1. `IVectorStore` 接口设计 - 向量存储
2. `RAGTool` 实现 - 知识库检索
3. 文档分块 (Chunking)

---

## [2026-01-20] Phase 3: Week 6 PackageManagerTool 完成

### 新增的文件

**Abstractions:**
```text
src/Dawning.Agents.Abstractions/Tools/
└── PackageManagerOptions.cs     ← 包管理工具配置
```

**Core:**
```text
src/Dawning.Agents.Core/Tools/BuiltIn/
└── PackageManagerTool.cs        ← 19 个包管理工具方法
```

**Tests:**
```text
tests/Dawning.Agents.Tests/Tools/
└── PackageManagerToolTests.cs   ← 23 个单元测试
```

### 实现的工具方法 (19 个)

| 包管理器 | 方法 | 风险等级 |
|----------|------|----------|
| **Winget** | WingetSearch, WingetShow, WingetList | Low |
| **Winget** | WingetInstall, WingetUninstall | High |
| **Pip** | PipList, PipShow | Low |
| **Pip** | PipInstall, PipUninstall | High |
| **Npm** | NpmSearch, NpmView, NpmList | Low |
| **Npm** | NpmInstall, NpmUninstall | High |
| **Dotnet** | DotnetToolSearch, DotnetToolList | Low |
| **Dotnet** | DotnetToolInstall, DotnetToolUninstall, DotnetToolUpdate | High |

### 安全特性

- **白名单机制**: 只允许安装白名单中的包
- **黑名单机制**: 禁止安装黑名单中的包
- **高风险标记**: 所有安装/卸载操作标记为 `RequiresConfirmation = true`
- **超时控制**: 默认 300 秒超时

### 使用示例

```csharp
// 注册工具
services.AddPackageManagerTools(options =>
{
    options.WhitelistedPackages = ["Git.*", "Microsoft.*"];
    options.BlacklistedPackages = ["*hack*", "*malware*"];
    options.DefaultTimeoutSeconds = 300;
});

// 使用工具
var tool = new PackageManagerTool(options);
await tool.DotnetToolList(global: true);
await tool.WingetSearch("git");
```

### Demo 命令

```bash
dotnet run -- -pm    # 演示 PackageManagerTool
```

---

## [2026-01-20] Phase 2.5: Week 4 Memory 系统完成

### 新增的接口（Abstractions）

```csharp
// 对话消息记录
public record ConversationMessage
{
    public string Id { get; init; }
    public required string Role { get; init; }
    public required string Content { get; init; }
    public DateTime Timestamp { get; init; }
    public int? TokenCount { get; init; }
}

// 对话记忆管理接口
public interface IConversationMemory
{
    Task AddMessageAsync(ConversationMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ChatMessage>> GetContextAsync(int? maxTokens = null, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
    Task<int> GetTokenCountAsync(CancellationToken ct = default);
    int MessageCount { get; }
}

// Token 计数器接口
public interface ITokenCounter
{
    int CountTokens(string text);
    int CountTokens(IEnumerable<ChatMessage> messages);
    string ModelName { get; }
    int MaxContextTokens { get; }
}
```

### 新增的实现类（Core）

| 类 | 描述 |
|---|---|
| `SimpleTokenCounter` | 基于字符估算的 Token 计数器（英文 4 字符/token，中文 1.5 字符/token） |
| `BufferMemory` | 存储所有消息的简单缓冲记忆 |
| `WindowMemory` | 只保留最后 N 条消息的滑动窗口记忆 |
| `SummaryMemory` | 自动摘要旧消息的智能记忆（需要 LLM） |

### DI 扩展方法

```csharp
// 根据配置自动选择 Memory 类型
services.AddMemory(configuration);

// 或直接指定类型
services.AddBufferMemory();
services.AddWindowMemory(windowSize: 10);
services.AddSummaryMemory(maxRecentMessages: 6, summaryThreshold: 10);
services.AddTokenCounter();
```

### 配置选项

```json
{
  "Memory": {
    "Type": "Window",
    "WindowSize": 10,
    "MaxRecentMessages": 6,
    "SummaryThreshold": 10,
    "ModelName": "gpt-4",
    "MaxContextTokens": 8192
  }
}
```

### 测试覆盖

- `SimpleTokenCounterTests` - 10 个测试
- `BufferMemoryTests` - 11 个测试
- `WindowMemoryTests` - 10 个测试
- `SummaryMemoryTests` - 13 个测试

**总计：150 个测试通过**（包括之前的 106 个）

---

## [2026-01-19] Phase 3.5: Week 5.5 Tool Sets 与 Virtual Tools 完成

### 新增的接口（Abstractions）

```csharp
// 工具集 - 将相关工具分组管理
public interface IToolSet
{
    string Name { get; }
    string Description { get; }
    string? Icon { get; }
    IReadOnlyList<ITool> Tools { get; }
    int Count { get; }
    ITool? GetTool(string toolName);
    bool Contains(string toolName);
}

// 虚拟工具 - 延迟加载工具组（参考 GitHub Copilot）
public interface IVirtualTool : ITool
{
    IReadOnlyList<ITool> ExpandedTools { get; }
    bool IsExpanded { get; }
    IToolSet ToolSet { get; }
    void Expand();
    void Collapse();
}

// 智能工具选择器
public interface IToolSelector
{
    Task<IReadOnlyList<ITool>> SelectToolsAsync(
        string query, IReadOnlyList<ITool> availableTools,
        int maxTools = 20, CancellationToken ct = default);
    Task<IReadOnlyList<IToolSet>> SelectToolSetsAsync(...);
}

// 工具审批处理器
public interface IToolApprovalHandler
{
    Task<bool> RequestApprovalAsync(ITool tool, string input, CancellationToken ct = default);
    Task<bool> RequestUrlApprovalAsync(ITool tool, string url, CancellationToken ct = default);
    Task<bool> RequestCommandApprovalAsync(ITool tool, string command, CancellationToken ct = default);
}

// 审批策略枚举
public enum ApprovalStrategy
{
    AlwaysApprove,   // 开发/测试环境
    AlwaysDeny,      // 安全敏感环境
    RiskBased,       // 基于风险等级（推荐）
    Interactive      // 交互式确认
}
```

### 新增的实现（Core）

```
src/Dawning.Agents.Core/
└── Tools/
    ├── ToolSet.cs                  # 工具集实现 ✨ 新
    ├── VirtualTool.cs              # 虚拟工具实现 ✨ 新
    ├── DefaultToolSelector.cs      # 默认工具选择器 ✨ 新
    ├── DefaultToolApprovalHandler.cs # 默认审批处理器 ✨ 新
    └── ToolServiceCollectionExtensions.cs # 扩展 DI 注册方法
```

### IToolRegistry 扩展

```csharp
public interface IToolRegistry
{
    // 原有方法...
    
    // 新增方法
    IReadOnlyList<ITool> GetToolsByCategory(string category);
    IReadOnlyList<string> GetCategories();
    void RegisterToolSet(IToolSet toolSet);
    IToolSet? GetToolSet(string name);
    IReadOnlyList<IToolSet> GetAllToolSets();
    void RegisterVirtualTool(IVirtualTool virtualTool);
    IReadOnlyList<IVirtualTool> GetVirtualTools();
}
```

### DI 注册方式

```csharp
// 注册工具选择器和审批处理器
services.AddToolSelector();  // 默认 keyword-based
services.AddToolApprovalHandler(ApprovalStrategy.RiskBased);

// 注册工具集
services.AddToolSet(new ToolSet("math", "数学工具", mathTools));
services.AddToolSetFrom<MathTool>("math", "数学计算工具集");

// 注册虚拟工具
services.AddVirtualTool(new VirtualTool(toolSet));
services.AddVirtualToolFrom<GitTool>("git", "Git 版本控制工具集", "🔧");
```

### DefaultToolApprovalHandler 特性

- **信任的 URL**: localhost, github.com, microsoft.com, azure.com, nuget.org
- **安全的命令**: ls, dir, pwd, git status, dotnet --version 等
- **危险的命令**: rm -rf /, format, shutdown, del /s /q 等（自动拒绝）
- **自动批准列表**: 可添加自定义 URL 和命令

### 测试统计

| 测试文件 | 测试数量 | 说明 |
|----------|----------|------|
| ToolSetTests.cs | 15 | ToolSet 和 VirtualTool |
| ToolSelectorTests.cs | 7 | DefaultToolSelector |
| ToolApprovalHandlerTests.cs | 12 | DefaultToolApprovalHandler |
| 原有测试 | 72 | LLM, Agent, Tools |
| **总计** | **106** | |

---

## [2026-01-19] Phase 3: Week 5 Tools/Skills 系统完成

### 新增的文件结构

```
src/Dawning.Agents.Abstractions/
└── Tools/
    ├── ITool.cs                    # 工具核心接口（扩展安全属性）
    ├── IToolRegistry.cs            # 工具注册表接口
    ├── ToolResult.cs               # 执行结果（新增 NeedConfirmation）
    ├── FunctionToolAttribute.cs    # 工具特性（新增安全属性）
    └── ToolRiskLevel.cs            # 风险等级枚举 ✨ 新

src/Dawning.Agents.Core/
└── Tools/
    ├── MethodTool.cs               # 方法工具实现
    ├── ToolRegistry.cs             # 工具注册表实现
    ├── ToolServiceCollectionExtensions.cs
    └── BuiltIn/
        ├── DateTimeTool.cs         # 日期时间工具 (4 methods)
        ├── MathTool.cs             # 数学工具 (8 methods)
        ├── JsonTool.cs             # JSON 工具 (4 methods)
        ├── UtilityTool.cs          # 实用工具 (5 methods)
        ├── FileSystemTool.cs       # 文件系统工具 (13 methods) ✨ 新
        ├── HttpTool.cs             # HTTP 工具 (6 methods) ✨ 新
        ├── ProcessTool.cs          # 进程工具 (6 methods) ✨ 新
        ├── GitTool.cs              # Git 工具 (18 methods) ✨ 新
        └── BuiltInToolExtensions.cs # DI 注册扩展（更新）

tests/Dawning.Agents.Tests/
└── Tools/
    ├── FunctionToolAttributeTests.cs
    ├── MethodToolTests.cs
    ├── ToolRegistryTests.cs
    └── BuiltInToolTests.cs         # 内置工具测试 ✨ 新
```

### 安全机制设计（参考 GitHub Copilot）

#### 风险等级（ToolRiskLevel）

```csharp
public enum ToolRiskLevel
{
    Low = 0,     // 读取操作：GetTime, Calculate, ReadFile
    Medium = 1,  // 网络操作：HttpGet, SearchWeb
    High = 2     // 危险操作：DeleteFile, RunCommand, GitPush
}
```

#### 工具属性扩展

```csharp
[FunctionTool(
    "删除文件",
    RequiresConfirmation = true,  // 需要用户确认
    RiskLevel = ToolRiskLevel.High,
    Category = "FileSystem"
)]
public string DeleteFile(string path) { ... }
```

#### ITool 接口扩展

```csharp
public interface ITool
{
    string Name { get; }
    string Description { get; }
    string ParametersSchema { get; }
    bool RequiresConfirmation { get; }      // 是否需要确认
    ToolRiskLevel RiskLevel { get; }        // 风险等级
    string? Category { get; }               // 工具分类
    Task<ToolResult> ExecuteAsync(...);
}
```

### 内置工具统计

| 类别 | 工具类 | 方法数 | 风险等级 |
|------|--------|--------|----------|
| DateTime | DateTimeTool | 4 | Low |
| Math | MathTool | 8 | Low |
| Json | JsonTool | 4 | Low |
| Utility | UtilityTool | 5 | Low |
| FileSystem | FileSystemTool | 13 | Low/Medium/High |
| Http | HttpTool | 6 | Medium |
| Process | ProcessTool | 6 | High |
| Git | GitTool | 18 | Low/Medium/High |
| **总计** | **8 类** | **64 方法** | |

### DI 注册方式

```csharp
// 注册所有内置工具（包括高风险）
services.AddAllBuiltInTools();

// 按类别注册
services.AddFileSystemTools();  // 文件系统
services.AddHttpTools();        // HTTP
services.AddProcessTools();     // 进程
services.AddGitTools();         // Git

// 只注册安全工具（不包括 Process/Git 高风险方法）
services.AddBuiltInTools();
```

### 测试统计

- 新增测试: 11 个（BuiltInToolTests）
- 总测试数: 74 个（全部通过）

---

## [2026-01-19] 下一步规划：Tool Sets 与 Virtual Tools

### 背景

参考 GitHub Copilot 的工具管理策略：

- 默认 40 个工具精简为 13 个核心工具
- 非核心工具分为 4 个 Virtual Tool 组
- 使用 Embedding-Guided Tool Routing 智能选择

### 计划实现的功能

#### 1. Tool Sets（工具集）

将相关工具分组，便于管理和引用。

```csharp
public interface IToolSet
{
    string Name { get; }
    string Description { get; }
    IReadOnlyList<ITool> Tools { get; }
}

// 使用方式
var searchTools = new ToolSet("search", "搜索相关工具", 
    [grepTool, searchFilesTool, semanticSearchTool]);
```

#### 2. Virtual Tools（虚拟工具）

延迟加载的工具组，减少 LLM 的工具选择压力。

```csharp
public interface IVirtualTool : ITool
{
    IReadOnlyList<ITool> ExpandedTools { get; }
    bool IsExpanded { get; }
    void Expand();
}

// LLM 先看到虚拟工具摘要，需要时再展开
// "FileSystemTools" → 展开为 13 个具体文件操作工具
```

#### 3. Tool Selector（工具选择器）

基于语义匹配的智能工具路由。

```csharp
public interface IToolSelector
{
    Task<IReadOnlyList<ITool>> SelectToolsAsync(
        string query,
        IReadOnlyList<ITool> availableTools,
        int maxTools = 20,
        CancellationToken ct = default);
}

// 实现策略
// - EmbeddingToolSelector: 基于 Embedding 相似度
// - LLMToolSelector: 使用 LLM 选择
// - HybridToolSelector: 混合策略
```

#### 4. Tool Approval Workflow（审批流程）

增强的工具执行确认机制。

```csharp
public interface IToolApprovalHandler
{
    Task<bool> RequestApprovalAsync(
        ITool tool,
        string input,
        CancellationToken ct = default);
}

// 支持的审批策略
// - AlwaysApprove: 自动批准所有
// - NeverApprove: 总是拒绝（只读模式）
// - RiskBasedApproval: 基于风险等级
// - InteractiveApproval: 交互式确认
```

### 预期架构

```
┌─────────────────────────────────────────────────────┐
│                    Agent                            │
├─────────────────────────────────────────────────────┤
│  ToolSelector (选择工具)                            │
│       ↓                                             │
│  ToolRegistry (管理所有工具)                        │
│       │                                             │
│       ├── Core Tools (13个核心工具，直接可见)       │
│       │   ├── read_file                            │
│       │   ├── edit_file                            │
│       │   ├── search                               │
│       │   └── terminal                             │
│       │                                             │
│       └── Virtual Tools (按需展开)                  │
│           ├── NotebookTools → [run_cell, ...]      │
│           ├── WebTools → [fetch, http_get, ...]    │
│           ├── TestingTools → [run_tests, ...]      │
│           └── GitTools → [commit, push, ...]       │
│                                                     │
│  ToolApprovalHandler (审批确认)                     │
│       ↓                                             │
│  Tool.ExecuteAsync()                                │
└─────────────────────────────────────────────────────┘
```

---

## [2026-01-18] Phase 2: Week 3 Agent 核心循环实现

### 新增的文件结构

```
src/Dawning.Agents.Abstractions/
├── Agent/
│   ├── IAgent.cs              # Agent 核心接口
│   ├── AgentContext.cs        # 执行上下文
│   ├── AgentStep.cs           # 单步执行记录
│   ├── AgentResponse.cs       # 执行响应
│   └── AgentOptions.cs        # 配置选项
└── Prompts/
    └── IPromptTemplate.cs     # 提示词模板接口

src/Dawning.Agents.Core/
├── Agent/
│   ├── AgentBase.cs                        # Agent 基类（核心循环）
│   ├── ReActAgent.cs                       # ReAct 模式实现
│   └── AgentServiceCollectionExtensions.cs # DI 注册扩展
└── Prompts/
    ├── PromptTemplate.cs      # 模板实现
    └── AgentPrompts.cs        # 预定义模板

tests/Dawning.Agents.Tests/
├── Agent/
│   ├── AgentModelsTests.cs    # 数据模型测试 (9 tests)
│   └── ReActAgentTests.cs     # ReActAgent 测试 (6 tests)
└── Prompts/
    └── PromptTemplateTests.cs # 模板测试 (7 tests)
```

### 核心接口设计

```csharp
public interface IAgent
{
    string Name { get; }
    string Instructions { get; }
    Task<AgentResponse> RunAsync(string input, CancellationToken ct = default);
    Task<AgentResponse> RunAsync(AgentContext context, CancellationToken ct = default);
}
```

### ReAct 模式实现

- **Thought**: Agent 的思考过程
- **Action**: 要执行的动作
- **Action Input**: 动作输入参数
- **Observation**: 动作执行结果
- **Final Answer**: 最终答案

### 测试统计

- 新增测试: 21 个
- 总测试数: 63 个（全部通过）

### 其他变更

- 项目重命名: `DawningAgents` → `Dawning.Agents`
- 更新 copilot-instructions.md 添加 CSharpier 格式规范

---

## [2026-01-17] Phase 1: Week 2 项目初始化完成

### 创建的解决方案结构

```
dawning-agents/
├── .editorconfig                    # 代码规范
├── .github/workflows/build.yml      # GitHub Actions CI/CD
├── Directory.Build.props            # 统一项目配置 (net10.0)
├── Dawning.Agents.sln                # 解决方案
├── src/
│   ├── Dawning.Agents.Core/          # 核心类库
│   │   └── LLM/
│   │       ├── ILLMProvider.cs      # LLM 抽象接口
│   │       └── OllamaProvider.cs    # Ollama 本地模型实现
│   └── Dawning.Agents.Demo/          # 演示控制台
│       └── Program.cs
└── tests/
    └── Dawning.Agents.Tests/         # 单元测试 (8 tests)
        └── LLM/
            └── OllamaProviderTests.cs
```

### 核心接口设计

```csharp
public interface ILLMProvider
{
    string Name { get; }
    Task<ChatCompletionResponse> ChatAsync(...);
    IAsyncEnumerable<string> ChatStreamAsync(...);
}
```

### 技术栈

- **.NET**: 10.0 (最新 LTS)
- **本地 LLM**: Ollama + deepseek-coder (1.3b/6.7B)
- **测试框架**: xUnit + FluentAssertions + Moq
- **CI/CD**: GitHub Actions

### NuGet 包

| 包 | 版本 | 用途 |
|---|---|---|
| Microsoft.Extensions.Http | 10.0.2 | HTTP 客户端 |
| Microsoft.Extensions.Logging.Abstractions | 10.0.2 | 日志抽象 |
| xUnit | 2.9.2 | 单元测试 |
| FluentAssertions | 8.8.0 | 断言库 |
| Moq | 4.20.72 | Mock 框架 |

---

## [2026-01-16] Phase 0: 框架分析文档全面更新

### 背景

微软在 2025年11月宣布将 **Semantic Kernel** 和 **AutoGen** 整合为统一的 **Microsoft Agent Framework**。同时 **OpenAI Agents SDK**（Swarm 的生产版本）成为主流框架。因此需要更新所有框架分析文档。

### 删除的文档

- `docs/readings/03-semantic-kernel-analysis/` - Semantic Kernel 分析（已过时）
- `docs/readings/04-autogen-analysis/` - AutoGen 分析（已过时）

### 新增的文档

| 文件 | 描述 |
|------|------|
| `docs/readings/03-ms-agent-framework-analysis/ms-agent-framework-analysis-zh.md` | MS Agent Framework 架构分析（中文） |
| `docs/readings/03-ms-agent-framework-analysis/ms-agent-framework-analysis-en.md` | MS Agent Framework 架构分析（英文） |
| `docs/readings/04-openai-agents-sdk-analysis/openai-agents-sdk-analysis-zh.md` | OpenAI Agents SDK 架构分析（中文） |
| `docs/readings/04-openai-agents-sdk-analysis/openai-agents-sdk-analysis-en.md` | OpenAI Agents SDK 架构分析（英文） |

### 更新的文档

#### `LEARNING_PLAN.md`

- **Week 1 Day 5-7**: Semantic Kernel/AutoGen → MS Agent Framework/OpenAI Agents SDK
- **Week 5**: SK Plugins → OpenAI Agents SDK `@function_tool` + MS Agent Framework `ai_function`
- **Week 7**: AutoGen 源码 → MS Agent Framework HandoffBuilder + OpenAI Agents SDK Handoff
- **资源列表**: 更新必读源码（新增 LangGraph、MS Agent Framework、OpenAI Agents SDK）

#### `docs/readings/05-framework-comparison/`

- **三框架对比**: LangChain/LangGraph, MS Agent Framework, OpenAI Agents SDK
- **新增双编排模式**:
  - `IWorkflow` - Workflow 编排（LLM 动态决策交接）
  - `IStateGraph` - 状态机编排（开发者预定义流程）
- **更新设计原则**: 从"四个核心原语 + 工作流"改为"四个核心原语 + 双编排模式"
- **新增接口**: `IStateGraph<TState>`, `StateGraphBuilder<TState>`

#### `docs/readings/06-week2-setup-guide/`

- **Python 包更新**:
  - 移除: `autogen-agentchat`
  - 新增: `openai-agents`, `langgraph`, `agent-framework`
- **.NET 包更新**:
  - 移除: `Microsoft.SemanticKernel`
  - 新增: `Microsoft.Agents.AI --prerelease`

### 安装的 VS Code 扩展

- `shd101wyy.markdown-preview-enhanced` - 增强的 Markdown 预览（支持 Mermaid）

---

## [2026-01-XX] Phase 0: 初始框架分析（历史记录）

### 创建的文档

- `docs/readings/00-agent-core-concepts/` - Agent 核心概念
- `docs/readings/01-building-effective-agents/` - 构建有效 Agent
- `docs/readings/02-langchain-analysis/` - LangChain 分析
- `docs/readings/02-openai-function-calling/` - OpenAI Function Calling
- `docs/readings/03-react-paper/` - ReAct 论文分析
- `docs/readings/04-chain-of-thought/` - 思维链分析
- `docs/readings/05-framework-comparison/` - 框架对比（初版，比较 LangChain/SK/AutoGen）
- `docs/readings/06-week2-setup-guide/` 至 `16-week12-deployment/` - 12周学习计划

---

## dawning-agents 设计决策摘要

### 核心原语（来自 OpenAI Agents SDK）

```csharp
public interface IAgent { }      // Agent - LLM + 指令 + 工具
public interface ITool { }       // Tool - 可调用的功能
public interface IHandoff { }    // Handoff - Agent 间委托
public interface IGuardrail { }  // Guardrail - 输入/输出验证
```

### 双编排模式

```csharp
// Workflow 编排 - LLM 动态决策（来自 MS Agent Framework）
public interface IWorkflow<TContext> { }
public class HandoffBuilder<TContext> { }

// 状态机编排 - 开发者预定义流程（来自 LangGraph）
public interface IStateGraph<TState> { }
public class StateGraphBuilder<TState> { }
```

### 场景选择指南

| 场景 | 推荐模式 | 原因 |
|------|----------|------|
| 多 Agent 协作、客服分流 | Workflow (HandoffBuilder) | LLM 智能决策交接目标 |
| 审批流、数据管道、多轮迭代 | StateGraph | 需要确定性的流程控制 |
| 简单对话 | 直接用 Agent | 无需编排 |

### 关键设计来源

| 特性 | 来源 |
|------|------|
| 四个核心原语 | OpenAI Agents SDK |
| Guardrails | OpenAI Agents SDK |
| Tracing | OpenAI Agents SDK |
| HandoffBuilder | MS Agent Framework |
| 两层架构 | MS Agent Framework |
| StateGraph | LangGraph |
| `[Tool]` 属性 | .NET 最佳实践 |

---

## 当前文档结构

```text
docs/readings/
├── 00-agent-core-concepts/           # Agent 核心概念
├── 01-building-effective-agents/     # 构建有效 Agent
├── 02-langchain-analysis/            # LangChain 分析
├── 02-openai-function-calling/       # OpenAI Function Calling
├── 03-ms-agent-framework-analysis/   # MS Agent Framework 分析 ✨ 新
├── 03-react-paper/                   # ReAct 论文
├── 04-chain-of-thought/              # 思维链
├── 04-openai-agents-sdk-analysis/    # OpenAI Agents SDK 分析 ✨ 新
├── 05-framework-comparison/          # 框架对比 ✅ 已更新
├── 06-week2-setup-guide/             # Week 2 环境搭建 ✅ 已更新
├── 07-week3-agent-loop/              # Week 3 Agent 循环
├── 08-week4-memory/                  # Week 4 记忆系统
├── 09-week5-tools/                   # Week 5 工具系统
├── 10-week6-rag/                     # Week 6 RAG
├── 11-week7-multi-agent/             # Week 7 多 Agent
├── 12-week8-communication/           # Week 8 通信
├── 13-week9-safety/                  # Week 9 安全
├── 14-week10-human-loop/             # Week 10 人机协作
├── 15-week11-observability/          # Week 11 可观测性
└── 16-week12-deployment/             # Week 12 部署
```

---

## 下一步计划

### Phase 1: 核心原语实现（Week 1-2）

- [ ] 创建解决方案结构
- [ ] 实现 `IAgent` 和 `Agent`
- [ ] 实现 `ITool` 和 `FunctionTool`
- [ ] 实现 `[Tool]` 属性发现
- [ ] OpenAI 集成
- [ ] 基础 `Runner`

### Phase 2: Handoff 与 Guardrails（Week 3-4）

- [ ] 实现 `IHandoff`
- [ ] 实现 `HandoffBuilder`
- [ ] 实现 `IGuardrail`
- [ ] 输入/输出护栏

### Phase 3: 双编排模式（Week 5-6）

- [ ] 实现 `HandoffWorkflow`
- [ ] 实现 `StateGraph` 和 `StateGraphBuilder`
- [ ] 条件边和循环
- [ ] 人机协作

### Phase 4: 可观测性（Week 7-8）

- [ ] Tracing 系统
- [ ] OpenTelemetry 集成

### Phase 5: 完善（Week 9-10）

- [ ] 更多 LLM 提供商
- [ ] Session 管理
- [ ] 文档和示例
