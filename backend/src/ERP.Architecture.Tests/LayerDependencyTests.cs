using FluentAssertions;
using NetArchTest.Rules;

namespace ERP.Architecture.Tests;

/// <summary>
/// Reglas de capas (docs/ARCHITECTURE-RULES.md B-01, B-03, B-05).
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly System.Reflection.Assembly DomainAssembly =
        typeof(ERP.Domain.Common.BaseEntity).Assembly;

    private static readonly System.Reflection.Assembly ApplicationAssembly =
        typeof(ERP.Application.DependencyInjection).Assembly;

    private static readonly System.Reflection.Assembly InfrastructureAssembly =
        typeof(ERP.Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Domain_must_not_reference_ef_core()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Domain_must_not_reference_asp_net_core()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Domain_must_not_reference_infrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn("ERP.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Application_must_not_reference_infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn("ERP.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Application_must_not_reference_ef_core()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Infrastructure_must_not_reference_api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot().HaveDependencyOn("ERP.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}
