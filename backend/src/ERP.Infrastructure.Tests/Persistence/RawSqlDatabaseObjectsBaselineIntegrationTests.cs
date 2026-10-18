using ERP.Application.Common;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Items;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// ZH-DB-FINAL-BASELINE-SQUASH-01 — contraparte física (PostgreSQL 16 real vía Testcontainers) de
/// ERP.Architecture.Tests/RawSqlDatabaseObjectsSurviveMigrationSquashTests: una BD vacía migrada con
/// la cadena vigente contiene cada objeto que existe solo como raw SQL de migración (el model
/// snapshot no los conoce). El comportamiento de cada restricción se cubre en
/// BusinessPartnerIdentificationUniqueIndexTests y PurchaseExpenseReprocessAfterCancelConstraintsTests.
/// Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class RawSqlDatabaseObjectsBaselineIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_raw_sql_objects_baseline_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext(Guid.Empty);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Extension_pg_trgm_existe()
    {
        await using var db = CreateContext(Guid.Empty);

        (
            await Scalar(
                db,
                "SELECT count(*)::int AS \"Value\" FROM pg_extension WHERE extname = 'pg_trgm'"
            )
        )
            .Should()
            .Be(1);
    }

    [Theory]
    [InlineData(
        "uq_mbp_identification",
        "CREATE UNIQUE INDEX uq_mbp_identification ON public.master_business_partners USING btree (tenant_id, identification_type, identification_number)"
    )]
    [InlineData(
        "ix_items_short_name_trgm",
        "CREATE INDEX ix_items_short_name_trgm ON public.items USING gin (short_name gin_trgm_ops)"
    )]
    [InlineData(
        "ix_items_description_trgm",
        "CREATE INDEX ix_items_description_trgm ON public.items USING gin (description gin_trgm_ops)"
    )]
    [InlineData(
        "uq_items_tenant_company_sku",
        "CREATE UNIQUE INDEX uq_items_tenant_company_sku ON public.items USING btree (tenant_id, company_id, sku)"
    )]
    public async Task Indice_raw_existe_con_su_definicion(
        string indexName,
        string expectedDefinition
    )
    {
        await using var db = CreateContext(Guid.Empty);

        var definition = await db
            .Database.SqlQuery<string>(
                $"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE indexname = {indexName}"
            )
            .SingleAsync();

        definition.Should().Be(expectedDefinition);
    }

    [Fact]
    public async Task Funcion_de_exclusividad_Compra_Gasto_existe()
    {
        await using var db = CreateContext(Guid.Empty);

        (
            await Scalar(
                db,
                "SELECT count(*)::int AS \"Value\" FROM pg_proc WHERE proname = 'enforce_purchase_expense_exclusivity' AND prorettype = 'trigger'::regtype"
            )
        )
            .Should()
            .Be(1);
    }

    [Theory]
    [InlineData("tr_expense_purchase_exclusivity", "expense_documents")]
    [InlineData("tr_purchase_expense_exclusivity", "purchase_invoices")]
    public async Task Trigger_de_exclusividad_existe_habilitado_sobre_su_tabla(
        string triggerName,
        string tableName
    )
    {
        await using var db = CreateContext(Guid.Empty);

        var definition = await db
            .Database.SqlQuery<string>(
                $"""
                SELECT pg_get_triggerdef(t.oid) AS "Value"
                  FROM pg_trigger t
                 WHERE t.tgname = {triggerName} AND NOT t.tgisinternal AND t.tgenabled = 'O'
                """
            )
            .SingleAsync();

        definition
            .Should()
            .Be(
                $"CREATE TRIGGER {triggerName} BEFORE INSERT OR UPDATE OF tenant_id, access_key ON public.{tableName} "
                    + "FOR EACH ROW EXECUTE FUNCTION enforce_purchase_expense_exclusivity()"
            );
    }

    [Fact]
    public async Task Check_de_naturaleza_de_Item_existe_con_su_definicion()
    {
        await using var db = CreateContext(Guid.Empty);

        var definition = await db
            .Database.SqlQuery<string>(
                $"""
                SELECT pg_get_constraintdef(c.oid) AS "Value"
                  FROM pg_constraint c
                 WHERE c.conname = 'ck_items_nature' AND c.conrelid = 'public.items'::regclass AND c.contype = 'c'
                """
            )
            .SingleAsync();

        definition
            .Should()
            .Be(
                "CHECK (((nature)::text = ANY ((ARRAY['Product'::character varying, 'Service'::character varying])::text[])))"
            );
    }

    [Theory]
    [InlineData("a1_item_barcode_unique")]
    [InlineData("a1_inventory_product_scope")]
    public async Task Funcion_A1_existe(string functionName)
    {
        await using var db = CreateContext(Guid.Empty);

        var count = await db
            .Database.SqlQuery<int>(
                $"""
                SELECT count(*)::int AS "Value"
                  FROM pg_proc
                 WHERE proname = {functionName} AND prorettype = 'trigger'::regtype
                """
            )
            .SingleAsync();

        count.Should().Be(1);
    }

    [Theory]
    [InlineData("a1_variant_barcode_unique", "item_variant_barcodes", "a1_item_barcode_unique")]
    [InlineData("a1_packaging_barcode_unique", "item_packaging_levels", "a1_item_barcode_unique")]
    [InlineData("a1_stock_product_scope", "current_stocks", "a1_inventory_product_scope")]
    [InlineData("a1_movement_product_scope", "stock_movements", "a1_inventory_product_scope")]
    public async Task Trigger_A1_existe_habilitado_sobre_su_tabla(
        string triggerName,
        string tableName,
        string functionName
    )
    {
        await using var db = CreateContext(Guid.Empty);

        var definition = await db
            .Database.SqlQuery<string>(
                $"""
                SELECT pg_get_triggerdef(t.oid) AS "Value"
                  FROM pg_trigger t
                 WHERE t.tgname = {triggerName} AND NOT t.tgisinternal AND t.tgenabled = 'O'
                """
            )
            .SingleAsync();

        definition
            .Should()
            .Be(
                $"CREATE TRIGGER {triggerName} BEFORE INSERT OR UPDATE ON public.{tableName} "
                    + $"FOR EACH ROW EXECUTE FUNCTION {functionName}()"
            );
    }

    [Fact]
    public async Task Busqueda_por_similitud_de_Item_Matching_funciona_sobre_la_baseline()
    {
        var createdBy = Guid.NewGuid();
        Guid tenantId;
        Guid companyId;

        await using (var db = CreateContext(Guid.Empty))
        {
            var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], createdBy);
            tenantId = tenant.Id;
            var company = Company.CreateManaged(tenant.Id, "1790012345001", "Similarity Company", createdBy: createdBy);
            companyId = company.Id;
            db.Companies.Add(company);
            var itemType = ItemTypeDefinition.Create(tenantId, "PHYSICAL", "Fisico", 1, createdBy);
            var item = Item.Create(
                tenantId,
                "SKU-LECHE",
                "Leche entera 1L",
                "Leche entera 1L",
                itemType.Id,
                "UNIT",
                ItemTaxConfig.Create("10", "10"),
                ItemSaleConfig.Create(),
                ItemStockConfig.Create(),
                createdBy, companyId: companyId
            );
            db.Tenants.Add(tenant);
            db.ItemTypes.Add(itemType);
            db.Items.Add(item);
            await db.SaveChangesAsync();
        }

        await using var readDb = CreateContext(tenantId, companyId);
        var matches = await new ItemRepository(readDb).SearchBySimilarityAsync(
            "Leche entera 1L",
            tenantId,
            maxResults: 5,
            minScore: 0.8
        );

        matches.Should().ContainSingle().Which.Sku.Should().Be("SKU-LECHE");
    }

    [Fact]
    public async Task Exclusion_de_periodos_contables_existe_con_su_definicion()
    {
        await using var db = CreateContext(Guid.Empty);

        var definition = await db
            .Database.SqlQuery<string>(
                $"""
                SELECT pg_get_constraintdef(c.oid) AS "Value"
                  FROM pg_constraint c
                 WHERE c.conname = 'ex_accounting_periods_company_no_overlap'
                   AND c.conrelid = 'public.accounting_periods'::regclass AND c.contype = 'x'
                """
            )
            .SingleAsync();

        definition
            .Should()
            .Be("EXCLUDE USING gist (company_id WITH =, daterange(start_date, end_date, '[]'::text) WITH &&)");
    }

    [Fact]
    public async Task Tabla_background_job_progress_existe_con_su_estructura()
    {
        await using var db = CreateContext(Guid.Empty);

        var columns = await db
            .Database.SqlQuery<string>(
                $"""
                SELECT string_agg(column_name || ' ' || data_type || ' ' || is_nullable, ', ' ORDER BY ordinal_position) AS "Value"
                  FROM information_schema.columns
                 WHERE table_schema = 'public' AND table_name = 'background_job_progress'
                """
            )
            .SingleAsync();
        var primaryKey = await db
            .Database.SqlQuery<string>(
                $"""
                SELECT pg_get_constraintdef(c.oid) AS "Value"
                  FROM pg_constraint c
                 WHERE c.conrelid = 'public.background_job_progress'::regclass AND c.contype = 'p'
                """
            )
            .SingleAsync();

        columns
            .Should()
            .Be("stream text NO, candidate_id uuid NO, candidate_created_at timestamp with time zone NO");
        primaryKey.Should().Be("PRIMARY KEY (stream)");
    }

    /// <summary>
    /// Definición vigente (espacios normalizados: el fin de línea del archivo de migración no altera
    /// la semántica plpgsql). Detecta una consolidación que copie una versión obsoleta, como la
    /// exclusividad que ignoraba solo Cancelled (status &lt;&gt; 3) en lugar de Draft/Confirmed.
    /// </summary>
    [Theory]
    [InlineData(
        "enforce_purchase_expense_exclusivity",
        """BEGIN IF NEW.access_key IS NULL THEN RETURN NEW; END IF; PERFORM pg_advisory_xact_lock(hashtextextended( 'purchase-expense:' || NEW.tenant_id::text || ':' || NEW.access_key, 0)); IF TG_TABLE_NAME = 'expense_documents' THEN IF EXISTS (SELECT 1 FROM purchase_invoices WHERE tenant_id = NEW.tenant_id AND access_key = NEW.access_key AND status IN (1, 2)) THEN RAISE EXCEPTION 'La factura ya fue registrada como compra.' USING ERRCODE = '23505', CONSTRAINT = 'uq_purchase_expense_access_key'; END IF; ELSE IF EXISTS (SELECT 1 FROM expense_documents WHERE tenant_id = NEW.tenant_id AND access_key = NEW.access_key AND status <> 2) THEN RAISE EXCEPTION 'La factura ya fue registrada como gasto.' USING ERRCODE = '23505', CONSTRAINT = 'uq_purchase_expense_access_key'; END IF; END IF; RETURN NEW; END;"""
    )]
    [InlineData(
        "a1_item_barcode_unique",
        """DECLARE barcode_value text; BEGIN IF TG_TABLE_NAME='item_variant_barcodes' THEN barcode_value=NEW.code; ELSE barcode_value=NEW.barcode; END IF; IF barcode_value IS NULL THEN RETURN NEW; END IF; PERFORM pg_advisory_xact_lock(hashtextextended(NEW.tenant_id::text || NEW.company_id::text || barcode_value, 0)); IF EXISTS(SELECT 1 FROM item_variant_barcodes b WHERE b.tenant_id=NEW.tenant_id AND b.company_id=NEW.company_id AND b.code=barcode_value AND (TG_TABLE_NAME<>'item_variant_barcodes' OR b.id<>NEW.id)) OR EXISTS(SELECT 1 FROM item_packaging_levels p WHERE p.tenant_id=NEW.tenant_id AND p.company_id=NEW.company_id AND p.barcode=barcode_value AND (TG_TABLE_NAME<>'item_packaging_levels' OR p.id<>NEW.id)) THEN RAISE EXCEPTION 'Barcode already belongs to this Company' USING ERRCODE='23505', CONSTRAINT='uq_item_barcode_tenant_company'; END IF; RETURN NEW; END"""
    )]
    [InlineData(
        "a1_inventory_product_scope",
        """BEGIN IF NOT EXISTS(SELECT 1 FROM items i WHERE i.id=NEW.product_id AND i.tenant_id=NEW.tenant_id AND i.company_id=NEW.company_id AND i.nature='Product') THEN RAISE EXCEPTION 'Inventory requires a Product owned by this Company' USING ERRCODE='23514', CONSTRAINT='ck_inventory_item_nature_scope'; END IF; RETURN NEW; END"""
    )]
    public async Task Funcion_raw_tiene_su_definicion_vigente(string functionName, string expectedBody)
    {
        await using var db = CreateContext(Guid.Empty);

        var body = await db
            .Database.SqlQuery<string>(
                $"""
                SELECT btrim(regexp_replace(p.prosrc, '\s+', ' ', 'g')) AS "Value"
                  FROM pg_proc p
                 WHERE p.proname = {functionName} AND p.prorettype = 'trigger'::regtype
                """
            )
            .SingleAsync();

        body.Should().Be(expectedBody);
    }

    private static async Task<int> Scalar(ErpDbContext db, string sql) =>
#pragma warning disable EF1002 // SQL constante del test, sin entrada externa.
        await db.Database.SqlQueryRaw<int>(sql).SingleAsync();
#pragma warning restore EF1002

    private ErpDbContext CreateContext(Guid tenantId, Guid companyId = default) =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .Options,
            new FixedCurrentTenant(tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(companyId)
        );

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId { get; } = tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
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
