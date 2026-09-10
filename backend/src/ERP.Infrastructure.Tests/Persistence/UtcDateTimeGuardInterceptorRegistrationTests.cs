using FluentAssertions;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// ZH-DATETIME-UTC-GUARDRAILS-01: gate de arquitectura sin Docker/DB — si alguien quita
/// UtcDateTimeGuardInterceptor de DependencyInjection.cs (registro AddScoped o entrada en
/// AddInterceptors), el guard global deja de ejecutarse en producción sin que ningún test que
/// dependa de PostgreSQL real lo note (esos tests instancian su propio interceptor directamente,
/// sin pasar por DI). Deliberadamente en su propia clase, sin campo PostgreSqlContainer, para no
/// heredar el requisito de Docker de <see cref="UtcDateTimeGuardInterceptorTests"/>.
/// </summary>
public sealed class UtcDateTimeGuardInterceptorRegistrationTests
{
    [Fact]
    public void UtcDateTimeGuardInterceptor_stays_registered_in_DependencyInjection()
    {
        var diFile = Path.Combine(
            ResolveBackendRoot(),
            "src",
            "ERP.Infrastructure",
            "DependencyInjection.cs"
        );
        File.Exists(diFile).Should().BeTrue($"no se encontró {diFile}");

        var text = File.ReadAllText(diFile);

        text.Should()
            .Contain(
                "services.AddScoped<UtcDateTimeGuardInterceptor>();",
                "UtcDateTimeGuardInterceptor debe registrarse en el contenedor DI"
            );
        text.Should()
            .Contain(
                "sp.GetRequiredService<UtcDateTimeGuardInterceptor>()",
                "UtcDateTimeGuardInterceptor debe añadirse a AddInterceptors() del ErpDbContext — "
                    + "de lo contrario el guard nunca se ejecuta en SaveChanges"
            );
    }

    private static string ResolveBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (
                File.Exists(Path.Combine(dir.FullName, "ERP.sln"))
                || Directory.Exists(Path.Combine(dir.FullName, "src", "ERP.API"))
            )
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "No se encontró la raíz backend (ERP.sln / src/ERP.API)."
        );
    }
}
