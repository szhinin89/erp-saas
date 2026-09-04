using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Application.Modules.Ride.Parsers;
using ERP.Application.Modules.Ride.Templates;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Ride.ValueObjects;
using FluentAssertions;

namespace ERP.Infrastructure.Tests.Ride;

/// <summary>
/// RETENTIONS-RIDE-PDF-RENDERER-03D — construye <see cref="RetentionRideDocumentLayout"/> reales
/// (XML real vía <see cref="RetentionXmlBuilder"/> → <see cref="RetentionRideXmlParser"/> real →
/// <see cref="RetentionRideTemplate"/> real) — nunca objetos armados a mano, mismo criterio que
/// <see cref="RideRenderingFixtures"/> (Factura). El número completo usado en <see cref="Full"/>
/// es <c>001-001-000000850</c>, el mismo que exige el test de contenido de esta fase.
/// </summary>
internal static class RetentionRideRenderingFixtures
{
    private static RetentionRideDocumentLayout ComposeFrom(
        RetentionElectronicDocumentData data,
        RideBranding? branding = null
    )
    {
        var xmlResult = new RetentionXmlBuilder().Build(data);
        xmlResult.IsSuccess.Should().BeTrue(xmlResult.Error);

        var parseResult = new RetentionRideXmlParser().Parse(xmlResult.Value!.Xml);
        parseResult.IsSuccess.Should().BeTrue(parseResult.Error);

        var layout = new RetentionRideTemplate().Compose(
            parseResult.Value!,
            branding ?? RideBranding.Empty()
        );
        return (RetentionRideDocumentLayout)layout;
    }

