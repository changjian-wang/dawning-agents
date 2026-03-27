namespace Dawning.Agents.Core.Prompts;

/// <summary>
/// Predefined agent prompt templates.
/// </summary>
public static class AgentPrompts
{
    /// <summary>
    /// Gets the ReAct agent system prompt template.
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
    /// Gets the ReAct agent user prompt template.
    /// </summary>
    public static readonly PromptTemplate ReActUser = PromptTemplate.Create(
        "react-user",
        """
        Question: {question}

        {history}
        """
    );

    /// <summary>
    /// Gets the simple conversational agent system prompt template.
    /// </summary>
    public static readonly PromptTemplate SimpleSystem = PromptTemplate.Create(
        "simple-system",
        """
        {instructions}

        You are a helpful AI assistant. Answer the user's questions directly and concisely.
        """
    );
}
