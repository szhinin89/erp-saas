using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SriRetentionIncomeCatalogAts20260806 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "rate_kind",
                schema: "global",
                table: "sri_retention_code_version",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "rate_rule_text",
                schema: "global",
                table: "sri_retention_code_version",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_normative_source",
                columns: new[] { "id", "document", "document_sha256", "introduced_in_migration", "notes", "published_on", "reference_url", "section", "version" },
                values: new object[] { new Guid("40000000-0000-0000-0000-000000000003"), "CATALOGO_ATS", "bd3f7834f2cd31187af39cd2f4c685a646d7e49316e92ee493da5f2dd9776e3e", "SriRetentionIncomeCatalogAts20260806", "Descargado el 2026-10-02 desde https://www.sri.gob.ec/formularios-e-instructivos1. Fecha de actualización = inicio del bloque (06/08/2026). Tarifas no numéricas (\"12 o 14\", \"1 /0 según resolución…\") se registran como regla condicional, nunca como porcentaje.", new DateOnly(2026, 8, 6), "https://www.sri.gob.ec/o/sri-portlet-biblioteca-alfresco-internet/descargar/e6a826af-b22c-40bb-8752-d711f293b8f9/Catalogo_ATS.xls", "Hoja TABLAS RETENCIONES — Tabla 3.10 Conceptos de retención en la fuente de IR (AIR), bloque DESDE 06/AGOSTO/2026", "2026-08-06" });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"),
                column: "name",
                value: "Honorarios profesionales y demás pagos por servicios relacionados con el título profesional");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"),
                columns: new[] { "name", "percentage" },
                values: new object[] { "Servicios predomina el intelecto no relacionados con el título profesional", 10.00m });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"),
                columns: new[] { "name", "percentage" },
                values: new object[] { "Servicios predomina la mano de obra", 3.00m });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"),
                columns: new[] { "name", "percentage" },
                values: new object[] { "Servicios prestados por medios de comunicación y agencias de publicidad", 3.00m });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000005"),
                columns: new[] { "is_active", "name" },
                values: new object[] { false, "Servicio de transporte privado de pasajeros o transporte público o privado de carga" });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000006"),
                columns: new[] { "name", "percentage" },
                values: new object[] { "Transferencia de bienes muebles de naturaleza corporal", 2.00m });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000007"),
                columns: new[] { "name", "percentage" },
                values: new object[] { "Arrendamiento bienes inmuebles", 10.00m });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000008"),
                columns: new[] { "name", "percentage" },
                values: new object[] { "Anticipo dividendos", 25.00m });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000009"),
                columns: new[] { "is_active", "name" },
                values: new object[] { false, "Dividendos distribuidos a personas naturales residentes" });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000010"),
                column: "is_active",
                value: false);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000011"),
                column: "is_active",
                value: false);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000012"),
                columns: new[] { "name", "percentage" },
                values: new object[] { "Otras retenciones aplicables el 1% (incluye régimen RIMPE - Emprendedores, para este caso aplica con cualquier forma de pago inclusive los pagos que deban realizar las tarjetas de crédito/débito)", 1.00m });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000013"),
                column: "is_active",
                value: false);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("41000000-0000-0000-0000-000000000001"),
                columns: new[] { "rate_kind", "rate_rule_text" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("41000000-0000-0000-0000-000000000002"),
                columns: new[] { "rate_kind", "rate_rule_text" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("41000000-0000-0000-0000-000000000003"),
                columns: new[] { "rate_kind", "rate_rule_text" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("41000000-0000-0000-0000-000000000004"),
                columns: new[] { "rate_kind", "rate_rule_text" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("41000000-0000-0000-0000-000000000005"),
                columns: new[] { "rate_kind", "rate_rule_text" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("41000000-0000-0000-0000-000000000006"),
                columns: new[] { "rate_kind", "rate_rule_text" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("41000000-0000-0000-0000-000000000007"),
                columns: new[] { "rate_kind", "rate_rule_text" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("41000000-0000-0000-0000-000000000008"),
                columns: new[] { "rate_kind", "rate_rule_text" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("41000000-0000-0000-0000-000000000009"),
                columns: new[] { "rate_kind", "rate_rule_text" },
                values: new object[] { 1, null });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000001"),
                columns: new[] { "rate_kind", "rate_rule_text", "valid_until" },
                values: new object[] { 1, null, new DateOnly(2026, 8, 5) });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000002"),
                columns: new[] { "rate_kind", "rate_rule_text", "valid_until" },
                values: new object[] { 1, null, new DateOnly(2026, 8, 5) });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000003"),
                columns: new[] { "rate_kind", "rate_rule_text", "valid_until" },
                values: new object[] { 1, null, new DateOnly(2026, 8, 5) });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000004"),
                columns: new[] { "rate_kind", "rate_rule_text", "valid_until" },
                values: new object[] { 1, null, new DateOnly(2026, 8, 5) });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000005"),
                columns: new[] { "rate_kind", "rate_rule_text", "valid_until" },
                values: new object[] { 1, null, new DateOnly(2026, 8, 5) });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000006"),
                columns: new[] { "rate_kind", "rate_rule_text", "valid_until" },
                values: new object[] { 1, null, new DateOnly(2026, 8, 5) });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000007"),
                columns: new[] { "rate_kind", "rate_rule_text", "valid_until" },
                values: new object[] { 1, null, new DateOnly(2026, 8, 5) });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000008"),
                columns: new[] { "rate_kind", "rate_rule_text", "valid_until" },
                values: new object[] { 1, null, new DateOnly(2026, 8, 5) });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000009"),
                columns: new[] { "rate_kind", "rate_rule_text", "valid_until" },
                values: new object[] { 1, null, new DateOnly(2026, 8, 5) });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000010"),
                columns: new[] { "rate_kind", "rate_rule_text", "valid_until" },
                values: new object[] { 1, null, new DateOnly(2026, 8, 5) });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000011"),
                columns: new[] { "rate_kind", "rate_rule_text", "valid_until" },
                values: new object[] { 1, null, new DateOnly(2026, 8, 5) });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000012"),
                columns: new[] { "rate_kind", "rate_rule_text", "valid_until" },
                values: new object[] { 1, null, new DateOnly(2026, 8, 5) });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000013"),
                columns: new[] { "rate_kind", "rate_rule_text", "valid_until" },
                values: new object[] { 1, null, new DateOnly(2026, 8, 5) });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_retention_code_version",
                columns: new[] { "id", "ats_code", "normative_source_id", "percentage", "rate_kind", "rate_rule_text", "retention_code_id", "valid_from", "valid_until", "xml_code" },
                values: new object[,]
                {
                    { new Guid("43000000-0000-0000-0000-000000000001"), "303", new Guid("40000000-0000-0000-0000-000000000003"), 10.00m, 1, null, new Guid("20000000-0000-0000-0000-000000000001"), new DateOnly(2026, 8, 6), null, "303" },
                    { new Guid("43000000-0000-0000-0000-000000000002"), "304", new Guid("40000000-0000-0000-0000-000000000003"), 10.00m, 1, null, new Guid("20000000-0000-0000-0000-000000000002"), new DateOnly(2026, 8, 6), null, "304" },
                    { new Guid("43000000-0000-0000-0000-000000000003"), "307", new Guid("40000000-0000-0000-0000-000000000003"), 3.00m, 1, null, new Guid("20000000-0000-0000-0000-000000000003"), new DateOnly(2026, 8, 6), null, "307" },
                    { new Guid("43000000-0000-0000-0000-000000000004"), "309", new Guid("40000000-0000-0000-0000-000000000003"), 3.00m, 1, null, new Guid("20000000-0000-0000-0000-000000000004"), new DateOnly(2026, 8, 6), null, "309" },
                    { new Guid("43000000-0000-0000-0000-000000000005"), "310", new Guid("40000000-0000-0000-0000-000000000003"), null, 2, "1 /0 según resolución NAC-DGERCGC26-00000028", new Guid("20000000-0000-0000-0000-000000000005"), new DateOnly(2026, 8, 6), null, "310" },
                    { new Guid("43000000-0000-0000-0000-000000000006"), "312", new Guid("40000000-0000-0000-0000-000000000003"), 2.00m, 1, null, new Guid("20000000-0000-0000-0000-000000000006"), new DateOnly(2026, 8, 6), null, "312" },
                    { new Guid("43000000-0000-0000-0000-000000000007"), "320", new Guid("40000000-0000-0000-0000-000000000003"), 10.00m, 1, null, new Guid("20000000-0000-0000-0000-000000000007"), new DateOnly(2026, 8, 6), null, "320" },
                    { new Guid("43000000-0000-0000-0000-000000000008"), "325", new Guid("40000000-0000-0000-0000-000000000003"), 25.00m, 1, null, new Guid("20000000-0000-0000-0000-000000000008"), new DateOnly(2026, 8, 6), null, "325" },
                    { new Guid("43000000-0000-0000-0000-000000000009"), "327", new Guid("40000000-0000-0000-0000-000000000003"), null, 2, "12 o 14", new Guid("20000000-0000-0000-0000-000000000009"), new DateOnly(2026, 8, 6), null, "327" },
                    { new Guid("43000000-0000-0000-0000-000000000012"), "343", new Guid("40000000-0000-0000-0000-000000000003"), 1.00m, 1, null, new Guid("20000000-0000-0000-0000-000000000012"), new DateOnly(2026, 8, 6), null, "343" }
                });

            // ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01 — decisión explícita del propietario: los defaults de
            // proveedor que apuntan a un concepto de Renta cuyo significado/descripción cambió al alinearse con
            // el Catálogo ATS (304, 307, 309, 310, 320, 325, 327, 343) o que ya no existe en el bloque vigente
            // (341, 342, 344) NO siguen aplicándose en silencio: quedan deshabilitados hasta que el usuario los
            // valide y reactive explícitamente. Nunca se remapean a otro código. 303 y 312 conservan su
            // significado y no se tocan. Documentos y snapshots no se modifican. updated_by = Guid.Empty
            // (actor de sistema, mismo criterio que los jobs).
            migrationBuilder.Sql(
                """
                UPDATE master_supplier_retention_defaults
                SET is_active = false,
                    updated_at = now(),
                    updated_by = '00000000-0000-0000-0000-000000000000'
                WHERE is_active
                  AND sri_retention_code_id IN (
                    '20000000-0000-0000-0000-000000000002', -- 304
                    '20000000-0000-0000-0000-000000000003', -- 307
                    '20000000-0000-0000-0000-000000000004', -- 309
                    '20000000-0000-0000-0000-000000000005', -- 310
                    '20000000-0000-0000-0000-000000000007', -- 320
                    '20000000-0000-0000-0000-000000000008', -- 325
                    '20000000-0000-0000-0000-000000000009', -- 327
                    '20000000-0000-0000-0000-000000000010', -- 341
                    '20000000-0000-0000-0000-000000000011', -- 342
                    '20000000-0000-0000-0000-000000000012', -- 343
                    '20000000-0000-0000-0000-000000000013'  -- 344
                  );
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // La deshabilitación de defaults de proveedor del Up NO se revierte: no se conoce cuáles estaban
            // activos antes y reactivarlos en bloque aplicaría significados no validados (fail-closed).

            migrationBuilder.DeleteData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("43000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("43000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("43000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("43000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("43000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("43000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("43000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("43000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("43000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("43000000-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                schema: "global",
                table: "sri_normative_source",
                keyColumn: "id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000003"));

            migrationBuilder.DropColumn(
                name: "rate_kind",
                schema: "global",
                table: "sri_retention_code_version");

            migrationBuilder.DropColumn(
                name: "rate_rule_text",
                schema: "global",
                table: "sri_retention_code_version");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"),
                column: "name",
                value: "Honorarios profesionales y demás servicios");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"),
                columns: new[] { "name", "percentage" },
                values: new object[] { "Servicios – predomina mano de obra", 2.00m });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"),
                columns: new[] { "name", "percentage" },
                values: new object[] { "Publicidad y comunicación", 1.75m });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"),
                columns: new[] { "name", "percentage" },
                values: new object[] { "Arrendamiento bienes inmuebles (persona natural)", 8.00m });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000005"),
                columns: new[] { "is_active", "name" },
                values: new object[] { true, "Seguros y reaseguros (10% de primas)" });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000006"),
                columns: new[] { "name", "percentage" },
                values: new object[] { "Transf. bienes muebles de naturaleza corporal", 1.00m });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000007"),
                columns: new[] { "name", "percentage" },
                values: new object[] { "Servicios entre sociedades", 2.75m });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000008"),
                columns: new[] { "name", "percentage" },
                values: new object[] { "Compra bienes corporales muebles", 1.75m });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000009"),
                columns: new[] { "is_active", "name" },
                values: new object[] { true, "Actividades de construcción (contrato)" });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000010"),
                column: "is_active",
                value: true);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000011"),
                column: "is_active",
                value: true);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000012"),
                columns: new[] { "name", "percentage" },
                values: new object[] { "Otras retenciones aplicables al 1.75%", 1.75m });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000013"),
                column: "is_active",
                value: true);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000001"),
                column: "valid_until",
                value: null);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000002"),
                column: "valid_until",
                value: null);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000003"),
                column: "valid_until",
                value: null);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000004"),
                column: "valid_until",
                value: null);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000005"),
                column: "valid_until",
                value: null);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000006"),
                column: "valid_until",
                value: null);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000007"),
                column: "valid_until",
                value: null);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000008"),
                column: "valid_until",
                value: null);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000009"),
                column: "valid_until",
                value: null);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000010"),
                column: "valid_until",
                value: null);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000011"),
                column: "valid_until",
                value: null);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000012"),
                column: "valid_until",
                value: null);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code_version",
                keyColumn: "id",
                keyValue: new Guid("42000000-0000-0000-0000-000000000013"),
                column: "valid_until",
                value: null);
        }
    }
}
