using ERP.Application.Common;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Domain.Modules.SriCatalogs.Enums;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Subfase 5D-3) — CreateSalesReturnDraftHandler propaga
/// IVA/ICE/IRBPNR desde SalesInvoiceDetail.Taxes de la línea original, prorrateados por la fracción
/// de cantidad devuelta. Nunca consulta configuración tributaria actual del ítem ni la última compra
/// — el snapshot fiscal es siempre el de la factura original, congelado en el momento de la venta.
/// </summary>
public sealed class SalesReturnDraftSpecialTaxTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();
    private static readonly Guid CashSessionId = Guid.NewGuid();

    private static (SalesInvoice Invoice, SalesInvoiceDetail Line) BuildAuthorizedInvoiceWithLine(
        decimal quantity,
        decimal unitPrice,
        string? iceCode = null,
        decimal iceRate = 0m,
        SriTaxCalculationType iceCalculationType = SriTaxCalculationType.Percentage,
        decimal? iceExactAmount = null,
        string? irbpnrCode = null,
        decimal irbpnrRate = 0m,
        decimal irbpnrAmount = 0m
    )
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(PaymentTermId, "Contado", 1, 0);
        var inv = SalesInvoice.CreateDraft(
            TenantId, CompanyId, BranchId, CustomerId, customer,
            invoiceNumber: "001-001-000000010", issueDate: new DateOnly(2026, 7, 25),
            createdBy: UserId, paymentTerm: paymentTerm, cashSessionId: CashSessionId
        );

        var line = SalesInvoiceDetail.Create(
            inv.Id, TenantId, "Producto con impuestos especiales", quantity, unitPrice,
            vatCode: "2", uomCode: "UNIT", iceCode: iceCode
        );
        if (!string.IsNullOrWhiteSpace(irbpnrCode))
            line.ReplaceTaxes(
                [
                    SalesInvoiceDetailTax.Create(
                        line.Id, TenantId, "5", irbpnrCode, "IRBPNR", irbpnrRate,
                        SriTaxCalculationType.Specific, line.TaxableBase, irbpnrAmount,
                        SalesTaxSource.Calculated
                    ),
                ]
            );
        line.ApplyTaxes("2", 15m, "IVA 15%", iceCode, iceRate, "ICE", iceCalculationType, iceExactAmount);
        inv.ReplaceLines(new[] { line }, UserId);

        var payment = SalesInvoicePayment.Create(
            inv.Id, TenantId, Guid.NewGuid(), "01", "Efectivo", line.TaxInclusiveTotal
        );
        inv.ReplacePayments(new[] { payment }, UserId);
        inv.Authorize(UserId);

        return (inv, inv.Lines.Single());
    }

    private static Mock<ISalesInvoiceRepository> MockInvoiceRepo(SalesInvoice invoice)
    {
        var repo = new Mock<ISalesInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        return repo;
    }

    private static Mock<ISalesReturnRepository> MockReturnRepo(decimal alreadyReturned = 0m)
    {
        var repo = new Mock<ISalesReturnRepository>();
        repo.Setup(r =>
                r.GetReturnedQuantityByInvoiceDetailAsync(
                    TenantId,
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(alreadyReturned);
        return repo;
    }

    private static ICurrentTenant Tenant() => Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId);

    private static ICurrentCompany Company() =>
        Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId);

    private static ICurrentUser User() => Mock.Of<ICurrentUser>(u => u.UserId == UserId);

    private static async Task<SalesReturn> CreateReturnAsync(
        SalesInvoice invoice,
        SalesInvoiceDetail line,
        decimal returnQuantity
    )
    {
        var invoiceRepo = MockInvoiceRepo(invoice);
        var returnRepo = MockReturnRepo();
        SalesReturn? captured = null;
        returnRepo
            .Setup(r => r.AddAsync(It.IsAny<SalesReturn>(), It.IsAny<CancellationToken>()))
            .Callback<SalesReturn, CancellationToken>((r, _) => captured = r)
            .Returns(Task.CompletedTask);

        var handler = new CreateSalesReturnDraftHandler(
            returnRepo.Object, invoiceRepo.Object, Tenant(), Company(), User()
        );
        var result = await handler.Handle(
            new CreateSalesReturnDraftCommand(
                invoice.Id,
                "Producto en mal estado",
                new List<SalesReturnLineInput> { new(line.Id, returnQuantity) }
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        captured.Should().NotBeNull();
        return captured!;
    }

    [Fact]
    public async Task Devolucion_total_con_ICE_e_IRBPNR_prorratea_al_100_por_ciento()
    {
        var (invoice, line) = BuildAuthorizedInvoiceWithLine(
            quantity: 10m, unitPrice: 100m,
            iceCode: "3010", iceRate: 10m,
            irbpnrCode: "5001", irbpnrRate: 0.02m, irbpnrAmount: 1.00m
        );

        var salesReturn = await CreateReturnAsync(invoice, line, returnQuantity: 10m);

        var returnLine = salesReturn.Lines.Single();
        returnLine.IceAmount.Should().Be(line.IceAmount);
        returnLine.VatAmount.Should().Be(line.VatAmount);
        returnLine.IrbpnrAmount.Should().Be(1.00m);
    }

    [Fact]
    public async Task Devolucion_parcial_prorratea_IVA_ICE_e_IRBPNR_por_la_fraccion_de_cantidad()
    {
        // Factura: 10 unidades. IRBPNR total=1.00 (0.10/unidad). Se devuelven 3 → fracción 0.3.
        var (invoice, line) = BuildAuthorizedInvoiceWithLine(
            quantity: 10m, unitPrice: 100m,
            iceCode: "3010", iceRate: 10m,
            irbpnrCode: "5001", irbpnrRate: 0.02m, irbpnrAmount: 1.00m
        );

        var salesReturn = await CreateReturnAsync(invoice, line, returnQuantity: 3m);

        var returnLine = salesReturn.Lines.Single();
        returnLine.IrbpnrAmount.Should().Be(0.30m); // 0.3 * 1.00
        returnLine.IceAmount.Should().Be(30m); // 30% de 300 (proporción de cantidad ya refleja en TaxableBase)
    }

    [Fact]
    public async Task Devolucion_con_ICE_Specific_prorratea_el_monto_exacto_por_cantidad()
    {
        var (invoice, line) = BuildAuthorizedInvoiceWithLine(
            quantity: 10m, unitPrice: 100m,
            iceCode: "3053", iceCalculationType: SriTaxCalculationType.Specific, iceExactAmount: 5.00m
        );

        var salesReturn = await CreateReturnAsync(invoice, line, returnQuantity: 3m);

        var returnLine = salesReturn.Lines.Single();
        returnLine.IceAmount.Should().Be(1.50m); // 0.3 * 5.00, nunca recalculado desde una tarifa
    }

    [Fact]
    public async Task Devolucion_sin_IRBPNR_en_la_factura_no_genera_IRBPNR_falso()
    {
        var (invoice, line) = BuildAuthorizedInvoiceWithLine(quantity: 5m, unitPrice: 10m);

        var salesReturn = await CreateReturnAsync(invoice, line, returnQuantity: 5m);

        var returnLine = salesReturn.Lines.Single();
        returnLine.IrbpnrAmount.Should().Be(0m);
        returnLine.IrbpnrCode.Should().BeNull();
        returnLine.Taxes.Should().NotContain(t => t.TaxCode == "5");
    }

    [Fact]
    public async Task Totales_revertidos_coinciden_con_el_snapshot_original_para_devolucion_total()
    {
        var (invoice, line) = BuildAuthorizedInvoiceWithLine(
            quantity: 1m, unitPrice: 100m,
            iceCode: "3010", iceRate: 10m,
            irbpnrCode: "5001", irbpnrRate: 0.02m, irbpnrAmount: 0.02m
        );

        var salesReturn = await CreateReturnAsync(invoice, line, returnQuantity: 1m);

        var returnLine = salesReturn.Lines.Single();
        returnLine.TaxInclusiveTotal.Should().Be(line.TaxInclusiveTotal);
    }

    [Fact]
    public void CreateSalesReturnDraftHandler_no_depende_de_IItemRepository_ni_de_Compras()
    {
        // Reglas 7/8 — el snapshot fiscal viene exclusivamente de SalesInvoiceDetail.Taxes de la
        // factura original: si el ítem cambia después, o si existe una compra más reciente, la
        // devolución no puede verse afectada porque estructuralmente no puede leer ninguna de las dos.
        var ctor = typeof(CreateSalesReturnDraftHandler).GetConstructors().Single();
        ctor.GetParameters()
            .Should()
            .NotContain(
                p =>
                    p.ParameterType.Name.Contains("ItemRepository")
                    || (p.ParameterType.Namespace ?? "").Contains("Purchases"),
                "la devolución de venta debe conservar el snapshot fiscal original de la factura, "
                    + "sin consultar el ítem actual ni ninguna compra"
            );
    }
}
