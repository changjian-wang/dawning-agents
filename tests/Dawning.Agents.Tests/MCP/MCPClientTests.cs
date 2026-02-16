namespace Dawning.Agents.Tests.MCP;

using Dawning.Agents.MCP.Client;
using Dawning.Agents.MCP.Protocol;
using FluentAssertions;
using Xunit;

public class MCPClientOptionsTests
{
    [Fact]
    public void Default_Options_Should_Have_Expected_Values()
    {
        // Arrange & Act
        var options = new MCPClientOptions();

        // Assert
        options.Name.Should().Be("Dawning.Agents");
        options.Version.Should().Be("1.0.0");
        options.ConnectionTimeoutSeconds.Should().Be(30);
        options.RequestTimeoutSeconds.Should().Be(60);
        options.ToolCallTimeoutSeconds.Should().Be(120);
        options.AutoReconnect.Should().BeTrue();
        options.MaxReconnectAttempts.Should().Be(3);
        options.ReconnectIntervalSeconds.Should().Be(5);
    }

    [Fact]
    public void SectionName_Should_Be_MCPClient()
    {
        MCPClientOptions.SectionName.Should().Be("MCPClient");
    }

    [Fact]
    public void Options_Should_Be_Configurable()
    {
        // Arrange & Act
        var options = new MCPClientOptions
        {
            Name = "CustomClient",
            Version = "2.0.0",
            ConnectionTimeoutSeconds = 60,
            RequestTimeoutSeconds = 120,
            ToolCallTimeoutSeconds = 300,
            AutoReconnect = false,
            MaxReconnectAttempts = 5,
            ReconnectIntervalSeconds = 10,
        };

        // Assert
        options.Name.Should().Be("CustomClient");
        options.Version.Should().Be("2.0.0");
        options.ConnectionTimeoutSeconds.Should().Be(60);
        options.RequestTimeoutSeconds.Should().Be(120);
        options.ToolCallTimeoutSeconds.Should().Be(300);
        options.AutoReconnect.Should().BeFalse();
        options.MaxReconnectAttempts.Should().Be(5);
        options.ReconnectIntervalSeconds.Should().Be(10);
    }
}

public class MCPToolProxyTests
{
    [Fact]
    public void MCPToolProxy_Should_Have_Correct_Properties()
    {
        // Arrange
        var definition = new MCPToolDefinition
        {
            Name = "test_tool",
            Description = "A test tool",
            InputSchema = new MCPInputSchema
            {
                Type = "object",
                Properties = new Dictionary<string, MCPPropertySchema>
                {
                    ["param1"] = new MCPPropertySchema { Type = "string" },
                },
            },
        };

        // Act - 我们使用一个 null client 因为只测试属性
        // 实际使用时会有真实的 client
        var proxy = new MCPToolProxy(null!, definition);

        // Assert
        proxy.Name.Should().Be("test_tool");
        proxy.Description.Should().Be("A test tool");
        proxy.Category.Should().Be("MCP");
        proxy.RiskLevel.Should().Be(Dawning.Agents.Abstractions.Tools.ToolRiskLevel.Medium);
        proxy.RequiresConfirmation.Should().BeFalse();
        proxy.ParametersSchema.Should().Contain("param1");
    }

    [Fact]
    public void MCPToolProxy_Should_Handle_Null_Description()
    {
        // Arrange
        var definition = new MCPToolDefinition
        {
            Name = "test_tool",
            Description = null,
            InputSchema = new MCPInputSchema(),
        };

        // Act
        var proxy = new MCPToolProxy(null!, definition);

        // Assert
        proxy.Description.Should().BeEmpty();
    }
}

public class MCPExceptionTests
{
    [Fact]
    public void MCPException_Should_Have_ErrorCode_And_Message()
    {
        // Arrange & Act
        var exception = new MCPException(MCPErrorCodes.ToolNotFound, "Tool not found");

        // Assert
        exception.ErrorCode.Should().Be(MCPErrorCodes.ToolNotFound);
        exception.Message.Should().Be("Tool not found");
    }

    [Fact]
    public void MCPException_Should_Support_All_Error_Codes()
    {
        // Arrange & Act & Assert
        new MCPException(MCPErrorCodes.ParseError, "Parse error")
            .ErrorCode.Should()
            .Be(-32700);
        new MCPException(MCPErrorCodes.InvalidRequest, "Invalid request")
            .ErrorCode.Should()
            .Be(-32600);
        new MCPException(MCPErrorCodes.MethodNotFound, "Method not found")
            .ErrorCode.Should()
            .Be(-32601);
        new MCPException(MCPErrorCodes.InvalidParams, "Invalid params")
            .ErrorCode.Should()
            .Be(-32602);
        new MCPException(MCPErrorCodes.InternalError, "Internal error")
            .ErrorCode.Should()
            .Be(-32603);
        new MCPException(MCPErrorCodes.ResourceNotFound, "Resource not found")
            .ErrorCode.Should()
            .Be(-32001);
        new MCPException(MCPErrorCodes.ToolNotFound, "Tool not found")
            .ErrorCode.Should()
            .Be(-32002);
        new MCPException(MCPErrorCodes.ToolExecutionFailed, "Execution failed")
            .ErrorCode.Should()
            .Be(-32003);
    }
}
