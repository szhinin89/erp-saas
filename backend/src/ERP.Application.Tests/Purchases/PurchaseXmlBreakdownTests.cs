using ERP.Domain.MasterData.ValueObjects;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.MasterData.Services;
using ERP.Application.Tests.TestSupport;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Purchases.Services;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using ERP.Domain.Modules.SriCatalogs.Enums;
using FluentAssertions;
using Moq;
using Resolver = ERP.Application.Modules.Purchases.Services.ISriTaxResolver;

namespace ERP.Application.Tests.Purchases;

public sealed class PurchaseXmlBreakdownTests
{
    private const string Xml = """
        <factura><infoTributaria><ruc>1790012345001</ruc><razonSocial>Arca Continental</razonSocial>
        <codDoc>01</codDoc><estab>029</estab><ptoEmi>001</ptoEmi><secuencial>001341832</secuencial>
        </infoTributaria><infoFactura><fechaEmision>01/10/2026</fechaEmision>
        <totalSinImpuestos>59.74</totalSinImpuestos><importeTotal>80.52</importeTotal><totalConImpuestos>
        <totalImpuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><baseImponible>68.71</baseImponible><valor>10.31</valor></totalImpuesto>
        <totalImpuesto><codigo>3</codigo><codigoPorcentaje>3053</codigoPorcentaje><baseImponible>49.83</baseImponible><valor>8.97</valor></totalImpuesto>
        <totalImpuesto><codigo>5</codigo><codigoPorcentaje>5001</codigoPorcentaje><baseImponible>75</baseImponible><valor>1.50</valor></totalImpuesto>
        </totalConImpuestos></infoFactura>
        <detalles><detalle><codigoPrincipal>13563</codigoPrincipal><descripcion>3sFNT+3sFRESA+3sSP+6sCCSO1.35P</descripcion>
        <cantidad>5</cantidad><precioUnitario>11.948</precioUnitario><descuento>0</descuento>
        <precioTotalSinImpuesto>59.74</precioTotalSinImpuesto><impuestos>
        <impuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><tarifa>15</tarifa><baseImponible>68.71</baseImponible><valor>10.31</valor></impuesto>
        <impuesto><codigo>3</codigo><codigoPorcentaje>3053</codigoPorcentaje><tarifa>0.18</tarifa><baseImponible>49.83</baseImponible><valor>8.97</valor></impuesto>
        <impuesto><codigo>5</codigo><codigoPorcentaje>5001</codigoPorcentaje><tarifa>0.02</tarifa><baseImponible>75</baseImponible><valor>1.50</valor></impuesto>
        </impuestos></detalle></detalles></factura>
        """;

    private static (PurchaseInvoice Invoice, PurchaseReceptionDocument Source, PurchaseReceptionLine Original,
        Mock<IPurchaseReceptionDocumentRepository> Repo, Mock<Resolver> Tax) Fixture(bool foreignCompany = false, decimal discount = 0)
    {
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid(); var branch = Guid.NewGuid();
        var user = Guid.NewGuid(); var supplier = Guid.NewGuid();
        var invoice = PurchaseInvoice.CreateDraft(tenant, company, branch, supplier, "Arca Continental",
            "1790012345001", "01", "029-001-001341832", new DateOnly(2026, 10, 1), user,
            Guid.NewGuid(), "Contado", 1, 0, globalWarehouseId: Guid.NewGuid());
        var source = PurchaseReceptionDocument.Create(tenant, foreignCompany ? Guid.NewGuid() : company,
            branch, PurchaseReceptionSourceDocType.Invoice, invoice.SupplierTaxId, invoice.SupplierName,
            supplier, new string('1', 49), invoice.InvoiceNumber, invoice.IssueDate, null, 59.74m, 10.31m, 80.52m, user);
        var original = PurchaseReceptionLine.Create(source.Id, tenant, "3sFNT+3sFRESA+3sSP+6sCCSO1.35P",
            5, 11.948m + discount / 5, "4", "2", 15, 10.31m, 0, discount, 59.74m, 80.52m,
            iceCode: "3053", iceValue: 8.97m, supplierCode: "13563", taxes:
            [("2", "4", 15m, 68.71m, 10.31m), ("3", "3053", .18m, 49.83m, 8.97m), ("5", "5001", .02m, 75m, 1.50m)]);
        source.AttachSriAuthorization(source.AccessKey, DateTime.UtcNow, Xml, DateTime.UtcNow,
            [original], user, "01", null,
            new PurchaseReceptionProcessingOutcome(PurchaseReceptionProcessingStatus.Processed, 1, 1, null));
        var repo = new Mock<IPurchaseReceptionDocumentRepository>();
        repo.Setup(r => r.GetByLineIdAsync(tenant, original.Id, It.IsAny<CancellationToken>())).ReturnsAsync(source);
        var tax = new Mock<Resolver>();
        tax.Setup(t => t.GetVatCatalogEntryAsync("4", It.IsAny<CancellationToken>())).ReturnsAsync(
            new SriTaxCatalogEntry("4", "IVA", 15, null, SriTaxCalculationType.Percentage));
        tax.Setup(t => t.GetXmlIceCatalogEntryAsync("3053", It.IsAny<CancellationToken>())).ReturnsAsync(
            new SriTaxCatalogEntry("3053", "ICE", null, .18m, SriTaxCalculationType.Specific));
        tax.Setup(t => t.GetIrbpnrCatalogEntryAsync("5001", It.IsAny<CancellationToken>())).ReturnsAsync(
            new SriTaxCatalogEntry("5001", "IRBPNR", null, .02m, SriTaxCalculationType.Specific));
        return (invoice, source, original, repo, tax);
    }

