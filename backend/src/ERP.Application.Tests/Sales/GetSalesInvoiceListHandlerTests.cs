using ERP.Application.Common;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// SALES-ELECTRONIC-STATUS-CLARITY-01 — el listado de Ventas expone el estado REAL del documento
/// electrónico (independiente del estado comercial), resuelto con UNA sola lectura en lote por
/// página — nunca una consulta por factura — y "None" cuando la factura no tiene documento.
/// </summary>
public sealed class GetSalesInvoiceListHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static SalesInvoice CreateInvoice(string number)
    {
        var customer = CustomerSnapshot.Create("Cliente", "1710034065", "05", null, null);
        var paymentTerm = PaymentTermSnapshot.Create(Guid.NewGuid(), "Contado", 1, 0);
        var emissionPointId = Guid.NewGuid();
        var cashSession = CashSession.Open(
            TenantId,
            CompanyId,
            BranchId,
            UserId,
            Guid.NewGuid(),
            "CAJA-01",
            "Caja Principal",
            emissionPointId,
            "002",
            100m,
            UserId
        );
        return SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            Guid.NewGuid(),
            customer,
            number,
            new DateOnly(2026, 11, 4),
            UserId,
            paymentTerm,
            cashSession.Id,
            docTypeCode: "01",
            emissionPointId: emissionPointId,
            emissionType: EmissionType.Electronic,
            sriPaymentMethodCode: "01"
        );
    }

    [Fact]
    public async Task Lista_expone_estado_electronico_real_con_una_sola_lectura_en_lote()
    {
        var draftEdoc = CreateInvoice("001-002-000000015");
        var authorizedEdoc = CreateInvoice("001-002-000000017");
        var withoutEdoc = CreateInvoice("001-002-000000018");
        var items = new List<SalesInvoice> { draftEdoc, authorizedEdoc, withoutEdoc };

        var sales = new Mock<ISalesInvoiceRepository>();
        sales
            .Setup(r =>
                r.GetPagedAsync(TenantId, null, null, 1, 25, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((items, items.Count));

        var edocs = new Mock<IElectronicDocumentRepository>(MockBehavior.Strict);
        edocs
            .Setup(r =>
                r.GetCurrentStatesBySourcesAsync(
                    TenantId,
                    "Sales",
                    It.Is<IReadOnlyCollection<Guid>>(ids =>
                        ids.Count == 3
                        && ids.Contains(draftEdoc.Id)
                        && ids.Contains(authorizedEdoc.Id)
                        && ids.Contains(withoutEdoc.Id)
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<Guid, ElectronicDocumentState>
                {
                    [draftEdoc.Id] = ElectronicDocumentState.Draft,
                    [authorizedEdoc.Id] = ElectronicDocumentState.Authorized,
                }
            );

        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);

        var result = await new GetSalesInvoiceListHandler(sales.Object, edocs.Object, tenant.Object)
            .Handle(new GetSalesInvoiceListQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var byId = result.Value!.Items.ToDictionary(i => i.Id);
        byId[draftEdoc.Id].ElectronicStatus.Should().Be("Draft");
        byId[authorizedEdoc.Id].ElectronicStatus.Should().Be("Authorized");
        byId[withoutEdoc.Id].ElectronicStatus.Should().Be("None");
        // El estado comercial sigue siendo independiente del electrónico.
        byId[authorizedEdoc.Id].Status.Should().Be("Draft");

        // Una sola lectura en lote y ninguna consulta individual por factura (mock Strict).
        edocs.Verify(
            r =>
                r.GetCurrentStatesBySourcesAsync(
                    TenantId,
                    "Sales",
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
