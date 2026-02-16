# RFC: Tools 系统重新设计

> **状态**: Phase A+B 已完成  
> **创建日期**: 2026-02-16  
> **更新日期**: 2026-02-18  
> **作者**: Dawning.Agents Team

---

## 1. 动机

当前 Tools 系统有 **92 个内置方法**（10 个类，~4300 行），但大多数都可以被一个 `bash` 工具替代。参考 Claude Code、Cursor、GitHub Copilot 等主流 AI Agent 的设计，它们只提供 5-8 个精心设计的核心工具，其余全部通过 shell 能力覆盖。

核心问题：
- **膨胀**: 92 个方法中 90% 可被 `bash` 替代（MathTool、DateTimeTool、GitTool 等）
- **冗余**: `FileSystemTool.ReadFile` vs `cat`，`GitTool.GetStatus` vs `git status`
- **缺乏动态性**: 无法在运行时创建、复用、共享工具
- **维护负担**: ~4300 行内置工具代码 + ~4900 行测试

## 2. 设计目标

| 目标 | 说明 |
|------|------|
| **最小核心** | 只内置不可被 bash 替代的工具（结构化读写、搜索、shell） |
| **动态工具** | Agent 可在运行时创建脚本工具，session 内可复用 |
| **持久化层级** | Session → User → Global 三级工具存储 |
| **安全隔离** | bash/脚本工具默认沙箱执行，可配置信任级别 |
| **向后兼容** | 保留 `ITool` / `IToolRegistry` 核心接口，旧工具可通过 Extra 包使用 |

## 3. 新架构

### 3.1 核心工具集（仅 5 个）

```
┌─────────────────────────────────────────────────────┐
│                 Core Tools (内置)                     │
├────────────┬────────────────────────────────────────-┤
│ read_file  │ 读取文件，支持行号范围、分块、编码检测     │
│ write_file │ 创建/覆盖文件                            │
│ edit_file  │ 基于搜索替换的精确编辑                    │
│ search     │ 文本搜索 (grep) + 文件名搜索 (glob)      │
│ bash       │ 执行 shell 命令，万能后备                 │
└────────────┴─────────────────────────────────────────┘
```

**为什么这 5 个不能被 bash 替代？**

| 工具 | 不可替代的原因 |
|------|---------------|
| `read_file` | 需要结构化的行号标注、大文件分块读取、token 预算控制 |
| `write_file` | 需要自动创建父目录、内容完整性校验、编码处理 |
| `edit_file` | `sed` 对多行替换不可靠，需要上下文匹配 + 唯一性校验 |
| `search` | 需要结构化的匹配结果（文件名、行号、上下文），远比 `grep` 输出更适合 LLM 消费 |
| `bash` | 本身就是 shell，是其他一切工具的基础 |

### 3.2 动态工具（EphemeralTool）

Agent 可以在运行时创建脚本工具，注册后在当前 session 中复用：

```csharp
/// <summary>
/// 动态脚本工具 — 在运行时由 Agent 创建，可跨调用复用
/// </summary>
public class EphemeralTool : ITool
{
    public string Name { get; }
    public string Description { get; }
    public string Script { get; }           // 脚本内容
    public ScriptRuntime Runtime { get; }   // Bash / Python / Node
    public ToolScope Scope { get; set; }    // Session / User / Global
    public IReadOnlyList<ScriptParameter> Parameters { get; }
    
    public Task<ToolResult> ExecuteAsync(string input, CancellationToken ct);
}

public enum ScriptRuntime { Bash, Python, Node }
public enum ToolScope { Session, User, Global }

public record ScriptParameter(
    string Name,
    string Description,
    string Type = "string",
    bool Required = true,
    string? DefaultValue = null
);
```

**典型工作流：**

```
用户: "帮我分析这个项目的依赖关系"

Agent 思考: 没有现成的依赖分析工具，我来创建一个

Agent 调用 bash: dotnet list package --format json  
Agent 发现: 输出很长，后续还要多次运行

Agent 调用 create_tool: {
    "name": "list_deps",
    "description": "列出 .NET 项目依赖及版本",
    "runtime": "bash",
    "script": "dotnet list \"$project\" package --format json | jq '.projects[].frameworks[].topLevelPackages[]'",
    "parameters": [{"name": "project", "description": "项目路径", "default": "."}],
    "scope": "session"
}

后续调用: list_deps(project="src/Core")  ← 直接复用，无需重新编写
```

### 3.3 持久化层级

