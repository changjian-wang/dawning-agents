namespace Dawning.Agents.Tests.Redis;

using Dawning.Agents.Redis;
using FluentAssertions;

public class RedisHealthCheckTests
{
    [Fact]
    public void Constructor_NullRedis_ThrowsArgumentNullException()
    {
        var act = () => new RedisHealthCheck(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("redis");
    }
}