    private static async Task<List<PurchaseInvoiceDetail>> Components(PurchaseInvoice invoice,
        PurchaseReceptionLine original, Mock<IPurchaseReceptionDocumentRepository> repo, Mock<Resolver> tax)
    {
        var result = new List<PurchaseInvoiceDetail>();
        foreach (var first in new[] { true, false })
        {
            var line = PurchaseInvoiceDetail.Create(invoice.Id, invoice.TenantId, "Producto real", first ? 15 : 30,
                1, "4", "UNIT", itemId: Guid.NewGuid(), purchaseReceptionLineId: original.Id);
            var input = new PurchaseXmlAllocationInput((first ? 30 : 29.74m) + original.Discount / 2, original.Discount / 2,
                original.Taxes.Select(t => new PurchaseXmlTaxAllocationInput(t.Id,
                    first ? decimal.Floor(t.TaxableBase / 2 * 100) / 100 : t.TaxableBase - decimal.Floor(t.TaxableBase / 2 * 100) / 100,
                    first ? decimal.Floor(t.TaxAmount / 2 * 100) / 100 : t.TaxAmount - decimal.Floor(t.TaxAmount / 2 * 100) / 100)).ToList());
            (await PurchaseXmlBreakdown.ApplyAsync(line, input, repo.Object, tax.Object, default)).Should().BeNull();
            result.Add(line);
        }
        return result;
    }

