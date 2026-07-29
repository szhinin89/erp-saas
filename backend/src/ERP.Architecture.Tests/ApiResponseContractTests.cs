using ERP.Application.Common;
using FluentAssertions;
using NetArchTest.Rules;
using System.Reflection;

namespace ERP.Architecture.Tests;

/// <summary>
/// Gobernanza del contrato de respuesta (<c>code</c> como fuente única de verdad).
/// Reglas A-D: docs/AI-RULES/BACKEND-RULES.md (sección "Envelope de respuesta").
/// </summary>
public sealed class ApiResponseContractTests
{
    private static readonly Assembly DomainAssembly =
        typeof(ERP.Domain.Common.BaseEntity).Assembly;

    private static readonly Assembly ApplicationAssembly =
        typeof(ERP.Application.DependencyInjection).Assembly;

    private static readonly Assembly ApiAssembly =
        typeof(ERP.API.Extensions.ResponseFactory).Assembly;

    /// <summary>Regla C: el dominio no conoce el contrato de respuesta de la API.</summary>
    [Fact]
    public void Domain_must_not_reference_application()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn("ERP.Application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    /// <summary>Regla C: el dominio no conoce ApiResponse/ResponseFactory (viven en ERP.API).</summary>
    [Fact]
    public void Domain_must_not_reference_api()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn("ERP.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    /// <summary>Regla B: los handlers no conocen ResponseFactory/ApiResponse/ApiResultExtensions (viven en ERP.API).</summary>
    [Fact]
    public void Application_must_not_reference_api()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn("ERP.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    /// <summary>Regla A: ningún controller construye <c>ApiResponse&lt;T&gt;</c> a mano; todos pasan por ResponseFactory/ApiResultExtensions.</summary>
    [Fact]
    public void Controllers_must_not_construct_ApiResponse_manually()
    {
        var backendRoot = ResolveBackendSrcRoot();
        var controllersDir = Path.Combine(backendRoot, "ERP.API", "Controllers");
        Directory.Exists(controllersDir).Should().BeTrue();

        var forbidden = new[] { "new ApiResponse<", "new ApiResponse(", "new ApiResponse {" };
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(controllersDir, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            if (forbidden.Any(pattern => text.Contains(pattern, StringComparison.Ordinal)))
            {
                violations.Add(Path.GetRelativePath(backendRoot, file).Replace('\\', '/'));
            }
        }

        violations.Should().BeEmpty(
            "los controllers no deben construir ApiResponse<T> manualmente; deben usar " +
            "ApiResultExtensions (ApiOk/ApiCreated/ToOkOrBadRequest/...), que delega en ResponseFactory.");
    }

    /// <summary>Regla B: los handlers (Application) no construyen mensajes vía MessageCatalog ni el envelope ApiResponse.</summary>
    [Fact]
    public void Application_must_not_reference_MessageCatalog_outside_its_own_file()
    {
        var backendRoot = ResolveBackendSrcRoot();
        var applicationDir = Path.Combine(backendRoot, "ERP.Application");
        Directory.Exists(applicationDir).Should().BeTrue();

        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(applicationDir, "*.cs", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(file);
            if (fileName is "MessageCatalog.cs" or "ApiResponseCodes.cs" or "ApiSeverity.cs") continue;

            var text = File.ReadAllText(file);
            // "MessageCatalog." detecta uso real (Resolve/RegisteredCodes); excluye
            // referencias de documentación como <see cref="MessageCatalog"/>.
            if (text.Contains("MessageCatalog.", StringComparison.Ordinal))
            {
                violations.Add(Path.GetRelativePath(backendRoot, file).Replace('\\', '/'));
            }
        }

        violations.Should().BeEmpty(
            "MessageCatalog solo debe ser consumido por ResponseFactory (ERP.API); los handlers " +
            "devuelven Result<T> con un ApiResponseCodes.* y dejan que ResponseFactory resuelva el mensaje.");
    }

    /// <summary>
    /// Recorre <see cref="ApiResponseCodes"/> y sus clases anidadas por dominio
    /// (<c>Common</c>, futuros <c>Inventory</c>/<c>Sales</c>/...) para cubrir
    /// automáticamente cualquier código nuevo agregado al catálogo.
    /// </summary>
    private static IEnumerable<string> CollectResponseCodes(Type type)
    {
        var fields = type
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.GetField)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

        var nested = type
            .GetNestedTypes(BindingFlags.Public | BindingFlags.Static)
            .Where(t => t.IsAbstract && t.IsSealed)
            .SelectMany(CollectResponseCodes);

        return fields.Concat(nested);
    }

    /// <summary>Regla D: cada ApiResponseCodes.* (incluyendo clases anidadas por dominio) tiene una entrada propia en MessageCatalog y viceversa (sin huérfanos).</summary>
    [Fact]
    public void MessageCatalog_has_exactly_one_entry_per_ApiResponseCode()
    {
        var declaredCodes = CollectResponseCodes(typeof(ApiResponseCodes))
            .ToHashSet(StringComparer.Ordinal);

        declaredCodes.Should().NotBeEmpty();

        var catalogCodes = MessageCatalog.RegisteredCodes.ToHashSet(StringComparer.Ordinal);

        var missingFromCatalog = declaredCodes.Except(catalogCodes).ToList();
        var orphanInCatalog = catalogCodes.Except(declaredCodes).ToList();

        missingFromCatalog.Should().BeEmpty(
            "todo código de ApiResponseCodes debe tener una entrada en MessageCatalog: " +
            string.Join(", ", missingFromCatalog));

        orphanInCatalog.Should().BeEmpty(
            "MessageCatalog no debe tener entradas para códigos que ya no existen en ApiResponseCodes: " +
            string.Join(", ", orphanInCatalog));
    }

    private static string ResolveBackendSrcRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "ERP.API")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("No se encontró backend/src (ERP.API).");
    }
}
