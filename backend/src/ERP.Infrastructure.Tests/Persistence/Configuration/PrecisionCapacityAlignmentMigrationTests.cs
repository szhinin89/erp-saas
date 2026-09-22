using System.Globalization;
using ERP.Application.Common;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.Configuration;

/// <summary>
/// ERP-PRECISION-CAPACITY-05A (PostgreSQL 16 real vía Testcontainers). Verifica la migración
/// PrecisionCapacityAlignment05A: capacidad de columnas (numeric(p,s) del mapa aprobado),
/// round-trip de 10 decimales internos / 6 en cantidades, normalización de policies de venta &gt; 6
/// y CHECK constraints nuevos. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PrecisionCapacityAlignmentMigrationTests : IAsyncLifetime
{
    private const string MigrationBefore = "20260921142248_PrecisionPolicyBackfillAndLegacyCleanup04";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_precision_capacity_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext() =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            new FixedTenant(),
            new NoOpPublisher(),
            new FixedCompany()
        );

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();
        return conn;
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        return await cmd.ExecuteScalarAsync();
    }

    private static async Task ExecAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    // (tabla, columna, precisión, escala) — mapa aprobado ERP-PRECISION-CAPACITY-05A.
    private static List<object[]> ApprovedColumns()
    {
        var data = new List<object[]>();
        void Add(string table, int p, int s, params string[] cols)
        {
            foreach (var c in cols)
                data.Add([table, c, p, s]);
        }

        // Compra / costo / promedio → (22,10)
        Add("purchase_invoice_details", 22, 10, "unit_price", "landed_unit_cost", "conversion_factor");
        Add("purchase_reception_lines", 22, 10, "unit_price");
        Add("expense_lines", 22, 10, "unit_amount");
        Add("purchase_return_details", 22, 10, "unit_cost");
        Add("stock_movements", 22, 10, "unit_cost", "running_average_cost");
        Add("stock_adjustment_lines", 22, 10, "unit_cost_base", "conversion_factor");
        Add("sales_invoice_details", 22, 10, "unit_cost_at_sale", "conversion_factor");
        Add("sales_return_details", 22, 10, "conversion_factor");
        Add("current_stocks", 22, 10, "total_stock_value");

        // Conversiones de ítem
        Add("item_unit_conversions", 18, 10, "factor");
        Add("item_packaging_levels", 18, 10, "base_quantity");

        // Cantidades → (20,6)
        Add("current_stocks", 20, 6, "quantity", "reserved_quantity");
        Add("stock_movements", 20, 6, "previous_quantity", "quantity", "result_quantity");
        Add("stock_transfer_lines", 20, 6, "quantity");
        Add(
            "stock_adjustment_lines",
            20,
            6,
            "quantity",
            "quantity_in_base_uom",
            "current_stock_before",
            "current_stock_after"
        );
        Add("sales_invoice_details", 20, 6, "quantity", "quantity_in_base_uom");
        Add("sales_return_details", 20, 6, "quantity", "quantity_in_base_uom");
        Add("purchase_invoice_details", 20, 6, "quantity", "quantity_in_base_uom", "ordered_quantity");
        Add("purchase_reception_lines", 20, 6, "quantity");
        Add("purchase_credit_note_details", 20, 6, "quantity");
        Add("purchase_return_details", 20, 6, "quantity");
        Add("expense_lines", 20, 6, "quantity");
        Add("items", 16, 6, "min_stock_qty", "max_stock_qty");

        // Porcentajes → (9,6)
        Add("sales_invoice_details", 9, 6, "discount_pct");
        Add("sales_return_details", 9, 6, "discount_pct");
        Add("purchase_invoice_details", 9, 6, "discount_pct");
        Add("purchase_reception_lines", 9, 6, "discount_pct");
        Add("expense_lines", 9, 6, "discount_pct");
        Add("items", 9, 6, "max_discount_percent");

        // Sin cambio: precio de venta y rule_value (18,6); inventory_lots ya (18,6).
        Add("items", 18, 6, "base_sale_price");
        Add("sales_invoice_details", 18, 6, "unit_price", "list_price_at_sale");
        Add("sales_return_details", 18, 6, "unit_price");
        Add("pricing_rules", 18, 6, "rule_value");
        Add("price_lists", 18, 6, "rule_value");
        Add("inventory_lots", 18, 6, "current_qty", "initial_qty");

        // Explícitamente NO tocados.
        Add("stock_movements", 18, 2, "running_stock_value");
        Add("credit_installments", 5, 2, "percentage");
        Add("warehouses", 18, 4, "capacity", "daily_dispatch_goal");
        Add("item_packaging_levels", 10, 3, "weight");
        return data;
    }

    [Fact]
    public async Task Columnas_tienen_la_capacidad_del_mapa_aprobado()
    {
        await using (var db = CreateContext())
            await db.Database.MigrateAsync();

        await using var conn = await OpenAsync();
        var failures = new List<string>();
        foreach (var row in ApprovedColumns())
        {
            var (table, column, precision, scale) = ((string)row[0], (string)row[1], (int)row[2], (int)row[3]);
            await using var cmd = new NpgsqlCommand(
                "SELECT numeric_precision, numeric_scale FROM information_schema.columns "
                    + "WHERE table_schema='public' AND table_name=@t AND column_name=@c",
                conn
            );
            cmd.Parameters.AddWithValue("t", table);
            cmd.Parameters.AddWithValue("c", column);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync())
            {
                failures.Add($"{table}.{column}: no existe");
                continue;
            }
            var p = Convert.ToInt32(r.GetValue(0), CultureInfo.InvariantCulture);
            var s = Convert.ToInt32(r.GetValue(1), CultureInfo.InvariantCulture);
            if (p != precision || s != scale)
                failures.Add($"{table}.{column}: esperado ({precision},{scale}) real ({p},{s})");
        }

        failures.Should().BeEmpty();
    }

    [Fact]
    public async Task RoundTrip_10_decimales_en_costos_y_6_en_cantidades()
    {
        await using (var db = CreateContext())
            await db.Database.MigrateAsync();

        await using var conn = await OpenAsync();
        await ExecAsync(conn, "SET session_replication_role = replica");

        // stock_movements: costo/promedio a 10 decimales, cantidades a 6.
        await InsertAsync(
            conn,
            "stock_movements",
            new()
            {
                ["unit_cost"] = "1234567.1234567891",
                ["running_average_cost"] = "0.0000000001",
                ["previous_quantity"] = "1.123456",
                ["quantity"] = "2.654321",
                ["result_quantity"] = "3.777777",
            }
        );
        // current_stocks: valor de stock a 10 decimales.
        await InsertAsync(
            conn,
            "current_stocks",
            new()
            {
                ["total_stock_value"] = "9876543210.9876543219",
                ["quantity"] = "0.000001",
                ["reserved_quantity"] = "123456.654321",
            }
        );
        // purchase_invoice_details: precio compra, costo, factor, cantidad y descuento.
        await InsertAsync(
            conn,
            "purchase_invoice_details",
            new()
            {
                ["unit_price"] = "0.1234567891",
                ["landed_unit_cost"] = "0.9876543219",
                ["conversion_factor"] = "12.0000000001",
                ["quantity"] = "5.123456",
                ["discount_pct"] = "12.345678",
            }
        );
        // item_unit_conversions / item_packaging_levels.
        await InsertAsync(conn, "item_unit_conversions", new() { ["factor"] = "0.3333333333" });
        await InsertAsync(conn, "item_packaging_levels", new() { ["base_quantity"] = "24.0000000001" });

        (await ScalarAsync(conn, "SELECT unit_cost::text FROM stock_movements")).Should().Be("1234567.1234567891");
        (await ScalarAsync(conn, "SELECT running_average_cost::text FROM stock_movements"))
            .Should()
            .Be("0.0000000001");
        (await ScalarAsync(conn, "SELECT quantity::text FROM stock_movements")).Should().Be("2.654321");
        (await ScalarAsync(conn, "SELECT total_stock_value::text FROM current_stocks"))
            .Should()
            .Be("9876543210.9876543219");
        (await ScalarAsync(conn, "SELECT reserved_quantity::text FROM current_stocks")).Should().Be("123456.654321");
        (await ScalarAsync(conn, "SELECT unit_price::text FROM purchase_invoice_details"))
            .Should()
            .Be("0.1234567891");
        (await ScalarAsync(conn, "SELECT landed_unit_cost::text FROM purchase_invoice_details"))
            .Should()
            .Be("0.9876543219");
        (await ScalarAsync(conn, "SELECT conversion_factor::text FROM purchase_invoice_details"))
            .Should()
            .Be("12.0000000001");
        (await ScalarAsync(conn, "SELECT discount_pct::text FROM purchase_invoice_details")).Should().Be("12.345678");
        (await ScalarAsync(conn, "SELECT factor::text FROM item_unit_conversions")).Should().Be("0.3333333333");
        (await ScalarAsync(conn, "SELECT base_quantity::text FROM item_packaging_levels"))
            .Should()
            .Be("24.0000000001");
    }

    [Theory]
    [InlineData("1.230000")]
    [InlineData("1.234500")]
    [InlineData("1.234567")]
    public async Task Sales_price_and_quantity_survive_PostgreSQL_round_trip(string price)
    {
        await using (var db = CreateContext())
            await db.Database.MigrateAsync();
        await using var conn = await OpenAsync();
        await ExecAsync(conn, "SET session_replication_role = replica");
        await InsertAsync(conn, "sales_invoice_details", new()
        {
            ["unit_price"] = price,
            ["quantity"] = "1.123456",
            ["quantity_in_base_uom"] = "1.123456",
        });
        (await ScalarAsync(conn, "SELECT unit_price::text FROM sales_invoice_details")).Should().Be(price);
        (await ScalarAsync(conn, "SELECT quantity::text FROM sales_invoice_details")).Should().Be("1.123456");
    }

    /// <summary>
    /// Inserta una fila mínima rellenando las columnas NOT NULL sin default con un valor neutro por
    /// tipo; las columnas indicadas en <paramref name="values"/> reciben el valor de la prueba.
    /// (FKs desactivadas con session_replication_role=replica.)
    /// </summary>
    private static async Task InsertAsync(NpgsqlConnection conn, string table, Dictionary<string, string> values)
    {
        var cols = new List<string>();
        var vals = new List<string>();
        await using (
            var cmd = new NpgsqlCommand(
                "SELECT column_name, data_type FROM information_schema.columns "
                    + "WHERE table_schema='public' AND table_name=@t AND is_nullable='NO' AND column_default IS NULL",
                conn
            )
        )
        {
            cmd.Parameters.AddWithValue("t", table);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var name = r.GetString(0);
                if (values.ContainsKey(name))
                    continue;
                cols.Add(name);
                vals.Add(
                    r.GetString(1) switch
                    {
                        "uuid" => "gen_random_uuid()",
                        "boolean" => "false",
                        "jsonb" or "json" => "'{}'",
                        "numeric" or "integer" or "smallint" or "bigint" => "0",
                        var t when t.StartsWith("timestamp", StringComparison.Ordinal) => "now()",
                        "date" => "now()::date",
                        _ => "'x'",
                    }
                );
            }
        }

        foreach (var (k, v) in values)
        {
            cols.Add(k);
            vals.Add(v);
        }

        await ExecAsync(conn, $"INSERT INTO {table} ({string.Join(",", cols)}) VALUES ({string.Join(",", vals)})");
    }

    [Fact]
    public async Task Policy_de_venta_mayor_a_6_se_normaliza_a_6_y_CHECK_nuevos_rigen()
    {
        await using (var db = CreateContext())
            await db.Database.GetService<IMigrator>().MigrateAsync(MigrationBefore);

        await using (var conn = await OpenAsync())
        {
            await ExecAsync(conn, "SET session_replication_role = replica");
            await InsertAsync(
                conn,
                "company_precision_policy",
                new()
                {
                    ["profile_type"] = "'custom'",
                    ["sales_unit_price_decimals"] = "8",
                    ["purchase_unit_price_decimals"] = "8",
                    ["quantity_decimals"] = "6",
                    ["percentage_decimals"] = "6",
                    ["unit_cost_decimals"] = "8",
                    ["average_cost_decimals"] = "8",
                    ["conversion_factor_decimals"] = "8",
                    ["settlement_tolerance_amount"] = "0.01",
                }
            );
        }

        await using (var db = CreateContext())
            await db.Database.MigrateAsync();

        await using var conn2 = await OpenAsync();
        await ExecAsync(conn2, "SET session_replication_role = replica");
        Convert
            .ToInt32(
                await ScalarAsync(conn2, "SELECT sales_unit_price_decimals FROM company_precision_policy"),
                CultureInfo.InvariantCulture
            )
            .Should()
            .Be(6);
        // Los demás valores (≤ 10) se preservan.
        Convert
            .ToInt32(
                await ScalarAsync(conn2, "SELECT purchase_unit_price_decimals FROM company_precision_policy"),
                CultureInfo.InvariantCulture
            )
            .Should()
            .Be(8);

        // Ventas rechaza > 6; compra/costo/promedio/factor aceptan hasta 10 y rechazan 11.
        var limits = new (string Col, int Ok, int Bad)[]
        {
            ("sales_unit_price_decimals", 6, 7),
            ("purchase_unit_price_decimals", 10, 11),
            ("unit_cost_decimals", 10, 11),
            ("average_cost_decimals", 10, 11),
            ("conversion_factor_decimals", 10, 11),
            ("quantity_decimals", 6, 7),
            ("percentage_decimals", 6, 7),
        };
        foreach (var (col, ok, bad) in limits)
        {
            await ExecAsync(conn2, $"UPDATE company_precision_policy SET {col} = {ok}");
            var act = async () => await ExecAsync(conn2, $"UPDATE company_precision_policy SET {col} = {bad}");
            await act.Should().ThrowAsync<PostgresException>($"{col} > {ok} debe violar el CHECK");
        }
    }

    private sealed class FixedTenant : ICurrentTenant
    {
        public Guid TenantId => Guid.Empty;
        public string? Slug => null;
    }

    private sealed class FixedCompany : ICurrentCompany
    {
        public Guid CompanyId => Guid.Empty;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => false;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }
}
