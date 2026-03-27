using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools.Core;
using FluentAssertions;
using Moq;
using Xunit;

namespace Dawning.Agents.Tests.Tools.Core;

/// <summary>
/// ToolSession tests
/// </summary>
public class ToolSessionTests : IDisposable
{
    private readonly Mock<IToolSandbox> _mockSandbox;
    private readonly Mock<IToolStore> _mockStore;
    private readonly ToolSession _session;

    public ToolSessionTests()
    {
        _mockSandbox = new Mock<IToolSandbox>();
        _mockStore = new Mock<IToolStore>();
        _session = new ToolSession(_mockSandbox.Object, _mockStore.Object);
    }

    public void Dispose()
    {
        _session.Dispose();
    }

    #region CreateTool

    [Fact]
    public void CreateTool_ShouldCreateAndReturnTool()
    {
        // Arrange
        var definition = CreateDefinition("my_tool");

        // Act
        var tool = _session.CreateTool(definition);

        // Assert
        tool.Should().NotBeNull();
        tool.Name.Should().Be("my_tool");
    }

    [Fact]
    public void CreateTool_ShouldAppearInSessionTools()
    {
        // Arrange & Act
        _session.CreateTool(CreateDefinition("tool1"));
        _session.CreateTool(CreateDefinition("tool2"));

        // Assert
        var tools = _session.GetSessionTools();
        tools.Should().HaveCount(2);
        tools.Select(t => t.Name).Should().Contain("tool1").And.Contain("tool2");
    }

    [Fact]
    public void CreateTool_SameName_ShouldReplace()
    {
        // Arrange
        _session.CreateTool(CreateDefinition("my_tool", "v1"));
        _session.CreateTool(CreateDefinition("my_tool", "v2"));

        // Assert
        var tools = _session.GetSessionTools();
        tools.Should().HaveCount(1);
        tools[0].Description.Should().Be("v2");
    }

    [Fact]
    public void CreateTool_NullDefinition_ShouldThrow()
    {
        // Act
        var act = () => _session.CreateTool(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateTool_EmptyName_ShouldThrow()
    {
        // Act
        var act = () =>
            _session.CreateTool(
                new EphemeralToolDefinition
                {
                    Name = "",
                    Description = "test",
                    Script = "echo",
                }
            );

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region GetSessionTools

    [Fact]
    public void GetSessionTools_Empty_ShouldReturnEmpty()
    {
        _session.GetSessionTools().Should().BeEmpty();
    }

    #endregion

    #region PromoteToolAsync

    [Fact]
    public async Task PromoteToolAsync_ShouldCallStore()
    {
        // Arrange
        _session.CreateTool(CreateDefinition("my_tool"));

        _mockStore
            .Setup(s =>
                s.SaveToolAsync(
                    It.IsAny<EphemeralToolDefinition>(),
                    ToolScope.User,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        // Act
        await _session.PromoteToolAsync("my_tool", ToolScope.User);

        // Assert
        _mockStore.Verify(
            s =>
                s.SaveToolAsync(
                    It.Is<EphemeralToolDefinition>(d => d.Name == "my_tool"),
                    ToolScope.User,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task PromoteToolAsync_SessionScope_ShouldThrow()
    {
        // Arrange
        _session.CreateTool(CreateDefinition("my_tool"));

        // Act
        var act = () => _session.PromoteToolAsync("my_tool", ToolScope.Session);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task PromoteToolAsync_NonexistentTool_ShouldThrow()
    {
        // Act
        var act = () => _session.PromoteToolAsync("missing", ToolScope.User);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region RemoveToolAsync

    [Fact]
    public async Task RemoveToolAsync_SessionTool_ShouldRemoveFromMemory()
    {
        // Arrange
        _session.CreateTool(CreateDefinition("tool1"));
        _session.GetSessionTools().Should().HaveCount(1);

        // Act
        await _session.RemoveToolAsync("tool1", ToolScope.Session);

        // Assert
        _session.GetSessionTools().Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveToolAsync_UserScope_ShouldDelegateToStore()
    {
        // Act
        await _session.RemoveToolAsync("some_tool", ToolScope.User);

        // Assert
        _mockStore.Verify(
            s => s.DeleteToolAsync("some_tool", ToolScope.User, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    #endregion

    #region ListToolsAsync

    [Fact]
    public async Task ListToolsAsync_Session_ShouldReturnSessionTools()
    {
        // Arrange
        _session.CreateTool(CreateDefinition("s1"));
        _session.CreateTool(CreateDefinition("s2"));

        // Act
        var tools = await _session.ListToolsAsync(ToolScope.Session);

        // Assert
        tools.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListToolsAsync_User_ShouldDelegateToStore()
    {
        // Arrange
        var storedTools = new List<EphemeralToolDefinition>
        {
            CreateDefinition("stored1"),
            CreateDefinition("stored2"),
        };

        _mockStore
            .Setup(s => s.LoadToolsAsync(ToolScope.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedTools);

        // Act
        var tools = await _session.ListToolsAsync(ToolScope.User);

        // Assert
        tools.Should().HaveCount(2);
    }

    #endregion

    #region Dispose

    [Fact]
    public void Dispose_ShouldClearSessionTools()
    {
        // Arrange
        _session.CreateTool(CreateDefinition("tool1"));

        // Act
        _session.Dispose();

        // Assert — calling after dispose should throw
        var act = () => _session.GetSessionTools();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_MultipleDispose_ShouldNotThrow()
    {
        // Act
        var act = () =>
        {
            _session.Dispose();
            _session.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    private static EphemeralToolDefinition CreateDefinition(
        string name,
        string description = "test tool"
    )
    {
        return new EphemeralToolDefinition
        {
            Name = name,
            Description = description,
            Script = "echo test",
        };
    }
}
