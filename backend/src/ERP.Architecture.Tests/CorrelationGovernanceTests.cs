using ERP.API.Middleware;
using FluentAssertions;
using NetArchTest.Rules;
using System.Reflection;

namespace ERP.Architecture.Tests;

/// <summary>
/// Fase 4 (hardening del contrato de respuesta): <see cref="RequestCorrelationMiddleware"/>
/// es la ÚNICA fuente del <c>correlationId</c> por request. Ningún otro componente
/// (ResponseFactory, controllers, handlers, otros middlewares) debe leer
/// <c>HttpContext.TraceIdentifier</c>, generar su propio id o escribir el header
/// <c>X-Correlation-Id</c> directamente.
/// </summary>
public sealed class CorrelationGovernanceTests
{
    private static readonly Assembly ApiAssembly =
        typeof(ERP.API.Extensions.ResponseFactory).Assembly;

    [Fact]
    public void Only_RequestCorrelationMiddleware_reads_TraceIdentifier_or_writes_correlation_header()
    {
        var backendRoot = ResolveBackendSrcRoot();
        var apiDir = Path.Combine(backendRoot, "ERP.API");
        Directory.Exists(apiDir).Should().BeTrue();

        var allowedFile = Path.Combine("ERP.API", "Middleware", "RequestCorrelationMiddleware.cs");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(apiDir, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(backendRoot, file).Replace('\\', '/');
            if (relative == allowedFile.Replace('\\', '/')) continue;

            var text = File.ReadAllText(file);
            if (text.Contains("TraceIdentifier", StringComparison.Ordinal)
                || text.Contains("X-Correlation-Id", StringComparison.Ordinal))
            {
                violations.Add(relative);
            }
        }

        violations.Should().BeEmpty(
            "TraceIdentifier y el header X-Correlation-Id solo deben usarse dentro de " +
            "RequestCorrelationMiddleware; el resto del código debe llamar a " +
            "RequestCorrelationMiddleware.Resolve(context).");
    }

    [Fact]
    public void ResponseFactory_and_ExceptionMiddleware_do_not_generate_their_own_correlation_id()
    {
        var backendRoot = ResolveBackendSrcRoot();
        var files = new[]
        {
            Path.Combine(backendRoot, "ERP.API", "Extensions", "ResponseFactory.cs"),
            Path.Combine(backendRoot, "ERP.API", "Middleware", "ExceptionMiddleware.cs"),
        };

        foreach (var file in files)
        {
            File.Exists(file).Should().BeTrue(file);
            var text = File.ReadAllText(file);
            text.Should().NotContain("Guid.NewGuid()",
                $"{Path.GetFileName(file)} no debe generar su propio correlationId; debe usar RequestCorrelationMiddleware.Resolve(context).");
        }
    }

    /// <summary>ResponseFactory (ERP.API) no debe depender de Infrastructure: solo construye el envelope a partir de Application.Common.</summary>
    [Fact]
    public void ResponseFactory_must_not_depend_on_infrastructure()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That().HaveNameMatching("ResponseFactory")
            .ShouldNot().HaveDependencyOn("ERP.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
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
