namespace Dawning.Agents.Tests.Observability;

using Dawning.Agents.Abstractions.Observability;
using FluentAssertions;

public class TelemetryConfigTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Act
        var config = new TelemetryConfig();

        // Assert
        config.EnableLogging.Should().BeTrue();
        config.EnableMetrics.Should().BeTrue();
        config.EnableTracing.Should().BeTrue();
        config.ServiceName.Should().Be("Dawning.Agents");
        config.ServiceVersion.Should().Be("1.0.0");
        config.Environment.Should().Be("development");
        config.MinLogLevel.Should().Be(TelemetryLogLevel.Information);
        config.TraceSampleRate.Should().Be(1.0);
        config.OtlpEndpoint.Should().BeNull();
    }

    [Fact]
    public void SectionName_ShouldBeTelemetry()
    {
        // Assert
        TelemetryConfig.SectionName.Should().Be("Telemetry");
    }

    [Theory]
    [InlineData(TelemetryLogLevel.Trace, 0)]
    [InlineData(TelemetryLogLevel.Debug, 1)]
    [InlineData(TelemetryLogLevel.Information, 2)]
    [InlineData(TelemetryLogLevel.Warning, 3)]
    [InlineData(TelemetryLogLevel.Error, 4)]
    [InlineData(TelemetryLogLevel.Critical, 5)]
    public void LogLevel_ShouldHaveCorrectValues(TelemetryLogLevel level, int expectedValue)
    {
        // Assert
        ((int)level).Should().Be(expectedValue);
    }

    [Fact]
    public void Config_ShouldBeSettable()
    {
        // Act
        var config = new TelemetryConfig
        {
            EnableLogging = false,
            EnableMetrics = false,
            EnableTracing = false,
            ServiceName = "TestService",
            ServiceVersion = "2.0.0",
            Environment = "production",
            MinLogLevel = TelemetryLogLevel.Warning,
            TraceSampleRate = 0.5,
            OtlpEndpoint = "http://localhost:4317",
        };

        // Assert
        config.EnableLogging.Should().BeFalse();
        config.EnableMetrics.Should().BeFalse();
        config.EnableTracing.Should().BeFalse();
        config.ServiceName.Should().Be("TestService");
        config.ServiceVersion.Should().Be("2.0.0");
        config.Environment.Should().Be("production");
        config.MinLogLevel.Should().Be(TelemetryLogLevel.Warning);
        config.TraceSampleRate.Should().Be(0.5);
        config.OtlpEndpoint.Should().Be("http://localhost:4317");
    }
}