```
┌───────────────────────────────────────────────┐
│              Tool Resolution Order              │
│                                                 │
│  ① Core Tools (内置, 不可覆盖)                   │
│      ↓                                          │
│  ② Session Tools (内存, 随 session 销毁)          │
│      ↓                                          │
│  ③ User Tools (~/.dawning/tools/*.json)          │
│      ↓                                          │
│  ④ Global Tools (项目/.dawning/tools/*.json)     │
│      ↓                                          │
│  ⑤ MCP Tools (远程 MCP 服务器)                    │
└───────────────────────────────────────────────┘
```

| 层级 | 存储位置 | 生命周期 | 使用场景 |
|------|---------|---------|---------|
| **Core** | 代码内置 | 永久 | read_file, write_file, edit_file, search, bash |
| **Session** | 内存 (`IToolSession`) | 单次对话 | Agent 临时创建的一次性脚本 |
| **User** | `~/.dawning/tools/` | 跨项目 | 用户常用的个人工具（如 "my_deploy.sh"） |
| **Global** | `{project}/.dawning/tools/` | 跟随项目 | 团队共享的项目特定工具（提交到 git） |
| **MCP** | 远程服务器 | 连接存活期间 | 已有的 MCP 集成不变 |

**工具描述文件格式** (`.dawning/tools/{name}.tool.json`)：

```json
{
  "name": "list_deps",
  "description": "列出 .NET 项目依赖及版本",
  "runtime": "bash",
  "script": "dotnet list \"$project\" package --format json | jq '.projects[].frameworks[].topLevelPackages[]'",
  "parameters": [
    {
      "name": "project",
      "description": "项目路径",
      "type": "string",
      "required": false,
      "default": "."
    }
  ],
  "metadata": {
    "author": "aluneth",
    "created": "2026-02-16T10:30:00Z",
    "tags": ["dotnet", "dependencies"]
  }
}
```

### 3.4 安全模型

```
┌─────────────────────────────────────────────────────────┐
│                    Security Layers                        │
│                                                           │
│  Layer 1: Tool-level Permissions                          │
│  ┌─────────────────────────────────────────────────────┐ │
│  │ Core Tools:                                          │ │
│  │   read_file  → 开放（受工作目录限制）                  │ │
│  │   write_file → 需确认（新文件）                       │ │
│  │   edit_file  → 需确认（已有文件修改）                  │ │
│  │   search     → 开放                                  │ │
│  │   bash       → 需确认（可配置自动审批命令白名单）       │ │
│  └─────────────────────────────────────────────────────┘ │
│                                                           │
│  Layer 2: Sandbox Execution (bash & 脚本工具)              │
│  ┌─────────────────────────────────────────────────────┐ │
│  │ 策略 (可配)       说明                                │ │
│  │ ────────────────  ──────────────────────────────────  │ │
│  │ Trust             直接在主机执行（开发环境默认）        │ │
│  │ WorkingDir        限制在工作目录内执行                  │ │
│  │ Timeout           最大执行时间（默认 30s, 可配）        │ │
│  │ NetworkRestrict   禁止网络访问                         │ │
│  │ Docker            Docker 容器中执行（生产推荐）         │ │
│  └─────────────────────────────────────────────────────┘ │
│                                                           │
│  Layer 3: Command Analysis (bash 执行前)                   │
│  ┌─────────────────────────────────────────────────────┐ │
│  │ • 危险命令检测: rm -rf /, mkfs, dd, :(){ :|:& };:   │ │
│  │ • 敏感路径保护: /etc, /usr, ~/.ssh 等                 │ │
│  │ • 网络活动审计: curl/wget 目标域名检查                 │ │
│  │ • 权限升级检测: sudo, su, chmod 777                   │ │
│  │ • 可配置白名单: 允许 git, dotnet, npm 等常用命令       │ │
│  └─────────────────────────────────────────────────────┘ │
│                                                           │
│  Layer 4: Audit Trail                                     │
│  ┌─────────────────────────────────────────────────────┐ │
│  │ 所有工具调用记录: 时间戳, 工具名, 输入, 输出,          │ │
│  │ 执行耗时, 成功/失败, 审批决策                          │ │
│  └─────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

### 3.5 新接口设计

```csharp
// ── 保留的核心接口 (Abstractions) ──

// ITool, ToolResult, ToolRiskLevel — 保持不变
// IToolRegistry — 简化，移除 ToolSet/VirtualTool 相关方法
// ToolDefinition, ToolCall — LLM 层保持不变

// ── 新增接口 ──

/// <summary>
/// 工具会话 — 管理 session 级别的动态工具
/// </summary>
public interface IToolSession : IDisposable
{
    /// <summary>创建并注册一个动态脚本工具</summary>
    EphemeralTool CreateTool(EphemeralToolDefinition definition);
    
    /// <summary>获取当前 session 的所有工具（含继承的 User/Global 工具）</summary>
    IReadOnlyList<ITool> GetTools();
    
