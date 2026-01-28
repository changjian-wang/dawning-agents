using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dawning.Agents.Abstractions.Security;
using Dawning.Agents.Core.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dawning.Agents.Tests.Security;

public class ApiKeyAuthenticationProviderTests
{
    private static IOptions<SecurityOptions> CreateOptions(
        Dictionary<string, ApiKeyConfig>? apiKeys = null
    )
    {
        var options = new SecurityOptions
        {
            ApiKeys = apiKeys ?? new Dictionary<string, ApiKeyConfig>(),
        };
        return Options.Create(options);
    }

    [Fact]
    public async Task AuthenticateAsync_WithEmptyToken_ShouldReturnFailure()
    {
        // Arrange
        var provider = new ApiKeyAuthenticationProvider(CreateOptions());

        // Act
        var result = await provider.AuthenticateAsync("");

        // Assert
        result.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be("Token is required");
    }

    [Fact]
    public async Task AuthenticateAsync_WithNullToken_ShouldReturnFailure()
    {
        // Arrange
        var provider = new ApiKeyAuthenticationProvider(CreateOptions());

        // Act
        var result = await provider.AuthenticateAsync(null!);

        // Assert
        result.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be("Token is required");
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidToken_ShouldReturnJwtNotImplemented()
    {
        // Arrange
        var provider = new ApiKeyAuthenticationProvider(CreateOptions());

        // Act
        var result = await provider.AuthenticateAsync("some-jwt-token");

        // Assert
        result.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Contain("not implemented");
    }

    [Fact]
    public async Task AuthenticateApiKeyAsync_WithEmptyKey_ShouldReturnFailure()
    {
        // Arrange
        var provider = new ApiKeyAuthenticationProvider(CreateOptions());

        // Act
        var result = await provider.AuthenticateApiKeyAsync("");

        // Assert
        result.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be("API key is required");
    }

    [Fact]
    public async Task AuthenticateApiKeyAsync_WithInvalidKey_ShouldReturnFailure()
    {
        // Arrange
        var provider = new ApiKeyAuthenticationProvider(CreateOptions());

        // Act
        var result = await provider.AuthenticateApiKeyAsync("invalid-key");

        // Assert
        result.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be("Invalid API key");
    }

    [Fact]
    public async Task AuthenticateApiKeyAsync_WithValidKey_ShouldReturnSuccess()
    {
        // Arrange
        var apiKeys = new Dictionary<string, ApiKeyConfig>
        {
            ["valid-key"] = new ApiKeyConfig
            {
                Name = "Test Key",
                IsEnabled = true,
                Roles = ["user", "reader"],
            },
        };
        var provider = new ApiKeyAuthenticationProvider(CreateOptions(apiKeys));

        // Act
        var result = await provider.AuthenticateApiKeyAsync("valid-key");

        // Assert
        result.IsAuthenticated.Should().BeTrue();
        result.UserId.Should().Be("valid-key");
        result.UserName.Should().Be("Test Key");
        result.Roles.Should().Contain("user");
        result.Roles.Should().Contain("reader");
    }

    [Fact]
    public async Task AuthenticateApiKeyAsync_WithDisabledKey_ShouldReturnFailure()
    {
        // Arrange
        var apiKeys = new Dictionary<string, ApiKeyConfig>
        {
            ["disabled-key"] = new ApiKeyConfig { Name = "Disabled Key", IsEnabled = false },
        };
        var provider = new ApiKeyAuthenticationProvider(CreateOptions(apiKeys));

        // Act
        var result = await provider.AuthenticateApiKeyAsync("disabled-key");

        // Assert
        result.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be("API key is disabled");
    }

    [Fact]
    public async Task AuthenticateApiKeyAsync_WithExpiredKey_ShouldReturnFailure()
    {
        // Arrange
        var apiKeys = new Dictionary<string, ApiKeyConfig>
        {
            ["expired-key"] = new ApiKeyConfig
            {
                Name = "Expired Key",
                IsEnabled = true,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1),
            },
        };
        var provider = new ApiKeyAuthenticationProvider(CreateOptions(apiKeys));

        // Act
        var result = await provider.AuthenticateApiKeyAsync("expired-key");

        // Assert
        result.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be("API key has expired");
    }

