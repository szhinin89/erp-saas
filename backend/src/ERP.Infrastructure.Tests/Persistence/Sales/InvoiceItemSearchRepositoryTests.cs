using ERP.Application.Common;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.Sales;

/// <summary>
/// Suite de integración (PostgreSQL 16 real vía Testcontainers) para
/// <c>InvoiceItemSearchRepository</c> — SALES-RETAIL-READY-01-FIX01. Cubre búsqueda por
/// SKU/nombre/descripción (comportamiento preexistente), búsqueda por código de barras real
/// (ItemVariantBarcode.Code) y el ranking de prioridad barcode/SKU exacto &gt; parcial &gt; nombre.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class InvoiceItemSearchRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_invoice_item_search_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _createdBy;
    private Guid _itemTypeId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext(Guid.Empty);
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var itemType = ItemTypeDefinition.Create(tenant.Id, "PHYSICAL", "Fisico", 1, _createdBy);
        db.Tenants.Add(tenant);
        db.ItemTypes.Add(itemType);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _itemTypeId = itemType.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany()
        );
    }

    /// <summary>Crea un ítem para venta, opcionalmente con un código de barras real asignado
    /// vía la única puerta de entrada del dominio (ItemVariant.AddBarcode).</summary>
    private Item SeedItem(
        Guid tenantId,
        string sku,
        string shortName,
        string description,
        string? barcode = null
    )
    {
        var item = Item.Create(
            tenantId,
            sku,
            shortName,
            description,
            _itemTypeId,
            "UNIT",
            ItemTaxConfig.Create(null, null),
            ItemSaleConfig.Create(),
            ItemStockConfig.Create(tracksStock: false),
            _createdBy
        );

        if (barcode is not null)
        {
            var variant = item.AddVariant([], null, 0, _createdBy);
            variant.AddBarcode(barcode, "EAN13", tenantId, _createdBy, isPrimary: true);
        }

        return item;
    }

    // ── a) SKU search sigue funcionando ───────────────────────────────

    [Fact]
    public async Task SearchAsync_encuentra_por_coincidencia_parcial_de_sku()
    {
        await using (var db = CreateContext(_tenantId))
        {
            db.Items.Add(SeedItem(_tenantId, "COLA-300", "Cola 300ml", "Bebida gaseosa 300ml"));
            await db.SaveChangesAsync();
        }

        await using var readDb = CreateContext(_tenantId);
        var repo = new InvoiceItemSearchRepository(readDb);

        var results = await repo.SearchAsync(_tenantId, Guid.Empty, "COLA-3", null, 10);

        results.Should().ContainSingle(r => r.Sku == "COLA-300");
    }

    // ── b) Búsqueda por nombre sigue funcionando ──────────────────────

    [Fact]
    public async Task SearchAsync_encuentra_por_coincidencia_parcial_de_nombre()
    {
        await using (var db = CreateContext(_tenantId))
        {
            db.Items.Add(
                SeedItem(_tenantId, "SKU-GALLETA-01", "Galleta de Chocolate", "Galleta rellena")
            );
            await db.SaveChangesAsync();
        }

        await using var readDb = CreateContext(_tenantId);
        var repo = new InvoiceItemSearchRepository(readDb);

        var results = await repo.SearchAsync(_tenantId, Guid.Empty, "Chocolate", null, 10);

        results.Should().ContainSingle(r => r.Sku == "SKU-GALLETA-01");
    }

    // ── c) Búsqueda por barcode real funciona ─────────────────────────

    [Fact]
    public async Task SearchAsync_encuentra_por_codigo_de_barras_real_aunque_no_coincida_con_sku_ni_nombre()
    {
        const string barcode = "7861234567890";

        await using (var db = CreateContext(_tenantId))
        {
            db.Items.Add(
                SeedItem(
                    _tenantId,
                    "ARR-01",
                    "Arroz Superior 1kg",
                    "Arroz blanco superior, funda 1kg",
                    barcode: barcode
                )
            );
            await db.SaveChangesAsync();
        }

        await using var readDb = CreateContext(_tenantId);
        var repo = new InvoiceItemSearchRepository(readDb);

        var results = await repo.SearchAsync(_tenantId, Guid.Empty, barcode, null, 10);

        results.Should().ContainSingle(r => r.Sku == "ARR-01");
    }

    // ── d) Ranking: barcode exacto antes que coincidencias parciales ──

    [Fact]
    public async Task SearchAsync_prioriza_barcode_exacto_sobre_coincidencia_parcial_de_texto()
    {
        const string scannedCode = "7899988877766";

        await using (var db = CreateContext(_tenantId))
        {
            // El código escaneado coincide EXACTO con el barcode de este ítem.
            var exactMatch = SeedItem(
                _tenantId,
                "LECHE-01",
                "Leche Entera 1L",
                "Leche entera pasteurizada",
                barcode: scannedCode
            );
            // Este otro ítem solo coincide PARCIALMENTE (el código escaneado aparece como
            // substring de su descripción) — no debe ganarle al match exacto de barcode.
            var partialMatch = SeedItem(
                _tenantId,
                "PROMO-01",
                "Combo Promocional",
                $"Combo válido con cupón {scannedCode} en caja",
                barcode: null
            );

            db.Items.AddRange(exactMatch, partialMatch);
            await db.SaveChangesAsync();
        }

        await using var readDb = CreateContext(_tenantId);
        var repo = new InvoiceItemSearchRepository(readDb);

        var results = await repo.SearchAsync(_tenantId, Guid.Empty, scannedCode, null, 10);

        results.Should().HaveCount(2);
        results[0].Sku.Should().Be("LECHE-01");
        results[1].Sku.Should().Be("PROMO-01");
    }

    // ── e) No devuelve barcodes de otro tenant ────────────────────────

    [Fact]
    public async Task SearchAsync_no_encuentra_barcode_registrado_en_otro_tenant()
    {
        const string barcode = "7770001112223";

        Guid otherTenantId;
        await using (var db = CreateContext(Guid.Empty))
        {
            var otherTenant = Tenant.Create(
                "Other Tenant",
                $"other-{Guid.NewGuid():N}"[..16],
                _createdBy
            );
            var otherItemType = ItemTypeDefinition.Create(
                otherTenant.Id,
                "PHYSICAL",
                "Fisico",
                1,
                _createdBy
            );
            db.Tenants.Add(otherTenant);
            db.ItemTypes.Add(otherItemType);
            await db.SaveChangesAsync();

            var otherItem = Item.Create(
                otherTenant.Id,
                "OTHER-SKU",
                "Producto de otro tenant",
                "Producto de otro tenant",
                otherItemType.Id,
                "UNIT",
                ItemTaxConfig.Create(null, null),
                ItemSaleConfig.Create(),
                ItemStockConfig.Create(tracksStock: false),
                _createdBy
            );
            var variant = otherItem.AddVariant([], null, 0, _createdBy);
            variant.AddBarcode(barcode, "EAN13", otherTenant.Id, _createdBy, isPrimary: true);
            db.Items.Add(otherItem);
            await db.SaveChangesAsync();

            otherTenantId = otherTenant.Id;
        }

        // Búsqueda ejecutada en el contexto del tenant original (_tenantId), que no tiene
        // ningún ítem con ese código de barras.
        await using var readDb = CreateContext(_tenantId);
        var repo = new InvoiceItemSearchRepository(readDb);

        var results = await repo.SearchAsync(_tenantId, Guid.Empty, barcode, null, 10);

        results.Should().BeEmpty();
        otherTenantId.Should().NotBe(_tenantId); // guarda de sanity del setup
    }

    // ── f) No rompe pageSize ───────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_respeta_pageSize_incluso_con_ranking_por_barcode()
    {
        await using (var db = CreateContext(_tenantId))
        {
            for (var i = 0; i < 5; i++)
                db.Items.Add(
                    SeedItem(
                        _tenantId,
                        $"PAN-0{i}",
                        $"Pan Integral {i}",
                        $"Pan integral variedad {i}"
                    )
                );
            await db.SaveChangesAsync();
        }

        await using var readDb = CreateContext(_tenantId);
        var repo = new InvoiceItemSearchRepository(readDb);

        var results = await repo.SearchAsync(_tenantId, Guid.Empty, "Pan Integral", null, 2);

        results.Should().HaveCount(2);
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId { get; } = tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany : ICurrentCompany
    {
        public Guid CompanyId => Guid.Empty;
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
