using FluentAssertions;

namespace ERP.Architecture.Tests;

/// <summary>
/// Fase S1 (hardening) — CI bloqueante. La auditoría de cierre de Access/IAM encontró dos
/// endpoints anónimos que permitían tomar control de cuentas/tenants ajenos sin autenticación:
/// POST /api/v1/auth/register (5A — creaba un usuario Admin en cualquier tenant existente
/// indicando TenantId/Role en el body) y POST /api/v1/auth/password-reset (5B — cambiaba la
/// contraseña de cualquier usuario solo con TenantId+Email, sin contraseña actual, token ni OTP).
/// Ambos se eliminaron. Este test evita que se reintroduzcan por accidente — mismo patrón que
/// PlatformControlPlaneGuardTests.
/// </summary>
public sealed class AuthAttackSurfaceGuardTests
{
    [Fact]
    public void AuthController_must_not_expose_register_or_direct_password_reset_routes()
    {
        var backendRoot = ResolveBackendSrcRoot();
        var file = Path.Combine(backendRoot, "ERP.API", "Controllers", "AuthController.cs");
        File.Exists(file).Should().BeTrue();

        var text = File.ReadAllText(file);
        var forbidden = new[]
        {
            "HttpPost(\"register\")",
            "HttpPost(\"password-reset\")",
            "RegisterCommand",
            "DirectPasswordResetCommand",
        };

        var violations = forbidden
            .Where(marker => text.Contains(marker, StringComparison.Ordinal))
            .ToList();
        violations
            .Should()
            .BeEmpty(
                "5A/5B: registro anónimo multiempresa y reset directo de contraseña sin verificación "
                    + "quedaron eliminados permanentemente — el alta del primer usuario vive únicamente en "
                    + "SetupController (token de instalación de un solo uso) y el reset de contraseña solo "
                    + "vía ForgotPassword+ResetPasswordWithToken (token por email)."
            );
    }

    [Fact]
    public void RegisterCommand_and_DirectPasswordResetCommand_source_files_must_not_exist()
    {
        var backendRoot = ResolveBackendSrcRoot();
        var applicationDir = Path.Combine(backendRoot, "ERP.Application");
        Directory.Exists(applicationDir).Should().BeTrue();

        var forbiddenFileNames = new[]
        {
            "RegisterCommand.cs",
            "RegisterHandler.cs",
            "RegisterCommandValidator.cs",
            "DirectPasswordResetCommand.cs",
            "DirectPasswordResetHandler.cs",
            "DirectPasswordResetCommandValidator.cs",
        };

        var found = Directory
            .EnumerateFiles(applicationDir, "*.cs", SearchOption.AllDirectories)
            .Select(f => Path.GetFileName(f))
            .Where(name => forbiddenFileNames.Contains(name, StringComparer.Ordinal))
            .ToList();

        found
            .Should()
            .BeEmpty(
                "estos casos de uso fueron eliminados en la Fase S1 (hallazgos 5A/5B), no deben recrearse"
            );
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
