using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using ERP.Application.Modules.Purchases.Services;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

public sealed class PurchaseXmlConfirmationGuardTests
{
    private const string Xml = """
        <factura><infoTributaria><ruc>1790012345001</ruc><razonSocial>Proveedor</razonSocial>
        <codDoc>01</codDoc><estab>001</estab><ptoEmi>001</ptoEmi><secuencial>000000001</secuencial>
        </infoTributaria><infoFactura><fechaEmision>27/09/2026</fechaEmision>
        <totalSinImpuestos>90.00</totalSinImpuestos><importeTotal>90.00</importeTotal></infoFactura>
        <detalles><detalle><codigoPrincipal>SKU</codigoPrincipal><descripcion>Producto</descripcion>
        <cantidad>2</cantidad><precioUnitario>50</precioUnitario><descuento>10</descuento>
        <precioTotalSinImpuesto>90.00</precioTotalSinImpuesto><impuestos><impuesto>
        <codigo>2</codigo><codigoPorcentaje>0</codigoPorcentaje><tarifa>0</tarifa>
        <baseImponible>90</baseImponible><valor>0</valor></impuesto></impuestos>
        </detalle></detalles></factura>
        """;

    private const string FreightDetail = """
        <detalle><codigoPrincipal>FLETE</codigoPrincipal><descripcion>Transporte</descripcion>
        <cantidad>1</cantidad><precioUnitario>10</precioUnitario><descuento>0</descuento>
        <precioTotalSinImpuesto>10.00</precioTotalSinImpuesto><impuestos><impuesto>
        <codigo>2</codigo><codigoPorcentaje>0</codigoPorcentaje><tarifa>0</tarifa>
        <baseImponible>10</baseImponible><valor>0</valor></impuesto></impuestos></detalle></detalles>
        """;

    private static (PurchaseInvoice Invoice, PurchaseXmlConfirmationGuard Guard) Build(
        string scenario
    )
    {
        var freightLineRemoved = scenario == "freight_line_removed";
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var supplier = Guid.NewGuid();
        var user = Guid.NewGuid();
        var key = new string('1', 49);
        var invoice = PurchaseInvoice.CreateDraft(
            tenant,
            company,
            branch,
            supplier,
            "Proveedor",
            "1790012345001",
            "01",
            "001-001-000000001",
            new DateOnly(2026, 9, 27),
            user,
            Guid.NewGuid(),
            "Contado",
            1,
            0,
            accessKey: key,
            globalWarehouseId: Guid.NewGuid()
        );
        var source = PurchaseReceptionDocument.Create(
            tenant,
            scenario == "wrong_company" ? Guid.NewGuid() : company,
            branch,
            PurchaseReceptionSourceDocType.Invoice,
            "1790012345001",
            "Proveedor",
            supplier,
            key,
            invoice.InvoiceNumber,
            invoice.IssueDate,
            null,
            freightLineRemoved ? 100m : 90m,
            0m,
            freightLineRemoved ? 100m : 90m,
            user
        );
        var original = PurchaseReceptionLine.Create(
            source.Id,
            tenant,
            "Producto",
            2m,
            50m,
            "0",
            "2",
            0m,
            0m,
            10m,
            10m,
            90m,
            90m,
            supplierCode: "SKU"
        );
        var transport = PurchaseReceptionLine.Create(
            source.Id,
            tenant,
            "Transporte",
            1m,
            10m,
            "0",
            "2",
            0m,
            0m,
            0m,
            0m,
            10m,
            10m,
            supplierCode: "FLETE"
        );
        var xml = scenario switch
        {
            // Header rounding/tip differences are informational: line bases still reconcile.
            "header_rounding" => Xml.Replace("<importeTotal>90.00", "<importeTotal>90.01"),
            "missing_net_total" => Xml.Replace("<totalSinImpuestos>90.00</totalSinImpuestos>", ""),
            "net_total" => Xml.Replace("<totalSinImpuestos>90.00", "<totalSinImpuestos>80.00"),
            "freight_line_removed" => Xml.Replace("</detalles>", FreightDetail)
                .Replace("<totalSinImpuestos>90.00", "<totalSinImpuestos>100.00")
                .Replace("<importeTotal>90.00", "<importeTotal>100.00"),
            "bad_xml_line" => Xml.Replace("<cantidad>2</cantidad>", "<cantidad>invalid</cantidad>"),
            _ => Xml,
        };
        var receptionLines = freightLineRemoved
            ? new[] { original, transport }
            : new[] { original };
        if (scenario != "manual_imported")
            source.AttachSriAuthorization(
                key,
                DateTime.UtcNow,
                xml,
                DateTime.UtcNow,
                receptionLines,
                user,
                "01",
                null,
                new PurchaseReceptionProcessingOutcome(
                    scenario == "clean"
                        ? PurchaseReceptionProcessingStatus.Processed
                        : PurchaseReceptionProcessingStatus.ProcessedWithWarnings,
                    scenario == "missing_line" ? 2 : receptionLines.Length,
                    receptionLines.Length,
                    "Advertencia informativa de matching"
                )
            );
        // Real flow: CreatePurchaseDraft marks the reception Processed for the new purchase.
        if (scenario == "processed_same_purchase")
            source.MarkProcessed(invoice.Id, user);
        if (scenario == "processed_other_purchase")
            source.MarkProcessed(Guid.NewGuid(), user);
        var line = PurchaseInvoiceDetail.Create(
            invoice.Id,
            tenant,
            "Producto",
            scenario == "quantity" ? 3m : 2m,
            50m,
            "0",
            "UNIT",
            discountPct: 10m,
            itemId: Guid.NewGuid(),
            purchaseReceptionLineId: scenario is "unlinked" or "manual_imported"
                ? null
                : original.Id,
            exactDiscountAmount: 10m
        );
        invoice.ReplaceLines([line], user);
        if (scenario == "duplicate")
            invoice.ReplaceLines(
                [
                    line,
                    PurchaseInvoiceDetail.Create(
                        invoice.Id,
                        tenant,
                        "Duplicado",
                        2m,
                        50m,
                        "0",
                        "UNIT",
                        itemId: Guid.NewGuid(),
                        purchaseReceptionLineId: original.Id
                    ),
                ],
                user
            );
        // Additional allocated costs do not alter the original supplier XML amounts.
        invoice.DistributeCosts(12.50m, 3m, user);
        if (freightLineRemoved)
            invoice.DistributeAdditionalCost(
                ERP.Domain.Modules.Purchases.Enums.PurchaseCostType.Freight,
                10m,
                [invoice.Lines[0].Id],
                user
            );
        var repo = new Mock<IPurchaseReceptionDocumentRepository>();
        if (scenario != "unknown_source")
            repo.Setup(r => r.GetByAccessKeyAsync(tenant, key, It.IsAny<CancellationToken>()))
                .ReturnsAsync(source);
        return (
            invoice,
            new PurchaseXmlConfirmationGuard(repo.Object, new PurchaseXmlDraftParser())
        );
    }

