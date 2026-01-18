# Week 3: Agent 核心循环 - 实现指南

> Phase 2: 单 Agent 开发核心技能
> Week 3 学习资料：理解并实现 Agent 循环

---

## Day 1-2: 理解 Agent 循环

### 1. Agent 执行周期

任何 AI Agent 的核心都是其执行循环。这遵循一个基本模式：

```text
┌─────────────────────────────────────────────────────────────────┐
│                     Agent 执行循环                               │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│    ┌──────────┐                                                  │
│    │   开始   │                                                  │
│    └────┬─────┘                                                  │
│         │                                                        │
│         ▼                                                        │
│    ┌──────────┐     ┌──────────┐     ┌──────────┐               │
│    │   观察   │────►│   思考   │────►│   行动   │               │
│    │ (Observe)│     │ (Think)  │     │  (Act)   │               │
│    └──────────┘     └──────────┘     └────┬─────┘               │
│         ▲                                  │                     │
│         │                                  │                     │
│         └──────────────────────────────────┘                     │
│                    (循环直到完成)                                 │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. LangChain Agent 架构分析

#### LangChain 源码中的关键组件

```python
# 从 langchain/agents/agent.py 简化

class AgentExecutor:
    """Agent 执行循环实现。"""
    
    def __init__(self, agent, tools, memory=None, max_iterations=15):
        self.agent = agent
        self.tools = {tool.name: tool for tool in tools}
        self.memory = memory
        self.max_iterations = max_iterations
    
    def run(self, input: str) -> str:
        """主执行循环。"""
        intermediate_steps = []
        iterations = 0
        
        while iterations < self.max_iterations:
            # 思考：Agent 决定做什么
            output = self.agent.plan(
                input=input,
                intermediate_steps=intermediate_steps
            )
            
            # 检查 Agent 是否想要结束
            if isinstance(output, AgentFinish):
                return output.return_values["output"]
            
            # 行动：执行选择的操作
            action = output  # AgentAction
            tool = self.tools[action.tool]
            observation = tool.run(action.tool_input)
            
            # 观察：记录结果
            intermediate_steps.append((action, observation))
            iterations += 1
        
        return "达到最大迭代次数"
```

#### 规划函数

```python
# 从 langchain/agents/mrkl/base.py

class ZeroShotAgent:
    """使用推理的 ReAct 风格 Agent。"""
    
    def plan(self, input: str, intermediate_steps: list) -> AgentAction | AgentFinish:
        """根据当前状态决定下一个动作。"""
        
        # 使用当前状态构建提示
        prompt = self._build_prompt(input, intermediate_steps)
        
        # 获取 LLM 响应
        llm_output = self.llm.predict(prompt)
        
        # 解析响应
        return self._parse_output(llm_output)
    
    def _build_prompt(self, input: str, steps: list) -> str:
        """使用之前步骤的草稿本构建提示。"""
        scratchpad = ""
        for action, observation in steps:
            scratchpad += f"思考：{action.log}\n"
            scratchpad += f"行动：{action.tool}\n"
            scratchpad += f"行动输入：{action.tool_input}\n"
            scratchpad += f"观察：{observation}\n"
        
        return f"""回答以下问题：
{input}

你可以使用以下工具：
{self._format_tools()}

使用此格式：
思考：你的推理
行动：工具名称
行动输入：工具的输入
观察：工具结果
...（根据需要重复）
思考：我现在知道答案了
最终答案：最终答案

{scratchpad}
思考："""
```

### 3. ReAct 模式深入解析

ReAct（推理 + 行动）模式交错进行：

- **推理轨迹**：Agent 思考过程的语言解释
- **行动**：与外部工具或环境的交互

```text
问题：埃菲尔铁塔所在国家的首都是什么？

思考 1：我需要找到埃菲尔铁塔的位置。
行动 1：Search[埃菲尔铁塔位置]
观察 1：埃菲尔铁塔位于法国巴黎。

思考 2：埃菲尔铁塔在法国。现在我需要法国的首都。
行动 2：Search[法国首都]
观察 2：巴黎是法国的首都。

思考 3：我现在知道答案了。
最终答案：巴黎
```

---

## Day 3-4: 实现基础 Agent

### 1. 核心接口设计

#### IAgent 接口

```csharp
namespace Dawning.Agents.Core.Agents;

/// <summary>
/// 表示 Agent 执行的结果
/// </summary>
public record AgentResponse
{
    /// <summary>
    /// Agent 的最终输出
    /// </summary>
    public required string Output { get; init; }
    
    /// <summary>
    /// Agent 是否成功完成
    /// </summary>
    public bool IsSuccess { get; init; } = true;
    