    /// <summary>提升工具的持久化层级</summary>
    Task PromoteToolAsync(string name, ToolScope targetScope, CancellationToken ct = default);
    
    /// <summary>从指定层级移除工具</summary>
    Task RemoveToolAsync(string name, ToolScope scope, CancellationToken ct = default);
}

/// <summary>
/// 工具持久化存储
/// </summary>
public interface IToolStore
{
    Task<IReadOnlyList<EphemeralToolDefinition>> LoadToolsAsync(
        ToolScope scope, CancellationToken ct = default);
    Task SaveToolAsync(
        EphemeralToolDefinition definition, ToolScope scope, CancellationToken ct = default);
    Task DeleteToolAsync(
        string name, ToolScope scope, CancellationToken ct = default);
}

/// <summary>
/// Bash / 脚本执行沙箱
/// </summary>
public interface IToolSandbox
{
    Task<ToolExecutionResult> ExecuteAsync(
        string command,
        ToolSandboxOptions options,
        CancellationToken ct = default);
}

public record ToolSandboxOptions
{
    public string WorkingDirectory { get; init; } = ".";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public SandboxMode Mode { get; init; } = SandboxMode.Trust;
    public IDictionary<string, string> Environment { get; init; } = 
        new Dictionary<string, string>();
}

public enum SandboxMode { Trust, WorkingDir, Timeout, Docker }

public record ToolExecutionResult
{
    public int ExitCode { get; init; }
    public string Stdout { get; init; } = "";
    public string Stderr { get; init; } = "";
    public TimeSpan Duration { get; init; }
    public bool TimedOut { get; init; }
}
```

### 3.6 `create_tool` — Agent 自我扩展

`create_tool` 本身也是一个内置核心工具，让 Agent 可以自主创建新工具：

```
┌─────────────────────────────────────────────────────────┐
│                    Core Tools (6 个)                      │
├─────────────┬───────────────────────────────────────────-┤
│ read_file   │ 读取文件                                    │
│ write_file  │ 创建/覆盖文件                               │
│ edit_file   │ 精确编辑文件                                │
│ search      │ grep + glob 搜索                            │
│ bash        │ 执行 shell 命令                              │
│ create_tool │ 创建动态脚本工具（Agent 自我扩展）            │
└─────────────┴────────────────────────────────────────────┘
```

`create_tool` 的参数 schema：

```json
{
  "name": "工具名称 (snake_case)",
  "description": "工具用途描述",
  "runtime": "bash | python | node",
  "script": "脚本内容，用 $param_name 引用参数",
  "parameters": [
    { "name": "param1", "description": "说明", "required": true }
  ],
  "scope": "session | user | global"
}
```

## 4. 实施计划

### Phase A: 核心工具实现 ✅

| 步骤 | 任务 | 状态 |
|------|------|------|
| A1 | 实现 `BashTool` + `IToolSandbox` (Trust + WorkingDir + Timeout 模式) | ✅ |
| A2 | 实现 `ReadFileTool` (行号标注、分块读取、编码检测) | ✅ |
| A3 | 实现 `WriteFileTool` (自动创建目录、内容校验) | ✅ |
| A4 | 实现 `EditFileTool` (搜索替换、唯一性校验、diff 预览) | ✅ |
| A5 | 实现 `SearchTool` (grep 模式 + glob 模式，结构化结果) | ✅ |
| A6 | 简化 `IToolRegistry`，移除 ToolSet/VirtualTool 方法 | ✅ |

### Phase B: 动态工具 + 持久化 + Agent 集成 ✅

| 步骤 | 任务 | 状态 |
|------|------|------|
| B1 | 实现 `EphemeralTool` + `CreateToolTool` | ✅ |
| B2 | 实现 `IToolStore` (JSON 文件存储) | ✅ |
| B3 | 实现 `IToolSession` (Session 内存 + User/Global 加载) | ✅ |
| B4 | 实现 `PromoteToolAsync` (Session → User → Global 升级) | ✅ |
| B5 | 更新 `FunctionCallingAgent` 适配新工具 (IToolSession + create_tool) | ✅ |
| B6 | DI 注册重构 (`AddCoreTools()` 一个方法搞定) | ✅ |

### Phase C: 安全 + 清理 (第 3 周)

| 步骤 | 任务 | 预估 |
|------|------|------|
| C1 | 实现 `CommandAnalyzer` (危险命令检测 + 白名单) | 1 天 |
| C2 | 实现 Docker 沙箱模式 (`DockerSandbox : IToolSandbox`) | 1 天 |
| C3 | 删除旧内置工具，移至 `Dawning.Agents.Tools.Extra` 可选包 | 0.5 天 |
| C4 | 删除 `ToolSet` / `VirtualTool` / `ToolSelector` 基础设施 | 0.5 天 |
| C5 | 清理测试，编写新工具测试 | 1 天 |
| C6 | 更新文档 + CHANGELOG | 0.5 天 |

### 变动统计预估

| 类别 | 当前 | 目标 | 变化 |
|------|------|------|------|
| 内置工具类 | 10 个, 4329 行 | 6 个, ~1200 行 | -72% |
| 工具基础设施 | 7 个, 1621 行 | 8 个, ~1800 行 | +11% (新增动态/持久化) |
| 工具测试 | 16 个, 4914 行 | ~12 个, ~2500 行 | -49% |
| **总计** | ~11000 行 | ~5500 行 | **-50%** |

## 5. 保留与删除清单

### ✅ 保留 (Abstractions 接口)

| 文件 | 原因 |
|------|------|
| `ITool.cs` (接口 + ToolResult) | 核心契约，全部保留 |
| `IToolRegistry.cs` | 保留核心方法，移除 ToolSet/VirtualTool 方法 |
| `FunctionToolAttribute.cs` | 保留 `[FunctionTool]` 特性（用于自定义工具类） |
| `IToolApprovalHandler.cs` | 保留审批接口 |

### ✅ 保留 (Core 基础设施)

| 文件 | 原因 |
|------|------|
| `ToolRegistry.cs` | 简化后保留 |
| `MethodTool.cs` | `[FunctionTool]` 扫描机制保留（供用户自定义工具类使用） |
| `ToolScanner.cs` | 配合 MethodTool 保留 |
| `DefaultToolApprovalHandler.cs` | 审批机制保留 |

### ❌ 删除 (内置工具)

| 文件 | 替代方案 |
|------|---------|
| `DateTimeTool.cs` | `bash: date` |
| `MathTool.cs` | `bash: bc`, `python -c` |
| `JsonTool.cs` | `bash: jq` |
| `UtilityTool.cs` | `bash: echo, uuid, base64` |
| `FileSystemTool.cs` | 新 `ReadFileTool` + `WriteFileTool` + `bash: ls, cp, mv` |
| `HttpTool.cs` | `bash: curl` |
| `ProcessTool.cs` | `bash` 本身 |
| `GitTool.cs` | `bash: git` |
| `PackageManagerTool.cs` | `bash: dotnet, npm, pip` |
| `CSharpierTool.cs` | `bash: dotnet csharpier` |

### ❌ 删除 (过度设计的基础设施)

| 文件 | 原因 |
|------|------|
| `IToolSet.cs` | 工具分组 — 6 个核心工具不需要分组 |
| `IVirtualTool.cs` | 虚拟工具 — 不再需要 |
| `IToolSelector.cs` | 智能选择 — 6 个工具不需要选择器 |
| `ToolSet.cs` | 同上 |
| `VirtualTool.cs` | 同上 |
| `DefaultToolSelector.cs` | 同上 |
| `PackageManagerOptions.cs` | 随 PackageManagerTool 删除 |

### 📦 移至可选包 `Dawning.Agents.Tools.Extra` (可选)

考虑到向后兼容，可以将旧内置工具打包为独立 NuGet 包，供仍需要它们的用户使用。此步骤为可选 — 如果确认无人使用可直接删除。

## 6. 迁移指南

### 用户迁移 (Breaking Change)

```diff
// 旧方式
- services.AddAllBuiltInTools();
- services.AddDateTimeTools();
- services.AddGitTools();

