using Dawning.Agents.Core.Prompts;
using FluentAssertions;

namespace Dawning.Agents.Tests.Prompts;

/// <summary>
/// AgentPrompts 单元测试
/// </summary>
public class AgentPromptsTests
{
    #region ReActSystem Tests

    [Fact]
    public void ReActSystem_HasCorrectName()
    {
        AgentPrompts.ReActSystem.Name.Should().Be("react-system");
    }

    [Fact]
    public void ReActSystem_ContainsExpectedPlaceholders()
    {
        var template = AgentPrompts.ReActSystem.Template;

        template.Should().Contain("{instructions}");
        template.Should().Contain("{tools}");
    }

    [Fact]
    public void ReActSystem_ContainsReActGuidance()
    {
        var template = AgentPrompts.ReActSystem.Template;

        template.Should().Contain("Thought:");
        template.Should().Contain("Action:");
        template.Should().Contain("Action Input:");
        template.Should().Contain("Final Answer:");
    }

    [Fact]
    public void ReActSystem_Render_ReplacesPlaceholders()
    {
        var result = AgentPrompts.ReActSystem.Format(
            new Dictionary<string, object>
            {
                ["instructions"] = "You are a helpful assistant.",
                ["tools"] = "- search: Search the web\n- calculate: Do math",
            }
        );

        result.Should().Contain("You are a helpful assistant.");
        result.Should().Contain("search: Search the web");
        result.Should().NotContain("{instructions}");
        result.Should().NotContain("{tools}");
    }

    #endregion

    #region ReActUser Tests

    [Fact]
    public void ReActUser_HasCorrectName()
    {
        AgentPrompts.ReActUser.Name.Should().Be("react-user");
    }

    [Fact]
    public void ReActUser_ContainsExpectedPlaceholders()
    {
        var template = AgentPrompts.ReActUser.Template;

        template.Should().Contain("{question}");
        template.Should().Contain("{history}");
    }

    [Fact]
    public void ReActUser_Render_ReplacesPlaceholders()
    {
        var result = AgentPrompts.ReActUser.Format(
            new Dictionary<string, object>
            {
                ["question"] = "What is the weather today?",
                ["history"] = "Previous conversation...",
            }
        );

        result.Should().Contain("What is the weather today?");
        result.Should().Contain("Previous conversation...");
        result.Should().NotContain("{question}");
        result.Should().NotContain("{history}");
    }

    #endregion

    #region SimpleSystem Tests

    [Fact]
    public void SimpleSystem_HasCorrectName()
    {
        AgentPrompts.SimpleSystem.Name.Should().Be("simple-system");
    }

    [Fact]
    public void SimpleSystem_ContainsInstructionsPlaceholder()
    {
        var template = AgentPrompts.SimpleSystem.Template;

        template.Should().Contain("{instructions}");
    }

    [Fact]
    public void SimpleSystem_Render_ReplacesPlaceholders()
    {
        var result = AgentPrompts.SimpleSystem.Format(
            new Dictionary<string, object> { ["instructions"] = "Be concise and helpful." }
        );

        result.Should().Contain("Be concise and helpful.");
        result.Should().NotContain("{instructions}");
    }

    #endregion

    #region Template Immutability Tests

    [Fact]
    public void Templates_AreReadOnly()
    {
        // Templates should be static readonly and not modifiable
        var template1 = AgentPrompts.ReActSystem;
        var template2 = AgentPrompts.ReActSystem;

        template1.Should().BeSameAs(template2);
    }

    #endregion
}
