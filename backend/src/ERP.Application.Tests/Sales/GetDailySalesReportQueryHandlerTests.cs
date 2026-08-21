using ERP.Application.Common;
using ERP.Application.Modules.Sales.UseCases.GetDailySalesReport;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// ERP-CORE-CLOSEOUT-08 — GetDailySalesReportQueryHandler sumaba Totals sobre TODAS las facturas
/// del rango sin filtrar por Status, inflando "ingresos" con facturas Draft (aún no emitidas) y
/// Cancelled (anuladas). Las filas siguen mostrando todas las facturas (con su Status real, para
/// auditoría/trazabilidad) — solo Totals se restringe a Authorized.
/// </summary>
public sealed class GetDailySalesReportQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();
    private static readonly Guid CashRegisterId = Guid.NewGuid();
    private static readonly Guid EmissionPointId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<ISalesInvoiceRepository> Repo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();

        public Fixture() => Tenant.Setup(t => t.TenantId).Returns(TenantId);

        public GetDailySalesReportQueryHandler BuildHandler() => new(Repo.Object, Tenant.Object);
    }

    private static SalesInvoice CreateInvoice(decimal grandTotalSeed, bool authorize, bool cancel = false)
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05", null, "Av. Test");
        var paymentTerm = PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0);
        var cashSession = CashSession.Open(
            TenantId,
            CompanyId,
            BranchId,
            UserId,
            CashRegisterId,
            "CAJA-01",
            "Caja Principal",
            EmissionPointId,
            "002",
            100m,
            UserId
        );

        var invoice = SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            customer,
            $"001-002-{Random.Shared.Next(100000, 999999)}",
            new DateOnly(2026, 8, 20),
            UserId,
            paymentTerm,
            cashSession.Id,
            docTypeCode: "01",
            emissionPointId: EmissionPointId,
            sriPaymentMethodCode: "01"
        );

        var line = SalesInvoiceDetail.Create(
            invoice.Id,
            TenantId,
            "Producto Test",
            quantity: 1m,
            unitPrice: grandTotalSeed,
            vatCode: "0",
            uomCode: "UNIT",
            snapshotSku: "SKU-001",
            snapshotItemName: "Producto Test"
        );
        line.ApplyTaxes("0", 0m, "IVA 0%", null, 0m, null);
        invoice.ReplaceLines(new[] { line }, UserId);

        if (authorize)
        {
            var payment = SalesInvoicePayment.Create(
                invoice.Id,
                TenantId,
                Guid.NewGuid(),
                "01",
                "Efectivo",
                line.TaxInclusiveTotal,
                null
            );
            invoice.ReplacePayments(new[] { payment }, UserId);
            invoice.Authorize(UserId);
            if (cancel)
                invoice.Cancel("Anulación de prueba", UserId);
        }

        return invoice;
    }

    [Fact]
    public async Task Totales_solo_incluyen_facturas_Authorized_excluyendo_Draft_y_Cancelled()
    {
        var authorized = CreateInvoice(100m, authorize: true);
        var draft = CreateInvoice(50m, authorize: false);
        var cancelled = CreateInvoice(30m, authorize: true, cancel: true);

        var f = new Fixture();
        f.Repo.Setup(r =>
                r.GetForDailyReportAsync(
                    TenantId,
                    It.IsAny<DateOnly>(),
                    It.IsAny<DateOnly>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new[] { authorized, draft, cancelled });

        var result = await f.BuildHandler()
            .Handle(new GetDailySalesReportQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Items.Should().HaveCount(3, "todas las facturas del rango siguen visibles para auditoría");
        result.Value.Totals.Count.Should().Be(1);
        result.Value.Totals.GrandTotal.Should().Be(authorized.GrandTotal);
    }

    [Fact]
    public async Task Sin_facturas_autorizadas_los_totales_son_cero()
    {
        var draft = CreateInvoice(50m, authorize: false);
        var f = new Fixture();
        f.Repo.Setup(r =>
                r.GetForDailyReportAsync(
                    TenantId,
                    It.IsAny<DateOnly>(),
                    It.IsAny<DateOnly>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new[] { draft });

        var result = await f.BuildHandler()
            .Handle(new GetDailySalesReportQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Totals.Count.Should().Be(0);
        result.Value.Totals.GrandTotal.Should().Be(0m);
    }
}
