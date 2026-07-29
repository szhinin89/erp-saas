using System.Runtime.CompilerServices;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Application.Modules.Ride.Parsers;
using ERP.Application.Modules.Ride.Templates;
using ERP.Domain.Modules.Ride.ValueObjects;
using FluentAssertions;
using QuestPDF.Infrastructure;

namespace ERP.Infrastructure.Tests.Ride;

/// <summary>
/// Construye <see cref="InvoiceRideDocumentLayout"/> reales (XML real vía <c>InvoiceXmlBuilder</c>
/// → <see cref="InvoiceRideXmlParser"/> real → <see cref="DefaultInvoiceRideTemplate"/> real) —
/// nunca objetos de dominio armados a mano, mismo criterio que la Fase 6.
/// </summary>
internal static class RideRenderingFixtures
{
    [ModuleInitializer]
    public static void SetQuestPdfLicense() => QuestPDF.Settings.License = LicenseType.Community;

    private sealed class FakeTaxCategoryCodeResolver : ISriTaxCategoryCodeResolver
    {
        public string? Resolve(string taxCode) =>
            taxCode switch
            {
                "VAT" => "2",
                "ICE" => "3",
                _ => null,
            };
    }

    private static InvoiceRideDocumentLayout ComposeFrom(
        ElectronicDocumentData data,
        RideBranding? branding = null
    )
    {
        var xmlResult = new InvoiceXmlBuilder(new FakeTaxCategoryCodeResolver()).Build(data);
        xmlResult.IsSuccess.Should().BeTrue(xmlResult.Error);

        var parseResult = new InvoiceRideXmlParser().Parse(xmlResult.Value!.Xml);
        parseResult.IsSuccess.Should().BeTrue(parseResult.Error);

        var layout = new DefaultInvoiceRideTemplate().Compose(
            parseResult.Value!,
            branding ?? RideBranding.Empty()
        );
        return (InvoiceRideDocumentLayout)layout;
    }

    /// <summary>Una línea, un impuesto, un pago, sin campos opcionales — el layout más pequeño válido.</summary>
    public static InvoiceRideDocumentLayout Minimal() =>
        ComposeFrom(
            new ElectronicDocumentData(
                Emission: new ElectronicDocumentEmissionContext(
                    "1",
                    "1",
                    "01",
                    "001",
                    "Av. Amazonas y Naciones Unidas",
                    "001",
                    "000000001",
                    new DateTime(2026, 7, 1)
                ),
                Issuer: new ElectronicDocumentIssuerData(
                    "1790012345001",
                    "ACME CIA LTDA",
                    null,
                    "Av. Amazonas y Naciones Unidas",
                    null,
                    true
                ),
                Counterparty: new ElectronicDocumentCounterpartyData(
                    "05",
                    "1710034065",
                    "Juan Pérez",
                    null,
                    null
                ),
                Details:
                [
                    new ElectronicDocumentDetailLine(
                        "SKU-001",
                        "Producto",
                        1m,
                        10m,
                        0m,
                        10m,
                        [new ElectronicDocumentDetailTax("VAT", "2", 10m, 15m, 1.5m)]
                    ),
                ],
                TaxSummary: [new ElectronicDocumentTaxSummary("VAT", "2", 10m, 1.5m)],
                Totals: new ElectronicDocumentTotals(10m, 0m, 1.5m, 11.5m, "USD"),
                Payments: [new ElectronicDocumentPayment("01", 11.5m, null, null)],
                AdditionalInfo: []
            )
        );

    /// <summary>Factura completa: nombre comercial, régimen, dirección de comprador, info adicional, dos pagos.</summary>
    public static InvoiceRideDocumentLayout Full() =>
        ComposeFrom(
            new ElectronicDocumentData(
                Emission: new ElectronicDocumentEmissionContext(
                    "2",
                    "1",
                    "01",
                    "001",
                    "Av. Amazonas y Naciones Unidas",
                    "001",
                    "000000123",
                    new DateTime(2026, 7, 8)
                ),
                Issuer: new ElectronicDocumentIssuerData(
                    "1790012345001",
                    "ACME CIA LTDA",
                    "ACME",
                    "Av. Amazonas y Naciones Unidas",
                    "CONTRIBUYENTE RÉGIMEN RIMPE",
                    true
                ),
                Counterparty: new ElectronicDocumentCounterpartyData(
                    "05",
                    "1710034065",
                    "Juan Pérez",
                    "Calle Falsa 123",
                    "juan@example.com"
                ),
                Details:
                [
                    new ElectronicDocumentDetailLine(
                        "SKU-001",
                        "Producto sin ICE",
                        2m,
                        10m,
                        0m,
                        20m,
                        [new ElectronicDocumentDetailTax("VAT", "2", 20m, 15m, 3m)]
                    ),
                    new ElectronicDocumentDetailLine(
                        "SKU-002",
                        "Producto con ICE",
                        1m,
                        50m,
                        0m,
                        50m,
                        [
                            new ElectronicDocumentDetailTax("VAT", "2", 50m, 15m, 7.5m),
                            new ElectronicDocumentDetailTax("ICE", "3010", 50m, 10m, 5m),
                        ]
                    ),
                ],
                TaxSummary:
                [
                    new ElectronicDocumentTaxSummary("VAT", "2", 70m, 10.5m),
                    new ElectronicDocumentTaxSummary("ICE", "3010", 50m, 5m),
                ],
                Totals: new ElectronicDocumentTotals(70m, 0m, 15.5m, 85.5m, "USD"),
                Payments:
                [
                    new ElectronicDocumentPayment("01", 50m, null, null),
                    new ElectronicDocumentPayment("16", 35.5m, 30, "dias"),
                ],
                AdditionalInfo:
                [
                    new ElectronicDocumentAdditionalField("Email", "cliente@example.com"),
                    new ElectronicDocumentAdditionalField("Teléfono", "0999999999"),
                ]
            ),
            branding: RideBranding.Create(
                logoStoragePath: "branding/logo.png",
                primaryColorHex: "#112233",
                secondaryColorHex: "#445566",
                footerText: "Gracias por su compra"
            )
        );

