using System.IO;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations;

/// <summary>
/// Carga la División Político Administrativa (DPA) de Ecuador en <c>geo_*</c>.
/// Origen: INEC — capas ArcGIS publicadas por Ecuador en Cifras (metadatos del servicio).
/// </summary>
public partial class SeedInecEcuadorGeography : Migration
{
    internal const string InecSqlResourceName = "ERP.Infrastructure.Seeding.Scripts.inec_ecuador_geography.sql";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var assembly = typeof(SeedInecEcuadorGeography).Assembly;
        using var stream = assembly.GetManifestResourceStream(InecSqlResourceName)
            ?? throw new InvalidOperationException(
                "Recurso embebido INEC no encontrado: " + InecSqlResourceName + ". Disponibles: "
                + string.Join(", ", assembly.GetManifestResourceNames()));

        using var reader = new StreamReader(stream);
        migrationBuilder.Sql(reader.ReadToEnd());
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Datos de referencia INEC: no se revierte automáticamente (posibles FKs desde branches).
    }
}
