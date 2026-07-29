using ERP.Application.Common;
using ERP.Application.Modules.Ride.Parsers;
using ERP.Application.Modules.Ride.Rendering;
using ERP.Application.Modules.Ride.Templates;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Application.Tests.Ride;

/// <summary>
/// Dobles de prueba EXCLUSIVOS de test (Fase 5, ADR-025) — nunca comprobante real. Demuestran
/// que el pipeline completa la orquestación conociendo únicamente los contratos de Strategy, sin
/// que <see cref="ERP.Application.Modules.Ride.Services.RidePipeline"/> sepa que estos tipos existen.
/// </summary>
internal sealed class FakeRideXmlParser(RideDocumentType documentType, RideModel? model = null)
    : IRideXmlParser
{
    public RideDocumentType DocumentType => documentType;

    public Result<RideModel> Parse(string authorizedXml) =>
        Result<RideModel>.Success(model ?? RideTestModelBuilder.Build());
}

internal sealed class FakeRideDocumentLayout : IRideDocumentLayout;

internal sealed class FakeRideTemplate(RideDocumentType documentType) : IRideTemplate
{
    public RideDocumentType DocumentType => documentType;

    public IRideDocumentLayout Compose(RideModel model, RideBranding branding) =>
        new FakeRideDocumentLayout();
}

/// <summary>Construye un <see cref="RideModel"/> mínimo válido — misma forma usada en RideModelTests (Fase 2).</summary>
internal static class RideTestModelBuilder
{
    public static RideModel Build()
    {
        var header = RideHeader.Create(
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

        var issuer = RideParty.Create(
            identificationType: null,
            identificationNumber: "1790012345001",
            legalName: "ZH Technologies S.A."
        );
        var receiver = RideParty.Create(
            identificationType: "05",
            identificationNumber: "1712345678",
            legalName: "Juan Pérez"
        );

        var line = RideLine.Create(
            code: "PROD-001",
            description: "Producto de prueba",
            quantity: 2m,
            unitPrice: 50m,
            discount: 0m,
            subtotal: 100m,
            taxes: [RideTaxSummary.Create("2", "2", 100m, 15m, rate: 15m)]
        );

        return RideModel.Create(
            header,
            issuer,
            receiver,
            lines: [line],
            taxSummary: [RideTaxSummary.Create("2", "2", 100m, 15m)],
            payments: [RidePaymentInfo.Create("01", 115m)],
            additionalInfo: []
        );
    }
}
