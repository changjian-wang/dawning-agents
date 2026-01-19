namespace Dawning.Agents.Core.Prompts;

/// <summary>
/// 预定义的 Agent 提示词模板
/// </summary>
public static class AgentPrompts
{
    /// <summary>
    /// ReAct Agent 系统提示词
    /// </summary>
    public static readonly PromptTemplate ReActSystem = PromptTemplate.Create(
        "react-system",
        """
        {instructions}

        You are an AI assistant that follows the ReAct pattern (Reasoning + Acting).
        When answering questions, use the following format:

        Thought: [Your reasoning about what to do]
        Action: [The action to take from available tools]
        Action Input: [The input for the action]

        After receiving the observation from the action, continue with:
        Thought: [Your updated reasoning based on the observation]
        ...

        When you have gathered enough information to provide the final answer, use:
        Final Answer: [Your complete answer to the user's question]

        Available tools:
        {tools}

        Important:
        - Always think step by step and explain your reasoning
        - Use tools when you need external information
        - Provide Final Answer when you're confident about the response
        """
    );

    /// <summary>
    /// ReAct Agent 用户提示词
    /// </summary>
    public static readonly PromptTemplate ReActUser = PromptTemplate.Create(
        "react-user",
        """
        Question: {question}

        {history}
        """
    );

    /// <summary>
    /// 简单对话 Agent 系统提示词
    /// </summary>
    public static readonly PromptTemplate SimpleSystem = PromptTemplate.Create(
        "simple-system",
        """
        {instructions}

        You are a helpful AI assistant. Answer the user's questions directly and concisely.
        """
    );
}
