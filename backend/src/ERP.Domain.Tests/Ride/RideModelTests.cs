using ERP.Domain.Modules.Ride.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.Ride;

/// <summary>
/// Construye <see cref="RideModel"/> con la forma completa de una factura autorizada real
/// (campos verificados contra <c>InvoiceXmlBuilder</c> de ElectronicDocuments — infoTributaria,
/// infoFactura, detalles, totalImpuesto, pagos, infoAdicional) — sin depender de ese módulo,
/// solo replicando la misma forma de datos con los VOs propios de Ride.
/// </summary>
public sealed class RideModelTests
{
    private static RideHeader ValidHeader() =>
        RideHeader.Create(
            environment: "2",
            emissionType: "1",
            documentTypeCode: "01",
            establishment: "001",
            emissionPoint: "001",
            sequential: "000000123",
            establishmentAddress: "Av. Amazonas y Naciones Unidas",
            issueDate: new DateOnly(2026, 7, 1),
            currencyCode: "DOLAR",
            accessKey: RideAccessKey.Create(new string('7', 49)),
            authorizationNumber: new string('7', 49),
            authorizationDate: new DateTime(2026, 7, 1, 10, 30, 0, DateTimeKind.Utc),
            subtotalWithoutTax: 100m,
            totalDiscount: 0m,
            tip: 0m,
            grandTotal: 115m
        );

    private static RideParty Issuer() =>
        RideParty.Create(
            identificationType: null,
            identificationNumber: "1790012345001",
            legalName: "ZH Technologies S.A.",
            tradeName: "ZH Tech",
            address: "Av. Amazonas N34-10",
            isAccountingRequired: true,
            taxRegime: null
        );

    private static RideParty Receiver() =>
        RideParty.Create(
            identificationType: "05",
            identificationNumber: "1712345678",
            legalName: "Juan Pérez",
            address: "Calle Falsa 123"
        );

    private static RideLine OneLine() =>
        RideLine.Create(
            code: "PROD-001",
            description: "Producto de prueba",
            quantity: 2m,
            unitPrice: 50m,
            discount: 0m,
            subtotal: 100m,
            taxes: [RideTaxSummary.Create("2", "2", 100m, 15m, rate: 15m)]
        );

    private static RidePaymentInfo OnePayment() => RidePaymentInfo.Create("01", 115m);

    [Fact]
    public void Create_with_full_authorized_invoice_shape_succeeds()
    {
        var model = RideModel.Create(
            header: ValidHeader(),
            issuer: Issuer(),
            receiver: Receiver(),
            lines: [OneLine()],
            taxSummary: [RideTaxSummary.Create("2", "2", 100m, 15m)],
            payments: [OnePayment()],
            additionalInfo: [RideAdditionalInfo.Create("Email", "cliente@example.com")]
        );

        model.Header.GrandTotal.Should().Be(115m);
        model.Issuer.LegalName.Should().Be("ZH Technologies S.A.");
        model.Receiver.IdentificationNumber.Should().Be("1712345678");
        model.Lines.Should().HaveCount(1);
        model.TaxSummary.Should().HaveCount(1);
        model.Payments.Should().HaveCount(1);
        model.AdditionalInfo.Should().HaveCount(1);
    }

    [Fact]
    public void Create_with_empty_optional_lists_succeeds()
    {
        var model = RideModel.Create(
            header: ValidHeader(),
            issuer: Issuer(),
            receiver: Receiver(),
            lines: [OneLine()],
            taxSummary: [RideTaxSummary.Create("2", "2", 100m, 15m)],
            payments: [OnePayment()],
            additionalInfo: []
        );

        model.AdditionalInfo.Should().BeEmpty();
    }

    [Fact]
    public void Create_with_null_header_throws()
    {
        var act = () =>
            RideModel.Create(
                header: null!,
                issuer: Issuer(),
                receiver: Receiver(),
                lines: [OneLine()],
                taxSummary: [],
                payments: [OnePayment()],
                additionalInfo: []
            );

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_with_null_issuer_throws()
    {
        var act = () =>
            RideModel.Create(
                header: ValidHeader(),
                issuer: null!,
                receiver: Receiver(),
                lines: [OneLine()],
                taxSummary: [],
                payments: [OnePayment()],
                additionalInfo: []
            );

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_with_null_lines_throws()
    {
        var act = () =>
            RideModel.Create(
                header: ValidHeader(),
                issuer: Issuer(),
                receiver: Receiver(),
                lines: null!,
                taxSummary: [],
                payments: [OnePayment()],
                additionalInfo: []
            );

        act.Should().Throw<ArgumentNullException>();
    }
}
