using ERP.Application.Common;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// PURCHASE-CREDIT-NOTE-AFFECTED-INVOICE-RESOLVES-CANCELLED-01 — pruebas de integración
/// (PostgreSQL 16 real vía Testcontainers) para <see cref="PurchaseInvoiceRepository.GetBySupplierAndInvoiceNumberAsync"/>,
/// el método que <c>PurchaseReceptionVerifier</c> usa para resolver <c>affectedPurchaseId</c> (la
/// factura afectada que habilita "Procesar NC" desde Recepción). Caso reportado: compra desde
/// recepción → anulada → misma recepción reprocesada → nueva compra Confirmed con el mismo
/// proveedor+número → "Procesar NC" seguía resolviendo la compra Cancelled vieja porque el método
/// no excluía Cancelled ni tenía ningún criterio determinista — corregido excluyendo Cancelled
/// (el índice único parcial de <c>purchase_invoices</c> ya garantiza que, excluyendo Cancelled,
/// hay a lo sumo una fila activa por proveedor+número, sin ambigüedad posible). Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PurchaseInvoiceAffectedInvoiceResolutionTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_purchase_invoice_affected_invoice_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _userId = Guid.NewGuid();

    private sealed record TenantContext(
        Guid TenantId,
        Guid CompanyId,
        Guid BranchId,
        Guid SupplierId,
        Guid PaymentTermId,
        Guid WarehouseId,
        Guid ItemId
    );

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(Guid tenantId = default, Guid companyId = default)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new ErpDbContext(
            options,
            new FixedCurrentTenant(() => tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(() => companyId)
        );
    }

    private async Task<TenantContext> SeedTenantAsync()
    {
        await using var db = CreateContext();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(
            tenant.Id,
            $"17{Random.Shared.Next(10000000, 99999999)}001",
            "Test S.A.",
            createdBy: _userId
        );
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var branch = Branch.Create(
            tenantId: tenant.Id,
            name: "Matriz",
            address: "Av. Principal 123",
            code: "B01",
            description: null,
            reference: null,
            postalCode: null,
            phone: null,
            secondaryPhone: null,
            email: null,
            website: null,
            managerName: null,
            managerPosition: null,
            managerEmail: null,
            managerPhone: null,
            countryId: null,
            provinceId: null,
            cantonId: null,
            parishId: null,
            latitude: null,
            longitude: null,
            openingDate: null,
            internalNotes: null,
            isMainBranch: true,
            createdBy: _userId,
            companyId: company.Id
        );
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var supplier = BusinessPartner.Create(
            tenant.Id,
            "05",
            "1710034065",
            1,
            "Proveedor Test",
            _userId
        );
        var paymentTerm = PaymentTerm.Create(
            tenant.Id,
            "CONT",
            "Contado",
            installments: 1,
            daysBetweenInstallments: 0,
            _userId
        );
        var warehouse = Warehouse.Create(
            tenant.Id,
            branch.Id,
            "Bodega Principal",
            "BOD-01",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _userId,
            company.Id,
            isMain: true
        );
        db.BusinessPartners.Add(supplier);
        db.Add(paymentTerm);
        db.Add(warehouse);
        await db.SaveChangesAsync();

        var itemType = ItemTypeDefinition.Create(tenant.Id, "MERCH", "Mercadería", 1, _userId);
        db.Set<ItemTypeDefinition>().Add(itemType);
        await db.SaveChangesAsync();

        var item = Item.Create(
            tenant.Id,
            sku: $"SKU-{Guid.NewGuid():N}"[..12],
            shortName: "Producto Test",
            description: "Producto Test",
            itemTypeId: itemType.Id,
            defaultUomCode: "UNIT",
            taxConfig: ItemTaxConfig.Create(saleVatCode: "10", purchaseVatCode: "10"),
            saleConfig: ItemSaleConfig.Create(isForSale: true),
            stockConfig: ItemStockConfig.Create(tracksStock: true),
            createdBy: _userId
        );
        db.Set<Item>().Add(item);
        await db.SaveChangesAsync();

        return new TenantContext(
            tenant.Id,
            company.Id,
            branch.Id,
            supplier.Id,
            paymentTerm.Id,
            warehouse.Id,
            item.Id
        );
    }

    private PurchaseInvoice BuildConfirmedInvoice(TenantContext ctx, string invoiceNumber) =>
        BuildInvoice(ctx, invoiceNumber, confirm: true);

    private PurchaseInvoice BuildInvoice(TenantContext ctx, string invoiceNumber, bool confirm)
    {
        var inv = PurchaseInvoice.CreateDraft(
            ctx.TenantId,
            ctx.CompanyId,
            ctx.BranchId,
            ctx.SupplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            invoiceNumber,
            DateOnly.FromDateTime(DateTime.UtcNow),
            _userId,
            ctx.PaymentTermId,
            "Contado",
            1,
            30,
            globalWarehouseId: ctx.WarehouseId
        );
        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            ctx.TenantId,
            "Producto 1",
            quantity: 10,
            unitPrice: 10.00m,
            vatCode: "10",
            uomCode: "UNIT",
            itemId: ctx.ItemId,
            warehouseId: ctx.WarehouseId
        );
        inv.ReplaceLines(new[] { line }, _userId);
        if (confirm)
            inv.Confirm(_userId);
        return inv;
    }

    [Fact]
    public async Task Compra_anulada_y_reprocesada_con_el_mismo_numero_resuelve_la_Confirmed_nueva_no_la_Cancelled_vieja()
    {
        var ctx = await SeedTenantAsync();
        const string invoiceNumber = "001-001-000010500";

        Guid cancelledId;
        await using (var db1 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var first = BuildConfirmedInvoice(ctx, invoiceNumber);
            db1.PurchaseInvoices.Add(first);
            await db1.SaveChangesAsync();
            cancelledId = first.Id;
        }
        await using (var dbCancel = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var first = await dbCancel.PurchaseInvoices.SingleAsync(x => x.Id == cancelledId);
            first.Cancel("Anulada por error", _userId);
            await dbCancel.SaveChangesAsync();
        }

        // "Reprocesar la misma recepción" — el mismo proveedor+número vuelve a confirmarse como
        // una compra NUEVA (el índice único filtrado permite esto justamente porque la primera ya
        // está Cancelled).
        Guid confirmedId;
        await using (var db2 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var second = BuildConfirmedInvoice(ctx, invoiceNumber);
            db2.PurchaseInvoices.Add(second);
            await db2.SaveChangesAsync();
            confirmedId = second.Id;
        }

        var repo = new PurchaseInvoiceRepository(
            CreateContext(ctx.TenantId, ctx.CompanyId),
            new FixedCurrentCompany(() => ctx.CompanyId)
        );

        var resolved = await repo.GetBySupplierAndInvoiceNumberAsync(
            ctx.TenantId,
            ctx.SupplierId,
            invoiceNumber
        );

        resolved.Should().NotBeNull();
        resolved!.Id.Should().Be(
            confirmedId,
            because: "la NC nueva debe apuntar a la compra Confirmed activa, nunca a la Cancelled histórica"
        );
        resolved.Id.Should().NotBe(cancelledId);
        resolved.Status.Should().Be(PurchaseStatus.Confirmed);
    }

    [Fact]
    public async Task Si_solo_existe_una_compra_Cancelled_con_ese_numero_no_resuelve_ninguna_factura_afectada()
    {
        var ctx = await SeedTenantAsync();
        const string invoiceNumber = "001-001-000010501";

        await using (var db = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var only = BuildConfirmedInvoice(ctx, invoiceNumber);
            db.PurchaseInvoices.Add(only);
            await db.SaveChangesAsync();

            var reloaded = await db.PurchaseInvoices.SingleAsync(x => x.Id == only.Id);
            reloaded.Cancel("Anulada por error", _userId);
            await db.SaveChangesAsync();
        }

        var repo = new PurchaseInvoiceRepository(
            CreateContext(ctx.TenantId, ctx.CompanyId),
            new FixedCurrentCompany(() => ctx.CompanyId)
        );

        var resolved = await repo.GetBySupplierAndInvoiceNumberAsync(
            ctx.TenantId,
            ctx.SupplierId,
            invoiceNumber
        );

        resolved.Should().BeNull(
            because: "una compra Cancelled es historial — nunca debe habilitar \"Procesar NC\" como si estuviera activa"
        );
    }

    [Fact]
    public async Task Con_una_compra_Draft_y_otra_Cancelled_resuelve_la_Draft_activa_no_la_historica()
    {
        // Defensa adicional: el filtro excluye Cancelled, no restringe a Confirmed — cualquier
        // estado activo (Draft/Confirmed) puede resolverse aquí; es CreateDraftPurchaseCreditNoteHandler
        // quien ya valida "Solo se pueden registrar notas de crédito sobre facturas de compra
        // confirmadas" con el mensaje correcto si la resuelta no está Confirmed.
        var ctx = await SeedTenantAsync();
        const string invoiceNumber = "001-001-000010502";

        Guid cancelledId;
        await using (var db1 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var cancelled = BuildConfirmedInvoice(ctx, invoiceNumber);
            db1.PurchaseInvoices.Add(cancelled);
            await db1.SaveChangesAsync();
            cancelledId = cancelled.Id;
        }
        await using (var dbCancel = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var cancelled = await dbCancel.PurchaseInvoices.SingleAsync(x => x.Id == cancelledId);
            cancelled.Cancel("Anulada por error", _userId);
            await dbCancel.SaveChangesAsync();
        }

        Guid draftId;
        await using (var db2 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            var draft = BuildInvoice(ctx, invoiceNumber, confirm: false);
            db2.PurchaseInvoices.Add(draft);
            await db2.SaveChangesAsync();
            draftId = draft.Id;
        }

        var repo = new PurchaseInvoiceRepository(
            CreateContext(ctx.TenantId, ctx.CompanyId),
            new FixedCurrentCompany(() => ctx.CompanyId)
        );

        var resolved = await repo.GetBySupplierAndInvoiceNumberAsync(
            ctx.TenantId,
            ctx.SupplierId,
            invoiceNumber
        );

        resolved.Should().NotBeNull();
        resolved!.Id.Should().Be(draftId);
        resolved.Status.Should().Be(PurchaseStatus.Draft);
    }

    // ── Test doubles mínimos ─────────────────────────────────────────────

    private sealed class FixedCurrentTenant(Func<Guid> tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId();
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Func<Guid> companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId();
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
