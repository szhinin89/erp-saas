using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCajaBancosModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cuenta_bancaria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    numero_cuenta = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tipo_cuenta = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    moneda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    saldo_inicial = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    saldo_actual = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cuenta_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cuenta_bancaria", x => x.id);
                    table.ForeignKey(
                        name: "FK_cuenta_bancaria_accounts_cuenta_contable_id",
                        column: x => x.cuenta_contable_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "caja_chica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    saldo_asignado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    saldo_actual = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cuenta_bancaria_id_reposicion = table.Column<Guid>(type: "uuid", nullable: true),
                    cuenta_contable_caja_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_caja_chica", x => x.id);
                    table.ForeignKey(
                        name: "FK_caja_chica_accounts_cuenta_contable_caja_id",
                        column: x => x.cuenta_contable_caja_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_caja_chica_cuenta_bancaria_cuenta_bancaria_id_reposicion",
                        column: x => x.cuenta_bancaria_id_reposicion,
                        principalTable: "cuenta_bancaria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "extracto_bancario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cuenta_bancaria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    periodo_desde = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    periodo_hasta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    saldo_inicial_extracto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    saldo_final_extracto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fecha_carga = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    conciliado = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extracto_bancario", x => x.id);
                    table.ForeignKey(
                        name: "FK_extracto_bancario_cuenta_bancaria_cuenta_bancaria_id",
                        column: x => x.cuenta_bancaria_id,
                        principalTable: "cuenta_bancaria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "arqueo_caja",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    caja_chica_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_arqueo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    efectivo_fisico = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    diferencia = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    observaciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    aprobado = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arqueo_caja", x => x.id);
                    table.ForeignKey(
                        name: "FK_arqueo_caja_caja_chica_caja_chica_id",
                        column: x => x.caja_chica_id,
                        principalTable: "caja_chica",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "gasto_caja_chica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    caja_chica_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    concepto = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tipo_comprobante = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    numero_comprobante = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    asiento_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gasto_caja_chica", x => x.id);
                    table.ForeignKey(
                        name: "FK_gasto_caja_chica_caja_chica_caja_chica_id",
                        column: x => x.caja_chica_id,
                        principalTable: "caja_chica",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gasto_caja_chica_journal_entries_asiento_contable_id",
                        column: x => x.asiento_contable_id,
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "movimiento_bancario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    extracto_bancario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    referencia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    asiento_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimiento_bancario", x => x.id);
                    table.ForeignKey(
                        name: "FK_movimiento_bancario_extracto_bancario_extracto_bancario_id",
                        column: x => x.extracto_bancario_id,
                        principalTable: "extracto_bancario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_movimiento_bancario_journal_entries_asiento_contable_id",
                        column: x => x.asiento_contable_id,
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_arqueo_caja_caja_chica_id",
                table: "arqueo_caja",
                column: "caja_chica_id");

            migrationBuilder.CreateIndex(
                name: "IX_caja_chica_cuenta_bancaria_id_reposicion",
                table: "caja_chica",
                column: "cuenta_bancaria_id_reposicion");

            migrationBuilder.CreateIndex(
                name: "IX_caja_chica_cuenta_contable_caja_id",
                table: "caja_chica",
                column: "cuenta_contable_caja_id");

            migrationBuilder.CreateIndex(
                name: "ux_caja_chica_tenant_nombre",
                table: "caja_chica",
                columns: new[] { "tenant_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cuenta_bancaria_cuenta_contable_id",
                table: "cuenta_bancaria",
                column: "cuenta_contable_id");

            migrationBuilder.CreateIndex(
                name: "ux_cuenta_bancaria_tenant_numero",
                table: "cuenta_bancaria",
                columns: new[] { "tenant_id", "numero_cuenta" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_extracto_bancario_cuenta_bancaria_id",
                table: "extracto_bancario",
                column: "cuenta_bancaria_id");

            migrationBuilder.CreateIndex(
                name: "ix_extracto_bancario_tenant_cuenta_periodo",
                table: "extracto_bancario",
                columns: new[] { "tenant_id", "cuenta_bancaria_id", "periodo_desde", "periodo_hasta" });

            migrationBuilder.CreateIndex(
                name: "IX_gasto_caja_chica_asiento_contable_id",
                table: "gasto_caja_chica",
                column: "asiento_contable_id");

            migrationBuilder.CreateIndex(
                name: "IX_gasto_caja_chica_caja_chica_id",
                table: "gasto_caja_chica",
                column: "caja_chica_id");

            migrationBuilder.CreateIndex(
                name: "IX_movimiento_bancario_asiento_contable_id",
                table: "movimiento_bancario",
                column: "asiento_contable_id");

            migrationBuilder.CreateIndex(
                name: "IX_movimiento_bancario_extracto_bancario_id",
                table: "movimiento_bancario",
                column: "extracto_bancario_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_bancario_tenant_extracto_fecha",
                table: "movimiento_bancario",
                columns: new[] { "tenant_id", "extracto_bancario_id", "fecha" });

            migrationBuilder.Sql(
                """
                INSERT INTO access_profile_permissions
                    (id, tenant_id, profile_id, permission_key, is_allowed, created_at, created_by)
                SELECT gen_random_uuid(), ap.tenant_id, ap.id, k.permission_key, false, NOW(), '44444444-4444-4444-4444-444444444444'::uuid
                FROM access_profiles ap
                CROSS JOIN (
                    VALUES
                        ('caja.extractos.view'),
                        ('caja.extractos.create'),
                        ('caja.conciliar'),
                        ('caja.cajachica.view'),
                        ('caja.cajachica.create'),
                        ('caja.cajachica.edit'),
                        ('caja.arqueos.perform'),
                        ('caja.flujo.view')
                ) AS k(permission_key)
                WHERE ap.is_active = true
                  AND NOT EXISTS (
                        SELECT 1 FROM access_profile_permissions x
                        WHERE x.tenant_id = ap.tenant_id
                          AND x.profile_id = ap.id
                          AND x.permission_key = k.permission_key);

                UPDATE access_profile_permissions app
                SET is_allowed = true,
                    updated_at = NOW(),
                    updated_by = '44444444-4444-4444-4444-444444444444'::uuid
                FROM access_profiles ap
                WHERE app.profile_id = ap.id
                  AND app.tenant_id = ap.tenant_id
                  AND app.created_by = '44444444-4444-4444-4444-444444444444'::uuid
                  AND app.permission_key LIKE 'caja.%'
                  AND ap.is_active = true
                  AND lower(trim(ap.name)) IN ('administrador', 'contador', 'tesorero', 'administrator', 'accountant', 'treasurer');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM access_profile_permissions
                WHERE permission_key LIKE 'caja.%'
                  AND created_by = '44444444-4444-4444-4444-444444444444'::uuid;
                """);

            migrationBuilder.DropTable(
                name: "arqueo_caja");

            migrationBuilder.DropTable(
                name: "gasto_caja_chica");

            migrationBuilder.DropTable(
                name: "movimiento_bancario");

            migrationBuilder.DropTable(
                name: "caja_chica");

            migrationBuilder.DropTable(
                name: "extracto_bancario");

            migrationBuilder.DropTable(
                name: "cuenta_bancaria");
        }
    }
}