    [Fact]
    public async Task AuthenticateApiKeyAsync_WithFutureExpiry_ShouldReturnSuccess()
    {
        // Arrange
        var expiresAt = DateTimeOffset.UtcNow.AddDays(30);
        var apiKeys = new Dictionary<string, ApiKeyConfig>
        {
            ["future-key"] = new ApiKeyConfig
            {
                Name = "Future Key",
                IsEnabled = true,
                ExpiresAt = expiresAt,
            },
        };
        var provider = new ApiKeyAuthenticationProvider(CreateOptions(apiKeys));

        // Act
        var result = await provider.AuthenticateApiKeyAsync("future-key");

        // Assert
        result.IsAuthenticated.Should().BeTrue();
        result.ExpiresAt.Should().Be(expiresAt);
    }
}

public class RoleBasedAuthorizationProviderTests
{
    private static IOptions<SecurityOptions> CreateOptions(
        Dictionary<string, RolePermissions>? roles = null
    )
    {
        var options = new SecurityOptions
        {
            Roles = roles ?? new Dictionary<string, RolePermissions>(),
        };
        return Options.Create(options);
    }

    private static AuthenticationResult CreateAuthUser(
        string userId = "user1",
        bool isAuthenticated = true,
        params string[] roles
    )
    {
        if (!isAuthenticated)
        {
            return AuthenticationResult.Failure("Not authenticated");
        }
        return AuthenticationResult.Success(userId, roles: roles);
    }

    [Fact]
    public async Task AuthorizeAsync_WithUnauthenticatedUser_ShouldReturnDenied()
    {
        // Arrange
        var provider = new RoleBasedAuthorizationProvider(CreateOptions());
        var user = CreateAuthUser(isAuthenticated: false);

        // Act
        var result = await provider.AuthorizeAsync(user, "resource", "action");

        // Assert
        result.IsAuthorized.Should().BeFalse();
        result.Reason.Should().Contain("not authenticated");
    }

