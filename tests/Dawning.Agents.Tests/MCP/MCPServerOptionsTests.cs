namespace Dawning.Agents.Tests.MCP;

using Dawning.Agents.MCP.Server;
using FluentAssertions;
using Xunit;

public class MCPServerOptionsTests
{
    [Fact]
    public void Default_Options_Should_Have_Expected_Values()
    {
        // Arrange & Act
        var options = new MCPServerOptions();

        // Assert
        options.Name.Should().Be("Dawning.Agents.MCP");
        options.Version.Should().Be("1.0.0");
        options.EnableTools.Should().BeTrue();
        options.EnableResources.Should().BeTrue();
        options.EnablePrompts.Should().BeTrue();
        options.EnableLogging.Should().BeTrue();
        options.ToolTimeoutSeconds.Should().Be(30);
        options.MaxConcurrentRequests.Should().Be(10);
        options.TransportType.Should().Be(MCPTransportType.Stdio);
        options.HttpPort.Should().Be(8080);
    }

    [Fact]
    public void SectionName_Should_Be_MCP()
    {
        MCPServerOptions.SectionName.Should().Be("MCP");
    }

    [Fact]
    public void Options_Should_Be_Configurable()
    {
        // Arrange & Act
        var options = new MCPServerOptions
        {
            Name = "CustomServer",
            Version = "2.0.0",
            EnableTools = false,
            EnableResources = false,
            ToolTimeoutSeconds = 60,
            MaxConcurrentRequests = 20,
            TransportType = MCPTransportType.Http,
            HttpPort = 9090,
        };

        // Assert
        options.Name.Should().Be("CustomServer");
        options.Version.Should().Be("2.0.0");
        options.EnableTools.Should().BeFalse();
        options.EnableResources.Should().BeFalse();
        options.ToolTimeoutSeconds.Should().Be(60);
        options.MaxConcurrentRequests.Should().Be(20);
        options.TransportType.Should().Be(MCPTransportType.Http);
        options.HttpPort.Should().Be(9090);
    }
}

public class MCPTransportTypeTests
{
    [Fact]
    public void TransportType_Should_Have_Stdio_And_Http()
    {
        // Assert
        Enum.GetValues<MCPTransportType>().Should().HaveCount(2);
        Enum.IsDefined(MCPTransportType.Stdio).Should().BeTrue();
        Enum.IsDefined(MCPTransportType.Http).Should().BeTrue();
    }

    [Fact]
    public void Stdio_Should_Be_Default()
    {
        // Arrange & Act
        var defaultValue = default(MCPTransportType);

        // Assert
        defaultValue.Should().Be(MCPTransportType.Stdio);
    }
}
