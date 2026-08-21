using ERP.Application.Common;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// ERP-CORE-CLOSEOUT-05-FIX01 (P0-2) — GetSalesInvoiceByIdHandler debía filtrar solo por
/// Tenant+Company, permitiendo leer por GUID una factura de otra sucursal de la misma empresa.
/// Ahora valida explícitamente <c>invoice.BranchId == ICurrentBranch.BranchId</c> antes de
/// devolver el detalle, igual que el patrón ya usado por GetSalesReceiptPrintPayloadQueryHandler.
/// </summary>
public sealed class GetSalesInvoiceByIdHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();
    private static readonly Guid CashRegisterId = Guid.NewGuid();
    private static readonly Guid CashSessionEmissionPointId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<ISalesInvoiceRepository> SalesInvoices { get; } = new();
        public Mock<IElectronicDocumentRepository> ElectronicDocuments { get; } = new();
        public Mock<ICurrentTenant> CurrentTenant { get; } = new();
        public Mock<ICurrentBranch> CurrentBranch { get; } = new();

        public Fixture(Guid? branchContextId = null)
        {
            CurrentTenant.Setup(t => t.TenantId).Returns(TenantId);
            CurrentBranch.Setup(b => b.BranchId).Returns(branchContextId ?? BranchId);
            ElectronicDocuments
                .Setup(r =>
                    r.GetBySourceAsync(
                        TenantId,
                        "Sales",
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync((ERP.Domain.Modules.ElectronicDocuments.Entities.ElectronicDocument?)null);
        }

        public GetSalesInvoiceByIdHandler BuildHandler() =>
            new(SalesInvoices.Object, ElectronicDocuments.Object, CurrentTenant.Object, CurrentBranch.Object);
    }

    private static SalesInvoice CreateInvoice(Guid branchId)
    {
        var customer = CustomerSnapshot.Create(
            "Cliente POS",
            "1710034065",
            "05",
            "cliente@example.com",
            "Av. Principal"
        );
        var paymentTerm = PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0);
        var cashSession = CashSession.Open(
            TenantId,
            CompanyId,
            branchId,
            UserId,
            CashRegisterId,
            "CAJA-01",
            "Caja Principal",
            CashSessionEmissionPointId,
            "002",
            100m,
            UserId
        );

        return SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            branchId,
            CustomerId,
            customer,
            "001-002-000000123",
            new DateOnly(2026, 8, 20),
            UserId,
            paymentTerm,
            cashSession.Id,
            docTypeCode: "01",
            emissionPointId: CashSessionEmissionPointId,
            emissionType: EmissionType.Physical,
            sriPaymentMethodCode: "01"
        );
    }

    [Fact]
    public async Task Factura_de_la_sucursal_activa_devuelve_detalle()
    {
        var invoice = CreateInvoice(BranchId);
        var f = new Fixture();
        f.SalesInvoices.Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var result = await f.BuildHandler()
            .Handle(new GetSalesInvoiceByIdQuery(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Id.Should().Be(invoice.Id);
    }

    [Fact]
    public async Task Factura_de_otra_sucursal_de_la_misma_empresa_devuelve_NotFound()
    {
        var otherBranchId = Guid.NewGuid();
        var invoice = CreateInvoice(otherBranchId);
        var f = new Fixture(branchContextId: BranchId);
        f.SalesInvoices.Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var result = await f.BuildHandler()
            .Handle(new GetSalesInvoiceByIdQuery(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Factura_inexistente_devuelve_NotFound()
    {
        var f = new Fixture();
        var missingId = Guid.NewGuid();
        f.SalesInvoices.Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SalesInvoice?)null);

        var result = await f.BuildHandler()
            .Handle(new GetSalesInvoiceByIdQuery(missingId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }
}
