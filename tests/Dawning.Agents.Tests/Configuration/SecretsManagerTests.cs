namespace Dawning.Agents.Tests.Configuration;

using Dawning.Agents.Core.Configuration;
using FluentAssertions;

public class SecretsManagerTests
{
    [Fact]
    public async Task EnvironmentSecretsManager_GetSecretAsync_ReturnsEnvironmentVariable()
    {
        // Arrange
        var manager = new EnvironmentSecretsManager();
        var testKey = "TEST_SECRET_KEY_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(testKey, "test-value");

        try
        {
            // Act
            var value = await manager.GetSecretAsync(testKey);

            // Assert
            value.Should().Be("test-value");
        }
        finally
        {
            Environment.SetEnvironmentVariable(testKey, null);
        }
    }

    [Fact]
    public async Task EnvironmentSecretsManager_GetSecretAsync_ReturnsNullWhenNotFound()
    {
        var manager = new EnvironmentSecretsManager();
        var nonExistentKey = "NON_EXISTENT_KEY_" + Guid.NewGuid().ToString("N");

        var value = await manager.GetSecretAsync(nonExistentKey);

        value.Should().BeNull();
    }

    [Fact]
    public async Task EnvironmentSecretsManager_SetSecretAsync_SetsEnvironmentVariable()
    {
        var manager = new EnvironmentSecretsManager();
        var testKey = "TEST_SECRET_KEY_" + Guid.NewGuid().ToString("N");

        try
        {
            await manager.SetSecretAsync(testKey, "new-value");

            Environment.GetEnvironmentVariable(testKey).Should().Be("new-value");
        }
        finally
        {
            Environment.SetEnvironmentVariable(testKey, null);
        }
    }

    [Fact]
    public async Task EnvironmentSecretsManager_DeleteSecretAsync_RemovesEnvironmentVariable()
    {
        var manager = new EnvironmentSecretsManager();
        var testKey = "TEST_SECRET_KEY_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(testKey, "to-delete");

        await manager.DeleteSecretAsync(testKey);

        Environment.GetEnvironmentVariable(testKey).Should().BeNull();
    }

    [Fact]
    public async Task EnvironmentSecretsManager_ExistsAsync_ReturnsTrueWhenExists()
    {
        var manager = new EnvironmentSecretsManager();
        var testKey = "TEST_SECRET_KEY_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(testKey, "exists");

        try
        {
            var exists = await manager.ExistsAsync(testKey);

            exists.Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable(testKey, null);
        }
    }

    [Fact]
    public async Task InMemorySecretsManager_SetAndGet_WorksCorrectly()
    {
        var manager = new InMemorySecretsManager();

        await manager.SetSecretAsync("key1", "value1");
        var value = await manager.GetSecretAsync("key1");

        value.Should().Be("value1");
        manager.Count.Should().Be(1);
    }

    [Fact]
    public async Task InMemorySecretsManager_Delete_RemovesSecret()
    {
        var manager = new InMemorySecretsManager();
        await manager.SetSecretAsync("key1", "value1");

        await manager.DeleteSecretAsync("key1");

        var value = await manager.GetSecretAsync("key1");
        value.Should().BeNull();
        manager.Count.Should().Be(0);
    }

    [Fact]
    public async Task InMemorySecretsManager_Clear_RemovesAllSecrets()
    {
        var manager = new InMemorySecretsManager();
        await manager.SetSecretAsync("key1", "value1");
        await manager.SetSecretAsync("key2", "value2");

        manager.Clear();

        manager.Count.Should().Be(0);
    }

    [Fact]
    public async Task InMemorySecretsManager_Exists_ReturnsFalseWhenNotExists()
    {
        var manager = new InMemorySecretsManager();

        var exists = await manager.ExistsAsync("nonexistent");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task CompositeSecretsManager_GetSecretAsync_SearchesInOrder()
    {
        var manager1 = new InMemorySecretsManager();
        var manager2 = new InMemorySecretsManager();
        await manager2.SetSecretAsync("key1", "from-manager2");

        var composite = new CompositeSecretsManager([manager1, manager2]);

        var value = await composite.GetSecretAsync("key1");

        value.Should().Be("from-manager2");
    }

    [Fact]
    public async Task CompositeSecretsManager_GetSecretAsync_ReturnsFirstMatch()
    {
        var manager1 = new InMemorySecretsManager();
        var manager2 = new InMemorySecretsManager();
        await manager1.SetSecretAsync("key1", "from-manager1");
        await manager2.SetSecretAsync("key1", "from-manager2");

        var composite = new CompositeSecretsManager([manager1, manager2]);

        var value = await composite.GetSecretAsync("key1");

        value.Should().Be("from-manager1");
    }

    [Fact]
    public async Task CompositeSecretsManager_SetSecretAsync_SetsOnFirstManager()
    {
        var manager1 = new InMemorySecretsManager();
        var manager2 = new InMemorySecretsManager();

        var composite = new CompositeSecretsManager([manager1, manager2]);
        await composite.SetSecretAsync("key1", "new-value");

        var value1 = await manager1.GetSecretAsync("key1");
        var value2 = await manager2.GetSecretAsync("key1");

        value1.Should().Be("new-value");
        value2.Should().BeNull();
    }

    [Fact]
    public async Task CompositeSecretsManager_DeleteSecretAsync_DeletesFromAllManagers()
    {
        var manager1 = new InMemorySecretsManager();
        var manager2 = new InMemorySecretsManager();
        await manager1.SetSecretAsync("key1", "value1");
        await manager2.SetSecretAsync("key1", "value1");

        var composite = new CompositeSecretsManager([manager1, manager2]);
        await composite.DeleteSecretAsync("key1");

        (await manager1.ExistsAsync("key1")).Should().BeFalse();
        (await manager2.ExistsAsync("key1")).Should().BeFalse();
    }
}
