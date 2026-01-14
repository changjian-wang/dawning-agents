# Week 3: Agent Core Loop - Implementation Guide

> Phase 2: Single Agent Development Core Skills
> Week 3 Learning Material: Understanding and Implementing the Agent Loop

---

## Day 1-2: Understanding the Agent Loop

### 1. The Agent Execution Cycle

The core of any AI agent is its execution loop. This follows a fundamental pattern:

```text
┌─────────────────────────────────────────────────────────────────┐
│                     Agent Execution Loop                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│    ┌──────────┐                                                  │
│    │  START   │                                                  │
│    └────┬─────┘                                                  │
│         │                                                        │
│         ▼                                                        │
│    ┌──────────┐     ┌──────────┐     ┌──────────┐               │
│    │ OBSERVE  │────►│  THINK   │────►│   ACT    │               │
│    │ (Input)  │     │ (Reason) │     │ (Execute)│               │
│    └──────────┘     └──────────┘     └────┬─────┘               │
│         ▲                                  │                     │
│         │                                  │                     │
│         └──────────────────────────────────┘                     │
│                    (Loop until done)                             │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2. LangChain Agent Architecture Analysis

#### Key Components from LangChain Source

```python
# Simplified from langchain/agents/agent.py

class AgentExecutor:
    """Agent execution loop implementation."""
    
    def __init__(self, agent, tools, memory=None, max_iterations=15):
        self.agent = agent
        self.tools = {tool.name: tool for tool in tools}
        self.memory = memory
        self.max_iterations = max_iterations
    
    def run(self, input: str) -> str:
        """Main execution loop."""
        intermediate_steps = []
        iterations = 0
        
        while iterations < self.max_iterations:
            # THINK: Agent decides what to do
            output = self.agent.plan(
                input=input,
                intermediate_steps=intermediate_steps
            )
            
            # Check if agent wants to finish
            if isinstance(output, AgentFinish):
                return output.return_values["output"]
            
            # ACT: Execute the chosen action
            action = output  # AgentAction
            tool = self.tools[action.tool]
            observation = tool.run(action.tool_input)
            
            # OBSERVE: Record the result
            intermediate_steps.append((action, observation))
            iterations += 1
        
        return "Max iterations reached"
```

#### The Planning Function

```python
# From langchain/agents/mrkl/base.py

class ZeroShotAgent:
    """ReAct-style agent that uses reasoning."""
    
    def plan(self, input: str, intermediate_steps: list) -> AgentAction | AgentFinish:
        """Decide next action based on current state."""
        
        # Build prompt with current state
        prompt = self._build_prompt(input, intermediate_steps)
        
        # Get LLM response
        llm_output = self.llm.predict(prompt)
        
        # Parse the response
        return self._parse_output(llm_output)
    
    def _build_prompt(self, input: str, steps: list) -> str:
        """Build prompt with scratchpad of previous steps."""
        scratchpad = ""
        for action, observation in steps:
            scratchpad += f"Thought: {action.log}\n"
            scratchpad += f"Action: {action.tool}\n"
            scratchpad += f"Action Input: {action.tool_input}\n"
            scratchpad += f"Observation: {observation}\n"
        
        return f"""Answer the following question:
{input}

You have access to the following tools:
{self._format_tools()}

Use this format:
Thought: your reasoning
Action: tool_name
Action Input: input to the tool
Observation: tool result
... (repeat as needed)
Thought: I now know the answer
Final Answer: the final answer

{scratchpad}
Thought:"""
```

### 3. ReAct Pattern Deep Dive

The ReAct (Reasoning + Acting) pattern interleaves:

- **Reasoning traces**: Verbal explanations of the agent's thought process
- **Actions**: Interactions with external tools or environments

```text
Question: What is the capital of the country where the Eiffel Tower is located?

Thought 1: I need to find where the Eiffel Tower is located.
Action 1: Search[Eiffel Tower location]
Observation 1: The Eiffel Tower is located in Paris, France.

Thought 2: The Eiffel Tower is in France. Now I need the capital of France.
Action 2: Search[capital of France]
Observation 2: Paris is the capital of France.

Thought 3: I now know the answer.
Final Answer: Paris
```

---

## Day 3-4: Implementing the Basic Agent

### 1. Core Interfaces Design

#### IAgent Interface

```csharp
namespace DawningAgents.Core.Agents;

/// <summary>
/// Represents the result of an agent execution
/// </summary>
public record AgentResponse
{
    /// <summary>
    /// The final output from the agent
    /// </summary>
    public required string Output { get; init; }
    