    /// <summary>Comprobante completo: nombre comercial, documento sustento, dos líneas de retención, información adicional.</summary>
    public static RetentionRideDocumentLayout Full() =>
        ComposeFrom(
            new RetentionElectronicDocumentData(
                Metadata: new RetentionElectronicDocumentMetadata(
                    RetentionId: Guid.NewGuid(),
                    TenantId: Guid.NewGuid(),
                    CompanyId: Guid.NewGuid(),
                    EmissionPointId: Guid.NewGuid(),
                    SourceDocumentType: RetentionSourceDocumentType.ExpenseDocument,
                    SourceDocumentId: Guid.NewGuid(),
                    GeneratedAtUtc: new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc)
                ),
                Emission: new ElectronicDocumentEmissionContext(
                    Environment: "1",
                    EmissionType: "1",
                    DocTypeCode: "07",
                    Establishment: "001",
                    EstablishmentAddress: "Av. Principal 123",
                    EmissionPoint: "001",
                    Sequential: "000000850",
                    IssueDate: new DateTime(2026, 8, 5)
                ),
                NumeroCompleto: "001-001-000000850",
                Issuer: new ElectronicDocumentIssuerData(
                    TaxId: "1790012345001",
                    LegalName: "Empresa Test S.A.",
                    TradeName: "Empresa Test",
                    MatrixAddress: "Matriz 456",
                    TaxRegime: null,
                    IsAccountingRequired: true
                ),
                RetentionInfo: new RetentionElectronicDocumentInfo(
                    SpecialTaxpayerNumber: null,
                    FiscalPeriod: "08/2026"
                ),
                SubjectWithheld: new ElectronicDocumentCounterpartyData(
                    IdentificationType: "05",
                    IdentificationNumber: "1710034065",
                    LegalName: "Proveedor Test",
                    Address: null,
                    Email: null
                ),
                SourceDocument: new RetentionElectronicDocumentSourceDocument(
                    TaxSupportCode: "01",
                    DocTypeCode: "01",
                    Number: "001-001-000000456",
                    AuthorizationNumber: "1234567890",
                    IssueDate: new DateOnly(2026, 8, 1),
                    Subtotal: 100m,
                    Total: 115m
                ),
                Lines:
                [
                    new RetentionElectronicDocumentTaxLine(
                        TaxType: RetentionTaxType.Vat,
                        SriTaxTypeCode: "2",
                        RetentionCode: "725",
                        RetentionCodeDescription: "IVA retenido 30% bienes",
                        BaseAmount: 15m,
                        RetentionRate: 30m,
                        RetainedAmount: 4.5m
                    ),
                    new RetentionElectronicDocumentTaxLine(
                        TaxType: RetentionTaxType.Income,
                        SriTaxTypeCode: "1",
                        RetentionCode: "303",
                        RetentionCodeDescription: "Honorarios profesionales",
                        BaseAmount: 100m,
                        RetentionRate: 8m,
                        RetainedAmount: 8m
                    ),
                ],
                Totals: new RetentionElectronicDocumentTotals(
                    TotalRetainedVat: 4.5m,
                    TotalRetainedIncome: 8m,
                    TotalRetained: 12.5m
                ),
                AdditionalInfo: []
            ),
            branding: RideBranding.Create(
                logoStoragePath: null,
                primaryColorHex: "#112233",
                secondaryColorHex: "#445566",
                footerText: "Comprobante generado electrónicamente"
            )
        );

    /// <summary>Sin nombre comercial, sin documento sustento (los tres campos opcionales del XSD 1.0.0 ausentes) — el layout más pequeño válido.</summary>
    public static RetentionRideDocumentLayout Minimal() =>
        ComposeFrom(
            new RetentionElectronicDocumentData(
                Metadata: new RetentionElectronicDocumentMetadata(
                    RetentionId: Guid.NewGuid(),
                    TenantId: Guid.NewGuid(),
                    CompanyId: Guid.NewGuid(),
                    EmissionPointId: Guid.NewGuid(),
                    SourceDocumentType: RetentionSourceDocumentType.ExpenseDocument,
                    SourceDocumentId: Guid.NewGuid(),
                    GeneratedAtUtc: new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc)
                ),
                Emission: new ElectronicDocumentEmissionContext(
                    Environment: "1",
                    EmissionType: "1",
                    DocTypeCode: "07",
                    Establishment: "001",
                    EstablishmentAddress: "Av. Principal 123",
                    EmissionPoint: "001",
                    Sequential: "000000001",
                    IssueDate: new DateTime(2026, 8, 1)
                ),
                NumeroCompleto: "001-001-000000001",
                Issuer: new ElectronicDocumentIssuerData(
                    TaxId: "1790012345001",
                    LegalName: "Empresa Test S.A.",
                    TradeName: null,
                    MatrixAddress: "Matriz 456",
                    TaxRegime: null,
                    IsAccountingRequired: true
                ),
                RetentionInfo: new RetentionElectronicDocumentInfo(
                    SpecialTaxpayerNumber: null,
                    FiscalPeriod: "08/2026"
                ),
                SubjectWithheld: new ElectronicDocumentCounterpartyData(
                    IdentificationType: "05",
                    IdentificationNumber: "1710034065",
                    LegalName: "Proveedor Test",
                    Address: null,
                    Email: null
                ),
                SourceDocument: new RetentionElectronicDocumentSourceDocument(
                    TaxSupportCode: null,
                    DocTypeCode: null,
                    Number: null,
                    AuthorizationNumber: null,
                    IssueDate: null,
                    Subtotal: null,
                    Total: null
                ),
                Lines:
                [
                    new RetentionElectronicDocumentTaxLine(
                        TaxType: RetentionTaxType.Income,
                        SriTaxTypeCode: "1",
                        RetentionCode: "303",
                        RetentionCodeDescription: "Honorarios profesionales",
                        BaseAmount: 100m,
                        RetentionRate: 8m,
                        RetainedAmount: 8m
                    ),
                ],
                Totals: new RetentionElectronicDocumentTotals(
                    TotalRetainedVat: 0m,
                    TotalRetainedIncome: 8m,
                    TotalRetained: 8m
                ),
                AdditionalInfo: []
            )
        );
}