    /// <summary>
    /// Agent 执行的中间步骤
    /// </summary>
    public IReadOnlyList<AgentStep> Steps { get; init; } = [];
    
    /// <summary>
    /// 执行期间使用的总 token 数
    /// </summary>
    public int TotalTokens { get; init; }
    
    /// <summary>
    /// 如果执行失败的错误消息
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// 表示 Agent 执行的单个步骤
/// </summary>
public record AgentStep
{
    public required string Thought { get; init; }
    public string? Action { get; init; }
    public string? ActionInput { get; init; }
    public string? Observation { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// 所有 Agent 的基础接口
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Agent 的唯一名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Agent 功能的描述
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// 使用给定上下文执行 Agent
    /// </summary>
    Task<AgentResponse> ExecuteAsync(
        AgentContext context, 
        CancellationToken cancellationToken = default);
}
```

#### AgentContext 上下文

```csharp
namespace Dawning.Agents.Core.Agents;

/// <summary>
/// Agent 执行的上下文
/// </summary>
public class AgentContext
{
    /// <summary>
    /// 用户的输入/问题
    /// </summary>
    public required string Input { get; init; }
    
    /// <summary>
    /// 可选的系统提示覆盖
    /// </summary>
    public string? SystemPrompt { get; init; }
    
    /// <summary>
    /// Agent 可用的工具
    /// </summary>
    public IReadOnlyList<ITool> Tools { get; init; } = [];
    
    /// <summary>
    /// 对话记忆
    /// </summary>
    public IConversationMemory? Memory { get; init; }
    
    /// <summary>
    /// 停止前的最大迭代次数
    /// </summary>
    public int MaxIterations { get; init; } = 10;
    
    /// <summary>
    /// 额外的元数据
    /// </summary>
    public IDictionary<string, object> Metadata { get; init; } = 
        new Dictionary<string, object>();
}
```

### 2. AgentBase 抽象类

```csharp
namespace Dawning.Agents.Core.Agents;

using Dawning.Agents.Core.LLM;
using Microsoft.Extensions.Logging;

/// <summary>
/// 所有 Agent 的基类，包含通用功能
/// </summary>
public abstract class AgentBase : IAgent
{
    protected readonly ILLMProvider LLM;
    protected readonly ILogger Logger;

    public abstract string Name { get; }
    public abstract string Description { get; }

