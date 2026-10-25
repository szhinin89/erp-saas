using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Application.MasterData.Services;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Infrastructure.Tests.TestData;
using Moq;
using ERP.Application.Modules.Purchases.Services;
using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Application.Common;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Exceptions;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.SriCatalogs.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

/// <summary>
/// Documentary allocations, atomic draft updates and isolation against disposable PostgreSQL.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PurchaseXmlBreakdownPostgreSqlTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_purchase_breakdown_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _paymentTermId;
    private Guid _supplierId;
    private Guid _createdBy;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _createdBy);
        var branch = Branch.Create(
            tenant.Id, "Matriz", "Av. Principal 123", "001",
            null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null,
            true, _createdBy, companyId: company.Id
        );
        var supplier = BusinessPartner.Create(
            tenant.Id, TaxIdentification.SriRuc, "1791352688001", 2, "Distribuidora Andina S.A.", _createdBy);
        var paymentTerm = PaymentTerm.Create(
            tenant.Id, "CONT", "Contado", installments: 1, daysBetweenInstallments: 0, _createdBy);

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.BusinessPartners.Add(supplier);
        db.PaymentTerms.Add(paymentTerm);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _paymentTermId = paymentTerm.Id;
        _supplierId = supplier.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(
                new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor()
            )
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    private async Task<(Guid InvoiceId, Guid SourceId, Guid OriginalId)> SeedAsync()
    {
        await using var db = CreateContext();
        var inv = PurchaseInvoice.CreateDraft(_tenantId, _companyId, _branchId, _supplierId,
            "Arca Continental", "1791352688001", "01", "029-001-001341832", new DateOnly(2026, 10, 1),
            _createdBy, _paymentTermId, "Contado", 1, 0);
        var source = PurchaseReceptionDocument.Create(_tenantId, _companyId, _branchId,
            PurchaseReceptionSourceDocType.Invoice, inv.SupplierTaxId, inv.SupplierName, _supplierId,
            new string('1', 49), inv.InvoiceNumber, inv.IssueDate, null, 59.74m, 10.31m, 80.52m, _createdBy);
        var original = PurchaseReceptionLine.Create(source.Id, _tenantId, "3sFNT+3sFRESA+3sSP+6sCCSO1.35P",
            5, 11.948m, "4", "2", 15, 10.31m, 0, 0, 59.74m, 80.52m,
            iceCode: "3053", iceValue: 8.97m, supplierCode: "13563", taxes:
            [("2", "4", 15m, 68.71m, 10.31m), ("3", "3053", .18m, 49.83m, 8.97m), ("5", "5001", .02m, 75m, 1.50m)]);
        source.AttachSriAuthorization(source.AccessKey, DateTime.UtcNow, "<factura/>", DateTime.UtcNow,
            [original], _createdBy, "01", null,
            new PurchaseReceptionProcessingOutcome(PurchaseReceptionProcessingStatus.Processed, 1, 1, null));
        db.PurchaseReceptionDocuments.Add(source);
        var itemType = ItemTypeDefinition.Create(_tenantId, "MERCH", "Mercaderia", 1, _createdBy);
        db.Set<ItemTypeDefinition>().Add(itemType);
        var lines = new List<PurchaseInvoiceDetail>();
        foreach (var first in new[] { true, false })
        {
            var item = Item.Create(_tenantId, first ? "FANTA" : "COCA", "Bebida", "Bebida", itemType.Id,
                "UNIT", ItemTaxConfig.Create("4", "4"), ItemSaleConfig.Create(), ItemStockConfig.Create(true),
                _createdBy, companyId: _companyId);
            db.Set<Item>().Add(item);
            var line = PurchaseInvoiceDetail.Create(inv.Id, _tenantId, item.Code.Description,
                first ? 15 : 30, 1, "4", "UNIT", itemId: item.Id, purchaseReceptionLineId: original.Id);
            line.ApplyXmlAllocation(first ? 30 : 29.74m, 0, original.Taxes.Select(t =>
            {
                var baseShare = Math.Floor(t.TaxableBase / 2 * 100) / 100;
                var taxShare = Math.Floor(t.TaxAmount / 2 * 100) / 100;
                return PurchaseInvoiceDetailTax.Create(line.Id, _tenantId, t.TaxCode, t.TaxRateCode,
                    "Documental", t.Tarifa, t.TaxCode == "2" ? SriTaxCalculationType.Percentage : SriTaxCalculationType.Specific,
                    first ? baseShare : t.TaxableBase - baseShare, first ? taxShare : t.TaxAmount - taxShare,
                    PurchaseTaxSource.XmlAllocation, t.Id);
            }).ToList());
            lines.Add(line);
        }
        inv.ReplaceLines(lines, _createdBy);
        db.PurchaseInvoices.Add(inv);
        await db.SaveChangesAsync();
        return (inv.Id, source.Id, original.Id);
    }

    private async Task<PurchaseInvoice> ReopenAsync(ErpDbContext db, Guid id) =>
        (await new PurchaseInvoiceRepository(db, new FixedCurrentCompany(_companyId)).GetByIdAsync(_tenantId, id))!;

    [Theory]
    [InlineData("valid")]
    [InlineData("mismatch")]
    [InlineData("foreign_product")]
    public async Task Update_handler_saves_complete_group_or_preserves_original_draft(string scenario)
    {
        var (id, _, _) = await SeedAsync();
        Guid[] originalIds;
        await using (var db = CreateContext())
        {
            var inv = await ReopenAsync(db, id);
            originalIds = inv.Lines.Select(l => l.Id).ToArray();
            var supplier = await db.BusinessPartners.SingleAsync(p => p.Id == _supplierId);
            var supplierRepo = new Mock<IBusinessPartnerRepository>();
            supplierRepo.Setup(r => r.GetByIdAsync(_supplierId, It.IsAny<CancellationToken>())).ReturnsAsync(supplier);
            var items = await db.Set<Item>().ToListAsync();
            var itemRepo = new Mock<IItemRepository>();
            itemRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), _tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid itemId, Guid tenantId, CancellationToken ct) => items.Single(i => i.Id == itemId));
            if (scenario == "foreign_product")
                typeof(Item).GetProperty("CompanyId")!.SetValue(items[0], Guid.NewGuid());
            var resolver = new Mock<ISriTaxResolver>();
            resolver.Setup(r => r.GetVatCatalogEntryAsync("4", It.IsAny<CancellationToken>())).ReturnsAsync(
                new ERP.Application.Common.Services.SriTaxCatalogEntry("4", "IVA", 15, null, SriTaxCalculationType.Percentage));
            resolver.Setup(r => r.GetXmlIceCatalogEntryAsync("3053", It.IsAny<CancellationToken>())).ReturnsAsync(
                new ERP.Application.Common.Services.SriTaxCatalogEntry("3053", "ICE", null, .18m, SriTaxCalculationType.Specific));
            resolver.Setup(r => r.GetIrbpnrCatalogEntryAsync("5001", It.IsAny<CancellationToken>())).ReturnsAsync(
                new ERP.Application.Common.Services.SriTaxCatalogEntry("5001", "IRBPNR", null, .02m, SriTaxCalculationType.Specific));
            var inputs = inv.Lines.Select(l => new PurchaseLineInput(l.ItemId, l.Description, l.Quantity + 1,
                l.UnitPrice, l.VatCode, PurchaseReceptionLineId: l.PurchaseReceptionLineId,
                XmlAllocation: new PurchaseXmlAllocationInput(l.LineSubtotal + (scenario == "mismatch" ? 1 : 0),
                    l.DiscountAmount, l.Taxes.Select(t => new PurchaseXmlTaxAllocationInput(
                        t.PurchaseReceptionLineTaxId!.Value, t.TaxableBase, t.TaxAmount)).ToList()))).ToList();
            var handler = new UpdatePurchaseDraftHandler(
                new PurchaseInvoiceRepository(db, new FixedCurrentCompany(_companyId)), supplierRepo.Object,
                Mock.Of<IPaymentTermDefaultResolver>(), itemRepo.Object, Mock.Of<IWarehouseRepository>(), resolver.Object,
                new PurchaseReceptionDocumentRepository(db, new FixedCurrentCompany(_companyId)),
                new FixedCurrentTenant(_tenantId), Mock.Of<ICurrentUser>(u => u.UserId == _createdBy),
                Mock.Of<IDatabaseExceptionTranslator>(), StandardPrecisionPolicyProvider.Instance, new UnitOfWork(db));
            var result = await handler.Handle(new UpdatePurchaseDraftCommand(id, _supplierId, inv.DocTypeCode,
                inv.InvoiceNumber, inv.IssueDate, inputs), default);
            result.IsSuccess.Should().Be(scenario == "valid", result.Error);
        }
        await using var reopened = CreateContext();
        var saved = await ReopenAsync(reopened, id);
        saved.Lines.Should().HaveCount(2);
        saved.GrandTotal.Should().Be(80.52m);
        saved.TotalCostValue.Should().Be(59.74m);
        if (scenario != "valid") saved.Lines.Select(l => l.Id).Should().BeEquivalentTo(originalIds);
        else saved.Lines.Sum(l => l.Quantity).Should().Be(47);
        var receptions = new PurchaseReceptionDocumentRepository(reopened, new FixedCurrentCompany(_companyId));
        (await PurchaseXmlBreakdown.ValidateAsync(saved, saved.Lines, receptions, default)).Should().BeNull();
    }

    [Fact]
    public async Task Roundtrip_preserves_allocations_and_documentary_source()
    {
        var (id, sourceId, originalId) = await SeedAsync();
        await using var db = CreateContext();
        var inv = await ReopenAsync(db, id);
        var receptions = new PurchaseReceptionDocumentRepository(db, new FixedCurrentCompany(_companyId));
        (await PurchaseXmlBreakdown.ValidateAsync(inv, inv.Lines, receptions, default)).Should().BeNull();
        inv.Lines.Should().HaveCount(2);
        inv.GrandTotal.Should().Be(80.52m);
        inv.TotalCostValue.Should().Be(59.74m);
        inv.Lines.Should().OnlyContain(l => l.IsXmlBreakdown && l.PurchaseReceptionLineId == originalId);
        inv.Lines.SelectMany(l => l.Taxes).Should().OnlyContain(t => t.PurchaseReceptionLineTaxId != null);
        foreach (var line in inv.Lines)
            line.ApplyTaxes("0", 0, "Changed", null, 0, null);
        await db.SaveChangesAsync();
        var source = (await receptions.GetByIdAsync(_tenantId, sourceId))!;
        source.XmlContent.Should().Be("<factura/>");
        source.Lines.Single().Quantity.Should().Be(5);
        source.Lines.Single().Taxes.Sum(t => t.TaxAmount).Should().Be(20.78m);
    }

    [Fact]
    public async Task Failure_after_replacing_lines_rolls_back_entire_draft()
    {
        var (id, _, _) = await SeedAsync();
        Guid[] originalIds;
        await using (var db = CreateContext())
        {
            var inv = await ReopenAsync(db, id);
            originalIds = inv.Lines.Select(l => l.Id).ToArray();
            var repo = new PurchaseInvoiceRepository(db, new FixedCurrentCompany(_companyId));
            var uow = new UnitOfWork(db);
            var action = async () => await uow.ExecuteInTransactionAsync(async ct =>
            {
                await repo.RemoveLinesByInvoiceAsync(inv.Id, [], ct);
                inv.ReplaceLines([], _createdBy);
                await db.SaveChangesAsync(ct);
                throw new InvalidOperationException("Injected persistence failure");
            });
            await action.Should().ThrowAsync<InvalidOperationException>();
        }
        await using var reopened = CreateContext();
        var saved = await ReopenAsync(reopened, id);
        saved.Lines.Select(l => l.Id).Should().BeEquivalentTo(originalIds);
        saved.GrandTotal.Should().Be(80.52m);
    }

    [Fact]
    public async Task Foreign_company_and_tenant_cannot_resolve_source_or_invoice()
    {
        var (id, sourceId, originalId) = await SeedAsync();
        await using var db = CreateContext();
        var otherCompany = new FixedCurrentCompany(Guid.NewGuid());
        var repo = new PurchaseReceptionDocumentRepository(db, otherCompany);
        (await repo.GetByIdAsync(_tenantId, sourceId)).Should().BeNull();
        (await repo.GetByLineIdAsync(_tenantId, originalId)).Should().BeNull();
        (await new PurchaseInvoiceRepository(db, otherCompany).GetByIdAsync(_tenantId, id)).Should().BeNull();
        (await new PurchaseReceptionDocumentRepository(db, new FixedCurrentCompany(_companyId))
            .GetByLineIdAsync(Guid.NewGuid(), originalId)).Should().BeNull();
    }

    [Fact]
    public async Task Duplicate_source_tax_in_same_component_is_rejected_by_database()
    {
        var (id, _, _) = await SeedAsync();
        await using var db = CreateContext();
        var inv = await ReopenAsync(db, id);
        var line = inv.Lines[0]; var tax = line.Taxes[0];
        db.Set<PurchaseInvoiceDetailTax>().Add(PurchaseInvoiceDetailTax.Create(line.Id, _tenantId,
            tax.TaxCode, tax.TaxRateCode, tax.TaxName, tax.Rate, tax.CalculationType,
            tax.TaxableBase, tax.TaxAmount, tax.Source, tax.PurchaseReceptionLineTaxId));
        var action = async () => await db.SaveChangesAsync();
        await action.Should().ThrowAsync<DbUpdateException>();
        await using var reopened = CreateContext();
        (await ReopenAsync(reopened, id)).GrandTotal.Should().Be(80.52m);
    }

    [Theory]
    [InlineData(2, "equal")]
    [InlineData(6, "unequal")]
    [InlineData(10, "zero")]
    [InlineData(10, "discount")]
    public async Task Cost_distribution_preserves_six_decimal_allocations_on_reopen(int unitCostDecimals, string scenario)
    {
        Guid id;
        decimal[] expected = scenario == "unequal"
            ? [0.142857m, 0.285714m, 0.571429m]
            : [0.333333m, 0.333333m, 0.333334m];
        Guid[] lineIds;
        decimal totalBefore;
        await using (var db = CreateContext())
        {
            var invoice = PurchaseInvoice.CreateDraft(_tenantId, _companyId, _branchId, _supplierId,
                "Proveedor test", "1791352688001", "01", "001-001-000000099", new DateOnly(2026, 10, 1),
                _createdBy, _paymentTermId, "Contado", 1, 0);
            var prices = scenario == "unequal" ? new[] { 1m, 2m, 4m }
                : scenario == "zero" ? new[] { 0m, 0m, 0m } : new[] { 1m, 1m, 1m };
            // Un costo externo solo se asigna a ítems de la compra (PURCHASE-COST-DISTRIBUTION-CANONICAL-01).
            var code = $"PR-{Guid.NewGuid():N}"[..16];
            var itemType = ItemTypeDefinition.Create(_tenantId, code, code, 1, _createdBy);
            var item = Item.Create(_tenantId, code, "Producto", "Producto", itemType.Id, "UNIT",
                ItemTaxConfig.Create("0", "0"), ItemSaleConfig.Create(), ItemStockConfig.Create(true),
                _createdBy, companyId: _companyId);
            db.Set<ItemTypeDefinition>().Add(itemType);
            db.Set<Item>().Add(item);
            var lines = prices.Select(price => PurchaseInvoiceDetail.Create(invoice.Id, _tenantId,
                "Producto test", 3, price / 3, "0", "UNIT", itemId: item.Id)).ToList();
            invoice.ReplaceLines(lines, _createdBy);
            if (scenario == "discount") invoice.ApplyGlobalDiscount(100, _createdBy);
            invoice.ApplyUnitCostPrecision(unitCostDecimals);
            // Equal bases are exact multiples of the same decimal, so each ratio is 1/3.
            lineIds = lines.Select(l => l.Id).ToArray();
            totalBefore = invoice.GrandTotal;
            invoice.Distribute(PurchaseCostType.Freight, 1m, lineIds, _createdBy);
            invoice.Lines.Select(l => l.FreightAllocated).Should().Equal(expected);
            invoice.Lines.Sum(l => l.FreightAllocated).Should().Be(1m);
            db.PurchaseInvoices.Add(invoice);
            await db.SaveChangesAsync();
            id = invoice.Id;
        }
        await using var reopened = CreateContext();
        var saved = await ReopenAsync(reopened, id);
        saved.ApplyUnitCostPrecision(unitCostDecimals);
        var ordered = lineIds.Select(lineId => saved.Lines.Single(l => l.Id == lineId)).ToArray();
        ordered.Select(l => l.FreightAllocated).Should().Equal(expected);
        ordered.Sum(l => l.FreightAllocated).Should().Be(1m);
        saved.GrandTotal.Should().Be(totalBefore + 1m);
        ordered.Should().OnlyContain(l => l.VatAmount == 0 && l.IceAmount == 0 && l.IrbpnrAmount == 0);
        foreach (var line in ordered)
            line.LandedUnitCost.Should().Be(Math.Round(line.TotalLineCost / line.QuantityInBaseUom,
                unitCostDecimals, MidpointRounding.AwayFromZero));
        await reopened.SaveChangesAsync();
        await using var again = CreateContext();
        var unchanged = await ReopenAsync(again, id);
        unchanged.TotalFreight.Should().Be(1m);
        unchanged.GrandTotal.Should().Be(saved.GrandTotal);
    }

    [Fact]
    public async Task Cost_distribution_preserves_xml_and_tax_snapshots_on_reopen()
    {
        var (id, sourceId, _) = await SeedAsync();
        Dictionary<Guid, (decimal Base, decimal Amount)> taxes;
        await using (var db = CreateContext())
        {
            var invoice = await ReopenAsync(db, id);
            taxes = invoice.Lines.SelectMany(l => l.Taxes)
                .ToDictionary(t => t.Id, t => (t.TaxableBase, t.TaxAmount));
            invoice.Distribute(PurchaseCostType.Freight, 1m,
                invoice.Lines.Select(l => l.Id).ToArray(), _createdBy);
            await db.SaveChangesAsync();
        }
        await using var reopened = CreateContext();
        var saved = await ReopenAsync(reopened, id);
        saved.Lines.SelectMany(l => l.Taxes).ToDictionary(t => t.Id, t => (t.TaxableBase, t.TaxAmount))
            .Should().BeEquivalentTo(taxes);
        saved.TotalFreight.Should().Be(1m);
        saved.GrandTotal.Should().Be(81.52m); // External cost: original 80.52 + 1.
        var source = (await new PurchaseReceptionDocumentRepository(reopened, new FixedCurrentCompany(_companyId))
            .GetByIdAsync(_tenantId, sourceId))!;
        source.XmlContent.Should().Be("<factura/>");
        source.Lines.Single().Taxes.Sum(t => t.TaxAmount).Should().Be(20.78m);
        (await PurchaseXmlBreakdown.ValidateAsync(saved, saved.Lines,
            new PurchaseReceptionDocumentRepository(reopened, new FixedCurrentCompany(_companyId)), default))
            .Should().BeNull();
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
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }
}
