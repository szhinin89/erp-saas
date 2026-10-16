using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SriAnnulmentPolicyVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sri_annulment_policy",
                schema: "global",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_annulment_policy", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sri_annulment_policy_version",
                schema: "global",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: true),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    day_of_following_month = table.Column<int>(type: "integer", nullable: true),
                    days_after_issue = table.Column<int>(type: "integer", nullable: true),
                    extend_to_next_business_day = table.Column<bool>(type: "boolean", nullable: false),
                    normative_source_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_annulment_policy_version", x => x.id);
                    table.CheckConstraint("ck_sri_annulment_policy_dates", "valid_from IS NULL OR valid_until IS NULL OR valid_from <= valid_until");
                    table.CheckConstraint("ck_sri_annulment_policy_deadline", "COALESCE((day_of_following_month BETWEEN 1 AND 28 AND days_after_issue IS NULL) OR (day_of_following_month IS NULL AND days_after_issue > 0), FALSE)");
                    table.ForeignKey(
                        name: "FK_sri_annulment_policy_version_sri_annulment_policy_policy_id",
                        column: x => x.policy_id,
                        principalSchema: "global",
                        principalTable: "sri_annulment_policy",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sri_annulment_policy_version_sri_normative_source_normative~",
                        column: x => x.normative_source_id,
                        principalSchema: "global",
                        principalTable: "sri_normative_source",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_annulment_policy",
                columns: new[] { "id", "code" },
                values: new object[] { new Guid("41000000-0000-0000-0000-000000000001"), "ELECTRONIC_DOCUMENT_ONLINE_ANNULMENT" });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_normative_source",
                columns: new[] { "id", "document", "document_sha256", "introduced_in_migration", "notes", "published_on", "reference_url", "section", "version" },
                values: new object[,]
                {
                    { new Guid("40000000-0000-0000-0000-000000000004"), "NAC-DGERCGC16-00000092", null, "SriAnnulmentPolicyVersioning", "Historia por fecha de emisión, hasta 2025-07-31; no se reinterpreta evidencia fiscal persistida.", new DateOnly(2016, 2, 22), "https://www.sri.gob.ec/o/sri-portlet-biblioteca-alfresco-internet/descargar/c97242e6-c271-4eb8-8f6a-f687313118ba/Guia%20para%20contribuyentes%20de%20anulaci%C3%B3n%20de%20comprobantes%20electr%C3%B3nicos.pdf", "Art. 7 — anulación en línea, 90 días", null },
                    { new Guid("40000000-0000-0000-0000-000000000005"), "NAC-DGERCGC25-00000017", null, "SriAnnulmentPolicyVersioning", "Firmada 2025-07-29. Día 7 del mes siguiente para emisión desde 2025-08-01; extensión por descanso obligatorio/feriado nacional o local según art. 3 reformado. No aplica excepción ISD al dominio IVA/Renta.", null, "https://www.sri.gob.ec/o/sri-portlet-biblioteca-alfresco-internet/descargar?id=e98fc8a6-299e-4ea9-8de7-2f6c70dbb4f5&nombre=NAC-DGERCGC25-00000017.pdf", "Art. 1 a), transitoria primera; reforma NAC-DGERCGC25-00000014 art. 3", null }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_annulment_policy_version",
                columns: new[] { "id", "day_of_following_month", "days_after_issue", "extend_to_next_business_day", "normative_source_id", "policy_id", "valid_from", "valid_until" },
                values: new object[,]
                {
                    { new Guid("42000000-0000-0000-0000-000000000001"), null, 90, false, new Guid("40000000-0000-0000-0000-000000000004"), new Guid("41000000-0000-0000-0000-000000000001"), new DateOnly(2016, 2, 22), new DateOnly(2025, 7, 31) },
                    { new Guid("42000000-0000-0000-0000-000000000002"), 7, null, true, new Guid("40000000-0000-0000-0000-000000000005"), new Guid("41000000-0000-0000-0000-000000000001"), new DateOnly(2025, 8, 1), null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_sri_annulment_policy_code",
                schema: "global",
                table: "sri_annulment_policy",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sri_annulment_policy_version_normative_source_id",
                schema: "global",
                table: "sri_annulment_policy_version",
                column: "normative_source_id");

            migrationBuilder.CreateIndex(
                name: "IX_sri_annulment_policy_version_policy_id_valid_from",
                schema: "global",
                table: "sri_annulment_policy_version",
                columns: new[] { "policy_id", "valid_from" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sri_annulment_policy_version",
                schema: "global");

            migrationBuilder.DropTable(
                name: "sri_annulment_policy",
                schema: "global");

            migrationBuilder.DeleteData(
                schema: "global",
                table: "sri_normative_source",
                keyColumn: "id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                schema: "global",
                table: "sri_normative_source",
                keyColumn: "id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000005"));
        }
    }
}