    protected AgentBase(ILLMProvider llm, ILogger logger)
    {
        LLM = llm ?? throw new ArgumentNullException(nameof(llm));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public abstract Task<AgentResponse> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 为 Agent 构建系统提示
    /// </summary>
    protected virtual string BuildSystemPrompt(AgentContext context)
    {
        return context.SystemPrompt ?? GetDefaultSystemPrompt();
    }

    /// <summary>
    /// 获取默认系统提示
    /// </summary>
    protected abstract string GetDefaultSystemPrompt();

    /// <summary>
    /// 格式化可用工具用于提示
    /// </summary>
    protected string FormatTools(IReadOnlyList<ITool> tools)
    {
        if (tools.Count == 0)
            return "没有可用工具。";

        var sb = new StringBuilder();
        foreach (var tool in tools)
        {
            sb.AppendLine($"- {tool.Name}：{tool.Description}");
            if (!string.IsNullOrEmpty(tool.Parameters))
            {
                sb.AppendLine($"  参数：{tool.Parameters}");
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// 按名称执行工具
    /// </summary>
    protected async Task<string> ExecuteToolAsync(
        string toolName,
        string input,
        IReadOnlyList<ITool> tools,
        CancellationToken cancellationToken)
    {
        var tool = tools.FirstOrDefault(t => 
            t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));

        if (tool == null)
        {
            Logger.LogWarning("未找到工具：{ToolName}", toolName);
            return $"错误：未找到工具 '{toolName}'。可用工具：{string.Join(", ", tools.Select(t => t.Name))}";
        }

        try
        {
            Logger.LogDebug("执行工具 {ToolName}，输入：{Input}", toolName, input);
            var result = await tool.ExecuteAsync(input, cancellationToken);
            Logger.LogDebug("工具 {ToolName} 结果：{Result}", toolName, result);
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "执行工具 {ToolName} 时出错", toolName);
            return $"执行工具时出错：{ex.Message}";
        }
    }
}
```

### 3. ReAct Agent 实现

```csharp
namespace Dawning.Agents.Core.Agents;

using System.Text.RegularExpressions;
using Dawning.Agents.Core.LLM;
using Microsoft.Extensions.Logging;

/// <summary>
/// 遵循 ReAct（推理 + 行动）模式的 Agent
/// </summary>
public partial class ReActAgent : AgentBase
{
    private const string DefaultName = "ReActAgent";
    private const string DefaultDescription = "一个通过推理和行动来解决问题的 Agent";

    public override string Name { get; }
    public override string Description { get; }

    public ReActAgent(
        ILLMProvider llm,
        ILogger<ReActAgent> logger,
        string? name = null,
        string? description = null) : base(llm, logger)
    {
        Name = name ?? DefaultName;
        Description = description ?? DefaultDescription;
    }

    public override async Task<AgentResponse> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        var steps = new List<AgentStep>();
        var scratchpad = new StringBuilder();
        var totalTokens = 0;

        Logger.LogInformation("开始 ReAct Agent 执行，输入：{Input}", context.Input);

        for (int iteration = 0; iteration < context.MaxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 使用当前状态构建提示
            var prompt = BuildPrompt(context, scratchpad.ToString());

            // 获取 LLM 响应
            var response = await LLM.ChatAsync(
                [new ChatMessage("user", prompt)],
                new ChatCompletionOptions
                {
                    SystemPrompt = BuildSystemPrompt(context),
                    Temperature = 0.0f,
                    MaxTokens = 1000
                },
                cancellationToken);

            totalTokens += response.TotalTokens;

            // 解析响应
            var parseResult = ParseResponse(response.Content);

            // 创建步骤记录
            var step = new AgentStep
            {
                Thought = parseResult.Thought,
                Action = parseResult.Action,
                ActionInput = parseResult.ActionInput
            };

            // 检查 Agent 是否想要结束
            if (parseResult.IsFinalAnswer)
            {
                Logger.LogInformation("Agent 在 {Iterations} 次迭代后得出最终答案", iteration + 1);
                steps.Add(step);

                return new AgentResponse
                {
                    Output = parseResult.FinalAnswer ?? parseResult.Thought,
                    IsSuccess = true,
                    Steps = steps,
                    TotalTokens = totalTokens
                };
            }

            // 如果指定了行动则执行
            if (!string.IsNullOrEmpty(parseResult.Action))
            {
                var observation = await ExecuteToolAsync(
                    parseResult.Action,
                    parseResult.ActionInput ?? "",
                    context.Tools,
                    cancellationToken);

                step = step with { Observation = observation };

                // 更新草稿本
                scratchpad.AppendLine($"思考：{parseResult.Thought}");
                scratchpad.AppendLine($"行动：{parseResult.Action}");
                scratchpad.AppendLine($"行动输入：{parseResult.ActionInput}");
                scratchpad.AppendLine($"观察：{observation}");
            }

            steps.Add(step);
            Logger.LogDebug("完成第 {Iteration} 次迭代", iteration + 1);
        }

        Logger.LogWarning("Agent 达到最大迭代次数但未得出最终答案");

        return new AgentResponse
        {
            Output = "我无法在允许的迭代次数内完成任务。",
            IsSuccess = false,
            Steps = steps,
            TotalTokens = totalTokens,
            Error = "达到最大迭代次数"
        };
    }

    protected override string GetDefaultSystemPrompt()
    {
        return """
            你是一个有帮助的 AI 助手，可以一步一步解决问题。
            你可以使用工具来帮助你收集信息和采取行动。
            
            在行动之前始终仔细思考，并解释你的推理。
            当你有足够的信息来回答时，提供最终答案。
            """;
    }

    private string BuildPrompt(AgentContext context, string scratchpad)
    {
        var toolsDescription = FormatTools(context.Tools);

        return $"""
            回答以下问题或完成任务：
            
            问题/任务：{context.Input}
            
            你可以使用以下工具：
            {toolsDescription}
            
            使用此格式：
            
            思考：[你关于下一步做什么的推理]
            行动：[要使用的工具名称]
            行动输入：[工具的输入]
            观察：[工具的结果 - 这将提供给你]
            
            ...（根据需要重复 思考/行动/行动输入/观察）
            
            当你有足够的信息来回答时：
            思考：我现在有足够的信息来回答了。
            最终答案：[你的最终答案]
            
            之前的步骤：
            {scratchpad}
            
            现在从你离开的地方继续：
            思考：
            """;
    }

    private ParsedResponse ParseResponse(string response)
    {
        var result = new ParsedResponse();

        // 提取思考
        var thoughtMatch = ThoughtRegex().Match(response);
        if (thoughtMatch.Success)
        {
            result.Thought = thoughtMatch.Groups[1].Value.Trim();
        }

        // 检查最终答案
        var finalAnswerMatch = FinalAnswerRegex().Match(response);
        if (finalAnswerMatch.Success)
        {
            result.IsFinalAnswer = true;
            result.FinalAnswer = finalAnswerMatch.Groups[1].Value.Trim();
            return result;
        }

        // 提取行动
        var actionMatch = ActionRegex().Match(response);
        if (actionMatch.Success)
        {
            result.Action = actionMatch.Groups[1].Value.Trim();
        }

        // 提取行动输入
        var actionInputMatch = ActionInputRegex().Match(response);
        if (actionInputMatch.Success)
        {
            result.ActionInput = actionInputMatch.Groups[1].Value.Trim();
        }

        return result;
    }

    [GeneratedRegex(@"思考[：:]\s*(.+?)(?=行动[：:]|最终答案[：:]|$)", RegexOptions.Singleline)]
    private static partial Regex ThoughtRegex();

    [GeneratedRegex(@"最终答案[：:]\s*(.+?)$", RegexOptions.Singleline)]
    private static partial Regex FinalAnswerRegex();

    [GeneratedRegex(@"行动[：:]\s*(.+?)(?=行动输入[：:]|$)", RegexOptions.Singleline)]
    private static partial Regex ActionRegex();

    [GeneratedRegex(@"行动输入[：:]\s*(.+?)(?=观察[：:]|思考[：:]|$)", RegexOptions.Singleline)]
    private static partial Regex ActionInputRegex();

    private record ParsedResponse
    {
        public string Thought { get; set; } = "";
        public string? Action { get; set; }
        public string? ActionInput { get; set; }
        public bool IsFinalAnswer { get; set; }
        public string? FinalAnswer { get; set; }
    }
}
```

---

## Day 5-7: Prompt 工程

### 1. 系统提示设计原则

#### 关键原则

| 原则 | 描述 | 示例 |
| ------ | ------ | ------ |
| **清晰性** | 明确说明 Agent 的角色 | "你是...的客服 Agent" |
| **约束** | 定义边界 | "只回答关于我们产品的问题" |
| **格式** | 指定输出结构 | "始终以 JSON 格式响应" |
| **语气** | 设置沟通风格 | "专业但友好" |
| **示例** | 提供演示 | 少样本示例 |

#### 有效的系统提示结构

```text
[角色定义]
你是 [具体角色]，[主要功能]。

[能力]
你可以使用：
- [工具/能力 1]
- [工具/能力 2]

[约束]
- [约束 1]
- [约束 2]

[输出格式]
响应时，使用此格式：
[格式规范]

[示例]（可选）
示例 1：...
示例 2：...
```

### 2. Prompt 模板实现

```csharp
namespace Dawning.Agents.Core.Prompts;

/// <summary>
/// Prompt 模板接口
/// </summary>
public interface IPromptTemplate
{
    /// <summary>
    /// 模板名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 带占位符的模板内容
    /// </summary>
    string Template { get; }
    