    /// <summary>
    /// Whether the agent completed successfully
    /// </summary>
    public bool IsSuccess { get; init; } = true;
    
    /// <summary>
    /// The intermediate steps taken by the agent
    /// </summary>
    public IReadOnlyList<AgentStep> Steps { get; init; } = [];
    
    /// <summary>
    /// Total tokens used during execution
    /// </summary>
    public int TotalTokens { get; init; }
    
    /// <summary>
    /// Error message if execution failed
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// Represents a single step in agent execution
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
/// Base interface for all agents
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Unique name of the agent
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Description of what the agent does
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Execute the agent with the given context
    /// </summary>
    Task<AgentResponse> ExecuteAsync(
        AgentContext context, 
        CancellationToken cancellationToken = default);
}
```

#### AgentContext

```csharp
namespace DawningAgents.Core.Agents;

/// <summary>
/// Context for agent execution
/// </summary>
public class AgentContext
{
    /// <summary>
    /// The user's input/question
    /// </summary>
    public required string Input { get; init; }
    
    /// <summary>
    /// Optional system prompt override
    /// </summary>
    public string? SystemPrompt { get; init; }
    
    /// <summary>
    /// Available tools for the agent
    /// </summary>
    public IReadOnlyList<ITool> Tools { get; init; } = [];
    
    /// <summary>
    /// Conversation memory
    /// </summary>
    public IConversationMemory? Memory { get; init; }
    
    /// <summary>
    /// Maximum iterations before stopping
    /// </summary>
    public int MaxIterations { get; init; } = 10;
    
    /// <summary>
    /// Additional metadata
    /// </summary>
    public IDictionary<string, object> Metadata { get; init; } = 
        new Dictionary<string, object>();
}
```

### 2. AgentBase Abstract Class

```csharp
namespace DawningAgents.Core.Agents;

using DawningAgents.Core.LLM;
using Microsoft.Extensions.Logging;

/// <summary>
/// Base class for all agents with common functionality
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
    /// Build the system prompt for the agent
    /// </summary>
    protected virtual string BuildSystemPrompt(AgentContext context)
    {
        return context.SystemPrompt ?? GetDefaultSystemPrompt();
    }

    /// <summary>
    /// Get the default system prompt
    /// </summary>
    protected abstract string GetDefaultSystemPrompt();

    /// <summary>
    /// Format available tools for the prompt
    /// </summary>
    protected string FormatTools(IReadOnlyList<ITool> tools)
    {
        if (tools.Count == 0)
            return "No tools available.";

        var sb = new StringBuilder();
        foreach (var tool in tools)
        {
            sb.AppendLine($"- {tool.Name}: {tool.Description}");
            if (!string.IsNullOrEmpty(tool.Parameters))
            {
                sb.AppendLine($"  Parameters: {tool.Parameters}");
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Execute a tool by name
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
            Logger.LogWarning("Tool not found: {ToolName}", toolName);
            return $"Error: Tool '{toolName}' not found. Available tools: {string.Join(", ", tools.Select(t => t.Name))}";
        }

        try
        {
            Logger.LogDebug("Executing tool {ToolName} with input: {Input}", toolName, input);
            var result = await tool.ExecuteAsync(input, cancellationToken);
            Logger.LogDebug("Tool {ToolName} result: {Result}", toolName, result);
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error executing tool {ToolName}", toolName);
            return $"Error executing tool: {ex.Message}";
        }
    }
}
```

### 3. ReAct Agent Implementation

```csharp
namespace DawningAgents.Core.Agents;

using System.Text.RegularExpressions;
using DawningAgents.Core.LLM;
using Microsoft.Extensions.Logging;

/// <summary>
/// Agent that follows the ReAct (Reasoning + Acting) pattern
/// </summary>
public partial class ReActAgent : AgentBase
{
    private const string DefaultName = "ReActAgent";
    private const string DefaultDescription = "An agent that reasons and acts to solve problems";

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

        Logger.LogInformation("Starting ReAct agent execution for input: {Input}", context.Input);

