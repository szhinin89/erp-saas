using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Constants;
using ERP.Domain.Modules.SriCatalogs.Entities;
using System.Globalization;

namespace ERP.Application.Modules.Retentions.Services;

/// <summary>
/// RETENTIONS-ELECTRONIC-DOCUMENT-MODEL-03A — traduce un <see cref="RetentionDocument"/> ya
/// <c>Issued</c> al modelo canónico <see cref="RetentionElectronicDocumentData"/>. Mismo criterio
/// de dos fases que <see cref="ERP.Application.Modules.Sales.Services.SalesInvoiceElectronicDocumentDataProvider"/>:
/// este proveedor solo construye el modelo — no genera XML, no genera RIDE, no firma ni envía al
/// SRI, no calcula <c>claveAcceso</c> (eso es responsabilidad exclusiva del futuro
/// <c>RetentionXmlBuilder</c>, RETENTIONS-SRI-XML-MAPPER-03B).
///
/// Deliberadamente NO implementa <see cref="IElectronicDocumentDataProvider"/> (esa interfaz
/// retorna <c>ElectronicDocumentData</c>, la forma comercial de Factura/Nota de Crédito — no la
/// forma de <c>RetentionElectronicDocumentData</c>). Tiene su propio contrato mínimo,
/// <see cref="IRetentionElectronicDocumentDataProvider"/>, para no forzar un genérico
/// <c>IElectronicDocumentDataProvider&lt;T&gt;</c> ni tocar el motor genérico de ElectronicDocuments
/// en esta fase — esa decisión de wiring queda para RETENTIONS-SRI-XML-MAPPER-03B.
///
/// Fuente única de cada dato: <see cref="RetentionDocument"/> y sus snapshots ya congelados
/// (documento sustento, líneas, totales) para todo lo que el agregado ya conoce; Company/
/// Establishment/EmissionPoint/SriSettings/BusinessPartner solo para lo que el agregado
/// deliberadamente no snapshotea (identidad de la empresa emisora, direcciones de
/// establecimiento, identificación del sujeto retenido). Nada se recalcula.
/// </summary>
public interface IRetentionElectronicDocumentDataProvider
{
    Task<Result<RetentionElectronicDocumentData>> GetDataAsync(
        ElectronicDocumentSourceReference reference,
        CancellationToken ct = default
    );
}

public sealed class RetentionElectronicDocumentDataProvider : IRetentionElectronicDocumentDataProvider
{
    private readonly IRetentionDocumentRepository _retentionRepository;
    private readonly IEmissionPointRepository _emissionPointRepository;
    private readonly IEstablishmentRepository _establishmentRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly ISriSettingsRepository _sriSettingsRepository;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly ISriDocTypeCatalogResolver _docTypeCatalogResolver;

    public RetentionElectronicDocumentDataProvider(
        IRetentionDocumentRepository retentionRepository,
        IEmissionPointRepository emissionPointRepository,
        IEstablishmentRepository establishmentRepository,
        ICompanyRepository companyRepository,
        ISriSettingsRepository sriSettingsRepository,
        IBusinessPartnerRepository businessPartnerRepository,
        ISriDocTypeCatalogResolver docTypeCatalogResolver
    )
    {
        _retentionRepository = retentionRepository;
        _emissionPointRepository = emissionPointRepository;
        _establishmentRepository = establishmentRepository;
        _companyRepository = companyRepository;
        _sriSettingsRepository = sriSettingsRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _docTypeCatalogResolver = docTypeCatalogResolver;
    }