    /// <summary>
    /// 必需的输入变量
    /// </summary>
    IReadOnlyList<string> InputVariables { get; }
    
    /// <summary>
    /// 使用给定的值格式化模板
    /// </summary>
    string Format(IDictionary<string, object> values);
    
    /// <summary>
    /// 验证是否提供了所有必需的变量
    /// </summary>
    bool Validate(IDictionary<string, object> values, out IList<string> missingVariables);
}

/// <summary>
/// 简单的 Prompt 模板实现
/// </summary>
public class PromptTemplate : IPromptTemplate
{
    private static readonly Regex VariablePattern = new(@"\{(\w+)\}", RegexOptions.Compiled);

    public string Name { get; }
    public string Template { get; }
    public IReadOnlyList<string> InputVariables { get; }

    public PromptTemplate(string name, string template)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Template = template ?? throw new ArgumentNullException(nameof(template));
        InputVariables = ExtractVariables(template);
    }

    /// <summary>
    /// 从字符串创建 Prompt 模板
    /// </summary>
    public static PromptTemplate FromTemplate(string template, string? name = null)
    {
        return new PromptTemplate(name ?? "default", template);
    }

    public string Format(IDictionary<string, object> values)
    {
        if (!Validate(values, out var missing))
        {
            throw new ArgumentException(
                $"缺少必需的变量：{string.Join(", ", missing)}");
        }

        var result = Template;
        foreach (var (key, value) in values)
        {
            result = result.Replace($"{{{key}}}", value?.ToString() ?? "");
        }

        return result;
    }