    /// <summary>Dos tarifas de IVA distintas (0% y 15%) en la misma factura — Fase 10, golden file "con IVA".</summary>
    public static InvoiceRideDocumentLayout WithMixedVatRates() =>
        ComposeFrom(
            new ElectronicDocumentData(
                Emission: new ElectronicDocumentEmissionContext(
                    "1",
                    "1",
                    "01",
                    "001",
                    "Av. Amazonas y Naciones Unidas",
                    "001",
                    "000000300",
                    new DateTime(2026, 7, 10)
                ),
                Issuer: new ElectronicDocumentIssuerData(
                    "1790012345001",
                    "ACME CIA LTDA",
                    null,
                    "Av. Amazonas y Naciones Unidas",
                    null,
                    true
                ),
                Counterparty: new ElectronicDocumentCounterpartyData(
                    "05",
                    "1710034065",
                    "Juan Pérez",
                    null,
                    null
                ),
                Details:
                [
                    new ElectronicDocumentDetailLine(
                        "SKU-010",
                        "Producto gravado 0%",
                        3m,
                        5m,
                        0m,
                        15m,
                        [new ElectronicDocumentDetailTax("VAT", "0", 15m, 0m, 0m)]
                    ),
                    new ElectronicDocumentDetailLine(
                        "SKU-011",
                        "Producto gravado 15%",
                        2m,
                        20m,
                        0m,
                        40m,
                        [new ElectronicDocumentDetailTax("VAT", "2", 40m, 15m, 6m)]
                    ),
                ],
                TaxSummary:
                [
                    new ElectronicDocumentTaxSummary("VAT", "0", 15m, 0m),
                    new ElectronicDocumentTaxSummary("VAT", "2", 40m, 6m),
                ],
                Totals: new ElectronicDocumentTotals(55m, 0m, 6m, 61m, "USD"),
                Payments: [new ElectronicDocumentPayment("01", 61m, null, null)],
                AdditionalInfo: []
            )
        );

    /// <summary>Línea con ICE además de IVA — Fase 10, golden file "con ICE".</summary>
    public static InvoiceRideDocumentLayout WithIce() =>
        ComposeFrom(
            new ElectronicDocumentData(
                Emission: new ElectronicDocumentEmissionContext(
                    "1",
                    "1",
                    "01",
                    "001",
                    "Av. Amazonas y Naciones Unidas",
                    "001",
                    "000000301",
                    new DateTime(2026, 7, 10)
                ),
                Issuer: new ElectronicDocumentIssuerData(
                    "1790012345001",
                    "ACME CIA LTDA",
                    null,
                    "Av. Amazonas y Naciones Unidas",
                    null,
                    true
                ),
                Counterparty: new ElectronicDocumentCounterpartyData(
                    "05",
                    "1710034065",
                    "Juan Pérez",
                    null,
                    null
                ),
                Details:
                [
                    new ElectronicDocumentDetailLine(
                        "SKU-020",
                        "Producto con ICE",
                        1m,
                        100m,
                        0m,
                        100m,
                        [
                            new ElectronicDocumentDetailTax("VAT", "2", 100m, 15m, 15m),
                            new ElectronicDocumentDetailTax("ICE", "3010", 100m, 10m, 10m),
                        ]
                    ),
                ],
                TaxSummary:
                [
                    new ElectronicDocumentTaxSummary("VAT", "2", 100m, 15m),
                    new ElectronicDocumentTaxSummary("ICE", "3010", 100m, 10m),
                ],
                Totals: new ElectronicDocumentTotals(100m, 0m, 25m, 125m, "USD"),
                Payments: [new ElectronicDocumentPayment("01", 125m, null, null)],
                AdditionalInfo: []
            )
        );

