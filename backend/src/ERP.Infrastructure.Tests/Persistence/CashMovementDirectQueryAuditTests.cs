using FluentAssertions;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// ERP-CORE-CLOSEOUT-05-FIX02 (P1-2) — <c>CashMovement</c> implementa solo <c>IMustHaveTenant</c>
/// (filtro EF tenant-only, no company/branch) porque hoy nunca se consulta directamente: siempre
/// se accede como hijo del agregado <c>CashSession</c>, que sí está scopeado por Company+Branch.
/// Este test hace explícita esa invariante y falla si alguien introduce una consulta directa
/// (<c>Set&lt;CashMovement&gt;()</c> o el DbSet <c>CashMovements</c>) fuera del mapeo EF, evitando
/// que el hueco documentado en <see cref="ERP.Domain.Modules.Caja.Entities.CashMovement"/> se
/// vuelva explotable silenciosamente.
/// </summary>
public sealed class CashMovementDirectQueryAuditTests
{
    private static readonly string[] AllowedRelativePaths =
    [
        // Mapeo EF: define el DbSet, no lo consulta.
        "src/ERP.Infrastructure/Persistence/ErpDbContext.cs",
        // Configuración EF (Fluent API), no consulta datos.
        "src/ERP.Infrastructure/Persistence/Configurations/Caja/CashMovementConfiguration.cs",
    ];

    [Fact]
    public void CashMovement_no_se_consulta_directamente_fuera_del_agregado_CashSession()
    {
        var backendRoot = ResolveBackendRoot();
        var violations = new List<string>();

        foreach (
            var file in Directory.EnumerateFiles(backendRoot, "*.cs", SearchOption.AllDirectories)
        )
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;
            if (file.Contains(".Tests", StringComparison.OrdinalIgnoreCase))
                continue;

            var text = File.ReadAllText(file);
            var hasDirectQuery =
                text.Contains("Set<CashMovement>(", StringComparison.Ordinal)
                || text.Contains(".CashMovements.", StringComparison.Ordinal)
                || text.Contains(".CashMovements\n", StringComparison.Ordinal)
                || text.Contains(".CashMovements;", StringComparison.Ordinal);
            if (!hasDirectQuery)
                continue;

            var relative = Path.GetRelativePath(backendRoot, file).Replace('\\', '/');
            if (!AllowedRelativePaths.Contains(relative))
                violations.Add(relative);
        }

        violations
            .Should()
            .BeEmpty(
                "CashMovement solo debe leerse como hijo de CashSession (ya scopeado por Company+Branch) — "
                    + "una consulta directa necesita su propio scope explícito antes de agregarse"
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

        throw new InvalidOperationException("No se encontró la raíz backend (ERP.API / ERP.sln).");
    }
}
