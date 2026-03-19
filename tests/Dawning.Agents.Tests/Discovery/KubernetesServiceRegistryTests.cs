namespace Dawning.Agents.Tests.Discovery;

using Dawning.Agents.Core.Discovery;
using FluentAssertions;

public class KubernetesServiceRegistryTests
{
    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        var act = () => new KubernetesServiceRegistry(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("httpClient");
    }

    [Fact]
    public void Constructor_ValidHttpClient_DoesNotThrow()
    {
        using var httpClient = new HttpClient();

        var act = () => new KubernetesServiceRegistry(httpClient);

        act.Should().NotThrow();
    }
}