    /// <summary>Varios campos de información adicional — Fase 10, golden file "con información adicional".</summary>
    public static InvoiceRideDocumentLayout WithAdditionalInfo() =>
        ComposeFrom(
            new ElectronicDocumentData(
                Emission: new ElectronicDocumentEmissionContext(
                    "1",
                    "1",
                    "01",
                    "001",
                    "Av. Amazonas y Naciones Unidas",
                    "001",
                    "000000302",
                    new DateTime(2026, 7, 10)
                ),
                Issuer: new ElectronicDocumentIssuerData(
                    "1790012345001",
                    "ACME CIA LTDA",
                    null,
                    "Av. Amazonas y Naciones Unidas",
                    null,
                    true
                ),
                Counterparty: new ElectronicDocumentCounterpartyData(
                    "05",
                    "1710034065",
                    "Juan Pérez",
                    "Calle Falsa 123",
                    "juan@example.com"
                ),
                Details:
                [
                    new ElectronicDocumentDetailLine(
                        "SKU-030",
                        "Producto",
                        1m,
                        10m,
                        0m,
                        10m,
                        [new ElectronicDocumentDetailTax("VAT", "2", 10m, 15m, 1.5m)]
                    ),
                ],
                TaxSummary: [new ElectronicDocumentTaxSummary("VAT", "2", 10m, 1.5m)],
                Totals: new ElectronicDocumentTotals(10m, 0m, 1.5m, 11.5m, "USD"),
                Payments: [new ElectronicDocumentPayment("01", 11.5m, null, null)],
                AdditionalInfo:
                [
                    new ElectronicDocumentAdditionalField("Email", "cliente@example.com"),
                    new ElectronicDocumentAdditionalField("Teléfono", "0999999999"),
                    new ElectronicDocumentAdditionalField(
                        "Dirección de entrega",
                        "Av. Siempre Viva 742"
                    ),
                    new ElectronicDocumentAdditionalField("Orden de compra", "OC-2026-00981"),
                ]
            )
        );

    /// <summary>Tres formas de pago distintas en la misma factura — Fase 10, golden file "con varios pagos".</summary>
    public static InvoiceRideDocumentLayout WithMultiplePayments() =>
        ComposeFrom(
            new ElectronicDocumentData(
                Emission: new ElectronicDocumentEmissionContext(
                    "1",
                    "1",
                    "01",
                    "001",
                    "Av. Amazonas y Naciones Unidas",
                    "001",
                    "000000303",
                    new DateTime(2026, 7, 10)
                ),
                Issuer: new ElectronicDocumentIssuerData(
                    "1790012345001",
                    "ACME CIA LTDA",
                    null,
                    "Av. Amazonas y Naciones Unidas",
                    null,
                    true
                ),
                Counterparty: new ElectronicDocumentCounterpartyData(
                    "05",
                    "1710034065",
                    "Juan Pérez",
                    null,
                    null
                ),
                Details:
                [
                    new ElectronicDocumentDetailLine(
                        "SKU-040",
                        "Producto",
                        1m,
                        100m,
                        0m,
                        100m,
                        [new ElectronicDocumentDetailTax("VAT", "2", 100m, 15m, 15m)]
                    ),
                ],
                TaxSummary: [new ElectronicDocumentTaxSummary("VAT", "2", 100m, 15m)],
                Totals: new ElectronicDocumentTotals(100m, 0m, 15m, 115m, "USD"),
                Payments:
                [
                    new ElectronicDocumentPayment("01", 50m, null, null),
                    new ElectronicDocumentPayment("16", 35m, 30, "dias"),
                    new ElectronicDocumentPayment("20", 30m, 60, "dias"),
                ],
                AdditionalInfo: []
            )
        );

    /// <summary>Ocho líneas de detalle — para el Caso 3 (factura con múltiples líneas).</summary>
    public static InvoiceRideDocumentLayout ManyLines(int lineCount = 8)
    {
        var details = Enumerable
            .Range(1, lineCount)
            .Select(i => new ElectronicDocumentDetailLine(
                $"SKU-{i:000}",
                $"Producto {i}",
                1m,
                10m,
                0m,
                10m,
                [new ElectronicDocumentDetailTax("VAT", "2", 10m, 15m, 1.5m)]
            ))
            .ToList();

        var subtotal = 10m * lineCount;
        var tax = 1.5m * lineCount;

        return ComposeFrom(
            new ElectronicDocumentData(
                Emission: new ElectronicDocumentEmissionContext(
                    "1",
                    "1",
                    "01",
                    "001",
                    "Av. Amazonas y Naciones Unidas",
                    "001",
                    "000000200",
                    new DateTime(2026, 7, 9)
                ),
                Issuer: new ElectronicDocumentIssuerData(
                    "1790012345001",
                    "ACME CIA LTDA",
                    null,
                    "Av. Amazonas y Naciones Unidas",
                    null,
                    true
                ),
                Counterparty: new ElectronicDocumentCounterpartyData(
                    "05",
                    "1710034065",
                    "Juan Pérez",
                    null,
                    null
                ),
                Details: details,
                TaxSummary: [new ElectronicDocumentTaxSummary("VAT", "2", subtotal, tax)],
                Totals: new ElectronicDocumentTotals(subtotal, 0m, tax, subtotal + tax, "USD"),
                Payments: [new ElectronicDocumentPayment("01", subtotal + tax, null, null)],
                AdditionalInfo: []
            )
        );
    }
}
