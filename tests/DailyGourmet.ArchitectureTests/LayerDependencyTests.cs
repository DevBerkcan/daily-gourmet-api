using DailyGourmet.Application.Abstractions;
using DailyGourmet.Infrastructure;
using NetArchTest.Rules;

namespace DailyGourmet.ArchitectureTests;

/// <summary>
/// Erzwingt die Abhängigkeitsrichtung aus §5/§53 automatisiert — ein Verstoß lässt den Build/Test-Lauf
/// fehlschlagen, nicht nur eine Doku-Regel verletzen.
/// </summary>
public sealed class LayerDependencyTests
{
    private const string ApplicationNamespace = "DailyGourmet.Application";
    private const string InfrastructureNamespace = "DailyGourmet.Infrastructure";
    private const string ApiNamespace = "DailyGourmet.Api";

    private static readonly string[] ForbiddenFrameworkNamespaces =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "System.Text.Json",
    ];

    [Fact]
    public void Domain_should_not_depend_on_other_layers()
    {
        var result = Types.InAssembly(typeof(DailyGourmet.Domain.AssemblyMarker).Assembly)
            .Should()
            .NotHaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailureMessage(result));
    }

    [Fact]
    public void Application_should_only_depend_on_domain()
    {
        var result = Types.InAssembly(typeof(ITenantContext).Assembly)
            .Should()
            .NotHaveDependencyOnAny(InfrastructureNamespace, ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailureMessage(result));
    }

    [Fact]
    public void Infrastructure_should_not_depend_on_api()
    {
        var result = Types.InAssembly(typeof(TenantContext).Assembly)
            .Should()
            .NotHaveDependencyOn(ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FailureMessage(result));
    }

    [Fact]
    public void Domain_should_not_depend_on_forbidden_frameworks()
    {
        var result = Types.InAssembly(typeof(DailyGourmet.Domain.AssemblyMarker).Assembly)
            .Should()
            .NotHaveDependencyOnAny(ForbiddenFrameworkNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FailureMessage(result));
    }

    [Fact]
    public void Application_should_not_depend_on_forbidden_frameworks()
    {
        var result = Types.InAssembly(typeof(ITenantContext).Assembly)
            .Should()
            .NotHaveDependencyOnAny(ForbiddenFrameworkNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FailureMessage(result));
    }

    private static string FailureMessage(TestResult result) =>
        $"Verstoß gegen die Abhängigkeitsregeln: {string.Join(", ", result.FailingTypeNames ?? [])}";
}