        for (int iteration = 0; iteration < context.MaxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Build prompt with current state
            var prompt = BuildPrompt(context, scratchpad.ToString());

            // Get LLM response
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

            // Parse the response
            var parseResult = ParseResponse(response.Content);

            // Create step record
            var step = new AgentStep
            {
                Thought = parseResult.Thought,
                Action = parseResult.Action,
                ActionInput = parseResult.ActionInput
            };

            // Check if agent wants to finish
            if (parseResult.IsFinalAnswer)
            {
                Logger.LogInformation("Agent reached final answer after {Iterations} iterations", iteration + 1);
                steps.Add(step);

                return new AgentResponse
                {
                    Output = parseResult.FinalAnswer ?? parseResult.Thought,
                    IsSuccess = true,
                    Steps = steps,
                    TotalTokens = totalTokens
                };
            }

            // Execute the action if specified
            if (!string.IsNullOrEmpty(parseResult.Action))
            {
                var observation = await ExecuteToolAsync(
                    parseResult.Action,
                    parseResult.ActionInput ?? "",
                    context.Tools,
                    cancellationToken);

                step = step with { Observation = observation };

                // Update scratchpad
                scratchpad.AppendLine($"Thought: {parseResult.Thought}");
                scratchpad.AppendLine($"Action: {parseResult.Action}");
                scratchpad.AppendLine($"Action Input: {parseResult.ActionInput}");
                scratchpad.AppendLine($"Observation: {observation}");
            }

            steps.Add(step);
            Logger.LogDebug("Completed iteration {Iteration}", iteration + 1);
        }

        Logger.LogWarning("Agent reached maximum iterations without final answer");

        return new AgentResponse
        {
            Output = "I was unable to complete the task within the allowed iterations.",
            IsSuccess = false,
            Steps = steps,
            TotalTokens = totalTokens,
            Error = "Maximum iterations reached"
        };
    }

    protected override string GetDefaultSystemPrompt()
    {
        return """
            You are a helpful AI assistant that solves problems step by step.
            You have access to tools that can help you gather information and take actions.
            
            Always think carefully before acting, and explain your reasoning.
            When you have enough information to answer, provide the final answer.
            """;
    }

    private string BuildPrompt(AgentContext context, string scratchpad)
    {
        var toolsDescription = FormatTools(context.Tools);

        return $"""
            Answer the following question or complete the task:
            
            Question/Task: {context.Input}
            
            You have access to the following tools:
            {toolsDescription}
            
            Use this format:
            
            Thought: [your reasoning about what to do next]
            Action: [the tool name to use]
            Action Input: [the input to the tool]
            Observation: [the result from the tool - this will be provided to you]
            
            ... (repeat Thought/Action/Action Input/Observation as needed)
            
            When you have enough information to answer:
            Thought: I now have enough information to answer.
            Final Answer: [your final answer]
            
            Previous steps:
            {scratchpad}
            
            Now continue from where you left off:
            Thought:
            """;
    }

    private ParsedResponse ParseResponse(string response)
    {
        var result = new ParsedResponse();

        // Extract Thought
        var thoughtMatch = ThoughtRegex().Match(response);
        if (thoughtMatch.Success)
        {
            result.Thought = thoughtMatch.Groups[1].Value.Trim();
        }

        // Check for Final Answer
        var finalAnswerMatch = FinalAnswerRegex().Match(response);
        if (finalAnswerMatch.Success)
        {
            result.IsFinalAnswer = true;
            result.FinalAnswer = finalAnswerMatch.Groups[1].Value.Trim();
            return result;
        }

        // Extract Action
        var actionMatch = ActionRegex().Match(response);
        if (actionMatch.Success)
        {
            result.Action = actionMatch.Groups[1].Value.Trim();
        }

        // Extract Action Input
        var actionInputMatch = ActionInputRegex().Match(response);
        if (actionInputMatch.Success)
        {
            result.ActionInput = actionInputMatch.Groups[1].Value.Trim();
        }

        return result;
    }

    [GeneratedRegex(@"Thought:\s*(.+?)(?=Action:|Final Answer:|$)", RegexOptions.Singleline)]
    private static partial Regex ThoughtRegex();

    [GeneratedRegex(@"Final Answer:\s*(.+?)$", RegexOptions.Singleline)]
    private static partial Regex FinalAnswerRegex();

    [GeneratedRegex(@"Action:\s*(.+?)(?=Action Input:|$)", RegexOptions.Singleline)]
    private static partial Regex ActionRegex();

    [GeneratedRegex(@"Action Input:\s*(.+?)(?=Observation:|Thought:|$)", RegexOptions.Singleline)]
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

## Day 5-7: Prompt Engineering

### 1. System Prompt Design Principles

#### Key Principles

| Principle | Description | Example |
|-----------|-------------|---------|
| **Clarity** | Be explicit about the agent's role | "You are a customer service agent for..." |
| **Constraints** | Define boundaries | "Only answer questions about our products" |
| **Format** | Specify output structure | "Always respond in JSON format" |
| **Tone** | Set communication style | "Be professional but friendly" |
| **Examples** | Provide demonstrations | Few-shot examples |