    public bool Validate(IDictionary<string, object> values, out IList<string> missingVariables)
    {
        missingVariables = InputVariables
            .Where(v => !values.ContainsKey(v))
            .ToList();

        return missingVariables.Count == 0;
    }

    private static List<string> ExtractVariables(string template)
    {
        return VariablePattern.Matches(template)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
    }
}

/// <summary>
/// Agent 的常用 Prompt 模板
/// </summary>
public static class AgentPrompts
{
    public static PromptTemplate ReActSystemPrompt => PromptTemplate.FromTemplate(
        """
        你是 {agent_name}，一个有帮助的 AI 助手。
        
        你的角色：{agent_description}
        
        你可以使用以下工具：
        {tools}
        
        解决问题时：
        1. 一步一步思考
        2. 需要外部信息时使用工具
        3. 清楚地解释你的推理
        4. 提供准确和有帮助的答案
        
        按此格式响应：
        思考：[你的推理]
        行动：[工具名称]（如果需要）
        行动输入：[工具的输入]
        
        当你有答案时：
        思考：我现在有足够的信息了。
        最终答案：[你的答案]
        """,
        "ReActSystemPrompt");

    public static PromptTemplate TaskPrompt => PromptTemplate.FromTemplate(
        """
        任务：{task}
        
        {context}
        
        请一步一步完成此任务。
        """,
        "TaskPrompt");

    public static PromptTemplate FewShotPrompt => PromptTemplate.FromTemplate(
        """
        {instruction}
        
        以下是一些示例：
        
        {examples}
        
        现在，请完成以下内容：
        
        {input}
        """,
        "FewShotPrompt");
}
```

### 3. 少样本提示

```csharp
namespace Dawning.Agents.Core.Prompts;

/// <summary>
/// 少样本提示构建器
/// </summary>
public class FewShotPromptBuilder
{
    private string _instruction = "";
    private readonly List<Example> _examples = [];
    private string _prefix = "";
    private string _suffix = "";

    public record Example(string Input, string Output);

    public FewShotPromptBuilder WithInstruction(string instruction)
    {
        _instruction = instruction;
        return this;
    }

    public FewShotPromptBuilder AddExample(string input, string output)
    {
        _examples.Add(new Example(input, output));
        return this;
    }

    public FewShotPromptBuilder WithPrefix(string prefix)
    {
        _prefix = prefix;
        return this;
    }

    public FewShotPromptBuilder WithSuffix(string suffix)
    {
        _suffix = suffix;
        return this;
    }

    public string Build(string input)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(_prefix))
        {
            sb.AppendLine(_prefix);
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(_instruction))
        {
            sb.AppendLine(_instruction);
            sb.AppendLine();
        }

        foreach (var example in _examples)
        {
            sb.AppendLine($"输入：{example.Input}");
            sb.AppendLine($"输出：{example.Output}");
            sb.AppendLine();
        }

        sb.AppendLine($"输入：{input}");
        sb.Append("输出：");

        if (!string.IsNullOrEmpty(_suffix))
        {
            sb.AppendLine();
            sb.Append(_suffix);
        }

        return sb.ToString();
    }
}

// 使用示例：
/*
var prompt = new FewShotPromptBuilder()
    .WithInstruction("将以下文本的情感分类为积极、消极或中性。")
    .AddExample("我喜欢这个产品！", "积极")
    .AddExample("这太糟糕了。", "消极")
    .AddExample("还行，没什么特别的。", "中性")
    .Build("客户服务太棒了！");
*/
```

---

## 总结

### Week 3 产出物

```text
src/Dawning.Agents.Core/
├── Agents/
│   ├── IAgent.cs              # Agent 接口
│   ├── AgentBase.cs           # 包含通用功能的基类
│   ├── AgentContext.cs        # 执行上下文
│   ├── AgentResponse.cs       # 响应模型
│   ├── AgentStep.cs           # 步骤记录
│   └── ReActAgent.cs          # ReAct 模式实现
└── Prompts/
    ├── IPromptTemplate.cs     # 模板接口
    ├── PromptTemplate.cs      # 模板实现
    ├── AgentPrompts.cs        # 常用 Agent 提示
    └── FewShotPromptBuilder.cs # 少样本构建器
```

### 学到的关键概念

| 概念 | 描述 |
| ------ | ------ |
| **Agent 循环** | 观察 → 思考 → 行动 周期 |
| **ReAct 模式** | 交错的推理和行动 |
| **草稿本** | 累积的步骤历史 |
| **Prompt 工程** | 系统提示、少样本、模板 |

### 下一步：Week 4

Week 4 将涵盖：

- 对话记忆管理
- Token 计数和上下文窗口
- Agent 状态机实现
