using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Services;
using ERP.Application.MasterData.Services;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Testcontainers.PostgreSql;
using PurchaseTaxResolver = ERP.Application.Modules.Purchases.Services.ISriTaxResolver;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

/// <summary>
/// PURCHASE-COST-DISTRIBUTION-CANONICAL-01 — persistencia canónica contra PostgreSQL desechable:
/// reapertura, reintento, reemplazo, concurrencia, guardado que conserva Ids, retiro y aislamiento.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PurchaseCostDistributionPostgreSqlTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_purchase_cost_distribution_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _userId = Guid.NewGuid();
    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _supplierId;
    private Guid _paymentTermId;
    /// <summary>Ítem Product de la empresa: un costo externo solo se asigna a ítems de la compra.</summary>
    private Item _product = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var tenant = Tenant.Create("Cost Tenant", $"cost-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Costos S.A.", createdBy: _userId);
        var branch = Branch.Create(tenant.Id, "Matriz", "Av. Principal 123", "001",
            null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, true, _userId, companyId: company.Id);
        var supplier = BusinessPartner.Create(tenant.Id, TaxIdentification.SriRuc, "1791352688001", 2,
            "Proveedor Costos", _userId);
        var paymentTerm = PaymentTerm.Create(tenant.Id, "CONT", "Contado", 1, 0, _userId);
        var type = ItemTypeDefinition.Create(tenant.Id, "COST-PROD", "COST-PROD", 0, _userId);
        _product = Item.Create(tenant.Id, "COST-PROD", "Producto", "Producto", type.Id, "UNIT",
            ItemTaxConfig.Create("0", "0"), ItemSaleConfig.Create(), ItemStockConfig.Create(stockControlEnabled: true),
            _userId, companyId: company.Id, nature: ItemNature.Product);
        db.AddRange(tenant, company, branch, supplier, paymentTerm, type, _product);
        await db.SaveChangesAsync();
        (_tenantId, _companyId, _branchId, _supplierId, _paymentTermId) =
            (tenant.Id, company.Id, branch.Id, supplier.Id, paymentTerm.Id);
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(Guid? tenantId = null, Guid? companyId = null) =>
        new(new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString())
                .AddInterceptors(new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor())
                .Options,
            new FixedCurrentTenant(tenantId ?? _tenantId), new NoOpPublisher(),
            new FixedCurrentCompany(companyId ?? _companyId));

    private PurchaseInvoiceRepository Repo(ErpDbContext db, Guid? companyId = null) =>
        new(db, new FixedCurrentCompany(companyId ?? _companyId));

    /// <summary>Borrador manual 100 / 200 / 300 sin IVA.</summary>
    private async Task<(Guid InvoiceId, Guid[] LineIds)> SeedAsync()
    {
        await using var db = CreateContext();
        var invoice = PurchaseInvoice.CreateDraft(_tenantId, _companyId, _branchId, _supplierId,
            "Proveedor Costos", "1791352688001", "01", $"001-001-{Random.Shared.Next(1, 999999999):000000000}",
            new DateOnly(2026, 10, 1), _userId, _paymentTermId, "Contado", 1, 0);
        invoice.ReplaceLines(new[] { 100m, 200m, 300m }.Select(price => PurchaseInvoiceDetail.Create(invoice.Id,
            _tenantId, $"Producto {price}", 1, price, "0", "UNIT", itemId: _product.Id)).ToList(), _userId);
        db.PurchaseInvoices.Add(invoice);
        await db.SaveChangesAsync();
        return (invoice.Id, invoice.Lines.Select(l => l.Id).ToArray());
    }

    private ReplacePurchaseCostDistributionHandler ReplaceHandler(ErpDbContext db) =>
        new(Repo(db), Items(), new FixedCurrentTenant(_tenantId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == _branchId), Mock.Of<ICurrentUser>(u => u.UserId == _userId),
            StandardPrecisionPolicyProvider.Instance);

    private IItemRepository Items()
    {
        var items = new Mock<IItemRepository>();
        items.Setup(r => r.GetByIdLightAsync(_product.Id, _tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(_product);
        return items.Object;
    }

    private async Task<Result<PurchaseInvoiceDto>> ReplaceAsync(ReplacePurchaseCostDistributionCommand cmd)
    {
        await using var db = CreateContext();
        return await ReplaceHandler(db).Handle(cmd, default);
    }

    private async Task<PurchaseInvoice> ReopenAsync(Guid invoiceId)
    {
        var db = CreateContext();
        return (await Repo(db).GetByIdAsync(_tenantId, invoiceId))!;
    }

    private static ReplacePurchaseCostDistributionCommand External(Guid invoiceId, Guid id, long revision,
        decimal amount, params Guid[] targets) =>
        new(invoiceId, id, PurchaseCostType.Freight, revision, amount, [.. targets], null);

    [Fact]
    public async Task Reopen_preserves_origin_targets_amounts_and_external_total()
    {
        var (invoiceId, lines) = await SeedAsync();
        var id = Guid.NewGuid();

        (await ReplaceAsync(External(invoiceId, id, 0, 30m, lines[0], lines[1]))).IsSuccess.Should().BeTrue();

        var saved = await ReopenAsync(invoiceId);
        var distribution = saved.CostDistributions.Should().ContainSingle().Subject;
        distribution.Id.Should().Be(id);
        distribution.SourceInvoiceDetailId.Should().BeNull();
        distribution.Revision.Should().Be(1);
        distribution.Allocations.Select(a => (a.TargetInvoiceDetailId, a.Amount))
            .Should().BeEquivalentTo([(lines[0], 10m), (lines[1], 20m)]);
        saved.Lines.Select(l => l.FreightAllocated).Should().Equal(10m, 20m, 0m);
        saved.GrandTotal.Should().Be(630m);
    }

    [Fact]
    public async Task Identical_retry_and_edit_replace_without_adding()
    {
        var (invoiceId, lines) = await SeedAsync();
        var id = Guid.NewGuid();
        var create = External(invoiceId, id, 0, 30m, lines[0], lines[1]);

        (await ReplaceAsync(create)).IsSuccess.Should().BeTrue();
        (await ReplaceAsync(create)).IsSuccess.Should().BeTrue();
        (await ReopenAsync(invoiceId)).TotalFreight.Should().Be(30m);

        (await ReplaceAsync(External(invoiceId, id, 1, 12m, lines[2]))).IsSuccess.Should().BeTrue();

        var saved = await ReopenAsync(invoiceId);
        saved.TotalFreight.Should().Be(12m);
        saved.GrandTotal.Should().Be(612m);
        saved.CostDistributions.Single().Revision.Should().Be(2);
        saved.CostDistributions.Single().Allocations.Should().ContainSingle(a => a.TargetInvoiceDetailId == lines[2]);
        await using var db = CreateContext();
        (await db.Set<PurchaseCostDistributionAllocation>().CountAsync(a => a.InvoiceId == invoiceId)).Should().Be(1);
    }

    [Fact]
    public async Task Concurrent_change_keeps_the_first_and_rejects_the_second()
    {
        var (invoiceId, lines) = await SeedAsync();
        await using var first = CreateContext();
        await using var second = CreateContext();
        var firstHandler = ReplaceHandler(first);
        var secondInvoice = await Repo(second).GetByIdAsync(_tenantId, invoiceId);
        secondInvoice!.Lines.Should().HaveCount(3); // second context loaded before the first commit

        (await firstHandler.Handle(External(invoiceId, Guid.NewGuid(), 0, 10m, lines[0]), default))
            .IsSuccess.Should().BeTrue();
        secondInvoice.ReplaceCostDistribution(new PurchaseCostDistributionChange(Guid.NewGuid(), 0,
            PurchaseCostType.OtherCost, 50m, [lines[1]], null), new Dictionary<Guid, ItemNature> { [_product.Id] = ItemNature.Product }, _userId);
        var stale = () => second.SaveChangesAsync();

        await stale.Should().ThrowAsync<DbUpdateConcurrencyException>();
        var saved = await ReopenAsync(invoiceId);
        saved.CostDistributions.Should().ContainSingle(d => d.CostType == PurchaseCostType.Freight);
        saved.GrandTotal.Should().Be(610m);
    }

    [Fact]
    public async Task Draft_save_keeps_line_ids_and_blocks_removing_referenced_lines()
    {
        var (invoiceId, lines) = await SeedAsync();
        (await ReplaceAsync(External(invoiceId, Guid.NewGuid(), 0, 30m, lines[0], lines[1]))).IsSuccess.Should().BeTrue();

        var kept = await UpdateDraftAsync(invoiceId,
            new PurchaseLineInput(null, "Producto 100 editado", 2, 100, "0", LineId: lines[0]),
            new PurchaseLineInput(null, "Producto 200", 1, 200, "0", LineId: lines[1]),
            new PurchaseLineInput(null, "Producto nuevo", 1, 50, "0"));
        kept.IsSuccess.Should().BeTrue(kept.Error);

        var saved = await ReopenAsync(invoiceId);
        saved.Lines.Select(l => l.Id).Take(2).Should().Equal(lines[0], lines[1]);
        saved.Lines.Select(l => l.FreightAllocated).Should().Equal(15m, 15m, 0m);
        saved.CostDistributions.Single().Revision.Should().Be(2);

        var removed = await UpdateDraftAsync(invoiceId,
            new PurchaseLineInput(null, "Producto 100 editado", 2, 100, "0", LineId: lines[0]));
        removed.IsSuccess.Should().BeFalse();
        removed.Error.Should().Contain("distribución de costos");
        var unchanged = await ReopenAsync(invoiceId);
        unchanged.Lines.Should().HaveCount(3);
        unchanged.Lines.Select(l => l.FreightAllocated).Should().Equal(15m, 15m, 0m);
    }

    [Fact]
    public async Task Remove_deletes_rows_and_reverts_derived_totals()
    {
        var (invoiceId, lines) = await SeedAsync();
        var id = Guid.NewGuid();
        (await ReplaceAsync(External(invoiceId, id, 0, 30m, lines[0]))).IsSuccess.Should().BeTrue();

        await using (var db = CreateContext())
        {
            var handler = new RemovePurchaseCostDistributionHandler(Repo(db), new FixedCurrentTenant(_tenantId),
                Mock.Of<ICurrentBranch>(b => b.BranchId == _branchId), Mock.Of<ICurrentUser>(u => u.UserId == _userId),
                StandardPrecisionPolicyProvider.Instance);
            (await handler.Handle(new RemovePurchaseCostDistributionCommand(invoiceId, id, 1), default))
                .IsSuccess.Should().BeTrue();
        }

        var saved = await ReopenAsync(invoiceId);
        saved.CostDistributions.Should().BeEmpty();
        saved.GrandTotal.Should().Be(600m);
        saved.Lines.Should().OnlyContain(l => l.FreightAllocated == 0 && l.TotalLineCost == l.TaxableBase);
        await using var check = CreateContext();
        (await check.Set<PurchaseCostDistributionAllocation>().IgnoreQueryFilters()
            .CountAsync(a => a.InvoiceId == invoiceId)).Should().Be(0);
    }

    [Fact]
    public async Task Other_tenant_or_company_cannot_read_and_database_rejects_cross_invoice_targets()
    {
        var (invoiceId, lines) = await SeedAsync();
        var (otherInvoiceId, otherLines) = await SeedAsync();
        (await ReplaceAsync(External(invoiceId, Guid.NewGuid(), 0, 30m, lines[0]))).IsSuccess.Should().BeTrue();

        await using (var otherTenant = CreateContext(tenantId: Guid.NewGuid()))
        {
            (await otherTenant.Set<PurchaseCostDistribution>().CountAsync()).Should().Be(0);
            (await otherTenant.Set<PurchaseCostDistributionAllocation>().CountAsync()).Should().Be(0);
        }
        var foreignCompanyId = Guid.NewGuid();
        await using (var otherCompany = CreateContext(companyId: foreignCompanyId))
        {
            (await Repo(otherCompany, foreignCompanyId).GetByIdAsync(_tenantId, invoiceId)).Should().BeNull();
            (await otherCompany.Set<PurchaseCostDistribution>().CountAsync()).Should().Be(0);
        }

        await using var db = CreateContext();
        var distribution = await db.Set<PurchaseCostDistribution>().SingleAsync(d => d.InvoiceId == invoiceId);
        var crossInvoice = () => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE purchase_cost_distribution_allocations SET target_invoice_detail_id = {otherLines[0]} WHERE distribution_id = {distribution.Id}");

        (await crossInvoice.Should().ThrowAsync<Npgsql.PostgresException>()).Which.SqlState.Should().Be("23503");
        (await ReopenAsync(otherInvoiceId)).CostDistributions.Should().BeEmpty();
    }

    private async Task<Result<PurchaseInvoiceDto>> UpdateDraftAsync(Guid invoiceId, params PurchaseLineInput[] lines)
    {
        await using var db = CreateContext();
        var invoice = (await Repo(db).GetByIdAsync(_tenantId, invoiceId))!;
        var supplier = await db.BusinessPartners.SingleAsync(p => p.Id == _supplierId);
        var suppliers = new Mock<IBusinessPartnerRepository>();
        suppliers.Setup(r => r.GetByIdAsync(_supplierId, It.IsAny<CancellationToken>())).ReturnsAsync(supplier);
        var tax = new Mock<PurchaseTaxResolver>();
        tax.Setup(r => r.GetVatRateWithNameAsync("0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxRateResult(0m, "IVA 0%"));
        var handler = new UpdatePurchaseDraftHandler(Repo(db), suppliers.Object, Mock.Of<IPaymentTermDefaultResolver>(),
            Mock.Of<IItemRepository>(), Mock.Of<IWarehouseRepository>(), tax.Object,
            new PurchaseReceptionDocumentRepository(db, new FixedCurrentCompany(_companyId)),
            new FixedCurrentTenant(_tenantId), Mock.Of<ICurrentUser>(u => u.UserId == _userId),
            Mock.Of<IDatabaseExceptionTranslator>(), StandardPrecisionPolicyProvider.Instance, new UnitOfWork(db));
        return await handler.Handle(new UpdatePurchaseDraftCommand(invoiceId, _supplierId, invoice.DocTypeCode,
            invoice.InvoiceNumber, invoice.IssueDate, [.. lines]), default);
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => companyId != Guid.Empty;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