    [Fact]
    public async Task AuthorizeAsync_WithAdminRole_ShouldReturnAllowed()
    {
        // Arrange
        var provider = new RoleBasedAuthorizationProvider(CreateOptions());
        var user = CreateAuthUser(roles: "admin");

        // Act
        var result = await provider.AuthorizeAsync(user, "any-resource", "any-action");

        // Assert
        result.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_WithRegularUser_ShouldReturnAllowed()
    {
        // Arrange
        var provider = new RoleBasedAuthorizationProvider(CreateOptions());
        var user = CreateAuthUser(roles: "user");

        // Act
        var result = await provider.AuthorizeAsync(user, "resource", "read");

        // Assert
        result.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeToolAsync_WithUnauthenticatedUser_ShouldReturnDenied()
    {
        // Arrange
        var provider = new RoleBasedAuthorizationProvider(CreateOptions());
        var user = CreateAuthUser(isAuthenticated: false);

        // Act
        var result = await provider.AuthorizeToolAsync(user, "some-tool");

        // Assert
        result.IsAuthorized.Should().BeFalse();
    }

    [Fact]
    public async Task AuthorizeToolAsync_WithDeniedTool_ShouldReturnDenied()
    {
        // Arrange
        var roles = new Dictionary<string, RolePermissions>
        {
            ["restricted"] = new RolePermissions { DeniedTools = ["dangerous-tool"] },
        };
        var provider = new RoleBasedAuthorizationProvider(CreateOptions(roles));
        var user = CreateAuthUser(roles: "restricted");

        // Act
        var result = await provider.AuthorizeToolAsync(user, "dangerous-tool");

        // Assert
        result.IsAuthorized.Should().BeFalse();
        result.Reason.Should().Contain("denied");
    }

    [Fact]
    public async Task AuthorizeToolAsync_WithWildcardDeny_ShouldReturnDenied()
    {
        // Arrange
        var roles = new Dictionary<string, RolePermissions>
        {
            ["no-tools"] = new RolePermissions { DeniedTools = ["*"] },
        };
        var provider = new RoleBasedAuthorizationProvider(CreateOptions(roles));
        var user = CreateAuthUser(roles: "no-tools");

        // Act
        var result = await provider.AuthorizeToolAsync(user, "any-tool");

        // Assert
        result.IsAuthorized.Should().BeFalse();
    }

    [Fact]
    public async Task AuthorizeToolAsync_WithAllowedTool_ShouldReturnAllowed()
    {
        // Arrange
        var roles = new Dictionary<string, RolePermissions>
        {
            ["user"] = new RolePermissions { AllowedTools = ["safe-tool"] },
        };
        var provider = new RoleBasedAuthorizationProvider(CreateOptions(roles));
        var user = CreateAuthUser(roles: "user");

        // Act
        var result = await provider.AuthorizeToolAsync(user, "safe-tool");

        // Assert
        result.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeToolAsync_WithWildcardAllow_ShouldReturnAllowed()
    {
        // Arrange
        var roles = new Dictionary<string, RolePermissions>
        {
            ["power-user"] = new RolePermissions { AllowedTools = ["*"] },
        };
        var provider = new RoleBasedAuthorizationProvider(CreateOptions(roles));
        var user = CreateAuthUser(roles: "power-user");

        // Act
        var result = await provider.AuthorizeToolAsync(user, "any-tool");

        // Assert
        result.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeToolAsync_WithNoRoleConfig_ShouldReturnAllowedByDefault()
    {
        // Arrange
        var provider = new RoleBasedAuthorizationProvider(CreateOptions());
        var user = CreateAuthUser(roles: "unknown-role");

        // Act
        var result = await provider.AuthorizeToolAsync(user, "some-tool");

        // Assert
        result.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAgentAsync_WithUnauthenticatedUser_ShouldReturnDenied()
    {
        // Arrange
        var provider = new RoleBasedAuthorizationProvider(CreateOptions());
        var user = CreateAuthUser(isAuthenticated: false);

        // Act
        var result = await provider.AuthorizeAgentAsync(user, "some-agent");

        // Assert
        result.IsAuthorized.Should().BeFalse();
    }

    [Fact]
    public async Task AuthorizeAgentAsync_WithAllowedAgent_ShouldReturnAllowed()
    {
        // Arrange
        var roles = new Dictionary<string, RolePermissions>
        {
            ["user"] = new RolePermissions { AllowedAgents = ["helper-agent"] },
        };
        var provider = new RoleBasedAuthorizationProvider(CreateOptions(roles));
        var user = CreateAuthUser(roles: "user");

        // Act
        var result = await provider.AuthorizeAgentAsync(user, "helper-agent");

        // Assert
        result.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAgentAsync_WithWildcardAllow_ShouldReturnAllowed()
    {
        // Arrange
        var roles = new Dictionary<string, RolePermissions>
        {
            ["admin"] = new RolePermissions { AllowedAgents = ["*"] },
        };
        var provider = new RoleBasedAuthorizationProvider(CreateOptions(roles));
        var user = CreateAuthUser(roles: "admin");

        // Act
        var result = await provider.AuthorizeAgentAsync(user, "any-agent");

        // Assert
        result.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAgentAsync_WithNoConfig_ShouldReturnAllowedByDefault()
    {
        // Arrange
        var provider = new RoleBasedAuthorizationProvider(CreateOptions());
        var user = CreateAuthUser(roles: "user");

        // Act
        var result = await provider.AuthorizeAgentAsync(user, "some-agent");

        // Assert
        result.IsAuthorized.Should().BeTrue();
    }
}

public class AuthenticationResultTests
{
    [Fact]
    public void Success_ShouldCreateAuthenticatedResult()
    {
        // Act
        var result = AuthenticationResult.Success("user123", "John Doe", ["admin", "user"]);

        // Assert
        result.IsAuthenticated.Should().BeTrue();
        result.UserId.Should().Be("user123");
        result.UserName.Should().Be("John Doe");
        result.Roles.Should().Contain("admin");
        result.Roles.Should().Contain("user");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Success_WithMinimalParams_ShouldUseDefaults()
    {
        // Act
        var result = AuthenticationResult.Success("user123");

        // Assert
        result.IsAuthenticated.Should().BeTrue();
        result.UserId.Should().Be("user123");
        result.Roles.Should().BeEmpty();
        result.Claims.Should().BeEmpty();
    }

    [Fact]
    public void Failure_ShouldCreateUnauthenticatedResult()
    {
        // Act
        var result = AuthenticationResult.Failure("Invalid credentials");

        // Assert
        result.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be("Invalid credentials");
        result.UserId.Should().BeNull();
    }
}

public class AuthorizationResultTests
{
    [Fact]
    public void Allowed_ShouldCreateAuthorizedResult()
    {
        // Act
        var result = AuthorizationResult.Allowed();

        // Assert
        result.IsAuthorized.Should().BeTrue();
        result.Reason.Should().BeNull();
    }

    [Fact]
    public void Denied_ShouldCreateDeniedResult()
    {
        // Act
        var result = AuthorizationResult.Denied("Insufficient permissions");

        // Assert
        result.IsAuthorized.Should().BeFalse();
        result.Reason.Should().Be("Insufficient permissions");
    }
}