#### Effective System Prompt Structure

```text
[Role Definition]
You are [specific role] that [primary function].

[Capabilities]
You have access to:
- [Tool/Capability 1]
- [Tool/Capability 2]

[Constraints]
- [Constraint 1]
- [Constraint 2]

[Output Format]
When responding, use this format:
[Format specification]

[Examples] (optional)
Example 1: ...
Example 2: ...
```

### 2. Prompt Template Implementation

```csharp
namespace DawningAgents.Core.Prompts;

/// <summary>
/// Interface for prompt templates
/// </summary>
public interface IPromptTemplate
{
    /// <summary>
    /// Template name
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Template content with placeholders
    /// </summary>
    string Template { get; }
    
    /// <summary>
    /// Required input variables
    /// </summary>
    IReadOnlyList<string> InputVariables { get; }
    
    /// <summary>
    /// Format the template with given values
    /// </summary>
    string Format(IDictionary<string, object> values);
    
    /// <summary>
    /// Validate that all required variables are provided
    /// </summary>
    bool Validate(IDictionary<string, object> values, out IList<string> missingVariables);
}

/// <summary>
/// Simple prompt template implementation
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
    /// Create a prompt template from a string
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
                $"Missing required variables: {string.Join(", ", missing)}");
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
/// Common prompt templates for agents
/// </summary>
public static class AgentPrompts
{
    public static PromptTemplate ReActSystemPrompt => PromptTemplate.FromTemplate(
        """
        You are {agent_name}, a helpful AI assistant.
        
        Your role: {agent_description}
        
        You have access to the following tools:
        {tools}
        
        When solving problems:
        1. Think step by step
        2. Use tools when you need external information
        3. Explain your reasoning clearly
        4. Provide accurate and helpful answers
        
        Format your responses as:
        Thought: [your reasoning]
        Action: [tool name] (if needed)
        Action Input: [input for the tool]
        
        When you have the answer:
        Thought: I now have enough information.
        Final Answer: [your answer]
        """,
        "ReActSystemPrompt");

    public static PromptTemplate TaskPrompt => PromptTemplate.FromTemplate(
        """
        Task: {task}
        
        {context}
        
        Please complete this task step by step.
        """,
        "TaskPrompt");

    public static PromptTemplate FewShotPrompt => PromptTemplate.FromTemplate(
        """
        {instruction}
        
        Here are some examples:
        
        {examples}
        
        Now, please complete the following:
        
        {input}
        """,
        "FewShotPrompt");
}
```

### 3. Few-Shot Prompting

```csharp
namespace DawningAgents.Core.Prompts;

/// <summary>
/// Builder for few-shot prompts
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
            sb.AppendLine($"Input: {example.Input}");
            sb.AppendLine($"Output: {example.Output}");
            sb.AppendLine();
        }

        sb.AppendLine($"Input: {input}");
        sb.Append("Output:");

        if (!string.IsNullOrEmpty(_suffix))
        {
            sb.AppendLine();
            sb.Append(_suffix);
        }

        return sb.ToString();
    }
}

// Usage example:
/*
var prompt = new FewShotPromptBuilder()
    .WithInstruction("Classify the sentiment of the following text as positive, negative, or neutral.")
    .AddExample("I love this product!", "positive")
    .AddExample("This is terrible.", "negative")
    .AddExample("It's okay, nothing special.", "neutral")
    .Build("The customer service was amazing!");
*/
```

---

## Summary

### Week 3 Deliverables

```text
src/DawningAgents.Core/
├── Agents/
│   ├── IAgent.cs              # Agent interface
│   ├── AgentBase.cs           # Base class with common functionality
│   ├── AgentContext.cs        # Execution context
│   ├── AgentResponse.cs       # Response model
│   ├── AgentStep.cs           # Step record
│   └── ReActAgent.cs          # ReAct pattern implementation
└── Prompts/
    ├── IPromptTemplate.cs     # Template interface
    ├── PromptTemplate.cs      # Template implementation
    ├── AgentPrompts.cs        # Common agent prompts
    └── FewShotPromptBuilder.cs # Few-shot builder
```

### Key Concepts Learned

| Concept | Description |
| --------- | ------------- |
| **Agent Loop** | Observe → Think → Act cycle |
| **ReAct Pattern** | Interleaved reasoning and actions |
| **Scratchpad** | Accumulated history of steps |
| **Prompt Engineering** | System prompts, few-shot, templates |

### Next: Week 4

Week 4 will cover:

- Conversation memory management
- Token counting and context windows
- Agent state machine implementation
