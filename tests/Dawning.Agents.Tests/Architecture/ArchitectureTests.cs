using System.Reflection;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Core.Agent;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Dawning.Agents.Tests.Architecture;

/// <summary>
/// Architecture constraint tests — uses NetArchTest to verify inter-project dependencies, naming conventions, etc.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly AbstractionsAssembly = typeof(IAgent).Assembly;

    private static readonly Assembly CoreAssembly = typeof(AgentBase).Assembly;

    private static readonly string[] ProviderNamespaces =
    [
        "Dawning.Agents.Azure",
        "Dawning.Agents.OpenAI",
        "Dawning.Agents.Redis",
        "Dawning.Agents.Chroma",
        "Dawning.Agents.Pinecone",
        "Dawning.Agents.Qdrant",
        "Dawning.Agents.Weaviate",
        "Dawning.Agents.Serilog",
        "Dawning.Agents.OpenTelemetry",
        "Dawning.Agents.MCP",
    ];

    #region Dependency Rules

    [Fact]
    public void Abstractions_ShouldNotReference_Core()
    {
        var result = Types
            .InAssembly(AbstractionsAssembly)
            .ShouldNot()
            .HaveDependencyOn("Dawning.Agents.Core")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(FormatFailure("Abstractions should not reference Core", result));
    }

    [Fact]
    public void Abstractions_ShouldNotReference_AnyProvider()
    {
        var types = Types.InAssembly(AbstractionsAssembly);

        foreach (var provider in ProviderNamespaces)
        {
            var result = types.ShouldNot().HaveDependencyOn(provider).GetResult();

            result
                .IsSuccessful.Should()
                .BeTrue(FormatFailure($"Abstractions should not reference {provider}", result));
        }
    }

    [Fact]
    public void Abstractions_ShouldNotReference_ThirdPartyLibraries()
    {
        // Abstractions should be zero-dependency (only System.* and Microsoft.Extensions.*)
        var forbiddenDeps = new[]
        {
            "Polly",
            "FluentValidation",
            "Newtonsoft.Json",
            "Dapper",
            "Serilog",
        };

        var types = Types.InAssembly(AbstractionsAssembly);

        foreach (var dep in forbiddenDeps)
        {
            var result = types.ShouldNot().HaveDependencyOn(dep).GetResult();

            result
                .IsSuccessful.Should()
                .BeTrue(FormatFailure($"Abstractions should not reference {dep}", result));
        }
    }

    [Fact]
    public void Core_ShouldNotReference_AnyProvider()
    {
        var types = Types.InAssembly(CoreAssembly);

        foreach (var provider in ProviderNamespaces)
        {
            var result = types.ShouldNot().HaveDependencyOn(provider).GetResult();

            result
                .IsSuccessful.Should()
                .BeTrue(FormatFailure($"Core should not reference {provider}", result));
        }
    }

    #endregion

    #region Interface Naming

    [Fact]
    public void Interfaces_InAbstractions_ShouldStartWithI()
    {
        var result = Types
            .InAssembly(AbstractionsAssembly)
            .That()
            .AreInterfaces()
            .Should()
            .HaveNameStartingWith("I")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(FormatFailure("All interfaces in Abstractions should start with 'I'", result));
    }

    [Fact]
    public void Interfaces_InCore_ShouldStartWithI()
    {
        var result = Types
            .InAssembly(CoreAssembly)
            .That()
            .AreInterfaces()
            .Should()
            .HaveNameStartingWith("I")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(FormatFailure("All interfaces in Core should start with 'I'", result));
    }

    #endregion

    #region Options Naming

    [Fact]
    public void OptionsClasses_InAbstractions_ShouldBeClasses()
    {
        // Types ending with "Options" that are classes should indeed be classes (not structs)
        var result = Types
            .InAssembly(AbstractionsAssembly)
            .That()
            .HaveNameEndingWith("Options")
            .And()
            .AreNotInterfaces()
            .Should()
            .BeClasses()
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(
                FormatFailure("Options types (excluding interfaces) should be classes", result)
            );
    }

    [Fact]
    public void OptionsClasses_InAbstractions_ShouldBePublic()
    {
        // All Options classes should be public (for configuration binding)
        var result = Types
            .InAssembly(AbstractionsAssembly)
            .That()
            .HaveNameEndingWith("Options")
            .And()
            .AreClasses()
            .Should()
            .BePublic()
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(FormatFailure("Options classes in Abstractions should be public", result));
    }

    #endregion

    #region Async Naming

    [Fact]
    public void AsyncMethods_InAbstractions_ShouldEndWithAsync()
    {
        var interfaces = AbstractionsAssembly.GetExportedTypes().Where(t => t.IsInterface);

        var violations = new List<string>();

        foreach (var iface in interfaces)
        {
            var methods = iface
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m =>
                    (
                        m.ReturnType == typeof(Task)
                        || (
                            m.ReturnType.IsGenericType
                            && m.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)
                        )
                        || (
                            m.ReturnType.IsGenericType
                            && m.ReturnType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>)
                        )
                    ) && !m.Name.EndsWith("Async", StringComparison.Ordinal)
                );

            foreach (var method in methods)
            {
                violations.Add($"{iface.Name}.{method.Name}");
            }
        }

        violations
            .Should()
            .BeEmpty(
                "all async methods in Abstractions interfaces should end with 'Async', "
                    + $"but these do not: {string.Join(", ", violations)}"
            );
    }

    [Fact]
    public void AsyncMethods_InCore_PublicClasses_ShouldEndWithAsync()
    {
        var publicClasses = CoreAssembly.GetExportedTypes().Where(t => t.IsClass && !t.IsAbstract);

        var violations = new List<string>();

        foreach (var cls in publicClasses)
        {
            var methods = cls.GetMethods(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly
                )
                .Where(m =>
                    (
                        m.ReturnType == typeof(Task)
                        || (
                            m.ReturnType.IsGenericType
                            && m.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)
                        )
                        || (
                            m.ReturnType.IsGenericType
                            && m.ReturnType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>)
                        )
                    )
                    && !m.Name.EndsWith("Async", StringComparison.Ordinal)
                    // Exclude xUnit-style test methods and DI extension methods
                    && !m.IsSpecialName
                );

            foreach (var method in methods)
            {
                violations.Add($"{cls.Name}.{method.Name}");
            }
        }

        violations
            .Should()
            .BeEmpty(
                "all public async methods in Core should end with 'Async', "
                    + $"but these do not: {string.Join(", ", violations)}"
            );
    }

    #endregion

    #region Abstractions Structure

    [Fact]
    public void Abstractions_ShouldOnlyContainDataModels()
    {
        // Abstractions should only have interfaces, records, enums, and pure data classes
        // It should NOT have service implementation classes with behavior
        var allExported = AbstractionsAssembly.GetExportedTypes();

        // Service implementations are classes that implement domain service interfaces
        // (not marker/config interfaces like IValidatableOptions, IDisposable)
        var configInterfaces = new HashSet<string>
        {
            "IValidatableOptions",
            "IDisposable",
            "IAsyncDisposable",
        };

        var serviceImpls = allExported
            .Where(t =>
                t.IsClass
                && !t.IsAbstract
                && t.GetInterfaces()
                    .Any(i =>
                        i.Namespace != null
                        && i.Namespace.StartsWith("Dawning.Agents", StringComparison.Ordinal)
                        && i.Name.StartsWith("I", StringComparison.Ordinal)
                        && !configInterfaces.Contains(i.Name)
                    )
            )
            .ToList();

        serviceImpls
            .Should()
            .BeEmpty(
                "Abstractions should not contain service implementations (classes implementing domain interfaces), "
                    + $"but found: {string.Join(", ", serviceImpls.Select(t => t.FullName))}"
            );
    }

    [Fact]
    public void Core_ShouldReference_Abstractions()
    {
        // Verify Core assembly references Abstractions assembly
        var referencedAssemblies = CoreAssembly.GetReferencedAssemblies().Select(a => a.Name);

        referencedAssemblies
            .Should()
            .Contain(
                "Dawning.Agents.Abstractions",
                "Core should have a project reference to Abstractions"
            );
    }

    #endregion

    #region Layer Isolation

    [Fact]
    public void ServiceCollectionExtensions_ShouldResideInCore()
    {
        // All DI extension classes should be in Core, not Abstractions
        var diExtensions = Types
            .InAssembly(AbstractionsAssembly)
            .That()
            .HaveNameEndingWith("ServiceCollectionExtensions")
            .GetTypes();

        diExtensions
            .Should()
            .BeEmpty(
                "ServiceCollectionExtensions should only exist in Core, not Abstractions, "
                    + $"but found: {string.Join(", ", diExtensions.Select(t => t.FullName))}"
            );
    }

    [Fact]
    public void Validators_ShouldResideInCore()
    {
        // Validators should be in Core, not Abstractions
        var validators = Types
            .InAssembly(AbstractionsAssembly)
            .That()
            .HaveNameEndingWith("Validator")
            .GetTypes();

        validators
            .Should()
            .BeEmpty(
                "Validators should only exist in Core, not Abstractions, "
                    + $"but found: {string.Join(", ", validators.Select(t => t.FullName))}"
            );
    }

    #endregion

    #region Helpers

    private static string FormatFailure(string rule, TestResult result)
    {
        if (result.IsSuccessful || result.FailingTypes == null)
        {
            return rule;
        }

        var types = string.Join(", ", result.FailingTypes.Select(t => t.FullName).Take(10));
        return $"{rule}. Violating types: {types}";
    }

    #endregion
}
