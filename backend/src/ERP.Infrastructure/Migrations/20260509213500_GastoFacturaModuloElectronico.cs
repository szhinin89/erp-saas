using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GastoFacturaModuloElectronico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "monto",
                table: "gasto_facturas",
                newName: "subtotal");

            migrationBuilder.RenameColumn(
                name: "iva",
                table: "gasto_facturas",
                newName: "impuesto");

            migrationBuilder.RenameColumn(
                name: "fecha_factura",
                table: "gasto_facturas",
                newName: "fecha_emision");

            migrationBuilder.RenameColumn(
                name: "categoria",
                table: "gasto_facturas",
                newName: "categoria_gasto");

            migrationBuilder.AddColumn<DateTime>(
                name: "aprobado_en",
                table: "gasto_facturas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "aprobado_por",
                table: "gasto_facturas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "asiento_contable_id",
                table: "gasto_facturas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "clave_acceso",
                table: "gasto_facturas",
                type: "character varying(49)",
                maxLength: 49,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estado",
                table: "gasto_facturas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Borrador");

            migrationBuilder.AddColumn<string>(
                name: "motivo_rechazo",
                table: "gasto_facturas",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "rechazado_en",
                table: "gasto_facturas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "rechazado_por",
                table: "gasto_facturas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "validado_en",
                table: "gasto_facturas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "validado_por",
                table: "gasto_facturas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "xml_path",
                table: "gasto_facturas",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_gasto_facturas_tenant_clave_acceso",
                table: "gasto_facturas",
                columns: new[] { "tenant_id", "clave_acceso" },
                unique: true,
                filter: "clave_acceso IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_gasto_facturas_tenant_estado",
                table: "gasto_facturas",
                columns: new[] { "tenant_id", "estado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_gasto_facturas_tenant_clave_acceso",
                table: "gasto_facturas");

            migrationBuilder.DropIndex(
                name: "ix_gasto_facturas_tenant_estado",
                table: "gasto_facturas");

            migrationBuilder.DropColumn(
                name: "aprobado_en",
                table: "gasto_facturas");

            migrationBuilder.DropColumn(
                name: "aprobado_por",
                table: "gasto_facturas");

            migrationBuilder.DropColumn(
                name: "asiento_contable_id",
                table: "gasto_facturas");

            migrationBuilder.DropColumn(
                name: "clave_acceso",
                table: "gasto_facturas");

            migrationBuilder.DropColumn(
                name: "estado",
                table: "gasto_facturas");

            migrationBuilder.DropColumn(
                name: "motivo_rechazo",
                table: "gasto_facturas");

            migrationBuilder.DropColumn(
                name: "rechazado_en",
                table: "gasto_facturas");

            migrationBuilder.DropColumn(
                name: "rechazado_por",
                table: "gasto_facturas");

            migrationBuilder.DropColumn(
                name: "validado_en",
                table: "gasto_facturas");

            migrationBuilder.DropColumn(
                name: "validado_por",
                table: "gasto_facturas");

            migrationBuilder.DropColumn(
                name: "xml_path",
                table: "gasto_facturas");

            migrationBuilder.RenameColumn(
                name: "subtotal",
                table: "gasto_facturas",
                newName: "monto");

            migrationBuilder.RenameColumn(
                name: "impuesto",
                table: "gasto_facturas",
                newName: "iva");

            migrationBuilder.RenameColumn(
                name: "fecha_emision",
                table: "gasto_facturas",
                newName: "fecha_factura");

            migrationBuilder.RenameColumn(
                name: "categoria_gasto",
                table: "gasto_facturas",
                newName: "categoria");
        }
    }
}
