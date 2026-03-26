using Dawning.Agents.Abstractions.Distributed;
using Dawning.Agents.Abstractions.Telemetry;
using Dawning.Agents.Redis.Telemetry;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace Dawning.Agents.Tests.Redis;

/// <summary>
/// Redis Token 使用追踪器测试
/// </summary>
public sealed class RedisTokenUsageTrackerTests
{
    private readonly Mock<IConnectionMultiplexer> _connectionMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly Mock<IServer> _serverMock;
    private readonly RedisOptions _options;
    private readonly RedisTokenUsageTracker _tracker;

    public RedisTokenUsageTrackerTests()
    {
        _connectionMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();
        _serverMock = new Mock<IServer>();
        _options = new RedisOptions { InstanceName = "test:", DefaultDatabase = 0 };

        _connectionMock
            .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);
        _connectionMock
            .Setup(c => c.GetEndPoints(It.IsAny<bool>()))
            .Returns([new System.Net.DnsEndPoint("localhost", 6379)]);
        _connectionMock
            .Setup(c => c.GetServer(It.IsAny<System.Net.EndPoint>(), It.IsAny<object>()))
            .Returns(_serverMock.Object);

        _tracker = new RedisTokenUsageTracker(
            _connectionMock.Object,
            Options.Create(_options),
            NullLogger<RedisTokenUsageTracker>.Instance
        );
    }

    [Fact]
    public void Record_CreatesGlobalCountersAndPushesJson()
    {
        // Arrange
        var batch = new Mock<IBatch>();
        _databaseMock.Setup(d => d.CreateBatch(It.IsAny<object>())).Returns(batch.Object);

        batch
            .Setup(b =>
                b.StringIncrementAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<long>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(1);
        batch
            .Setup(b =>
                b.HashIncrementAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<long>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(1);
        batch
            .Setup(b =>
                b.ListLeftPushAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(1);
        batch
            .Setup(b =>
                b.ListTrimAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<long>(),
                    It.IsAny<long>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .Returns(Task.CompletedTask);

        var record = new TokenUsageRecord(
            "openai",
            100,
            50,
            DateTimeOffset.UtcNow,
            "gpt-4",
            "session-1"
        );

        // Act
        _tracker.Record(record);

        // Assert — global prompt counter
        batch.Verify(
            b =>
                b.StringIncrementAsync(
                    It.Is<RedisKey>(k => k.ToString().Contains("total:prompt")),
                    100,
                    It.IsAny<CommandFlags>()
                ),
            Times.Once
        );

        // Assert — global completion counter
        batch.Verify(
            b =>
                b.StringIncrementAsync(
                    It.Is<RedisKey>(k => k.ToString().Contains("total:completion")),
                    50,
                    It.IsAny<CommandFlags>()
                ),
            Times.Once
        );

        // Assert — per-source hash
        batch.Verify(
            b =>
                b.HashIncrementAsync(
                    It.Is<RedisKey>(k => k.ToString().Contains("source:openai")),
                    It.Is<RedisValue>(v => v.ToString() == "prompt"),
                    100,
                    It.IsAny<CommandFlags>()
                ),
            Times.Once
        );
    }

    [Fact]
    public void RecordOverload_CreatesRecordFromParameters()
    {
        // Arrange
        var batch = new Mock<IBatch>();
        _databaseMock.Setup(d => d.CreateBatch(It.IsAny<object>())).Returns(batch.Object);

        batch
            .Setup(b =>
                b.StringIncrementAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<long>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(1);
        batch
            .Setup(b =>
                b.HashIncrementAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<long>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(1);
        batch
            .Setup(b =>
                b.ListLeftPushAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(1);
        batch
            .Setup(b =>
                b.ListTrimAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<long>(),
                    It.IsAny<long>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .Returns(Task.CompletedTask);

        // Act
        _tracker.Record("azure", 200, 80, "gpt-35-turbo", "sess-2");

        // Assert
        batch.Verify(
            b =>
                b.StringIncrementAsync(
                    It.Is<RedisKey>(k => k.ToString().Contains("total:prompt")),
                    200,
                    It.IsAny<CommandFlags>()
                ),
            Times.Once
        );
    }

    [Fact]
    public void TotalPromptTokens_ReturnsZeroWhenMissing()
    {
        // Arrange
        _databaseMock
            .Setup(d => d.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns(RedisValue.Null);

        // Act & Assert
        _tracker.TotalPromptTokens.Should().Be(0);
    }

    [Fact]
    public void TotalPromptTokens_ReturnsStoredValue()
    {
        // Arrange
        _databaseMock
            .Setup(d =>
                d.StringGet(
                    It.Is<RedisKey>(k => k.ToString().Contains("total:prompt")),
                    It.IsAny<CommandFlags>()
                )
            )
            .Returns((RedisValue)500);
        _databaseMock
            .Setup(d =>
                d.StringGet(
                    It.Is<RedisKey>(k => k.ToString().Contains("total:completion")),
                    It.IsAny<CommandFlags>()
                )
            )
            .Returns((RedisValue)200);

        // Act & Assert
        _tracker.TotalPromptTokens.Should().Be(500);
        _tracker.TotalCompletionTokens.Should().Be(200);
        _tracker.TotalTokens.Should().Be(700);
    }

    [Fact]
    public void GetSummary_ReturnsEmptyWhenNoData()
    {
        // Arrange
        _databaseMock
            .Setup(d => d.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns(RedisValue.Null);
        _serverMock
            .Setup(s =>
                s.Keys(
                    It.IsAny<int>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<int>(),
                    It.IsAny<long>(),
                    It.IsAny<int>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .Returns(Array.Empty<RedisKey>());
        _databaseMock
            .Setup(d => d.HashGetAll(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns([]);

        // Act
        var summary = _tracker.GetSummary();

        // Assert
        summary.TotalPromptTokens.Should().Be(0);
        summary.TotalCompletionTokens.Should().Be(0);
        summary.CallCount.Should().Be(0);
    }

    [Fact]
    public void GetRecords_ReturnsFilteredBySource()
    {
        // Arrange
        var json1 =
            """{"source":"openai","promptTokens":100,"completionTokens":50,"model":"gpt-4"}""";
        var json2 =
            """{"source":"azure","promptTokens":200,"completionTokens":80,"model":"gpt-35"}""";

        _databaseMock
            .Setup(d =>
                d.ListRange(
                    It.IsAny<RedisKey>(),
                    It.IsAny<long>(),
                    It.IsAny<long>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .Returns([new RedisValue(json1), new RedisValue(json2)]);

        // Act
        var records = _tracker.GetRecords(source: "openai");

        // Assert
        records.Should().HaveCount(1);
        records[0].Source.Should().Be("openai");
        records[0].PromptTokens.Should().Be(100);
    }

    [Fact]
    public void Reset_DeletesAllKeysWhenNoFilter()
    {
        // Arrange
        var keys = new RedisKey[] { new("test:token_usage:total:prompt") };
        _serverMock
            .Setup(s =>
                s.Keys(
                    It.IsAny<int>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<int>(),
                    It.IsAny<long>(),
                    It.IsAny<int>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .Returns(keys);
        _databaseMock
            .Setup(d => d.KeyDelete(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns(true);

        // Act
        _tracker.Reset();

        // Assert
        _databaseMock.Verify(
            d => d.KeyDelete(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
            Times.Once
        );
    }

    [Fact]
    public void Reset_DeletesOnlySourceKeyWhenFiltered()
    {
        // Act
        _tracker.Reset(source: "openai");

        // Assert
        _databaseMock.Verify(
            d =>
                d.KeyDelete(
                    It.Is<RedisKey>(k => k.ToString().Contains("source:openai")),
                    It.IsAny<CommandFlags>()
                ),
            Times.Once
        );
    }

    [Fact]
    public void Record_ThrowsOnNullRecord()
    {
        var act = () => _tracker.Record((TokenUsageRecord)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordOverload_ThrowsOnEmptySource()
    {
        var act = () => _tracker.Record("", 100, 50);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ThrowsOnNullConnection()
    {
        var act = () => new RedisTokenUsageTracker(null!, Options.Create(_options));
        act.Should().Throw<ArgumentNullException>();
    }
}
