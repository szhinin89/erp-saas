using ERP.Application.Common;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.GetPurchaseReceptionXmlView;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases.PurchaseReception;

/// <summary>
/// Cubre el handler read-only de FLOW-READY-02E.1: nunca debe llamar <c>SaveChangesAsync</c> ni
/// mutar el documento — solo lee <c>XmlContent</c>/líneas ya persistidos.
/// </summary>
public sealed class GetPurchaseReceptionXmlViewHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private const string SampleFacturaXml =
        """
        <factura id="comprobante" version="1.1.0">
          <infoTributaria>
            <ruc>1791352688001</ruc>
            <razonSocial>QUALA ECUADOR S A</razonSocial>
            <nombreComercial>QUALA</nombreComercial>
            <claveAcceso>0107202601179135268800120150270001617400016174011</claveAcceso>
            <codDoc>01</codDoc>
            <estab>015</estab>
            <ptoEmi>027</ptoEmi>
            <secuencial>000161740</secuencial>
          </infoTributaria>
          <infoFactura>
            <fechaEmision>01/07/2026</fechaEmision>
            <totalSinImpuestos>15.96</totalSinImpuestos>
            <totalDescuento>1.50</totalDescuento>
            <totalConImpuestos>
              <totalImpuesto>
                <codigo>2</codigo>
                <codigoPorcentaje>2</codigoPorcentaje>
                <baseImponible>15.96</baseImponible>
                <valor>2.40</valor>
              </totalImpuesto>
            </totalConImpuestos>
            <importeTotal>18.35</importeTotal>
          </infoFactura>
          <detalles>
            <detalle>
              <descripcion>COCA COLA 500 ML</descripcion>
              <cantidad>10</cantidad>
              <precioUnitario>1.596</precioUnitario>
            </detalle>
          </detalles>
        </factura>
        """;

    private static PurchaseReceptionDocument SampleDocument() =>
        PurchaseReceptionDocument.Create(
            TenantId,
            CompanyId,
            BranchId,
            PurchaseReceptionSourceDocType.Invoice,
            "1791352688001",
            "QUALA ECUADOR S A",
            null,
            "0107202601179135268800120150270001617400016174011",
            "015-027-000161740",
            new DateOnly(2026, 7, 1),
            new DateTime(2026, 7, 1, 21, 6, 55, DateTimeKind.Utc),
            15.96m,
            2.4m,
            18.35m,
            UserId
        );

    private static (
        GetPurchaseReceptionXmlViewHandler handler,
        Mock<IPurchaseReceptionDocumentRepository> repo
    ) BuildHandler()
    {
        var repo = new Mock<IPurchaseReceptionDocumentRepository>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);

        var handler = new GetPurchaseReceptionXmlViewHandler(repo.Object, tenant.Object);
        return (handler, repo);
    }

    [Fact]
    public async Task Handle_returns_not_found_for_a_nonexistent_document()
    {
        var (handler, repo) = BuildHandler();
        var missingId = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseReceptionDocument?)null);

        var result = await handler.Handle(
            new GetPurchaseReceptionXmlViewQuery(missingId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Handle_reports_xml_not_available_when_XmlContent_is_empty()
    {
        var document = SampleDocument();
        var (handler, repo) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await handler.Handle(
            new GetPurchaseReceptionXmlViewQuery(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.RawXmlAvailable.Should().BeFalse();
        result.Value.RawXml.Should().BeNull();
        result.Value.TaxSummaries.Should().BeEmpty();
        result.Value.SupplierTradeName.Should().BeNull();
        // Header ya persistido en la entidad sigue disponible aunque no haya XML.
        result.Value.DocumentNumber.Should().Be(document.InvoiceNumber);
        result.Value.SupplierName.Should().Be(document.SupplierName);
    }

    [Fact]
    public async Task Handle_reads_header_extras_and_persisted_lines_from_a_verified_document()
    {
        var document = SampleDocument();
        var line = PurchaseReceptionLine.Create(
            document.Id,
            TenantId,
            "COCA COLA 500 ML",
            10m,
            1.596m,
            vatCode: "2",
            taxCode: "2",
            vatPercentage: 15m,
            taxValue: 2.4m,
            discountPct: 0m,
            discount: 0m,
            lineSubtotal: 15.96m,
            totalLine: 18.36m,
            supplierCode: "PROV-001"
        );
        document.AttachSriAuthorization(
            "0107202601179135268800120150270001617400016174011",
            DateTime.UtcNow,
            SampleFacturaXml,
            DateTime.UtcNow,
            [line],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "20",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                1,
                1,
                null
            )
        );

        var (handler, repo) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await handler.Handle(
            new GetPurchaseReceptionXmlViewQuery(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value!;
        dto.RawXmlAvailable.Should().BeTrue();
        dto.RawXml.Should().Be(SampleFacturaXml);
        dto.SupplierTradeName.Should().Be("QUALA");
        dto.DiscountAmount.Should().Be(1.50m);
        dto.TaxSummaries.Should().ContainSingle();
        dto.TaxSummaries[0].TaxType.Should().Be("2");
        dto.TaxSummaries[0].TaxableBase.Should().Be(15.96m);
        dto.TaxSummaries[0].TaxAmount.Should().Be(2.40m);
        dto.TaxSummaries[0].TaxRate.Should().BeNull();

        dto.Lines.Should().ContainSingle();
        var lineDto = dto.Lines[0];
        lineDto.MainCode.Should().Be("PROV-001");
        lineDto.Description.Should().Be("COCA COLA 500 ML");
        lineDto.TotalAmount.Should().Be(18.36m);
        lineDto.Taxes.Should().ContainSingle(t => t.TaxType == "2" && t.TaxAmount == 2.4m);
    }

    [Fact]
    public async Task Handle_never_persists_anything()
    {
        var document = SampleDocument();
        var (handler, repo) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        await handler.Handle(
            new GetPurchaseReceptionXmlViewQuery(document.Id),
            CancellationToken.None
        );

        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        document.Status.Should().Be(PurchaseReceptionDocumentStatus.Imported);
    }
}
