using Dawning.Agents.Azure;
using Dawning.Agents.MCP.Client;
using Dawning.Agents.OpenAI;
using Dawning.Agents.OpenTelemetry;
using FluentAssertions;

namespace Dawning.Agents.Tests.Validation;

/// <summary>
/// Provider/Infrastructure Options Validate() 测试
/// </summary>
public class ProviderOptionsValidateTests
{
    #region MCPClientOptions

    [Fact]
    public void MCPClientOptions_DefaultValues_ShouldBeValid()
    {
        var options = new MCPClientOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MCPClientOptions_InvalidName_ShouldThrow(string? name)
    {
        var options = new MCPClientOptions { Name = name! };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Name*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MCPClientOptions_InvalidConnectionTimeout_ShouldThrow(int timeout)
    {
        var options = new MCPClientOptions { ConnectionTimeoutSeconds = timeout };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*ConnectionTimeout*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MCPClientOptions_InvalidRequestTimeout_ShouldThrow(int timeout)
    {
        var options = new MCPClientOptions { RequestTimeoutSeconds = timeout };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*RequestTimeout*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MCPClientOptions_InvalidToolCallTimeout_ShouldThrow(int timeout)
    {
        var options = new MCPClientOptions { ToolCallTimeoutSeconds = timeout };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*ToolCallTimeout*");
    }

    [Fact]
    public void MCPClientOptions_NegativeMaxReconnect_ShouldThrow()
    {
        var options = new MCPClientOptions { MaxReconnectAttempts = -1 };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*MaxReconnect*");
    }

    [Fact]
    public void MCPClientOptions_ZeroMaxReconnect_ShouldNotThrow()
    {
        var options = new MCPClientOptions { MaxReconnectAttempts = 0 };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    #endregion

    #region OpenAIProviderOptions

    [Fact]
    public void OpenAIProviderOptions_ValidConfig_ShouldNotThrow()
    {
        var options = new OpenAIProviderOptions { ApiKey = "sk-test" };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OpenAIProviderOptions_InvalidApiKey_ShouldThrow(string? key)
    {
        var options = new OpenAIProviderOptions { ApiKey = key };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*ApiKey*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OpenAIProviderOptions_InvalidModel_ShouldThrow(string? model)
    {
        var options = new OpenAIProviderOptions { ApiKey = "sk-test", Model = model! };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Model*");
    }

    #endregion

    #region AzureOpenAIProviderOptions

    [Fact]
    public void AzureOpenAIProviderOptions_ValidConfig_ShouldNotThrow()
    {
        var options = new AzureOpenAIProviderOptions
        {
            Endpoint = "https://test.openai.azure.com",
            ApiKey = "key",
            DeploymentName = "gpt-4o",
        };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AzureOpenAIProviderOptions_InvalidEndpoint_ShouldThrow(string? endpoint)
    {
        var options = new AzureOpenAIProviderOptions
        {
            Endpoint = endpoint,
            ApiKey = "key",
            DeploymentName = "gpt-4o",
        };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Endpoint*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AzureOpenAIProviderOptions_InvalidApiKey_ShouldThrow(string? key)
    {
        var options = new AzureOpenAIProviderOptions
        {
            Endpoint = "https://test.openai.azure.com",
            ApiKey = key,
            DeploymentName = "gpt-4o",
        };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*ApiKey*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AzureOpenAIProviderOptions_InvalidDeploymentName_ShouldThrow(string? name)
    {
        var options = new AzureOpenAIProviderOptions
        {
            Endpoint = "https://test.openai.azure.com",
            ApiKey = "key",
            DeploymentName = name,
        };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*DeploymentName*");
    }

    #endregion

    #region OpenTelemetryOptions

    [Fact]
    public void OpenTelemetryOptions_DefaultValues_ShouldBeValid()
    {
        var options = new OpenTelemetryOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(2.0)]
    public void OpenTelemetryOptions_InvalidSamplingRatio_ShouldThrow(double ratio)
    {
        var options = new OpenTelemetryOptions { SamplingRatio = ratio };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*SamplingRatio*");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void OpenTelemetryOptions_ValidSamplingRatio_ShouldNotThrow(double ratio)
    {
        var options = new OpenTelemetryOptions { SamplingRatio = ratio };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OpenTelemetryOptions_InvalidServiceName_ShouldThrow(string? name)
    {
        var options = new OpenTelemetryOptions { ServiceName = name! };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*ServiceName*");
    }

    [Fact]
    public void OpenTelemetryOptions_InvalidOtlpEndpoint_ShouldThrow()
    {
        var options = new OpenTelemetryOptions { OtlpEndpoint = "not-a-uri" };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*OtlpEndpoint*");
    }

    [Fact]
    public void OpenTelemetryOptions_ValidOtlpEndpoint_ShouldNotThrow()
    {
        var options = new OpenTelemetryOptions { OtlpEndpoint = "http://localhost:4317" };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void OpenTelemetryOptions_NullOtlpEndpoint_ShouldNotThrow()
    {
        var options = new OpenTelemetryOptions { OtlpEndpoint = null };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    #endregion
}
