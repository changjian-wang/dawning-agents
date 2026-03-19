namespace Dawning.Agents.Tests.MCP;

using Dawning.Agents.MCP.Providers;
using FluentAssertions;
using Xunit;

public class FileSystemResourceProviderTests : IDisposable
{
    private readonly string _testDir;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_NullOrWhiteSpaceRootPath_Throws(string? rootPath)
    {
        var act = () => new FileSystemResourceProvider(rootPath!);

        act.Should().Throw<ArgumentException>();
    }

    public FileSystemResourceProviderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"mcp_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void GetResources_Should_Return_Empty_When_Directory_Empty()
    {
        // Arrange
        var provider = new FileSystemResourceProvider(_testDir);

        // Act
        var resources = provider.GetResources().ToList();

        // Assert
        resources.Should().BeEmpty();
    }

    [Fact]
    public void GetResources_Should_Return_Files_With_Allowed_Extensions()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDir, "readme.md"), "# Test");
        File.WriteAllText(Path.Combine(_testDir, "config.json"), "{}");
        File.WriteAllText(Path.Combine(_testDir, "image.png"), "binary"); // Should be excluded

        var provider = new FileSystemResourceProvider(_testDir);

        // Act
        var resources = provider.GetResources().ToList();

        // Assert
        resources.Should().HaveCount(2);
        resources.Select(r => r.Name).Should().Contain("readme.md");
        resources.Select(r => r.Name).Should().Contain("config.json");
        resources.Select(r => r.Name).Should().NotContain("image.png");
    }

    [Fact]
    public void GetResources_Should_Include_Subdirectories()
    {
        // Arrange
        var subDir = Path.Combine(_testDir, "docs");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "guide.md"), "# Guide");

        var provider = new FileSystemResourceProvider(_testDir);

        // Act
        var resources = provider.GetResources().ToList();

        // Assert
        resources.Should().HaveCount(1);
        resources[0].Uri.Should().Contain("docs/guide.md");
    }

    [Fact]
    public void GetResources_Should_Set_Correct_MimeType()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDir, "test.md"), "# Test");
        File.WriteAllText(Path.Combine(_testDir, "test.json"), "{}");
        File.WriteAllText(Path.Combine(_testDir, "test.cs"), "class Test {}");

        var provider = new FileSystemResourceProvider(_testDir);

        // Act
        var resources = provider.GetResources().ToList();

        // Assert
        resources.First(r => r.Name == "test.md").MimeType.Should().Be("text/markdown");
        resources.First(r => r.Name == "test.json").MimeType.Should().Be("application/json");
        resources.First(r => r.Name == "test.cs").MimeType.Should().Be("text/x-csharp");
    }

    [Fact]
    public void SupportsUri_Should_Return_True_For_File_Uri()
    {
        // Arrange
        var provider = new FileSystemResourceProvider(_testDir);

        // Act & Assert
        provider.SupportsUri("file:///test.txt").Should().BeTrue();
        provider.SupportsUri("file:///path/to/file.md").Should().BeTrue();
    }

    [Fact]
    public void SupportsUri_Should_Return_False_For_Other_Schemes()
    {
        // Arrange
        var provider = new FileSystemResourceProvider(_testDir);

        // Act & Assert
        provider.SupportsUri("http://example.com").Should().BeFalse();
        provider.SupportsUri("https://example.com").Should().BeFalse();
        provider.SupportsUri("ftp://example.com").Should().BeFalse();
    }

    [Fact]
    public async Task ReadResourceAsync_Should_Return_File_Content()
    {
        // Arrange
        var content = "Hello, World!";
        File.WriteAllText(Path.Combine(_testDir, "test.txt"), content);
        var provider = new FileSystemResourceProvider(_testDir);

        // Act
        var result = await provider.ReadResourceAsync("file:///test.txt");

        // Assert
        result.Should().NotBeNull();
        result!.Text.Should().Be(content);
        result.MimeType.Should().Be("text/plain");
    }

    [Fact]
    public async Task ReadResourceAsync_Should_Return_Null_For_NonExistent_File()
    {
        // Arrange
        var provider = new FileSystemResourceProvider(_testDir);

        // Act
        var result = await provider.ReadResourceAsync("file:///nonexistent.txt");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ReadResourceAsync_Should_Return_Null_For_Unsupported_Uri()
    {
        // Arrange
        var provider = new FileSystemResourceProvider(_testDir);

        // Act
        var result = await provider.ReadResourceAsync("http://example.com/test.txt");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ReadResourceAsync_Should_Prevent_Path_Traversal()
    {
        // Arrange
        var provider = new FileSystemResourceProvider(_testDir);

        // Act - Try to read outside root directory
        var result = await provider.ReadResourceAsync("file:///../../../etc/passwd");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetResourceTemplates_Should_Return_File_Template()
    {
        // Arrange
        var provider = new FileSystemResourceProvider(_testDir);

        // Act
        var templates = provider.GetResourceTemplates().ToList();

        // Assert
        templates.Should().HaveCount(1);
        templates[0].UriTemplate.Should().Be("file:///{path}");
        templates[0].Name.Should().Be("File");
    }

    [Fact]
    public void Constructor_Should_Accept_Custom_Extensions()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDir, "test.custom"), "custom");
        File.WriteAllText(Path.Combine(_testDir, "test.txt"), "text");

        var provider = new FileSystemResourceProvider(_testDir, allowedExtensions: [".custom"]);

        // Act
        var resources = provider.GetResources().ToList();

        // Assert
        resources.Should().HaveCount(1);
        resources[0].Name.Should().Be("test.custom");
    }

    [Fact]
    public void GetResources_Should_Handle_NonExistent_RootPath()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDir, "nonexistent");
        var provider = new FileSystemResourceProvider(nonExistentPath);

        // Act
        var resources = provider.GetResources().ToList();

        // Assert
        resources.Should().BeEmpty();
    }
}
