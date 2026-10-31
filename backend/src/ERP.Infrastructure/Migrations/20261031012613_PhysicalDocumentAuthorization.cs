using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PhysicalDocumentAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "physical_document_authorization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emission_point_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_type_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    authorization_number = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    authorized_on = table.Column<DateOnly>(type: "date", nullable: false),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: false),
                    first_sequential = table.Column<int>(type: "integer", nullable: false),
                    last_sequential = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_physical_document_authorization", x => x.id);
                    table.CheckConstraint("chk_phys_doc_auth_dates", "expires_on >= authorized_on");
                    table.CheckConstraint("chk_phys_doc_auth_range", "first_sequential >= 1 AND last_sequential >= first_sequential AND last_sequential <= 999999999");
                    table.ForeignKey(
                        name: "FK_physical_document_authorization_emission_point_emission_poi~",
                        column: x => x.emission_point_id,
                        principalTable: "emission_point",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_physical_document_authorization_sri_doc_type_doc_type_code",
                        column: x => x.doc_type_code,
                        principalSchema: "global",
                        principalTable: "sri_doc_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sri_tax_regime_physical_doc_type",
                schema: "global",
                columns: table => new
                {
                    tax_regime_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    doc_type_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    legal_basis = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_tax_regime_physical_doc_type", x => new { x.tax_regime_code, x.doc_type_code });
                    table.ForeignKey(
                        name: "FK_sri_tax_regime_physical_doc_type_sri_doc_type_doc_type_code",
                        column: x => x.doc_type_code,
                        principalSchema: "global",
                        principalTable: "sri_doc_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sri_tax_regime_physical_doc_type_sri_tax_regime_tax_regime_~",
                        column: x => x.tax_regime_code,
                        principalSchema: "global",
                        principalTable: "sri_tax_regime",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_tax_regime_physical_doc_type",
                columns: new[] { "doc_type_code", "tax_regime_code", "is_active", "legal_basis" },
                values: new object[,]
                {
                    { "01", "01", false, "BLOQUEADO: Res. NAC-DGERCGC22-00000024, preimpresos solo por fuerza mayor o caso fortuito (no verificable)" },
                    { "02", "03", true, "Res. NAC-DGERCGC21-00000060 (RIMPE Negocio Popular: notas de venta)" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_phys_doc_auth_point_doc_type",
                table: "physical_document_authorization",
                columns: new[] { "tenant_id", "company_id", "emission_point_id", "doc_type_code" });

            migrationBuilder.CreateIndex(
                name: "IX_physical_document_authorization_doc_type_code",
                table: "physical_document_authorization",
                column: "doc_type_code");

            migrationBuilder.CreateIndex(
                name: "IX_physical_document_authorization_emission_point_id",
                table: "physical_document_authorization",
                column: "emission_point_id");

            migrationBuilder.CreateIndex(
                name: "IX_sri_tax_regime_physical_doc_type_doc_type_code",
                schema: "global",
                table: "sri_tax_regime_physical_doc_type",
                column: "doc_type_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "physical_document_authorization");

            migrationBuilder.DropTable(
                name: "sri_tax_regime_physical_doc_type",
                schema: "global");
        }
    }
}
