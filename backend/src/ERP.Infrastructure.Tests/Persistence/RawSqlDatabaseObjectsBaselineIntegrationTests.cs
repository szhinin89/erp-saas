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