    [Fact]
    public async Task Create_handler_persists_only_components_and_returns_documentary_allocations()
    {
        var (invoice, source, original, receptionRepo, tax) = Fixture();
        receptionRepo.Setup(r => r.GetByAccessKeyAsync(invoice.TenantId, source.AccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(source);
        var allocated = await Components(invoice, original, receptionRepo, tax);
        var items = allocated.Select((line, index) => Item.Create(invoice.TenantId, $"SKU-{index}", "Bebida", "Bebida",
            Guid.NewGuid(), "UNIT", ItemTaxConfig.Create("4", "4"), ItemSaleConfig.Create(), ItemStockConfig.Create(true),
            Guid.NewGuid(), companyId: invoice.CompanyId)).ToArray();
        var itemRepo = new Mock<IItemRepository>();
        itemRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), invoice.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, Guid tenant, CancellationToken ct) => items.Single(i => i.Id == id));
        var supplier = BusinessPartner.Create(invoice.TenantId, "04", invoice.SupplierTaxId, 2, "Arca Continental", Guid.NewGuid());
        var suppliers = new Mock<IBusinessPartnerRepository>();
        suppliers.Setup(r => r.GetByIdAsync(invoice.SupplierId, It.IsAny<CancellationToken>())).ReturnsAsync(supplier);
        var roles = new Mock<IBusinessPartnerRoleRepository>();
        roles.Setup(r => r.GetByTypeAsync(invoice.SupplierId, ERP.Domain.MasterData.Enums.RoleType.Supplier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessPartnerRole.Create(invoice.TenantId, invoice.SupplierId, ERP.Domain.MasterData.Enums.RoleType.Supplier,
                Guid.NewGuid(), supplierConfig: SupplierRoleConfig.Create()));
        var terms = new Mock<IPaymentTermDefaultResolver>();
        terms.Setup(r => r.ResolveForPurchaseAsync(invoice.SupplierId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentTerm>.Success(PaymentTerm.Create(invoice.TenantId, "CONT", "Contado", 1, 0, Guid.NewGuid())));
        var repo = new Mock<IPurchaseInvoiceRepository>();
        PurchaseInvoice? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<PurchaseInvoice>(), It.IsAny<CancellationToken>()))
            .Callback<PurchaseInvoice, CancellationToken>((inv, ct) => saved = inv).Returns(Task.CompletedTask);
        var handler = new CreatePurchaseDraftHandler(repo.Object, suppliers.Object, roles.Object, terms.Object,
            itemRepo.Object, Mock.Of<IWarehouseRepository>(), tax.Object, receptionRepo.Object,
            Mock.Of<IExpenseDocumentRepository>(), Mock.Of<ICurrentTenant>(t => t.TenantId == invoice.TenantId),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == invoice.CompanyId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == invoice.BranchId), Mock.Of<ICurrentUser>(u => u.UserId == Guid.NewGuid()),
            Mock.Of<IDatabaseExceptionTranslator>(), PrecisionPolicyTestDouble.Mock());
        var inputs = allocated.Select((l, index) => new PurchaseLineInput(items[index].Id, l.Description, l.Quantity,
            l.UnitPrice, l.VatCode, PurchaseReceptionLineId: original.Id,
            XmlAllocation: new(l.LineSubtotal, l.DiscountAmount, l.Taxes.Select(t => new PurchaseXmlTaxAllocationInput(
                t.PurchaseReceptionLineTaxId!.Value, t.TaxableBase, t.TaxAmount)).ToList()))).ToList();
        var result = await handler.Handle(new CreatePurchaseDraftCommand(invoice.SupplierId, "01", invoice.InvoiceNumber,
            invoice.IssueDate, inputs), default);
        result.IsSuccess.Should().BeTrue(result.Error);
        saved!.Lines.Should().HaveCount(2);
        saved.GrandTotal.Should().Be(80.52m);
        saved.TotalCostValue.Should().Be(59.74m);
        result.Value!.Lines.Should().OnlyContain(l => l.XmlAllocatedSubtotal != null
            && l.Taxes.All(t => t.Source == "XmlAllocation" && t.PurchaseReceptionLineTaxId != null));
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        source.PurchaseId.Should().Be(saved.Id);
    }

    [Fact]
    public async Task Real_invoice_reconciles_and_retains_original_snapshot()
    {
        var (invoice, source, original, repo, tax) = Fixture();
        var lines = await Components(invoice, original, repo, tax);
        (await PurchaseXmlBreakdown.ValidateAsync(invoice, lines, repo.Object, default)).Should().BeNull();
        invoice.ReplaceLines(lines, Guid.NewGuid());
        invoice.GrandTotal.Should().Be(80.52m);
        invoice.TotalCostValue.Should().Be(59.74m);
        original.Quantity.Should().Be(5);
        original.Taxes.Sum(t => t.TaxAmount).Should().Be(20.78m);
        source.XmlContent.Should().Be(Xml);
        (await new PurchaseXmlConfirmationGuard(repo.Object, new PurchaseXmlDraftParser())
            .ValidateAsync(invoice, default)).Should().BeNull();
    }

    [Fact]
    public async Task Gross_subtotal_and_exact_discount_reconcile_separately()
    {
        var (invoice, _, original, repo, tax) = Fixture(discount: 2);
        var lines = await Components(invoice, original, repo, tax);
        (await PurchaseXmlBreakdown.ValidateAsync(invoice, lines, repo.Object, default)).Should().BeNull();
        lines.Sum(l => l.LineSubtotal).Should().Be(61.74m);
        lines.Sum(l => l.DiscountAmount).Should().Be(2);
        lines.Sum(l => l.TaxableBase).Should().Be(59.74m);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("mixed")]
    [InlineData("foreign_company")]
    [InlineData("tax_swap")]
    public async Task Invalid_groups_are_rejected(string scenario)
    {
        var (invoice, _, original, repo, tax) = Fixture(scenario == "foreign_company");
        var lines = await Components(invoice, original, repo, tax);
        if (scenario == "duplicate") lines.Add(lines[0]);
        if (scenario == "missing") lines.RemoveAt(1);
        if (scenario == "mixed") lines.Add(PurchaseInvoiceDetail.Create(invoice.Id, invoice.TenantId,
            "Promoción", 5, 11.948m, "4", "UNIT", purchaseReceptionLineId: original.Id));
        if (scenario == "tax_swap")
        {
            var changed = original.Taxes.Select(t => new PurchaseXmlTaxAllocationInput(t.Id, t.TaxableBase,
                t.TaxCode == "2" ? t.TaxAmount - 1 : t.TaxCode == "3" ? t.TaxAmount + 1 : t.TaxAmount)).ToList();
            (await PurchaseXmlBreakdown.ApplyAsync(lines[0], new(30, 0, changed), repo.Object, tax.Object, default)).Should().BeNull();
        }
        (await PurchaseXmlBreakdown.ValidateAsync(invoice, lines, repo.Object, default)).Should().NotBeNull();
    }

    [Fact]
    public async Task Foreign_tax_id_and_duplicate_assignment_are_rejected_without_mutation()
    {
        var (invoice, _, original, repo, tax) = Fixture();
        var line = PurchaseInvoiceDetail.Create(invoice.Id, invoice.TenantId, "Producto", 1, 30, "4", "UNIT",
            itemId: Guid.NewGuid(), purchaseReceptionLineId: original.Id);
        foreach (var inputs in new IReadOnlyList<PurchaseXmlTaxAllocationInput>[] {
            [new(Guid.NewGuid(), 1, 1)],
            [new(original.Taxes[0].Id, 1, 1), new(original.Taxes[0].Id, 1, 1)] })
            (await PurchaseXmlBreakdown.ApplyAsync(line, new(30, 0, inputs), repo.Object, tax.Object, default))
                .Should().NotBeNull();
        line.IsXmlBreakdown.Should().BeFalse();
    }
}
