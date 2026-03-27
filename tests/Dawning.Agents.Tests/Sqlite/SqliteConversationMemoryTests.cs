using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Core.Memory;
using Dawning.Agents.Sqlite;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Tests.Sqlite;

/// <summary>
/// Unit tests for SqliteConversationMemory.
/// </summary>
public class SqliteConversationMemoryTests : IAsyncLifetime
{
    private readonly ITokenCounter _tokenCounter = new SimpleTokenCounter();
    private SqliteDbContext _dbContext = null!;
    private SqliteConversationMemory _memory = null!;
    private readonly string _sessionId = Guid.NewGuid().ToString();
    private readonly string _dbName = $"test_{Guid.NewGuid():N}";

    // Keep-alive connection to prevent shared-cache in-memory DB from being destroyed.
    private SqliteConnection _keepAlive = null!;

    public async Task InitializeAsync()
    {
        var connStr = $"Data Source={_dbName};Mode=Memory;Cache=Shared";
        _keepAlive = new SqliteConnection(connStr);
        await _keepAlive.OpenAsync();

        var options = Options.Create(new SqliteMemoryOptions { ConnectionString = connStr });
        _dbContext = new SqliteDbContext(options);
        await _dbContext.EnsureSchemaAsync();
        _memory = new SqliteConversationMemory(_dbContext, _tokenCounter, _sessionId);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _keepAlive.DisposeAsync();
    }

    [Fact]
    public void SessionId_ReturnsConfiguredValue()
    {
        _memory.SessionId.Should().Be(_sessionId);
    }

    [Fact]
    public async Task AddMessageAsync_SingleMessage_PersistsToDatabase()
    {
        var message = new ConversationMessage { Role = "user", Content = "Hello" };

        await _memory.AddMessageAsync(message);

        _memory.MessageCount.Should().Be(1);
    }

    [Fact]
    public async Task AddMessageAsync_MultipleMessages_PersistsAll()
    {
        await _memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Hello" });
        await _memory.AddMessageAsync(
            new ConversationMessage { Role = "assistant", Content = "Hi there!" }
        );
        await _memory.AddMessageAsync(
            new ConversationMessage { Role = "user", Content = "How are you?" }
        );

        _memory.MessageCount.Should().Be(3);
    }

    [Fact]
    public async Task GetMessagesAsync_ReturnsAllMessagesInOrder()
    {
        await _memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "First" });
        await _memory.AddMessageAsync(
            new ConversationMessage { Role = "assistant", Content = "Second" }
        );

        var messages = await _memory.GetMessagesAsync();

        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be("user");
        messages[0].Content.Should().Be("First");
        messages[1].Role.Should().Be("assistant");
        messages[1].Content.Should().Be("Second");
    }

    [Fact]
    public async Task GetContextAsync_ReturnsAsChatMessages()
    {
        await _memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Hello" });
        await _memory.AddMessageAsync(
            new ConversationMessage { Role = "assistant", Content = "Hi!" }
        );

        var context = await _memory.GetContextAsync();

        context.Should().HaveCount(2);
        context[0].Role.Should().Be("user");
        context[0].Content.Should().Be("Hello");
        context[1].Role.Should().Be("assistant");
        context[1].Content.Should().Be("Hi!");
    }

    [Fact]
    public async Task GetContextAsync_WithMaxTokens_TrimsOldMessages()
    {
        await _memory.AddMessageAsync(
            new ConversationMessage
            {
                Role = "user",
                Content = "First message",
                TokenCount = 100,
            }
        );
        await _memory.AddMessageAsync(
            new ConversationMessage
            {
                Role = "assistant",
                Content = "Second message",
                TokenCount = 50,
            }
        );
        await _memory.AddMessageAsync(
            new ConversationMessage
            {
                Role = "user",
                Content = "Third message",
                TokenCount = 30,
            }
        );

        // Budget of 80 tokens should only include the last two messages (50 + 30 = 80)
        var context = await _memory.GetContextAsync(maxTokens: 80);

        context.Should().HaveCount(2);
        context[0].Content.Should().Be("Second message");
        context[1].Content.Should().Be("Third message");
    }

    [Fact]
    public async Task ClearAsync_RemovesAllMessages()
    {
        await _memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Hello" });
        await _memory.AddMessageAsync(
            new ConversationMessage { Role = "assistant", Content = "Hi!" }
        );

        await _memory.ClearAsync();

        _memory.MessageCount.Should().Be(0);
        var messages = await _memory.GetMessagesAsync();
        messages.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTokenCountAsync_ReturnsCorrectTotal()
    {
        await _memory.AddMessageAsync(
            new ConversationMessage
            {
                Role = "user",
                Content = "Hello",
                TokenCount = 10,
            }
        );
        await _memory.AddMessageAsync(
            new ConversationMessage
            {
                Role = "assistant",
                Content = "Hi!",
                TokenCount = 5,
            }
        );

        var tokenCount = await _memory.GetTokenCountAsync();

        tokenCount.Should().Be(15);
    }

    [Fact]
    public async Task SessionIsolation_DifferentSessions_DoNotShareData()
    {
        var session1 = new SqliteConversationMemory(_dbContext, _tokenCounter, "session-1");
        var session2 = new SqliteConversationMemory(_dbContext, _tokenCounter, "session-2");

        await session1.AddMessageAsync(
            new ConversationMessage { Role = "user", Content = "Session 1 message" }
        );
        await session2.AddMessageAsync(
            new ConversationMessage { Role = "user", Content = "Session 2 message" }
        );

        var messages1 = await session1.GetMessagesAsync();
        var messages2 = await session2.GetMessagesAsync();

        messages1.Should().HaveCount(1);
        messages1[0].Content.Should().Be("Session 1 message");
        messages2.Should().HaveCount(1);
        messages2[0].Content.Should().Be("Session 2 message");
    }

    [Fact]
    public async Task ClearAsync_OnlyAffectsOwnSession()
    {
        var session1 = new SqliteConversationMemory(_dbContext, _tokenCounter, "session-a");
        var session2 = new SqliteConversationMemory(_dbContext, _tokenCounter, "session-b");

        await session1.AddMessageAsync(
            new ConversationMessage { Role = "user", Content = "Keep me" }
        );
        await session2.AddMessageAsync(
            new ConversationMessage { Role = "user", Content = "Delete me" }
        );

        await session2.ClearAsync();

        session1.MessageCount.Should().Be(1);
        session2.MessageCount.Should().Be(0);
    }

    [Fact]
    public void Constructor_NullDbContext_ThrowsException()
    {
        var action = () => new SqliteConversationMemory(null!, _tokenCounter, "session");
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullTokenCounter_ThrowsException()
    {
        var action = () => new SqliteConversationMemory(_dbContext, null!, "session");
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_EmptySessionId_ThrowsException()
    {
        var action = () => new SqliteConversationMemory(_dbContext, _tokenCounter, "");
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WhitespaceSessionId_ThrowsException()
    {
        var action = () => new SqliteConversationMemory(_dbContext, _tokenCounter, "   ");
        action.Should().Throw<ArgumentException>();
    }
}
