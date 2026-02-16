namespace Dawning.Agents.Tests.MCP;

using System.Text.Json;
using Dawning.Agents.MCP.Protocol;
using FluentAssertions;
using Xunit;

public class MCPMessageTests
{
    [Fact]
    public void MCPRequest_Should_Serialize_Correctly()
    {
        // Arrange
        var request = new MCPRequest
        {
            Id = 1,
            Method = "tools/list",
            Params = new { cursor = (string?)null },
        };

        // Act
        var json = JsonSerializer.Serialize(request);
        var deserialized = JsonSerializer.Deserialize<MCPRequest>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.JsonRpc.Should().Be("2.0");
        deserialized.Id.Should().NotBeNull();
        deserialized.Method.Should().Be("tools/list");
    }

    [Fact]
    public void MCPResponse_Success_Should_Create_Valid_Response()
    {
        // Arrange & Act
        var response = MCPResponse.Success(1, new { data = "test" });

        // Assert
        response.JsonRpc.Should().Be("2.0");
        response.Id.Should().Be(1);
        response.Result.Should().NotBeNull();
        response.Error.Should().BeNull();
    }

    [Fact]
    public void MCPResponse_Failure_Should_Create_Valid_Error_Response()
    {
        // Arrange & Act
        var response = MCPResponse.Failure(1, MCPErrorCodes.MethodNotFound, "Unknown method");

        // Assert
        response.JsonRpc.Should().Be("2.0");
        response.Id.Should().Be(1);
        response.Result.Should().BeNull();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(MCPErrorCodes.MethodNotFound);
        response.Error.Message.Should().Be("Unknown method");
    }

    [Fact]
    public void MCPContent_TextContent_Should_Create_Text_Type()
    {
        // Arrange & Act
        var content = MCPContent.TextContent("Hello, World!");

        // Assert
        content.Type.Should().Be("text");
        content.Text.Should().Be("Hello, World!");
    }

    [Fact]
    public void MCPContent_ImageContent_Should_Create_Image_Type()
    {
        // Arrange & Act
        var content = MCPContent.ImageContent("base64data", "image/jpeg");

        // Assert
        content.Type.Should().Be("image");
        content.Data.Should().Be("base64data");
        content.MimeType.Should().Be("image/jpeg");
    }
}

public class MCPCapabilitiesTests
{
    [Fact]
    public void MCPServerCapabilities_Should_Serialize_WithAllProperties()
    {
        // Arrange
        var capabilities = new MCPServerCapabilities
        {
            Tools = new ToolsCapability { ListChanged = true },
            Resources = new ResourcesCapability { Subscribe = true, ListChanged = true },
            Prompts = new PromptsCapability { ListChanged = true },
            Logging = new LoggingCapability(),
        };

        // Act
        var json = JsonSerializer.Serialize(capabilities);
        var deserialized = JsonSerializer.Deserialize<MCPServerCapabilities>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Tools.Should().NotBeNull();
        deserialized.Tools!.ListChanged.Should().BeTrue();
        deserialized.Resources.Should().NotBeNull();
        deserialized.Resources!.Subscribe.Should().BeTrue();
    }

    [Fact]
    public void InitializeResult_Should_Contain_Required_Fields()
    {
        // Arrange
        var result = new InitializeResult
        {
            ProtocolVersion = MCPProtocolVersion.Latest,
            Capabilities = new MCPServerCapabilities
            {
                Tools = new ToolsCapability { ListChanged = true },
            },
            ServerInfo = new MCPServerInfo { Name = "TestServer", Version = "1.0.0" },
        };

        // Act
        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<InitializeResult>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.ProtocolVersion.Should().Be("2024-11-05");
        deserialized.ServerInfo.Name.Should().Be("TestServer");
        deserialized.ServerInfo.Version.Should().Be("1.0.0");
    }
}

public class MCPToolDefinitionTests
{
    [Fact]
    public void MCPToolDefinition_Should_Serialize_InputSchema()
    {
        // Arrange
        var tool = new MCPToolDefinition
        {
            Name = "calculate",
            Description = "Perform calculations",
            InputSchema = new MCPInputSchema
            {
                Type = "object",
                Properties = new Dictionary<string, MCPPropertySchema>
                {
                    ["expression"] = new MCPPropertySchema
                    {
                        Type = "string",
                        Description = "Math expression to evaluate",
                    },
                },
                Required = ["expression"],
            },
        };

        // Act
        var json = JsonSerializer.Serialize(tool);
        var deserialized = JsonSerializer.Deserialize<MCPToolDefinition>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Name.Should().Be("calculate");
        deserialized.InputSchema.Should().NotBeNull();
        deserialized.InputSchema.Properties.Should().ContainKey("expression");
        deserialized.InputSchema.Required.Should().Contain("expression");
    }

