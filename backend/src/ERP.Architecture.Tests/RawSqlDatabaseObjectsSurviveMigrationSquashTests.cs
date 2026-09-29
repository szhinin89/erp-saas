using System.Text.RegularExpressions;
using FluentAssertions;

namespace ERP.Architecture.Tests;

/// <summary>
/// ZH-BP-IDENTIFICATION-UNIQUE-01 — objetos de BD que NO están en el modelo EF (raw SQL en una
/// migración) desaparecen en silencio cuando se consolida el historial en una nueva línea base:
/// la línea base se regenera desde el model snapshot, que no los conoce. <c>uq_mbp_identification</c>
/// se perdió así tres veces. Este test (sin Docker) falla en cuanto una consolidación los deja
/// fuera de ERP.Infrastructure/Migrations.
/// Al consolidar: copiar el raw SQL de cada objeto listado aquí a la nueva cadena de migraciones.
/// Cada objeto de BD creado con migrationBuilder.Sql debe registrarse aquí.
/// </summary>
public sealed partial class RawSqlDatabaseObjectsSurviveMigrationSquashTests
{
    /// <summary>
    /// Nombre del objeto → fragmentos que deben existir en alguna migración (se comparan con espacios
    /// normalizados). Varios fragmentos cuando la definición vigente importa, no solo el nombre.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> RequiredRawSqlObjects =
        new Dictionary<string, string[]>
        {
            // ADR-BP-03: identificación única e incondicional por tenant.
            ["uq_mbp_identification"] =
            [
                "CREATE UNIQUE INDEX IF NOT EXISTS uq_mbp_identification ON master_business_partners (tenant_id, identification_type, identification_number)",
            ],
            // Exclusividad Compra↔Gasto por AccessKey — versión vigente que ignora Cancelled
            // (ReceptionReprocessAfterCancelStandard); la original sin filtro de status bloqueaba
            // para siempre tras anular.
            ["enforce_purchase_expense_exclusivity"] =
            [
                "CREATE OR REPLACE FUNCTION enforce_purchase_expense_exclusivity() RETURNS trigger",
                "WHERE tenant_id = NEW.tenant_id AND access_key = NEW.access_key AND status <> 3",
                "WHERE tenant_id = NEW.tenant_id AND access_key = NEW.access_key AND status <> 2",
                "CONSTRAINT = 'uq_purchase_expense_access_key'",
            ],
            ["tr_expense_purchase_exclusivity"] =
            [
                "CREATE TRIGGER tr_expense_purchase_exclusivity BEFORE INSERT OR UPDATE OF tenant_id, access_key ON expense_documents FOR EACH ROW EXECUTE FUNCTION enforce_purchase_expense_exclusivity()",
            ],
            ["tr_purchase_expense_exclusivity"] =
            [
                "CREATE TRIGGER tr_purchase_expense_exclusivity BEFORE INSERT OR UPDATE OF tenant_id, access_key ON purchase_invoices FOR EACH ROW EXECUTE FUNCTION enforce_purchase_expense_exclusivity()",
            ],
        };

    [Fact]
    public void Objetos_raw_sql_requeridos_existen_en_la_cadena_de_migraciones()
    {
        var migrationsDir = Path.Combine(ResolveBackendSrcRoot(), "ERP.Infrastructure", "Migrations");
        Directory.Exists(migrationsDir).Should().BeTrue();

        var migrationsSql = string.Join(
            "\n",
            Directory
                .GetFiles(migrationsDir, "*.cs")
                .Where(f =>
                    !f.EndsWith(".Designer.cs", StringComparison.Ordinal)
                    && !f.EndsWith("ModelSnapshot.cs", StringComparison.Ordinal)
                )
                .Select(f => Normalize(File.ReadAllText(f)))
        );

        var missing = RequiredRawSqlObjects
            .SelectMany(kv =>
                kv.Value
                    .Where(fragment => !migrationsSql.Contains(Normalize(fragment), StringComparison.OrdinalIgnoreCase))
                    .Select(fragment => $"{kv.Key}: {fragment}")
            )
            .ToList();

        missing
            .Should()
            .BeEmpty(
                "estos objetos existen solo como raw SQL de migración; si una consolidación del "
                    + "historial los omitió, la BD pierde la restricción sin que ningún build lo note"
            );
    }

    private static string Normalize(string sql) => Whitespace().Replace(sql, " ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

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
