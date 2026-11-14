using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Sales.Services;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Application.Tests.TestSupport;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// API-DOC-F-18: autorizar una factura anulada se rechaza (422) y el mensaje debe describir el
/// estado real (anulada), no «ya fue autorizada».
/// </summary>
public sealed class AuthorizeSalesInvoiceCancelledMessageTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static SalesInvoice BuildAuthorizedInvoice()
    {
        var invoice = SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            Guid.NewGuid(),
            CustomerSnapshot.Create("Cliente Test", "1710034065", "05"),
            invoiceNumber: "DRAFT-TEST",
            issueDate: new DateOnly(2026, 7, 25),
            createdBy: UserId,
            paymentTerm: PaymentTermSnapshot.Create(Guid.NewGuid(), "Contado", installments: 1, daysBetween: 0),
            cashSessionId: Guid.NewGuid(),
            emissionType: EmissionType.Physical
        );
        var line = SalesInvoiceDetail.Create(
            invoice.Id, TenantId, "Producto Test", 1m, 10m, vatCode: "0", uomCode: "UNIT");
        line.ApplyTaxes("0", 0m, "IVA 0%", null, 0m, null);
        invoice.ReplaceLines(new[] { line }, UserId);
        invoice.ReplacePayments(
            new[]
            {
                SalesInvoicePayment.Create(
                    invoice.Id, TenantId, Guid.NewGuid(), "01", "Efectivo", line.TaxInclusiveTotal, null),
            },
            UserId);
        invoice.Authorize(UserId);
        return invoice;
    }

    private static AuthorizeSalesInvoiceHandler BuildHandler(SalesInvoice invoice)
    {
        var repo = new Mock<ISalesInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        var branch = new Mock<ICurrentBranch>();
        branch.Setup(b => b.BranchId).Returns(BranchId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        return new AuthorizeSalesInvoiceHandler(
            repo.Object,
            Mock.Of<ISalesReceivableRepository>(),
            Mock.Of<IStockRepository>(),
            Mock.Of<IPaymentTermRepository>(),
            Mock.Of<ISriTaxResolver>(),
            Mock.Of<IDocumentSequenceRepository>(),
            Mock.Of<IEmissionPointRepository>(),
            Mock.Of<IEstablishmentRepository>(),
            Mock.Of<IElectronicDocumentRepository>(),
            Mock.Of<ISalesInvoiceEmissionStrategyResolver>(),
            Mock.Of<ICompanyClock>(),
            Mock.Of<IBusinessPartnerRepository>(),
            Mock.Of<ISalesFiscalPolicyResolver>(),
            Mock.Of<IPaymentMethodRepository>(),
            Mock.Of<ICompanyBankAccountRepository>(),
            Mock.Of<ERP.Domain.Modules.Caja.Interfaces.ICashSessionRepository>(),
            Mock.Of<ERP.Domain.Modules.Caja.Interfaces.ICashRegisterRepository>(),
            Mock.Of<ILogger<AuthorizeSalesInvoiceHandler>>(),
            tenant.Object,
            company.Object,
            branch.Object,
            user.Object,
            Mock.Of<IOperationalPreferencesResolver>(),
            PrecisionPolicyTestDouble.Mock(),
            Mock.Of<ERP.Domain.Modules.Items.Interfaces.IItemRepository>(),
            Mock.Of<IWarehouseRepository>());
    }

    [Fact]
    public async Task Autorizar_factura_anulada_se_rechaza_con_mensaje_de_anulada()
    {
        var invoice = BuildAuthorizedInvoice();
        invoice.Cancel("Anulación de prueba", UserId);

        var result = await BuildHandler(invoice).Handle(
            new AuthorizeSalesInvoiceCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("anulada");
        result.Error.Should().NotContain("ya fue autorizada");
    }

    [Fact]
    public async Task Autorizar_factura_ya_autorizada_sigue_siendo_idempotente()
    {
        var invoice = BuildAuthorizedInvoice();

        var result = await BuildHandler(invoice).Handle(
            new AuthorizeSalesInvoiceCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
