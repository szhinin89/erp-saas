using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Application.Modules.Pricing.Services;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// Hallazgo ALTO auditoría de aislamiento (Sales/Purchases cross-branch): ApplySalesDiscountCommand
/// y UpdateSalesDraftCommand están marcados IBranchScopedRequest, pero ese marker solo exige
/// sucursal activa autorizada — no que la factura cargada pertenezca a esa sucursal. Ambos handlers
/// deben rechazar con NotFound (nunca revelar existencia cross-branch) cuando la factura pertenece
/// a otra sucursal, y seguir funcionando normalmente cuando pertenece a la sucursal activa.
/// </summary>
public sealed class SalesDiscountAndDraftBranchScopeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();

    private static SalesInvoice CreateDraftInvoice(Guid branchId)
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0);

        return SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            branchId,
            CustomerId,
            customer,
            invoiceNumber: "DRAFT-BRANCH-SCOPE",
            issueDate: new DateOnly(2026, 7, 25),
            createdBy: UserId,
            paymentTerm: paymentTerm,
            cashSessionId: Guid.NewGuid()
        );
    }

    private static OperationalPreferences DefaultPreferences() =>
        new(
            SalesPos: new SalesPosPreferences(true, false, true, 0m, null, false, false, null, null),
            Cash: new CashPreferences(true, true, 0m, true, true, true),
            Purchases: new PurchasesPreferences(null, true, true, true, false),
            Inventory: new InventoryPreferences(false, true, false, 0m),
            Printing: new PrintingPreferences("AskBeforePrint", 1, "80mm", false, true, true, false),
            ElectronicDocuments: new ElectronicDocumentsPreferences(true, 3, true, true),
            Notifications: new NotificationsPreferences(true, false, "es")
        );

    // ── ApplySalesDiscountHandler ───────────────────────────────────────

    private static (
        ApplySalesDiscountHandler Handler,
        SalesInvoice Invoice
    ) BuildDiscountHandler(Guid invoiceBranchId, Guid activeBranchId)
    {
        var inv = CreateDraftInvoice(invoiceBranchId);

        var repo = new Mock<ISalesInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);

        var preferences = new Mock<IOperationalPreferencesResolver>();
        preferences.Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(DefaultPreferences());

        var handler = new ApplySalesDiscountHandler(
            repo.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == activeBranchId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId),
            preferences.Object
        );

        return (handler, inv);
    }

    [Fact]
    public async Task ApplyDiscount_factura_de_otra_sucursal_retorna_NotFound()
    {
        var otherBranchId = Guid.NewGuid();
        var (handler, inv) = BuildDiscountHandler(invoiceBranchId: BranchId, activeBranchId: otherBranchId);

        var result = await handler.Handle(
            new ApplySalesDiscountCommand(inv.Id, 10m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        inv.Lines.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyDiscount_factura_de_la_misma_sucursal_se_aplica_correctamente()
    {
        var (handler, inv) = BuildDiscountHandler(invoiceBranchId: BranchId, activeBranchId: BranchId);

        var result = await handler.Handle(
            new ApplySalesDiscountCommand(inv.Id, 10m),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
    }

    // ── UpdateSalesDraftHandler ─────────────────────────────────────────

    private static (
        UpdateSalesDraftHandler Handler,
        SalesInvoice Invoice
    ) BuildUpdateDraftHandler(Guid invoiceBranchId, Guid activeBranchId)
    {
        var inv = CreateDraftInvoice(invoiceBranchId);

        var repo = new Mock<ISalesInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);

        var bp = BusinessPartner.Create(
            TenantId,
            "05",
            "1710034065",
            legalEntityTypeCode: 1,
            legalName: "Cliente Test",
            createdBy: UserId
        );
        var bpRepo = new Mock<IBusinessPartnerRepository>();
        bpRepo.Setup(r => r.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>())).ReturnsAsync(bp);

        var companyTaxRepo = new Mock<ICompanySpecialTaxResponsibilityRepository>();
        companyTaxRepo
            .Setup(r =>
                r.GetResponsibleSriTaxCategoryCodesAsync(CompanyId, TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Array.Empty<string>());

        var preferences = new Mock<IOperationalPreferencesResolver>();
        preferences.Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(DefaultPreferences());

        var handler = new UpdateSalesDraftHandler(
            repo.Object,
            bpRepo.Object,
            Mock.Of<IBusinessPartnerRoleRepository>(),
            Mock.Of<IPaymentTermRepository>(),
            Mock.Of<IPaymentMethodRepository>(),
            Mock.Of<IItemRepository>(),
            Mock.Of<ISriTaxResolver>(),
            Mock.Of<IPricingResolver>(),
            companyTaxRepo.Object,
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == activeBranchId),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId),
            preferences.Object
        );

        return (handler, inv);
    }

    private static UpdateSalesDraftCommand BuildCommand(SalesInvoice inv) =>
        new(inv.Id, CustomerId, inv.IssueDate, new List<SalesLineInput>());

    [Fact]
    public async Task UpdateDraft_factura_de_otra_sucursal_retorna_NotFound()
    {
        var otherBranchId = Guid.NewGuid();
        var (handler, inv) = BuildUpdateDraftHandler(invoiceBranchId: BranchId, activeBranchId: otherBranchId);

        var result = await handler.Handle(BuildCommand(inv), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task UpdateDraft_factura_de_la_misma_sucursal_se_actualiza_correctamente()
    {
        var (handler, inv) = BuildUpdateDraftHandler(invoiceBranchId: BranchId, activeBranchId: BranchId);

        var result = await handler.Handle(BuildCommand(inv), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
    }
}
