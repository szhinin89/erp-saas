using ERP.Application.Common;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// FLOW-READY-02C.1 — pruebas de persistencia de <see cref="PurchaseCreditNote"/> contra PostgreSQL
/// real (Testcontainers, sin mocks/in-memory), diseño §5.2: constraints de duplicados
/// (ReceptionDocumentId, AccessKey, SupplierId+CreditNoteNumber) y aislamiento multi-tenant/
/// company/branch de esos mismos índices.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PurchaseCreditNoteConstraintsTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_purchase_credit_note_constraints_test")
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

    /// <summary>
    /// Los query filters fail-closed de <see cref="ErpDbContext"/> exigen un <c>ICurrentTenant</c>
    /// real para cualquier SELECT — por eso cada contexto se crea con el tenant/company al que
    /// pertenece la operación (nunca <c>Guid.Empty</c> salvo para el propio INSERT de
    /// <c>Tenant</c>/<c>Company</c>, que no está sujeto a esos filtros).
    /// </summary>
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

    private async Task<Guid> CreateConfirmedInvoiceAsync(TenantContext ctx)
    {
        await using var db = CreateContext(ctx.TenantId, ctx.CompanyId);
        var inv = PurchaseInvoice.CreateDraft(
            ctx.TenantId,
            ctx.CompanyId,
            ctx.BranchId,
            ctx.SupplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            $"001-001-{Random.Shared.Next(100000, 999999)}",
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
        inv.Confirm(_userId);
        db.PurchaseInvoices.Add(inv);
        await db.SaveChangesAsync();
        return inv.Id;
    }

    private async Task<Guid> CreateReceptionDocumentAsync(TenantContext ctx, string accessKey)
    {
        await using var db = CreateContext(ctx.TenantId, ctx.CompanyId);
        var doc = PurchaseReceptionDocument.Create(
            ctx.TenantId,
            ctx.CompanyId,
            ctx.BranchId,
            PurchaseReceptionSourceDocType.CreditNote,
            supplierRuc: "1710034065001",
            supplierName: "Proveedor Test",
            supplierId: ctx.SupplierId,
            accessKey: accessKey,
            invoiceNumber: "001-001-000000099",
            issueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            authorizationDate: DateTime.UtcNow,
            subtotal: 100m,
            vatAmount: 15m,
            totalAmount: 115m,
            createdBy: _userId
        );
        db.PurchaseReceptionDocuments.Add(doc);
        await db.SaveChangesAsync();
        return doc.Id;
    }

    private static PurchaseCreditNote BuildDraft(
        TenantContext ctx,
        Guid purchaseInvoiceId,
        Guid userId,
        string creditNoteNumber,
        string? accessKey = null,
        Guid? receptionDocumentId = null
    ) =>
        PurchaseCreditNote.CreateDraft(
            ctx.TenantId,
            ctx.CompanyId,
            ctx.BranchId,
            ctx.SupplierId,
            purchaseInvoiceId,
            receptionDocumentId,
            PurchaseCreditNoteApplicationType.Discount,
            creditNoteNumber,
            accessKey,
            authorizationNumber: null,
            authorizationDate: null,
            issueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            reason: "Descuento por promoción",
            lines: new[]
            {
                new PurchaseCreditNote.DraftLineInput("Descuento", 100m, "2", 15m, 15m),
            },
            taxSummaryLines: Array.Empty<PurchaseCreditNote.TaxSummaryDraftLineInput>(),
            userId,
            Guid.NewGuid(),
            $"hash-{Guid.NewGuid():N}"
        );

    [Fact]
    public async Task Duplicar_ReceptionDocumentId_en_el_mismo_tenant_lanza_DbUpdateException()
    {
        var ctx = await SeedTenantAsync();
        var invoiceId1 = await CreateConfirmedInvoiceAsync(ctx);
        var invoiceId2 = await CreateConfirmedInvoiceAsync(ctx);
        var receptionDocumentId = await CreateReceptionDocumentAsync(ctx, $"AK-{Guid.NewGuid():N}");

        await using (var db1 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            db1.PurchaseCreditNotes.Add(
                BuildDraft(
                    ctx,
                    invoiceId1,
                    _userId,
                    "001-001-000000001",
                    receptionDocumentId: receptionDocumentId
                )
            );
            await db1.SaveChangesAsync();
        }

        await using var db2 = CreateContext(ctx.TenantId, ctx.CompanyId);
        db2.PurchaseCreditNotes.Add(
            BuildDraft(
                ctx,
                invoiceId2,
                _userId,
                "001-001-000000002",
                receptionDocumentId: receptionDocumentId
            )
        );
        var act = async () => await db2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Duplicar_AccessKey_en_el_mismo_tenant_lanza_DbUpdateException()
    {
        var ctx = await SeedTenantAsync();
        var invoiceId1 = await CreateConfirmedInvoiceAsync(ctx);
        var invoiceId2 = await CreateConfirmedInvoiceAsync(ctx);
        var accessKey = $"AK-{Guid.NewGuid():N}";

        await using (var db1 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            db1.PurchaseCreditNotes.Add(
                BuildDraft(ctx, invoiceId1, _userId, "001-001-000000003", accessKey: accessKey)
            );
            await db1.SaveChangesAsync();
        }

        await using var db2 = CreateContext(ctx.TenantId, ctx.CompanyId);
        db2.PurchaseCreditNotes.Add(
            BuildDraft(ctx, invoiceId2, _userId, "001-001-000000004", accessKey: accessKey)
        );
        var act = async () => await db2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Duplicar_SupplierId_y_CreditNoteNumber_en_la_misma_empresa_lanza_DbUpdateException()
    {
        var ctx = await SeedTenantAsync();
        var invoiceId1 = await CreateConfirmedInvoiceAsync(ctx);
        var invoiceId2 = await CreateConfirmedInvoiceAsync(ctx);

        await using (var db1 = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            db1.PurchaseCreditNotes.Add(
                BuildDraft(ctx, invoiceId1, _userId, "001-001-000000005")
            );
            await db1.SaveChangesAsync();
        }

        await using var db2 = CreateContext(ctx.TenantId, ctx.CompanyId);
        db2.PurchaseCreditNotes.Add(BuildDraft(ctx, invoiceId2, _userId, "001-001-000000005"));
        var act = async () => await db2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Mismo_AccessKey_y_CreditNoteNumber_en_tenants_distintos_no_colisiona()
    {
        // Fail-closed multi-tenant (§4.1 raíz CLAUDE.md, §1bis del diseño): los índices únicos de
        // PurchaseCreditNote siempre incluyen TenantId — dos tenants distintos nunca chocan entre sí.
        var ctxA = await SeedTenantAsync();
        var ctxB = await SeedTenantAsync();
        var invoiceIdA = await CreateConfirmedInvoiceAsync(ctxA);
        var invoiceIdB = await CreateConfirmedInvoiceAsync(ctxB);
        var accessKey = $"AK-{Guid.NewGuid():N}";

        await using (var dbA = CreateContext(ctxA.TenantId, ctxA.CompanyId))
        {
            dbA.PurchaseCreditNotes.Add(
                BuildDraft(ctxA, invoiceIdA, _userId, "001-001-000000006", accessKey: accessKey)
            );
            await dbA.SaveChangesAsync();
        }

        await using var dbB = CreateContext(ctxB.TenantId, ctxB.CompanyId);
        dbB.PurchaseCreditNotes.Add(
            BuildDraft(ctxB, invoiceIdB, _userId, "001-001-000000006", accessKey: accessKey)
        );
        var act = async () => await dbB.SaveChangesAsync();

        await act.Should().NotThrowAsync();

        await using var verifyA = CreateContext(ctxA.TenantId, ctxA.CompanyId);
        await using var verifyB = CreateContext(ctxB.TenantId, ctxB.CompanyId);
        var countA = await verifyA.PurchaseCreditNotes.CountAsync(c => c.TenantId == ctxA.TenantId);
        var countB = await verifyB.PurchaseCreditNotes.CountAsync(c => c.TenantId == ctxB.TenantId);
        countA.Should().Be(1);
        countB.Should().Be(1);
    }

    [Fact]
    public async Task Crear_y_recuperar_una_PurchaseCreditNote_respeta_TenantId_CompanyId_BranchId()
    {
        var ctx = await SeedTenantAsync();
        var invoiceId = await CreateConfirmedInvoiceAsync(ctx);

        await using (var db = CreateContext(ctx.TenantId, ctx.CompanyId))
        {
            db.PurchaseCreditNotes.Add(
                BuildDraft(ctx, invoiceId, _userId, "001-001-000000007")
            );
            await db.SaveChangesAsync();
        }

        await using var verify = CreateContext(ctx.TenantId, ctx.CompanyId);
        var stored = await verify.PurchaseCreditNotes.SingleAsync(c =>
            c.PurchaseInvoiceId == invoiceId
        );
        stored.TenantId.Should().Be(ctx.TenantId);
        stored.CompanyId.Should().Be(ctx.CompanyId);
        stored.BranchId.Should().Be(ctx.BranchId);
        stored.SupplierId.Should().Be(ctx.SupplierId);
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

    private sealed class NoOpPublisher : MediatR.IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : MediatR.INotification => Task.CompletedTask;
    }
}