// 新方式
+ services.AddCoreTools();                  // 6 个核心工具一键注册
+ services.AddToolSession();                // 启用动态工具支持 (可选)
+ services.AddToolStore(configuration);     // 启用持久化 (可选)

// 如果仍需要旧工具
+ services.AddExtraTools();                 // 需安装 Dawning.Agents.Tools.Extra
```

### 自定义工具 (无变化)

```csharp
// [FunctionTool] 特性方式仍然可用
public class MyTools
{
    [FunctionTool("查询数据库")]
    public async Task<string> QueryDatabase(string sql) => ...;
}

services.AddToolsFrom<MyTools>();  // 不变
```

## 7. 开放问题

| # | 问题 | 建议 |
|---|------|------|
| 1 | 旧工具是否需要 Extra 包？ | 建议直接删除（破坏性修改优先原则） |
| 2 | `edit_file` 用什么算法？ | 建议 exact string match（同 Copilot），非 diff patch |
| 3 | Docker 沙箱 Phase C 是否值得做？ | 建议先做 Trust + Timeout，Docker 作为未来扩展 |
| 4 | `create_tool` 是否需要审批？ | 建议 `scope=global` 时需要确认，`session` 不需要 |
| 5 | Python/Node 运行时是否必需？ | 建议先只支持 Bash，Python/Node 作为可选扩展 |
