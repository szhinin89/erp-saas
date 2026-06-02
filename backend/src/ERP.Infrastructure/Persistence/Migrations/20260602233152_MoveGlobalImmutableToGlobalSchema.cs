using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveGlobalImmutableToGlobalSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "global");

            migrationBuilder.RenameTable(
                name: "sri_vat_rate",
                newName: "sri_vat_rate",
                newSchema: "global");

            migrationBuilder.RenameTable(
                name: "sri_uom",
                newName: "sri_uom",
                newSchema: "global");

            migrationBuilder.RenameTable(
                name: "sri_tax_support",
                newName: "sri_tax_support",
                newSchema: "global");

            migrationBuilder.RenameTable(
                name: "sri_tax_regime",
                newName: "sri_tax_regime",
                newSchema: "global");

            migrationBuilder.RenameTable(
                name: "sri_retention_code",
                newName: "sri_retention_code",
                newSchema: "global");

            migrationBuilder.RenameTable(
                name: "sri_payment_method",
                newName: "sri_payment_method",
                newSchema: "global");

            migrationBuilder.RenameTable(
                name: "sri_id_type",
                newName: "sri_id_type",
                newSchema: "global");

            migrationBuilder.RenameTable(
                name: "sri_ice_rate",
                newName: "sri_ice_rate",
                newSchema: "global");

            migrationBuilder.RenameTable(
                name: "sri_error_code",
                newName: "sri_error_code",
                newSchema: "global");

            migrationBuilder.RenameTable(
                name: "sri_environment",
                newName: "sri_environment",
                newSchema: "global");

            migrationBuilder.RenameTable(
                name: "sri_emission_type",
                newName: "sri_emission_type",
                newSchema: "global");

            migrationBuilder.RenameTable(
                name: "sri_doc_type",
                newName: "sri_doc_type",
                newSchema: "global");

            migrationBuilder.RenameTable(
                name: "sri_country",
                newName: "sri_country",
                newSchema: "global");

            migrationBuilder.RenameTable(
                name: "geo_provinces",
                newName: "geo_provinces",
                newSchema: "global");

            migrationBuilder.RenameTable(
                name: "geo_parishes",
                newName: "geo_parishes",
                newSchema: "global");

            migrationBuilder.RenameTable(
                name: "geo_cantons",
                newName: "geo_cantons",
                newSchema: "global");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_country",
                keyColumn: "code",
                keyValue: "CAN",
                column: "name",
                value: "CANADÃ");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_country",
                keyColumn: "code",
                keyValue: "DOM",
                column: "name",
                value: "REPÃšBLICA DOMINICANA");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_country",
                keyColumn: "code",
                keyValue: "ESP",
                column: "name",
                value: "ESPAÃ‘A");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_country",
                keyColumn: "code",
                keyValue: "JPN",
                column: "name",
                value: "JAPÃ“N");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_country",
                keyColumn: "code",
                keyValue: "MEX",
                column: "name",
                value: "MÃ‰XICO");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_country",
                keyColumn: "code",
                keyValue: "PAN",
                column: "name",
                value: "PANAMÃ");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_country",
                keyColumn: "code",
                keyValue: "PER",
                column: "name",
                value: "PERÃš");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "03",
                column: "name",
                value: "LiquidaciÃ³n de Compra de Bienes y PrestaciÃ³n de Servicios");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "04",
                column: "name",
                value: "Nota de CrÃ©dito");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "05",
                column: "name",
                value: "Nota de DÃ©bito");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "06",
                column: "name",
                value: "GuÃ­a de RemisiÃ³n");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "07",
                column: "name",
                value: "Comprobante de RetenciÃ³n");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "08",
                column: "name",
                value: "Tiquete de MÃ¡quina Registradora");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "18",
                column: "name",
                value: "Documento ElectrÃ³nico de ImportaciÃ³n");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_emission_type",
                keyColumn: "code",
                keyValue: (short)1,
                column: "name",
                value: "EmisiÃ³n Normal");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_emission_type",
                keyColumn: "code",
                keyValue: (short)2,
                column: "name",
                value: "EmisiÃ³n por Indisponibilidad del Sistema");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_environment",
                keyColumn: "code",
                keyValue: (short)1,
                column: "name",
                value: "ProducciÃ³n");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_error_code",
                keyColumn: "code",
                keyValue: "60",
                column: "name",
                value: "FIRMA INVÃLIDA");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_error_code",
                keyColumn: "code",
                keyValue: "65",
                column: "name",
                value: "AMBIENTE NO VÃLIDO");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_error_code",
                keyColumn: "code",
                keyValue: "72",
                column: "name",
                value: "NÃšMERO DE COMPROBANTE YA EXISTE");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_error_code",
                keyColumn: "code",
                keyValue: "73",
                column: "name",
                value: "CLAVE DE ACCESO INVÃLIDA");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_error_code",
                keyColumn: "code",
                keyValue: "90",
                column: "name",
                value: "CERTIFICADO INVÃLIDO O CADUCADO");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3041",
                column: "name",
                value: "Bebidas gaseosas con azÃºcar aÃ±adida");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3081",
                column: "name",
                value: "VehÃ­culos â‰¤3.5t (hasta USD 30k)");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3082",
                column: "name",
                value: "VehÃ­culos â‰¤3.5t (USD 30kâ€“40k)");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3083",
                column: "name",
                value: "VehÃ­culos â‰¤3.5t (mÃ¡s de USD 40k)");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3091",
                column: "name",
                value: "Aviones / helicÃ³pteros de uso privado");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3101",
                column: "name",
                value: "Servicios de televisiÃ³n pagada");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3111",
                column: "name",
                value: "Bebidas alcohÃ³licas (incl. cerveza)");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_id_type",
                keyColumn: "code",
                keyValue: "04",
                column: "name",
                value: "Registro Ãšnico de Contribuyentes");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_id_type",
                keyColumn: "code",
                keyValue: "05",
                column: "name",
                value: "CÃ©dula de ciudadanÃ­a");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_id_type",
                keyColumn: "code",
                keyValue: "08",
                column: "name",
                value: "IdentificaciÃ³n del exterior");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_payment_method",
                keyColumn: "code",
                keyValue: "01",
                column: "name",
                value: "Sin utilizaciÃ³n del sistema financiero");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_payment_method",
                keyColumn: "code",
                keyValue: "15",
                column: "name",
                value: "CompensaciÃ³n de deudas");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_payment_method",
                keyColumn: "code",
                keyValue: "16",
                column: "name",
                value: "Tarjeta de dÃ©bito");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_payment_method",
                keyColumn: "code",
                keyValue: "17",
                column: "name",
                value: "Dinero electrÃ³nico");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_payment_method",
                keyColumn: "code",
                keyValue: "19",
                column: "name",
                value: "Tarjeta de crÃ©dito");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_payment_method",
                keyColumn: "code",
                keyValue: "20",
                column: "name",
                value: "Otros con utilizaciÃ³n del sistema financiero");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_payment_method",
                keyColumn: "code",
                keyValue: "21",
                column: "name",
                value: "Endoso de tÃ­tulos");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                column: "name",
                value: "Ret. IVA 10% â€“ Bienes (tarifa vigente)");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                column: "name",
                value: "Ret. IVA 20% â€“ Servicios (tarifa vigente)");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                column: "name",
                value: "Ret. IVA 30% â€“ Presuntivo bienes");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"),
                column: "name",
                value: "Ret. IVA 70% â€“ Presuntivo servicios");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"),
                column: "name",
                value: "Ret. IVA 100% â€“ Liq. compra / honorarios");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"),
                column: "name",
                value: "Ret. IVA 15% â€“ Constructoras");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"),
                column: "name",
                value: "Honorarios profesionales y demÃ¡s servicios");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"),
                column: "name",
                value: "Servicios â€“ predomina mano de obra");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"),
                column: "name",
                value: "Publicidad y comunicaciÃ³n");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000009"),
                column: "name",
                value: "Actividades de construcciÃ³n (contrato)");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"),
                column: "name",
                value: "ISD â€“ Impuesto a la Salida de Divisas");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_tax_regime",
                keyColumn: "code",
                keyValue: "01",
                column: "name",
                value: "RÃ©gimen General");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_tax_regime",
                keyColumn: "code",
                keyValue: "02",
                column: "name",
                value: "RIMPE â€“ RÃ©gimen de Microempresas");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_tax_regime",
                keyColumn: "code",
                keyValue: "03",
                column: "name",
                value: "RIMPE â€“ Negocio Popular");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "01",
                column: "name",
                value: "CrÃ©dito Tributario para declaraciÃ³n de IVA");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "02",
                column: "name",
                value: "Costo o Gasto para declaraciÃ³n del IR");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "03",
                column: "name",
                value: "Activo Fijo â€“ CrÃ©dito Tributario IVA");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "04",
                column: "name",
                value: "Activo Fijo â€“ Costo o Gasto IR");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "05",
                column: "name",
                value: "LiquidaciÃ³n Gastos de Viaje, Hospedaje y AlimentaciÃ³n");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "06",
                column: "name",
                value: "RetenciÃ³n en la Fuente");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "07",
                column: "name",
                value: "DistribuciÃ³n de Dividendos, Beneficios o Ganancias");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "09",
                column: "name",
                value: "RetenciÃ³n del IVA 30%");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "10",
                column: "name",
                value: "RetenciÃ³n del IVA 70%");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "11",
                column: "name",
                value: "RetenciÃ³n del IVA 100%");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "12",
                column: "name",
                value: "ExportaciÃ³n de Bienes");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "14",
                column: "name",
                value: "ExportaciÃ³n de servicios con domicilio en el exterior");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "17",
                column: "name",
                value: "Nota de crÃ©dito deducible");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "20",
                column: "name",
                value: "Notas de crÃ©dito por devoluciones");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_uom",
                keyColumn: "code",
                keyValue: "01",
                column: "name",
                value: "Unidad BiolÃ³gica");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_uom",
                keyColumn: "code",
                keyValue: "12",
                column: "name",
                value: "Metro cÃºbico");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_uom",
                keyColumn: "code",
                keyValue: "20",
                column: "name",
                value: "VehÃ­culo");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_vat_rate",
                keyColumn: "code",
                keyValue: "2",
                column: "name",
                value: "12% IVA (histÃ³rico)");

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_vat_rate",
                keyColumn: "code",
                keyValue: "3",
                column: "name",
                value: "14% IVA (histÃ³rico)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "sri_vat_rate",
                schema: "global",
                newName: "sri_vat_rate");

            migrationBuilder.RenameTable(
                name: "sri_uom",
                schema: "global",
                newName: "sri_uom");

            migrationBuilder.RenameTable(
                name: "sri_tax_support",
                schema: "global",
                newName: "sri_tax_support");

            migrationBuilder.RenameTable(
                name: "sri_tax_regime",
                schema: "global",
                newName: "sri_tax_regime");

            migrationBuilder.RenameTable(
                name: "sri_retention_code",
                schema: "global",
                newName: "sri_retention_code");

            migrationBuilder.RenameTable(
                name: "sri_payment_method",
                schema: "global",
                newName: "sri_payment_method");

            migrationBuilder.RenameTable(
                name: "sri_id_type",
                schema: "global",
                newName: "sri_id_type");

            migrationBuilder.RenameTable(
                name: "sri_ice_rate",
                schema: "global",
                newName: "sri_ice_rate");

            migrationBuilder.RenameTable(
                name: "sri_error_code",
                schema: "global",
                newName: "sri_error_code");

            migrationBuilder.RenameTable(
                name: "sri_environment",
                schema: "global",
                newName: "sri_environment");

            migrationBuilder.RenameTable(
                name: "sri_emission_type",
                schema: "global",
                newName: "sri_emission_type");

            migrationBuilder.RenameTable(
                name: "sri_doc_type",
                schema: "global",
                newName: "sri_doc_type");

            migrationBuilder.RenameTable(
                name: "sri_country",
                schema: "global",
                newName: "sri_country");

            migrationBuilder.RenameTable(
                name: "geo_provinces",
                schema: "global",
                newName: "geo_provinces");

            migrationBuilder.RenameTable(
                name: "geo_parishes",
                schema: "global",
                newName: "geo_parishes");

            migrationBuilder.RenameTable(
                name: "geo_cantons",
                schema: "global",
                newName: "geo_cantons");

            migrationBuilder.UpdateData(
                table: "sri_country",
                keyColumn: "code",
                keyValue: "CAN",
                column: "name",
                value: "CANADÁ");

            migrationBuilder.UpdateData(
                table: "sri_country",
                keyColumn: "code",
                keyValue: "DOM",
                column: "name",
                value: "REPÚBLICA DOMINICANA");

            migrationBuilder.UpdateData(
                table: "sri_country",
                keyColumn: "code",
                keyValue: "ESP",
                column: "name",
                value: "ESPAÑA");

            migrationBuilder.UpdateData(
                table: "sri_country",
                keyColumn: "code",
                keyValue: "JPN",
                column: "name",
                value: "JAPÓN");

            migrationBuilder.UpdateData(
                table: "sri_country",
                keyColumn: "code",
                keyValue: "MEX",
                column: "name",
                value: "MÉXICO");

            migrationBuilder.UpdateData(
                table: "sri_country",
                keyColumn: "code",
                keyValue: "PAN",
                column: "name",
                value: "PANAMÁ");

            migrationBuilder.UpdateData(
                table: "sri_country",
                keyColumn: "code",
                keyValue: "PER",
                column: "name",
                value: "PERÚ");

            migrationBuilder.UpdateData(
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "03",
                column: "name",
                value: "Liquidación de Compra de Bienes y Prestación de Servicios");

            migrationBuilder.UpdateData(
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "04",
                column: "name",
                value: "Nota de Crédito");

            migrationBuilder.UpdateData(
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "05",
                column: "name",
                value: "Nota de Débito");

            migrationBuilder.UpdateData(
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "06",
                column: "name",
                value: "Guía de Remisión");

            migrationBuilder.UpdateData(
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "07",
                column: "name",
                value: "Comprobante de Retención");

            migrationBuilder.UpdateData(
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "08",
                column: "name",
                value: "Tiquete de Máquina Registradora");

            migrationBuilder.UpdateData(
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "18",
                column: "name",
                value: "Documento Electrónico de Importación");

            migrationBuilder.UpdateData(
                table: "sri_emission_type",
                keyColumn: "code",
                keyValue: (short)1,
                column: "name",
                value: "Emisión Normal");

            migrationBuilder.UpdateData(
                table: "sri_emission_type",
                keyColumn: "code",
                keyValue: (short)2,
                column: "name",
                value: "Emisión por Indisponibilidad del Sistema");

            migrationBuilder.UpdateData(
                table: "sri_environment",
                keyColumn: "code",
                keyValue: (short)1,
                column: "name",
                value: "Producción");

            migrationBuilder.UpdateData(
                table: "sri_error_code",
                keyColumn: "code",
                keyValue: "60",
                column: "name",
                value: "FIRMA INVÁLIDA");

            migrationBuilder.UpdateData(
                table: "sri_error_code",
                keyColumn: "code",
                keyValue: "65",
                column: "name",
                value: "AMBIENTE NO VÁLIDO");

            migrationBuilder.UpdateData(
                table: "sri_error_code",
                keyColumn: "code",
                keyValue: "72",
                column: "name",
                value: "NÚMERO DE COMPROBANTE YA EXISTE");

            migrationBuilder.UpdateData(
                table: "sri_error_code",
                keyColumn: "code",
                keyValue: "73",
                column: "name",
                value: "CLAVE DE ACCESO INVÁLIDA");

            migrationBuilder.UpdateData(
                table: "sri_error_code",
                keyColumn: "code",
                keyValue: "90",
                column: "name",
                value: "CERTIFICADO INVÁLIDO O CADUCADO");

            migrationBuilder.UpdateData(
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3041",
                column: "name",
                value: "Bebidas gaseosas con azúcar añadida");

            migrationBuilder.UpdateData(
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3081",
                column: "name",
                value: "Vehículos ≤3.5t (hasta USD 30k)");

            migrationBuilder.UpdateData(
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3082",
                column: "name",
                value: "Vehículos ≤3.5t (USD 30k–40k)");

            migrationBuilder.UpdateData(
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3083",
                column: "name",
                value: "Vehículos ≤3.5t (más de USD 40k)");

            migrationBuilder.UpdateData(
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3091",
                column: "name",
                value: "Aviones / helicópteros de uso privado");

            migrationBuilder.UpdateData(
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3101",
                column: "name",
                value: "Servicios de televisión pagada");

            migrationBuilder.UpdateData(
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3111",
                column: "name",
                value: "Bebidas alcohólicas (incl. cerveza)");

            migrationBuilder.UpdateData(
                table: "sri_id_type",
                keyColumn: "code",
                keyValue: "04",
                column: "name",
                value: "Registro Único de Contribuyentes");

            migrationBuilder.UpdateData(
                table: "sri_id_type",
                keyColumn: "code",
                keyValue: "05",
                column: "name",
                value: "Cédula de ciudadanía");

            migrationBuilder.UpdateData(
                table: "sri_id_type",
                keyColumn: "code",
                keyValue: "08",
                column: "name",
                value: "Identificación del exterior");

            migrationBuilder.UpdateData(
                table: "sri_payment_method",
                keyColumn: "code",
                keyValue: "01",
                column: "name",
                value: "Sin utilización del sistema financiero");

            migrationBuilder.UpdateData(
                table: "sri_payment_method",
                keyColumn: "code",
                keyValue: "15",
                column: "name",
                value: "Compensación de deudas");

            migrationBuilder.UpdateData(
                table: "sri_payment_method",
                keyColumn: "code",
                keyValue: "16",
                column: "name",
                value: "Tarjeta de débito");

            migrationBuilder.UpdateData(
                table: "sri_payment_method",
                keyColumn: "code",
                keyValue: "17",
                column: "name",
                value: "Dinero electrónico");

            migrationBuilder.UpdateData(
                table: "sri_payment_method",
                keyColumn: "code",
                keyValue: "19",
                column: "name",
                value: "Tarjeta de crédito");

            migrationBuilder.UpdateData(
                table: "sri_payment_method",
                keyColumn: "code",
                keyValue: "20",
                column: "name",
                value: "Otros con utilización del sistema financiero");

            migrationBuilder.UpdateData(
                table: "sri_payment_method",
                keyColumn: "code",
                keyValue: "21",
                column: "name",
                value: "Endoso de títulos");

            migrationBuilder.UpdateData(
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                column: "name",
                value: "Ret. IVA 10% – Bienes (tarifa vigente)");

            migrationBuilder.UpdateData(
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                column: "name",
                value: "Ret. IVA 20% – Servicios (tarifa vigente)");

            migrationBuilder.UpdateData(
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                column: "name",
                value: "Ret. IVA 30% – Presuntivo bienes");

            migrationBuilder.UpdateData(
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"),
                column: "name",
                value: "Ret. IVA 70% – Presuntivo servicios");

            migrationBuilder.UpdateData(
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"),
                column: "name",
                value: "Ret. IVA 100% – Liq. compra / honorarios");

            migrationBuilder.UpdateData(
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"),
                column: "name",
                value: "Ret. IVA 15% – Constructoras");

            migrationBuilder.UpdateData(
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"),
                column: "name",
                value: "Honorarios profesionales y demás servicios");

            migrationBuilder.UpdateData(
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"),
                column: "name",
                value: "Servicios – predomina mano de obra");

            migrationBuilder.UpdateData(
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"),
                column: "name",
                value: "Publicidad y comunicación");

            migrationBuilder.UpdateData(
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000009"),
                column: "name",
                value: "Actividades de construcción (contrato)");

            migrationBuilder.UpdateData(
                table: "sri_retention_code",
                keyColumn: "id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"),
                column: "name",
                value: "ISD – Impuesto a la Salida de Divisas");

            migrationBuilder.UpdateData(
                table: "sri_tax_regime",
                keyColumn: "code",
                keyValue: "01",
                column: "name",
                value: "Régimen General");

            migrationBuilder.UpdateData(
                table: "sri_tax_regime",
                keyColumn: "code",
                keyValue: "02",
                column: "name",
                value: "RIMPE – Régimen de Microempresas");

            migrationBuilder.UpdateData(
                table: "sri_tax_regime",
                keyColumn: "code",
                keyValue: "03",
                column: "name",
                value: "RIMPE – Negocio Popular");

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "01",
                column: "name",
                value: "Crédito Tributario para declaración de IVA");

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "02",
                column: "name",
                value: "Costo o Gasto para declaración del IR");

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "03",
                column: "name",
                value: "Activo Fijo – Crédito Tributario IVA");

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "04",
                column: "name",
                value: "Activo Fijo – Costo o Gasto IR");

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "05",
                column: "name",
                value: "Liquidación Gastos de Viaje, Hospedaje y Alimentación");

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "06",
                column: "name",
                value: "Retención en la Fuente");

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "07",
                column: "name",
                value: "Distribución de Dividendos, Beneficios o Ganancias");

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "09",
                column: "name",
                value: "Retención del IVA 30%");

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "10",
                column: "name",
                value: "Retención del IVA 70%");

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "11",
                column: "name",
                value: "Retención del IVA 100%");

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "12",
                column: "name",
                value: "Exportación de Bienes");

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "14",
                column: "name",
                value: "Exportación de servicios con domicilio en el exterior");

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "17",
                column: "name",
                value: "Nota de crédito deducible");

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "20",
                column: "name",
                value: "Notas de crédito por devoluciones");

            migrationBuilder.UpdateData(
                table: "sri_uom",
                keyColumn: "code",
                keyValue: "01",
                column: "name",
                value: "Unidad Biológica");

            migrationBuilder.UpdateData(
                table: "sri_uom",
                keyColumn: "code",
                keyValue: "12",
                column: "name",
                value: "Metro cúbico");

            migrationBuilder.UpdateData(
                table: "sri_uom",
                keyColumn: "code",
                keyValue: "20",
                column: "name",
                value: "Vehículo");

            migrationBuilder.UpdateData(
                table: "sri_vat_rate",
                keyColumn: "code",
                keyValue: "2",
                column: "name",
                value: "12% IVA (histórico)");

            migrationBuilder.UpdateData(
                table: "sri_vat_rate",
                keyColumn: "code",
                keyValue: "3",
                column: "name",
                value: "14% IVA (histórico)");
        }
    }
}
