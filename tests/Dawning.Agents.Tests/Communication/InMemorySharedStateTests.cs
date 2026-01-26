namespace Dawning.Agents.Tests.Communication;

using Dawning.Agents.Abstractions.Communication;
using Dawning.Agents.Core.Communication;
using FluentAssertions;

/// <summary>
/// InMemorySharedState 测试
/// </summary>
public class InMemorySharedStateTests
{
    #region GetAsync / SetAsync Tests

    [Fact]
    public async Task SetAsync_GetAsync_SimpleType_ShouldWork()
    {
        var state = new InMemorySharedState();

        await state.SetAsync("count", 42);

        var value = await state.GetAsync<int>("count");
        value.Should().Be(42);
    }

    [Fact]
    public async Task SetAsync_GetAsync_StringType_ShouldWork()
    {
        var state = new InMemorySharedState();

        await state.SetAsync("name", "test-value");

        var value = await state.GetAsync<string>("name");
        value.Should().Be("test-value");
    }

    [Fact]
    public async Task SetAsync_GetAsync_ComplexType_ShouldWork()
    {
        var state = new InMemorySharedState();

        var data = new TestData
        {
            Id = 1,
            Name = "test",
            IsActive = true,
        };
        await state.SetAsync("data", data);

        var value = await state.GetAsync<TestData>("data");
        value.Should().NotBeNull();
        value!.Id.Should().Be(1);
        value.Name.Should().Be("test");
        value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_NonExistentKey_ShouldReturnDefault()
    {
        var state = new InMemorySharedState();

        var intValue = await state.GetAsync<int>("missing");
        intValue.Should().Be(0);

        var stringValue = await state.GetAsync<string>("missing");
        stringValue.Should().BeNull();

        var objectValue = await state.GetAsync<TestData>("missing");
        objectValue.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_Overwrite_ShouldUpdateValue()
    {
        var state = new InMemorySharedState();

        await state.SetAsync("key", "value1");
        await state.SetAsync("key", "value2");

        var value = await state.GetAsync<string>("key");
        value.Should().Be("value2");
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ExistingKey_ShouldReturnTrue()
    {
        var state = new InMemorySharedState();
        await state.SetAsync("key", "value");

        var deleted = await state.DeleteAsync("key");

        deleted.Should().BeTrue();
        var exists = await state.ExistsAsync("key");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentKey_ShouldReturnFalse()
    {
        var state = new InMemorySharedState();

        var deleted = await state.DeleteAsync("missing");

        deleted.Should().BeFalse();
    }

    #endregion

    #region ExistsAsync Tests

    [Fact]
    public async Task ExistsAsync_ExistingKey_ShouldReturnTrue()
    {
        var state = new InMemorySharedState();
        await state.SetAsync("key", "value");

        var exists = await state.ExistsAsync("key");

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NonExistentKey_ShouldReturnFalse()
    {
        var state = new InMemorySharedState();

        var exists = await state.ExistsAsync("missing");

        exists.Should().BeFalse();
    }

    #endregion

    #region GetKeysAsync Tests

    [Fact]
    public async Task GetKeysAsync_AllKeys_ShouldReturnAll()
    {
        var state = new InMemorySharedState();
        await state.SetAsync("key1", "value1");
        await state.SetAsync("key2", "value2");
        await state.SetAsync("key3", "value3");

        var keys = await state.GetKeysAsync();

        keys.Should().HaveCount(3);
        keys.Should().Contain(["key1", "key2", "key3"]);
    }

    [Fact]
    public async Task GetKeysAsync_WithPattern_ShouldFilterKeys()
    {
        var state = new InMemorySharedState();
        await state.SetAsync("agent:1:status", "idle");
        await state.SetAsync("agent:2:status", "busy");
        await state.SetAsync("agent:1:task", "process");
        await state.SetAsync("config:timeout", "30");

        var agentKeys = await state.GetKeysAsync("agent:*");
        agentKeys.Should().HaveCount(3);

        var statusKeys = await state.GetKeysAsync("*:status");
        statusKeys.Should().HaveCount(2);

        var agent1Keys = await state.GetKeysAsync("agent:1:*");
        agent1Keys.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetKeysAsync_NoMatch_ShouldReturnEmpty()
    {
        var state = new InMemorySharedState();
        await state.SetAsync("key1", "value1");

        var keys = await state.GetKeysAsync("nomatch:*");

        keys.Should().BeEmpty();
    }

    #endregion

    #region OnChange Tests

    [Fact]
    public async Task OnChange_ShouldNotifyOnSet()
    {
        var state = new InMemorySharedState();
        string? changedKey = null;
        object? changedValue = null;

        state.OnChange(
            "watched-key",
            (key, value) =>
            {
                changedKey = key;
                changedValue = value;
            }
        );

        await state.SetAsync("watched-key", "new-value");

        changedKey.Should().Be("watched-key");
        changedValue.Should().Be("new-value");
    }

    [Fact]
    public async Task OnChange_ShouldNotifyOnDelete()
    {
        var state = new InMemorySharedState();
        await state.SetAsync("key", "value");

        string? changedKey = null;
        object? changedValue = "not-null";

        state.OnChange(
            "key",
            (key, value) =>
            {
                changedKey = key;
                changedValue = value;
            }
        );

        await state.DeleteAsync("key");

        changedKey.Should().Be("key");
        changedValue.Should().BeNull();
    }

    [Fact]
    public async Task OnChange_DifferentKey_ShouldNotNotify()
    {
        var state = new InMemorySharedState();
        var notified = false;

        state.OnChange("key-a", (_, _) => notified = true);

        await state.SetAsync("key-b", "value");

        notified.Should().BeFalse();
    }

    [Fact]
    public async Task OnChange_Dispose_ShouldStopNotifications()
    {
        var state = new InMemorySharedState();
        var callCount = 0;

        var subscription = state.OnChange("key", (_, _) => callCount++);

        await state.SetAsync("key", "value1");
        callCount.Should().Be(1);

        subscription.Dispose();

        await state.SetAsync("key", "value2");
        callCount.Should().Be(1); // 不应增加
    }

    #endregion

    #region ClearAsync Tests

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllKeys()
    {
        var state = new InMemorySharedState();
        await state.SetAsync("key1", "value1");
        await state.SetAsync("key2", "value2");

        await state.ClearAsync();

        state.Count.Should().Be(0);
        var exists1 = await state.ExistsAsync("key1");
        var exists2 = await state.ExistsAsync("key2");
        exists1.Should().BeFalse();
        exists2.Should().BeFalse();
    }

    #endregion

    #region Count Tests

    [Fact]
    public async Task Count_ShouldReflectStoredItems()
    {
        var state = new InMemorySharedState();

        state.Count.Should().Be(0);

        await state.SetAsync("key1", "value1");
        state.Count.Should().Be(1);

        await state.SetAsync("key2", "value2");
        state.Count.Should().Be(2);

        await state.DeleteAsync("key1");
        state.Count.Should().Be(1);
    }

    #endregion

    private record TestData
    {
        public int Id { get; init; }
        public string? Name { get; init; }
        public bool IsActive { get; init; }
    }
}