    [Theory]
    [InlineData("clean")]
    [InlineData("informational_warning")]
    [InlineData("header_rounding")]
    [InlineData("processed_same_purchase")]
    [InlineData("manual_imported")]
    public async Task Reconciled_xml_allows_confirmation_and_preserves_additional_costs(
        string scenario
    )
    {
        var (invoice, guard) = Build(scenario);
        (await guard.ValidateAsync(invoice, CancellationToken.None)).Should().BeNull();
        invoice.TotalFreight.Should().Be(12.50m);
        invoice.TotalOtherCosts.Should().Be(3m);
    }

    [Fact]
    public async Task Xml_line_removed_and_redistributed_as_freight_is_blocked_for_traceability()
    {
        // Totals alone would reconcile (the 10.00 moved into freight); the lost line link must not.
        var (invoice, guard) = Build("freight_line_removed");
        invoice.TotalFreight.Should().Be(22.50m);
        (await guard.ValidateAsync(invoice, CancellationToken.None))
            .Should()
            .Contain("omitidas")
            .And.Contain("trazabilidad");
    }

    [Theory]
    [InlineData("missing_line")]
    [InlineData("net_total")]
    [InlineData("missing_net_total")]
    [InlineData("freight_line_removed")]
    [InlineData("processed_other_purchase")]
    [InlineData("bad_xml_line")]
    [InlineData("quantity")]
    [InlineData("duplicate")]
    [InlineData("unlinked")]
    [InlineData("wrong_company")]
    [InlineData("unknown_source")]
    public async Task Critical_inconsistency_requires_reconciliation(string scenario)
    {
        var (invoice, guard) = Build(scenario);
        (await guard.ValidateAsync(invoice, CancellationToken.None))
            .Should()
            .NotBeNullOrWhiteSpace();
        invoice.Status.Should().Be(ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Draft);
    }
}
