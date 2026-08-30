using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocTypeSsot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "doc_type",
                schema: "global",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doc_type", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "doc_type_sri_map",
                schema: "global",
                columns: table => new
                {
                    doc_type_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    sri_doc_type_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doc_type_sri_map", x => x.doc_type_code);
                    table.ForeignKey(
                        name: "FK_doc_type_sri_map_doc_type_doc_type_code",
                        column: x => x.doc_type_code,
                        principalSchema: "global",
                        principalTable: "doc_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_doc_type_sri_map_sri_doc_type_sri_doc_type_code",
                        column: x => x.sri_doc_type_code,
                        principalSchema: "global",
                        principalTable: "sri_doc_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "doc_workflow_policy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_type_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    draft_mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    default_action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doc_workflow_policy", x => x.id);
                    table.ForeignKey(
                        name: "FK_doc_workflow_policy_doc_type_doc_type_code",
                        column: x => x.doc_type_code,
                        principalSchema: "global",
                        principalTable: "doc_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "doc_type",
                columns: new[] { "code", "is_active", "name" },
                values: new object[,]
                {
                    { "AJUINV", true, "Ajuste de Inventario" },
                    { "ASI", true, "Asiento Contable Manual" },
                    { "COBCLI", true, "Cobro a Cliente" },
                    { "FACCOM", true, "Factura de Compra" },
                    { "FACVEN", true, "Factura de Venta" },
                    { "GASDOC", true, "Documento de Gasto" },
                    { "NCCDEV", true, "Nota de Crédito de Compra" },
                    { "NCVDEV", true, "Nota de Crédito de Venta" },
                    { "PAGPRO", true, "Pago a Proveedor" },
                    { "RETGAS", true, "Retención en Gasto" }
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "doc_type_sri_map",
                columns: new[] { "doc_type_code", "sri_doc_type_code" },
                values: new object[,]
                {
                    { "FACVEN", "01" },
                    { "NCCDEV", "04" },
                    { "NCVDEV", "04" },
                    { "RETGAS", "07" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_doc_type_sri_map_sri_doc_type_code",
                schema: "global",
                table: "doc_type_sri_map",
                column: "sri_doc_type_code");

            migrationBuilder.CreateIndex(
                name: "IX_doc_workflow_policy_doc_type_code",
                table: "doc_workflow_policy",
                column: "doc_type_code");

            migrationBuilder.CreateIndex(
                name: "uq_doc_workflow_policy_company_doc_type",
                table: "doc_workflow_policy",
                columns: new[] { "tenant_id", "company_id", "doc_type_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "doc_type_sri_map",
                schema: "global");

            migrationBuilder.DropTable(
                name: "doc_workflow_policy");

            migrationBuilder.DropTable(
                name: "doc_type",
                schema: "global");
        }
    }
}
