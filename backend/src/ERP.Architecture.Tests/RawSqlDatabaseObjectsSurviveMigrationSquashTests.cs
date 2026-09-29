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
/// Todo objeto de BD creado con migrationBuilder.Sql debe registrarse aquí.
/// </summary>
public sealed partial class RawSqlDatabaseObjectsSurviveMigrationSquashTests
{
    /// <summary>Nombre del objeto → sentencia que debe existir (se compara con espacios normalizados).</summary>
    private static readonly IReadOnlyDictionary<string, string> RequiredRawSqlObjects =
        new Dictionary<string, string>
        {
            // ADR-BP-03: identificación única e incondicional por tenant.
            ["uq_mbp_identification"] =
                "CREATE UNIQUE INDEX IF NOT EXISTS uq_mbp_identification ON master_business_partners (tenant_id, identification_type, identification_number)",
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
            .Where(kv => !migrationsSql.Contains(Normalize(kv.Value), StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Key)
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