    public async Task<Result<RetentionElectronicDocumentData>> GetDataAsync(
        ElectronicDocumentSourceReference reference,
        CancellationToken ct = default
    )
    {
        var retention = await _retentionRepository.GetByIdAsync(
            reference.TenantId,
            reference.SourceEntityId,
            ct
        );
        if (retention is null)
            return Result<RetentionElectronicDocumentData>.NotFound("La retención no existe.");

        var errors = new List<string>();

        // Solo una retención Issued tiene RetentionNumber/IssueDate/FiscalPeriod asignados
        // (ver RetentionDocument.Issue) — nunca se genera este modelo para un Draft/Cancelled.
        if (retention.Status != RetentionStatus.Issued)
            errors.Add("La retención debe estar emitida para generar el documento electrónico.");
        if (retention.RetentionNumber is null || retention.IssueDate is null || retention.FiscalPeriod is null)
            errors.Add(
                "La retención no tiene número, fecha de emisión o período fiscal asignados."
            );
        if (retention.Lines.Count == 0)
            errors.Add("La retención no tiene líneas de impuesto retenido.");

        var emissionPoint = await _emissionPointRepository.GetByIdAsync(
            retention.EmissionPointId,
            reference.TenantId,
            ct
        );
        if (emissionPoint is null)
            errors.Add("El punto de emisión de la retención ya no existe o está inactivo.");

        var company = await _companyRepository.GetByIdForTenantAsync(
            reference.CompanyId,
            reference.TenantId,
            ct
        );
        if (company is null)
            errors.Add("La empresa emisora no existe.");

        var mainEstablishment = await _establishmentRepository.GetMainByCompanyAsync(
            reference.TenantId,
            reference.CompanyId,
            ct
        );
        if (mainEstablishment is null)
            errors.Add("La empresa no tiene un establecimiento matriz configurado.");

        var sriSettings = await _sriSettingsRepository.GetByCompanyIdAsync(reference.CompanyId, ct);
        if (sriSettings is null)
            errors.Add(
                "La empresa no tiene configuración SRI (ambiente / tipo de emisión) definida."
            );

        var subject = await _businessPartnerRepository.GetByIdAsync(
            retention.SubjectBusinessPartnerId,
            ct
        );
        if (subject is null)
            errors.Add("El sujeto retenido (proveedor) ya no existe.");

        // Única fuente de verdad del código de tipo de comprobante: el catálogo sri_doc_types —
        // mismo criterio que SalesInvoiceElectronicDocumentDataProvider, nunca se asume que "07"
        // sigue activo/habilitado sin validarlo contra el catálogo real.
        if (
            !await _docTypeCatalogResolver.IsActiveElectronicDocTypeAsync(
                SriDocumentTypeCodes.Withholding,
                ct
            )
        )
            errors.Add(
                $"El tipo de comprobante SRI '{SriDocumentTypeCodes.Withholding}' no está activo o habilitado para emisión electrónica en el catálogo."
            );

        if (errors.Count > 0)
            return Result<RetentionElectronicDocumentData>.ValidationFailure(string.Join(" ", errors));

        var data = new RetentionElectronicDocumentData(
            Metadata: new RetentionElectronicDocumentMetadata(
                RetentionId: retention.Id,
                TenantId: retention.TenantId,
                CompanyId: retention.CompanyId,
                EmissionPointId: retention.EmissionPointId,
                SourceDocumentType: retention.SourceDocumentType,
                SourceDocumentId: retention.SourceDocumentId,
                GeneratedAtUtc: DateTime.UtcNow
            ),
            Emission: new ElectronicDocumentEmissionContext(
                Environment: sriSettings!.Environment.ToString(CultureInfo.InvariantCulture),
                EmissionType: sriSettings.EmissionType.ToString(CultureInfo.InvariantCulture),
                DocTypeCode: SriDocumentTypeCodes.Withholding,
                Establishment: emissionPoint!.Establishment.Code,
                EstablishmentAddress: emissionPoint.Establishment.Address,
                EmissionPoint: emissionPoint.Code,
                Sequential: ExtractSequential(retention.RetentionNumber!),
                IssueDate: retention.IssueDate!.Value.ToDateTime(TimeOnly.MinValue)
            ),
            NumeroCompleto: retention.RetentionNumber!,
            Issuer: new ElectronicDocumentIssuerData(
                TaxId: company!.TaxIdentificationNumber,
                LegalName: company.LegalName,
                TradeName: company.TradeName,
                MatrixAddress: mainEstablishment!.Address,
                TaxRegime: ResolveContribuyenteRimpeText(company.TaxRegime),
                IsAccountingRequired: company.IsAccountingReq
            ),
            RetentionInfo: new RetentionElectronicDocumentInfo(
                SpecialTaxpayerNumber: company.SpecialTaxpayerNo,
                FiscalPeriod: retention.FiscalPeriod!
            ),
            SubjectWithheld: new ElectronicDocumentCounterpartyData(
                IdentificationType: subject!.Identification.Type,
                IdentificationNumber: subject.Identification.Number,
                LegalName: subject.Name.LegalName,
                // BusinessPartner no lleva Address/Email (viven en BusinessPartnerContact/
                // BusinessPartnerLocation, agregados separados) — gap conocido, no se inventa
                // ningún valor. El XSD de retención no exige estos campos para el sujeto
                // retenido; a diferencia de Factura, aquí son opcionales por diseño.
                Address: null,
                Email: null
            ),
            SourceDocument: new RetentionElectronicDocumentSourceDocument(
                TaxSupportCode: retention.SourceDocumentTaxSupportCode,
                DocTypeCode: retention.SourceDocumentSriTypeCode,
                Number: retention.SourceDocumentNumber,
                AuthorizationNumber: retention.SourceDocumentAuthorizationNumber,
                IssueDate: retention.SourceDocumentIssueDate,
                Subtotal: retention.SourceDocumentSubtotal,
                Total: retention.SourceDocumentTotal
            ),
            Lines: retention.Lines.Select(BuildTaxLine).ToList(),
            Totals: new RetentionElectronicDocumentTotals(
                TotalRetainedVat: retention.TotalRetainedVat,
                TotalRetainedIncome: retention.TotalRetainedIncome,
                TotalRetained: retention.TotalRetained
            ),
            AdditionalInfo: []
        );

        return Result<RetentionElectronicDocumentData>.Success(data);
    }

