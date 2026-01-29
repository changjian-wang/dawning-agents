using Dawning.Agents.Core.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Dawning.Agents.Tests.Configuration;

public class EnvironmentConfigurationExtensionsTests : IDisposable
{
    private readonly string _testDir;
    private readonly List<string> _createdFiles = [];

    public EnvironmentConfigurationExtensionsTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"dawning-env-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        foreach (var file in _createdFiles)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }

        GC.SuppressFinalize(this);
    }

    private string CreateEnvFile(string filename, string content)
    {
        var path = Path.Combine(_testDir, filename);
        File.WriteAllText(path, content);
        _createdFiles.Add(path);
        return path;
    }

    [Fact]
    public void AddEnvFile_WithValidFile_LoadsConfiguration()
    {
        // Arrange
        var envContent = """
            KEY1=value1
            KEY2=value2
            """;
        var envPath = CreateEnvFile(".env", envContent);

        // Act
        var config = new ConfigurationBuilder().AddEnvFile(envPath).Build();

        // Assert
        config["KEY1"].Should().Be("value1");
        config["KEY2"].Should().Be("value2");
    }

    [Fact]
    public void AddEnvFile_WithComments_IgnoresComments()
    {
        // Arrange
        var envContent = """
            # This is a comment
            KEY1=value1
            # Another comment
            KEY2=value2
            """;
        var envPath = CreateEnvFile(".env", envContent);

        // Act
        var config = new ConfigurationBuilder().AddEnvFile(envPath).Build();

        // Assert
        config["KEY1"].Should().Be("value1");
        config["KEY2"].Should().Be("value2");
    }

    [Fact]
    public void AddEnvFile_WithQuotedValues_RemovesQuotes()
    {
        // Arrange
        var envContent = """
            KEY1="quoted value"
            KEY2='single quoted'
            KEY3=unquoted
            """;
        var envPath = CreateEnvFile(".env", envContent);

        // Act
        var config = new ConfigurationBuilder().AddEnvFile(envPath).Build();

        // Assert
        config["KEY1"].Should().Be("quoted value");
        config["KEY2"].Should().Be("single quoted");
        config["KEY3"].Should().Be("unquoted");
    }

    [Fact]
    public void AddEnvFile_WithNestedKeys_ConvertsToConfigurationPath()
    {
        // Arrange
        var envContent = """
            LLM__Endpoint=http://localhost:11434
            LLM__Model=llama3.2
            Resilience__Retry__MaxRetryAttempts=3
            """;
        var envPath = CreateEnvFile(".env", envContent);

        // Act
        var config = new ConfigurationBuilder().AddEnvFile(envPath).Build();

        // Assert
        config["LLM:Endpoint"].Should().Be("http://localhost:11434");
        config["LLM:Model"].Should().Be("llama3.2");
        config["Resilience:Retry:MaxRetryAttempts"].Should().Be("3");
    }

    [Fact]
    public void AddEnvFile_WithEscapeSequences_ProcessesEscapes()
    {
        // Arrange
        var envContent = """
            MULTILINE="line1\nline2"
            TAB="col1\tcol2"
            """;
        var envPath = CreateEnvFile(".env", envContent);

        // Act
        var config = new ConfigurationBuilder().AddEnvFile(envPath).Build();

        // Assert
        config["MULTILINE"].Should().Be("line1\nline2");
        config["TAB"].Should().Be("col1\tcol2");
    }

    [Fact]
    public void AddEnvFile_WithMissingOptionalFile_DoesNotThrow()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDir, "nonexistent.env");

        // Act
        var action = () => new ConfigurationBuilder().AddEnvFile(nonExistentPath, optional: true).Build();

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void AddEnvFile_WithMissingRequiredFile_ThrowsFileNotFound()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDir, "nonexistent.env");

        // Act
        var action = () =>
            new ConfigurationBuilder().AddEnvFile(nonExistentPath, optional: false).Build();

        // Assert
        action.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void AddEnvFile_WithEmptyLines_SkipsEmptyLines()
    {
        // Arrange
        var envContent = """
            KEY1=value1

            KEY2=value2

            """;
        var envPath = CreateEnvFile(".env", envContent);

        // Act
        var config = new ConfigurationBuilder().AddEnvFile(envPath).Build();

        // Assert
        config["KEY1"].Should().Be("value1");
        config["KEY2"].Should().Be("value2");
    }

    [Fact]
    public void AddEnvFile_WithInvalidLines_SkipsInvalidLines()
    {
        // Arrange
        var envContent = """
            KEY1=value1
            invalid line without equals
            =no key
            KEY2=value2
            """;
        var envPath = CreateEnvFile(".env", envContent);

        // Act
        var config = new ConfigurationBuilder().AddEnvFile(envPath).Build();

        // Assert
        config["KEY1"].Should().Be("value1");
        config["KEY2"].Should().Be("value2");
    }

    [Fact]
    public void AddEnvFiles_WithMultipleFiles_LoadsInCorrectOrder()
    {
        // Arrange - 创建多个 .env 文件
        CreateEnvFile(".env", "KEY=base");
        CreateEnvFile(".env.local", "KEY=local");
        CreateEnvFile(".env.development", "KEY=development");
        CreateEnvFile(".env.development.local", "KEY=development-local");

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);

            // Act
            var config = new ConfigurationBuilder().AddEnvFiles("Development").Build();

            // Assert - 后加载的文件应该覆盖前面的值
            config["KEY"].Should().Be("development-local");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }
}
