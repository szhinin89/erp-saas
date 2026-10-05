using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SriRetentionCatalogVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sri_normative_source",
                schema: "global",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document = table.Column<string>(
                        type: "character varying(60)",
                        maxLength: 60,
                        nullable: false
                    ),
                    version = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: true
                    ),
                    section = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: true
                    ),
                    published_on = table.Column<DateOnly>(type: "date", nullable: true),
                    reference_url = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: true
                    ),
                    document_sha256 = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: true
                    ),
                    introduced_in_migration = table.Column<string>(
                        type: "character varying(150)",
                        maxLength: 150,
                        nullable: false
                    ),
                    notes = table.Column<string>(
                        type: "character varying(1000)",
                        maxLength: 1000,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_normative_source", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "sri_retention_code_version",
                schema: "global",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    retention_code_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: true),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    percentage = table.Column<decimal>(
                        type: "numeric(7,4)",
                        precision: 7,
                        scale: 4,
                        nullable: true
                    ),
                    xml_code = table.Column<string>(
                        type: "character varying(5)",
                        maxLength: 5,
                        nullable: true
                    ),
                    ats_code = table.Column<string>(
                        type: "character varying(10)",
                        maxLength: 10,
                        nullable: true
                    ),
                    normative_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_retention_code_version", x => x.id);
                    table.ForeignKey(
                        name: "FK_sri_retention_code_version_sri_normative_source_normative_s~",
                        column: x => x.normative_source_id,
                        principalSchema: "global",
                        principalTable: "sri_normative_source",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_sri_retention_code_version_sri_retention_code_retention_cod~",
                        column: x => x.retention_code_id,
                        principalSchema: "global",
                        principalTable: "sri_retention_code",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_normative_source",
                columns: new[]
                {
                    "id",
                    "document",
                    "document_sha256",
                    "introduced_in_migration",
                    "notes",
                    "published_on",
                    "reference_url",
                    "section",
                    "version",
                },
                values: new object[,]
                {
                    {
                        new Guid("40000000-0000-0000-0000-000000000001"),
                        "FICHA_TECNICA_OFFLINE",
                        "7333aebfbdf2cb3ba83f9fc67a7a7f0346ca59506480a260cc42f96dbdfc13c9",
                        "SriRetentionCatalogVersioning",
                        "Porcentaje → codigoRetencion: 10%→9, 20%→10, 30%→1, 50%→11, 70%→2, 100%→3, en cero→7, no procede→8. La tabla no define 15% (concepto 728 sin representación). Sin fecha de inicio confirmada: las versiones se registran sin límite inferior (ValidFrom null).",
                        new DateOnly(2026, 7, 27),
                        "https://www.sri.gob.ec/o/sri-portlet-biblioteca-alfresco-internet/descargar/f8d9bb36-5632-4f96-b463-b9265b55338c/FICHA%20TE%cc%81CNICA%20COMPROBANTES%20ELECTRO%cc%81NICOS%20ESQUEMA%20OFFLINE%20Versio%cc%81n%202.34.pdf",
                        "§9.15 Tabla 20 — Retención del IVA (incluye retención en cero y no procede)",
                        "2.34",
                    },
                    {
                        new Guid("40000000-0000-0000-0000-000000000002"),
                        "CATALOGO_ATS",
                        null,
                        "SriRetentionCatalogVersioning",
                        "PENDIENTE DE VERIFICACIÓN (ADR-037 DR-4). El XML usa el mismo código del catálogo, como en el ejemplo oficial de la Ficha v2.34 Anexo 1 (codigoRetencion 323B1). Porcentajes no verificados: las versiones de Renta no fijan Percentage y no se exige coincidencia.",
                        null,
                        "http://www.sri.gob.ec/web/guest/formularios-e-instructivos1",
                        "Retención en la fuente de Impuesto a la Renta (referido por Ficha v2.34 §9.15)",
                        null,
                    },
                }
            );

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"),
                column: "is_active",
                value: false
            );

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_retention_code",
                columns: new[]
                {
                    "id",
                    "applies_to",
                    "code",
                    "is_active",
                    "name",
                    "percentage",
                    "tax_type",
                },
                values: new object[,]
                {
                    {
                        new Guid("10000000-0000-0000-0000-000000000007"),
                        "SUPPLIER",
                        "IVA-50",
                        true,
                        "Ret. IVA 50%",
                        50.00m,
                        "IVA",
                    },
                    {
                        new Guid("10000000-0000-0000-0000-000000000008"),
                        "SUPPLIER",
                        "IVA-0",
                        false,
                        "Ret. IVA 0% – Retención en cero",
                        0.00m,
                        "IVA",
                    },
                    {
                        new Guid("10000000-0000-0000-0000-000000000009"),
                        "SUPPLIER",
                        "IVA-NP",
                        false,
                        "No procede retención de IVA",
                        0.00m,
                        "IVA",
                    },
                }
            );

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_retention_code_version",
                columns: new[]
                {
                    "id",
                    "ats_code",
                    "normative_source_id",
                    "percentage",
                    "retention_code_id",
                    "valid_from",
                    "valid_until",
                    "xml_code",
                },
                values: new object[,]
                {
                    {
                        new Guid("41000000-0000-0000-0000-000000000001"),
                        null,
                        new Guid("40000000-0000-0000-0000-000000000001"),
                        10.00m,
                        new Guid("10000000-0000-0000-0000-000000000001"),
                        null,
                        null,
                        "9",
                    },
                    {
                        new Guid("41000000-0000-0000-0000-000000000002"),
                        null,
                        new Guid("40000000-0000-0000-0000-000000000001"),
                        20.00m,
                        new Guid("10000000-0000-0000-0000-000000000002"),
                        null,
                        null,
                        "10",
                    },
                    {
                        new Guid("41000000-0000-0000-0000-000000000003"),
                        null,
                        new Guid("40000000-0000-0000-0000-000000000001"),
                        30.00m,
                        new Guid("10000000-0000-0000-0000-000000000003"),
                        null,
                        null,
                        "1",
                    },
                    {
                        new Guid("41000000-0000-0000-0000-000000000004"),
                        null,
                        new Guid("40000000-0000-0000-0000-000000000001"),
                        70.00m,
                        new Guid("10000000-0000-0000-0000-000000000004"),
                        null,
                        null,
                        "2",
                    },
                    {
                        new Guid("41000000-0000-0000-0000-000000000005"),
                        null,
                        new Guid("40000000-0000-0000-0000-000000000001"),
                        100.00m,
                        new Guid("10000000-0000-0000-0000-000000000005"),
                        null,
                        null,
                        "3",
                    },
                    {
                        new Guid("41000000-0000-0000-0000-000000000006"),
                        null,
                        new Guid("40000000-0000-0000-0000-000000000001"),
                        15.00m,
                        new Guid("10000000-0000-0000-0000-000000000006"),
                        null,
                        null,
                        null,
                    },
                    {
                        new Guid("41000000-0000-0000-0000-000000000007"),
                        null,
                        new Guid("40000000-0000-0000-0000-000000000001"),
                        50.00m,
                        new Guid("10000000-0000-0000-0000-000000000007"),
                        null,
                        null,
                        "11",
                    },
                    {
                        new Guid("41000000-0000-0000-0000-000000000008"),
                        null,
                        new Guid("40000000-0000-0000-0000-000000000001"),
                        0.00m,
                        new Guid("10000000-0000-0000-0000-000000000008"),
                        null,
                        null,
                        "7",
                    },
                    {
                        new Guid("41000000-0000-0000-0000-000000000009"),
                        null,
                        new Guid("40000000-0000-0000-0000-000000000001"),
                        0.00m,
                        new Guid("10000000-0000-0000-0000-000000000009"),
                        null,
                        null,
                        "8",
                    },
                    {
                        new Guid("42000000-0000-0000-0000-000000000001"),
                        null,
                        new Guid("40000000-0000-0000-0000-000000000002"),
                        null,
                        new Guid("20000000-0000-0000-0000-000000000001"),
                        null,
                        null,
                        "303",
                    },
                    {
                        new Guid("42000000-0000-0000-0000-000000000002"),
                        null,
                        new Guid("40000000-0000-0000-0000-000000000002"),
                        null,
                        new Guid("20000000-0000-0000-0000-000000000002"),
                        null,
                        null,
                        "304",
                    },
                    {
                        new Guid("42000000-0000-0000-0000-000000000003"),
                        null,
                        new Guid("40000000-0000-0000-0000-000000000002"),
                        null,
                        new Guid("20000000-0000-0000-0000-000000000003"),
                        null,
                        null,
                        "307",
                    },
                    {
                        new Guid("42000000-0000-0000-0000-000000000004"),
                        null,
                        new Guid("40000000-0000-0000-0000-000000000002"),
                        null,
                        new Guid("20000000-0000-0000-0000-000000000004"),
                        null,
                        null,
                        "309",
                    },
                    {
                        new Guid("42000000-0000-0000-0000-000000000005"),
                        null,
                        new Guid("40000000-0000-0000-0000-000000000002"),
                        null,
                        new Guid("20000000-0000-0000-0000-000000000005"),
                        null,
                        null,
                        "310",
                    },
                    {
                        new Guid("42000000-0000-0000-0000-000000000006"),
                        null,
                        new Guid("40000000-0000-0000-0000-000000000002"),
                        null,
                        new Guid("20000000-0000-0000-0000-000000000006"),
                        null,
                        null,
                        "312",
                    },
                    {
                        new Guid("42000000-0000-0000-0000-000000000007"),
                        null,
                        new Guid("40000000-0000-0000-0000-000000000002"),
                        null,
                        new Guid("20000000-0000-0000-0000-000000000007"),
                        null,
                        null,
                        "320",
                    },
                    {
                        new Guid("42000000-0000-0000-0000-000000000008"),
                        null,
                        new Guid("40000000-0000-0000-0000-000000000002"),
                        null,
                        new Guid("20000000-0000-0000-0000-000000000008"),
                        null,
                        null,
                        "325",
                    },
                    {
                        new Guid("42000000-0000-0000-0000-000000000009"),
                        null,
                        new Guid("40000000-0000-0000-0000-000000000002"),
                        null,
                        new Guid("20000000-0000-0000-0000-000000000009"),
                        null,
                        null,
                        "327",
                    },
                    {
                        new Guid("42000000-0000-0000-0000-000000000010"),
                        null,
                        new Guid("40000000-0000-0000-0000-000000000002"),
                        null,
                        new Guid("20000000-0000-0000-0000-000000000010"),
                        null,
                        null,
                        "341",
                    },
                    {
                        new Guid("42000000-0000-0000-0000-000000000011"),
                        null,
                        new Guid("40000000-0000-0000-0000-000000000002"),
                        null,
                        new Guid("20000000-0000-0000-0000-000000000011"),
                        null,
                        null,
                        "342",
                    },
                    {
                        new Guid("42000000-0000-0000-0000-000000000012"),
                        null,
                        new Guid("40000000-0000-0000-0000-000000000002"),
                        null,
                        new Guid("20000000-0000-0000-0000-000000000012"),
                        null,
                        null,
                        "343",
                    },
                    {
                        new Guid("42000000-0000-0000-0000-000000000013"),
                        null,
                        new Guid("40000000-0000-0000-0000-000000000002"),
                        null,
                        new Guid("20000000-0000-0000-0000-000000000013"),
                        null,
                        null,
                        "344",
                    },
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_sri_retention_code_version_normative_source_id",
                schema: "global",
                table: "sri_retention_code_version",
                column: "normative_source_id"
            );

            migrationBuilder
                .CreateIndex(
                    name: "uq_sri_retention_code_version_start",
                    schema: "global",
                    table: "sri_retention_code_version",
                    columns: new[] { "retention_code_id", "valid_from" },
                    unique: true
                )
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "sri_retention_code_version", schema: "global");

            migrationBuilder.DropTable(name: "sri_normative_source", schema: "global");

            migrationBuilder.DeleteData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000007")
            );

            migrationBuilder.DeleteData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008")
            );

            migrationBuilder.DeleteData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000009")
            );

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"),
                column: "is_active",
                value: true
            );
        }
    }
}