    [Fact]
    public void CallToolResult_Should_Support_Error_State()
    {
        // Arrange
        var result = new CallToolResult
        {
            Content = [MCPContent.TextContent("Error: Division by zero")],
            IsError = true,
        };

        // Act
        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<CallToolResult>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.IsError.Should().BeTrue();
        deserialized.Content.Should().HaveCount(1);
        deserialized.Content[0].Text.Should().Contain("Division by zero");
    }

    [Fact]
    public void ListToolsResult_Should_Support_Pagination()
    {
        // Arrange
        var result = new ListToolsResult
        {
            Tools = [new MCPToolDefinition { Name = "tool1", InputSchema = new MCPInputSchema() }],
            NextCursor = "page2",
        };

        // Act
        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<ListToolsResult>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Tools.Should().HaveCount(1);
        deserialized.NextCursor.Should().Be("page2");
    }
}

public class MCPResourceTests
{
    [Fact]
    public void MCPResource_Should_Have_Required_Properties()
    {
        // Arrange
        var resource = new MCPResource
        {
            Uri = "file:///docs/readme.md",
            Name = "readme.md",
            Description = "Project readme",
            MimeType = "text/markdown",
        };

        // Act
        var json = JsonSerializer.Serialize(resource);
        var deserialized = JsonSerializer.Deserialize<MCPResource>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Uri.Should().Be("file:///docs/readme.md");
        deserialized.Name.Should().Be("readme.md");
        deserialized.MimeType.Should().Be("text/markdown");
    }

    [Fact]
    public void ResourceContent_Should_Support_Text_And_Blob()
    {
        // Arrange - Text content
        var textContent = new ResourceContent
        {
            Uri = "file:///test.txt",
            Text = "Hello, World!",
            MimeType = "text/plain",
        };

        // Arrange - Blob content
        var blobContent = new ResourceContent
        {
            Uri = "file:///test.png",
            Blob = "base64encodeddata",
            MimeType = "image/png",
        };

        // Act & Assert
        textContent.Text.Should().Be("Hello, World!");
        textContent.Blob.Should().BeNull();
        blobContent.Blob.Should().Be("base64encodeddata");
        blobContent.Text.Should().BeNull();
    }
}

public class MCPPromptTests
{
    [Fact]
    public void MCPPrompt_Should_Support_Arguments()
    {
        // Arrange
        var prompt = new MCPPrompt
        {
            Name = "summarize",
            Description = "Summarize the given text",
            Arguments =
            [
                new MCPPromptArgument
                {
                    Name = "text",
                    Description = "Text to summarize",
                    Required = true,
                },
                new MCPPromptArgument
                {
                    Name = "maxLength",
                    Description = "Maximum length of summary",
                    Required = false,
                },
            ],
        };

        // Act
        var json = JsonSerializer.Serialize(prompt);
        var deserialized = JsonSerializer.Deserialize<MCPPrompt>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Name.Should().Be("summarize");
        deserialized.Arguments.Should().HaveCount(2);
        deserialized.Arguments![0].Required.Should().BeTrue();
        deserialized.Arguments[1].Required.Should().BeFalse();
    }

    [Fact]
    public void GetPromptResult_Should_Contain_Messages()
    {
        // Arrange
        var result = new GetPromptResult
        {
            Description = "Summarization prompt",
            Messages =
            [
                new MCPPromptMessage
                {
                    Role = "user",
                    Content = MCPContent.TextContent("Summarize this text: {{text}}"),
                },
            ],
        };

        // Act
        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<GetPromptResult>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Messages.Should().HaveCount(1);
        deserialized.Messages[0].Role.Should().Be("user");
        deserialized.Messages[0].Content.Type.Should().Be("text");
    }
}

public class MCPMethodsTests
{
    [Fact]
    public void MCPMethods_Should_Have_Correct_Values()
    {
        MCPMethods.Initialize.Should().Be("initialize");
        MCPMethods.ToolsList.Should().Be("tools/list");
        MCPMethods.ToolsCall.Should().Be("tools/call");
        MCPMethods.ResourcesList.Should().Be("resources/list");
        MCPMethods.ResourcesRead.Should().Be("resources/read");
        MCPMethods.PromptsList.Should().Be("prompts/list");
        MCPMethods.PromptsGet.Should().Be("prompts/get");
    }

    [Fact]
    public void MCPProtocolVersion_Should_Be_Correct()
    {
        MCPProtocolVersion.V2024_11_05.Should().Be("2024-11-05");
        MCPProtocolVersion.Latest.Should().Be("2024-11-05");
    }
}
