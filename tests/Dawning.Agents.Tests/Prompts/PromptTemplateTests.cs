using Dawning.Agents.Abstractions.Prompts;
using Dawning.Agents.Core.Prompts;
using FluentAssertions;

namespace Dawning.Agents.Tests.Prompts;

public class PromptTemplateTests
{
    [Fact]
    public void Format_ShouldReplaceVariables()
    {
        // Arrange
        var template = new PromptTemplate("test", "Hello {name}, you are {age} years old.");
        var variables = new Dictionary<string, object> { ["name"] = "Alice", ["age"] = 30 };

        // Act
        var result = template.Format(variables);

        // Assert
        result.Should().Be("Hello Alice, you are 30 years old.");
    }

    [Fact]
    public void Format_ShouldPreserveUnmatchedPlaceholders()
    {
        // Arrange
        var template = new PromptTemplate("test", "Hello {name}, your score is {score}.");
        var variables = new Dictionary<string, object> { ["name"] = "Bob" };

        // Act
        var result = template.Format(variables);

        // Assert
        result.Should().Be("Hello Bob, your score is {score}.");
    }

    [Fact]
    public void Format_ShouldReturnOriginalWhenNoVariables()
    {
        // Arrange
        var template = new PromptTemplate("test", "Hello {name}!");
        var variables = new Dictionary<string, object>();

        // Act
        var result = template.Format(variables);

        // Assert
        result.Should().Be("Hello {name}!");
    }

    [Fact]
    public void Format_ShouldHandleNullVariables()
    {
        // Arrange
        var template = new PromptTemplate("test", "Hello {name}!");
        Dictionary<string, object>? variables = null;

        // Act
        var result = template.Format(variables!);

        // Assert
        result.Should().Be("Hello {name}!");
    }

    [Fact]
    public void Create_ShouldCreateTemplateCorrectly()
    {
        // Arrange & Act
        var template = PromptTemplate.Create("my-template", "Content here");

        // Assert
        template.Name.Should().Be("my-template");
        template.Template.Should().Be("Content here");
    }

    [Fact]
    public void Constructor_ShouldThrowWhenNameIsNull()
    {
        // Act
        var act = () => new PromptTemplate(null!, "template");

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    [Fact]
    public void Constructor_ShouldThrowWhenTemplateIsNull()
    {
        // Act
        var act = () => new PromptTemplate("name", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("template");
    }
}