    /// <summary>
    /// Mismo criterio exacto que <c>SalesInvoiceElectronicDocumentDataProvider.ResolveContribuyenteRimpeText</c>
    /// — el elemento SRI solo debe existir para contribuyentes RIMPE, resuelto desde el catálogo
    /// oficial <c>sri_tax_regime</c> (columna <c>Abbrev</c>), nunca comparando códigos hardcodeados.
    /// </summary>
    private static string? ResolveContribuyenteRimpeText(SriTaxRegime? taxRegime) =>
        taxRegime?.Abbrev?.StartsWith("RIMPE", StringComparison.OrdinalIgnoreCase) == true
            ? "CONTRIBUYENTE RÉGIMEN RIMPE"
            : null;

    private static RetentionElectronicDocumentTaxLine BuildTaxLine(RetentionDocumentLine line) =>
        new(
            TaxType: line.TaxType,
            SriTaxTypeCode: ResolveSriTaxTypeCode(line.TaxType),
            RetentionCode: line.RetentionCode,
            RetentionCodeDescription: line.RetentionCodeDescription,
            BaseAmount: line.BaseAmount,
            RetentionRate: line.RetentionRate,
            RetainedAmount: line.RetainedAmount
        );

    private static string ResolveSriTaxTypeCode(RetentionTaxType taxType) =>
        taxType switch
        {
            RetentionTaxType.Vat => SriRetentionTaxTypeCodes.Vat,
            RetentionTaxType.Income => SriRetentionTaxTypeCodes.Income,
            _ => throw new ArgumentOutOfRangeException(
                nameof(taxType),
                taxType,
                "Tipo de impuesto retenido no soportado por el catálogo SRI de retenciones."
            ),
        };

    /// <summary>
    /// El secuencial ya fue capturado y congelado en <see cref="RetentionDocument.RetentionNumber"/>
    /// (formato "EST-PTO-SECUENCIAL", ver RETENTIONS-DOCUMENT-SEQUENCE-02E) — se extrae, nunca se
    /// vuelve a solicitar a <c>IDocumentSequenceRepository</c> (eso emitiría un número nuevo).
    /// Mismo helper que <c>SalesInvoiceElectronicDocumentDataProvider.ExtractSequential</c>.
    /// </summary>
    private static string ExtractSequential(string retentionNumber) => retentionNumber.Split('-')[^1];
}
