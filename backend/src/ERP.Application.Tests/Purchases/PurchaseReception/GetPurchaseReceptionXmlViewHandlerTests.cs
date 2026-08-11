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

    private const string ArcadorFacturaXml =
        """
        <factura id="comprobante" version="1.1.0">
          <infoTributaria>
            <ruc>1791415132001</ruc>
            <razonSocial>BEBIDAS ARCACONTINENTAL ECUADOR ARCADOR C.L.</razonSocial>
            <nombreComercial>ARCADOR</nombreComercial>
            <claveAcceso>0107202601179141513200120290010012937141234567811</claveAcceso>
            <codDoc>01</codDoc>
            <estab>029</estab>
            <ptoEmi>001</ptoEmi>
            <secuencial>001293714</secuencial>
          </infoTributaria>
          <infoFactura>
            <fechaEmision>01/07/2026</fechaEmision>
            <guiaRemision>001-002-000000111</guiaRemision>
            <totalSinImpuestos>74.39</totalSinImpuestos>
            <totalDescuento>0.00</totalDescuento>
            <totalConImpuestos>
              <totalImpuesto>
                <codigo>2</codigo>
                <codigoPorcentaje>4</codigoPorcentaje>
                <baseImponible>80.84</baseImponible>
                <valor>12.13</valor>
              </totalImpuesto>
              <totalImpuesto>
                <codigo>3</codigo>
                <codigoPorcentaje>3053</codigoPorcentaje>
                <baseImponible>35.81</baseImponible>
                <valor>6.45</valor>
              </totalImpuesto>
              <totalImpuesto>
                <codigo>5</codigo>
                <codigoPorcentaje>5001</codigoPorcentaje>
                <baseImponible>132.00</baseImponible>
                <valor>2.64</valor>
              </totalImpuesto>
            </totalConImpuestos>
            <propina>0.00</propina>
            <importeTotal>95.60</importeTotal>
            <pagos>
              <pago>
                <formaPago>20</formaPago>
                <plazo>30</plazo>
                <unidadTiempo>dias</unidadTiempo>
              </pago>
            </pagos>
          </infoFactura>
          <detalles>
            <detalle>
              <codigoPrincipal>0580</codigoPrincipal>
              <descripcion>SPRITE HARMONY 1350 PET(12)</descripcion>
              <detallesAdicionales>
                <detAdicional nombre="Unidad" valor="PACA"/>
              </detallesAdicionales>
              <impuestos>
                <impuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><tarifa>15.00</tarifa><baseImponible>10.72</baseImponible><valor>1.61</valor></impuesto>
                <impuesto><codigo>5</codigo><codigoPorcentaje>5001</codigoPorcentaje><tarifa>0.02</tarifa><baseImponible>12.00</baseImponible><valor>0.24</valor></impuesto>
              </impuestos>
            </detalle>
            <detalle>
              <codigoPrincipal>3172</codigoPrincipal>
              <descripcion>FANTA HARMONY NRJ 1350 PET(12)</descripcion>
              <impuestos>
                <impuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><tarifa>15.00</tarifa><baseImponible>21.44</baseImponible><valor>3.22</valor></impuesto>
                <impuesto><codigo>3</codigo><codigoPorcentaje>3053</codigoPorcentaje><tarifa>0.18</tarifa><baseImponible>15.89</baseImponible><valor>2.86</valor></impuesto>
                <impuesto><codigo>5</codigo><codigoPorcentaje>5001</codigoPorcentaje><tarifa>0.02</tarifa><baseImponible>24.00</baseImponible><valor>0.48</valor></impuesto>
              </impuestos>
            </detalle>
            <detalle>
              <codigoPrincipal>12469</codigoPrincipal>
              <descripcion>INCA-KOLA ORGL 900ML PET NR 12</descripcion>
              <impuestos>
                <impuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><tarifa>15.00</tarifa><baseImponible>12.98</baseImponible><valor>1.95</valor></impuesto>
                <impuesto><codigo>5</codigo><codigoPorcentaje>5001</codigoPorcentaje><tarifa>0.02</tarifa><baseImponible>36.00</baseImponible><valor>0.72</valor></impuesto>
              </impuestos>
            </detalle>
            <detalle>
              <codigoPrincipal>7796</codigoPrincipal>
              <descripcion>SPRITE HARMONY 500ML PET 12</descripcion>
              <impuestos>
                <impuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><tarifa>15.00</tarifa><baseImponible>4.48</baseImponible><valor>0.67</valor></impuesto>
                <impuesto><codigo>5</codigo><codigoPorcentaje>5001</codigoPorcentaje><tarifa>0.02</tarifa><baseImponible>12.00</baseImponible><valor>0.24</valor></impuesto>
              </impuestos>
            </detalle>
            <detalle>
              <codigoPrincipal>11604</codigoPrincipal>
              <descripcion>FIORA HARMONY FRESA 500 PT(12)</descripcion>
              <impuestos>
                <impuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><tarifa>15.00</tarifa><baseImponible>4.48</baseImponible><valor>0.67</valor></impuesto>
                <impuesto><codigo>3</codigo><codigoPorcentaje>3053</codigoPorcentaje><tarifa>0.18</tarifa><baseImponible>2.94</baseImponible><valor>0.53</valor></impuesto>
                <impuesto><codigo>5</codigo><codigoPorcentaje>5001</codigoPorcentaje><tarifa>0.02</tarifa><baseImponible>12.00</baseImponible><valor>0.24</valor></impuesto>
              </impuestos>
            </detalle>
            <detalle>
              <codigoPrincipal>11608</codigoPrincipal>
              <descripcion>FANTA HARMONY NRJ 500 PET(12)</descripcion>
              <impuestos>
                <impuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><tarifa>15.00</tarifa><baseImponible>8.96</baseImponible><valor>1.34</valor></impuesto>
                <impuesto><codigo>3</codigo><codigoPorcentaje>3053</codigoPorcentaje><tarifa>0.18</tarifa><baseImponible>5.89</baseImponible><valor>1.06</valor></impuesto>
                <impuesto><codigo>5</codigo><codigoPorcentaje>5001</codigoPorcentaje><tarifa>0.02</tarifa><baseImponible>24.00</baseImponible><valor>0.48</valor></impuesto>
              </impuestos>
            </detalle>
            <detalle>
              <codigoPrincipal>10111</codigoPrincipal>
              <descripcion>COCA-COLA 350ML LATA(6)</descripcion>
              <impuestos>
                <impuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><tarifa>15.00</tarifa><baseImponible>6.08</baseImponible><valor>0.91</valor></impuesto>
              </impuestos>
            </detalle>
            <detalle>
              <codigoPrincipal>0577</codigoPrincipal>
              <descripcion>SPRITE HARMONY 300 PET(12)</descripcion>
              <impuestos>
                <impuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><tarifa>15.00</tarifa><baseImponible>2.59</baseImponible><valor>0.39</valor></impuesto>
                <impuesto><codigo>5</codigo><codigoPorcentaje>5001</codigoPorcentaje><tarifa>0.02</tarifa><baseImponible>12.00</baseImponible><valor>0.24</valor></impuesto>
              </impuestos>
            </detalle>
            <detalle>
              <codigoPrincipal>0126</codigoPrincipal>
              <descripcion>COCA-COLA E 1250 GRB(12)</descripcion>
              <impuestos>
                <impuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><tarifa>15.00</tarifa><baseImponible>9.11</baseImponible><valor>1.37</valor></impuesto>
                <impuesto><codigo>3</codigo><codigoPorcentaje>3053</codigoPorcentaje><tarifa>0.18</tarifa><baseImponible>11.09</baseImponible><valor>2.00</valor></impuesto>
              </impuestos>
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

    private static PurchaseReceptionDocument ArcadorDocument() =>
        PurchaseReceptionDocument.Create(
            TenantId,
            CompanyId,
            BranchId,
            PurchaseReceptionSourceDocType.Invoice,
            "1791415132001",
            "BEBIDAS ARCACONTINENTAL ECUADOR ARCADOR C.L.",
            null,
            "0107202601179141513200120290010012937141234567811",
            "029-001-001293714",
            new DateOnly(2026, 7, 1),
            new DateTime(2026, 7, 1, 21, 6, 55, DateTimeKind.Utc),
            74.39m,
            12.13m,
            95.60m,
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
        dto.TaxSummaries[0].TaxCode.Should().Be("2");
        dto.TaxSummaries[0].TaxRateCode.Should().Be("2");
        dto.TaxSummaries[0].TaxName.Should().Be("IVA");
        dto.TaxSummaries[0].TaxableBase.Should().Be(15.96m);
        dto.TaxSummaries[0].Amount.Should().Be(2.40m);
        dto.TaxSummaries[0].Rate.Should().Be(15m);

        dto.Lines.Should().ContainSingle();
        var lineDto = dto.Lines[0];
        lineDto.MainCode.Should().Be("PROV-001");
        lineDto.Description.Should().Be("COCA COLA 500 ML");
        lineDto.TotalAmount.Should().Be(18.36m);
        lineDto.LineTotal.Should().Be(18.36m);
        lineDto.Taxes.Should().ContainSingle(t => t.TaxCode == "2" && t.Amount == 2.4m);
    }

    [Fact]
    public async Task Handle_exposes_xml_totals_irbpnr_line_taxes_and_rounding_difference()
    {
        var document = ArcadorDocument();
        var lines = new[]
        {
            ArcadorLine(document.Id, "0580", "SPRITE HARMONY 1350 PET(12)", 10.72m, 1.61m, 0m, 0.24m),
            ArcadorLine(document.Id, "3172", "FANTA HARMONY NRJ 1350 PET(12)", 18.58m, 3.22m, 2.86m, 0.48m),
            ArcadorLine(document.Id, "12469", "INCA-KOLA ORGL 900ML PET NR 12", 12.98m, 1.95m, 0m, 0.72m),
            ArcadorLine(document.Id, "7796", "SPRITE HARMONY 500ML PET 12", 4.48m, 0.67m, 0m, 0.24m),
            ArcadorLine(document.Id, "11604", "FIORA HARMONY FRESA 500 PT(12)", 3.95m, 0.67m, 0.53m, 0.24m),
            ArcadorLine(document.Id, "11608", "FANTA HARMONY NRJ 500 PET(12)", 7.90m, 1.34m, 1.06m, 0.48m),
            ArcadorLine(document.Id, "10111", "COCA-COLA 350ML LATA(6)", 6.08m, 0.91m, 0m, 0m),
            ArcadorLine(document.Id, "0577", "SPRITE HARMONY 300 PET(12)", 2.59m, 0.39m, 0m, 0.24m),
            ArcadorLine(document.Id, "0126", "COCA-COLA E 1250 GRB(12)", 7.11m, 1.37m, 2.00m, 0m),
        };
        document.AttachSriAuthorization(
            "0107202601179141513200120290010012937141234567811",
            DateTime.UtcNow,
            ArcadorFacturaXml,
            DateTime.UtcNow,
            lines,
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "20",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                9,
                9,
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
        dto.Subtotal.Should().Be(74.39m);
        dto.DiscountAmount.Should().Be(0m);
        dto.IceAmount.Should().Be(6.45m);
        dto.IrbpnrAmount.Should().Be(2.64m);
        dto.VatAmount.Should().Be(12.13m);
        dto.TipAmount.Should().Be(0m);
        dto.TotalAmount.Should().Be(95.60m);
        dto.LineCalculatedTotal.Should().Be(95.61m);
        dto.RoundingDifference.Should().Be(-0.01m);
        dto.PaymentMethodCode.Should().Be("20");
        dto.PaymentTerm.Should().Be("30");
        dto.ReferralGuide.Should().Be("001-002-000000111");

        dto.TaxSummaries.Should()
            .Contain(t =>
                t.TaxCode == "5"
                && t.TaxRateCode == "5001"
                && t.TaxName == "IRBPNR"
                && t.Rate == 0.02m
                && t.TaxableBase == 132m
                && t.Amount == 2.64m
            );

        var fanta1350 = dto.Lines.Single(l => l.MainCode == "3172");
        fanta1350.IrbpnrAmount.Should().Be(0.48m);
        fanta1350.LineTotal.Should().Be(25.14m);
        fanta1350.Taxes.Should().Contain(t => t.TaxCode == "5" && t.TaxRateCode == "5001");

        var fanta500 = dto.Lines.Single(l => l.MainCode == "11608");
        fanta500.IceAmount.Should().Be(1.06m);
        fanta500.IrbpnrAmount.Should().Be(0.48m);

        var lata = dto.Lines.Single(l => l.MainCode == "10111");
        lata.IrbpnrAmount.Should().Be(0m);
        lata.Taxes.Should().NotContain(t => t.TaxCode == "5");

        dto.Lines.Single(l => l.MainCode == "0580")
            .AdditionalDetails.Should()
            .ContainSingle(d => d.Name == "Unidad" && d.Value == "PACA");
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

    private static PurchaseReceptionLine ArcadorLine(
        Guid documentId,
        string supplierCode,
        string description,
        decimal taxableBase,
        decimal vatAmount,
        decimal iceAmount,
        decimal irbpnrAmount
    ) =>
        PurchaseReceptionLine.Create(
            documentId,
            TenantId,
            description,
            1m,
            taxableBase,
            vatCode: "4",
            taxCode: "2",
            vatPercentage: 15m,
            taxValue: vatAmount,
            discountPct: 0m,
            discount: 0m,
            lineSubtotal: taxableBase,
            totalLine: taxableBase + vatAmount + iceAmount + irbpnrAmount,
            iceCode: iceAmount > 0m ? "3053" : null,
            iceValue: iceAmount,
            supplierCode: supplierCode
        );
}
