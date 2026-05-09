using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddComprasFullSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "aprobado_en",
                table: "compra_facturas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "aprobado_por",
                table: "compra_facturas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "asiento_contable_id",
                table: "compra_facturas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "clave_acceso",
                table: "compra_facturas",
                type: "character varying(49)",
                maxLength: 49,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_rechazo",
                table: "compra_facturas",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "rechazado_en",
                table: "compra_facturas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "rechazado_por",
                table: "compra_facturas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "validado_en",
                table: "compra_facturas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "validado_por",
                table: "compra_facturas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "xml_path",
                table: "compra_facturas",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "producto_id",
                table: "compra_detalles",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "codigo_principal_proveedor",
                table: "compra_detalles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "descripcion",
                table: "compra_detalles",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_compra_facturas_tenant_clave_acceso",
                table: "compra_facturas",
                columns: new[] { "tenant_id", "clave_acceso" },
                unique: true,
                filter: "clave_acceso IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_compra_facturas_tenant_clave_acceso",
                table: "compra_facturas");

            migrationBuilder.DropColumn(
                name: "aprobado_en",
                table: "compra_facturas");

            migrationBuilder.DropColumn(
                name: "aprobado_por",
                table: "compra_facturas");

            migrationBuilder.DropColumn(
                name: "asiento_contable_id",
                table: "compra_facturas");

            migrationBuilder.DropColumn(
                name: "clave_acceso",
                table: "compra_facturas");

            migrationBuilder.DropColumn(
                name: "motivo_rechazo",
                table: "compra_facturas");

            migrationBuilder.DropColumn(
                name: "rechazado_en",
                table: "compra_facturas");

            migrationBuilder.DropColumn(
                name: "rechazado_por",
                table: "compra_facturas");

            migrationBuilder.DropColumn(
                name: "validado_en",
                table: "compra_facturas");

            migrationBuilder.DropColumn(
                name: "validado_por",
                table: "compra_facturas");

            migrationBuilder.DropColumn(
                name: "xml_path",
                table: "compra_facturas");

            migrationBuilder.DropColumn(
                name: "codigo_principal_proveedor",
                table: "compra_detalles");

            migrationBuilder.DropColumn(
                name: "descripcion",
                table: "compra_detalles");

            migrationBuilder.AlterColumn<Guid>(
                name: "producto_id",
                table: "compra_detalles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
