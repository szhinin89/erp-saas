using System.Globalization;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Entities;

namespace ERP.Application.Modules.Sales.Services;

/// <summary>
/// Segundo proveedor real de <see cref="IElectronicDocumentDataProvider"/> (P0-01 Fase 8/10),
/// mismo patrón exacto que <see cref="SalesInvoiceElectronicDocumentDataProvider"/> — traduce una
/// <see cref="SalesReturn"/> autorizada al modelo común <see cref="ElectronicDocumentData"/>.
///
/// La devolución hereda el emisor/establecimiento/punto de emisión de la <see cref="SalesInvoice"/>
/// original (una devolución no tiene punto de emisión propio — se emite desde el mismo punto que
/// emitió la factura que modifica). Los totales/impuestos/líneas provienen exclusivamente del
/// propio agregado <see cref="SalesReturn"/>/<see cref="SalesReturnDetail"/> — nada se recalcula.
/// El secuencial SRI ("04") ya viene congelado en <see cref="SalesReturn.CreditNoteDocumentNumber"/>
/// (capturado una única vez por <c>AuthorizeSalesReturnHandler</c>, nunca por este proveedor —
/// mismo criterio que <see cref="SalesInvoiceElectronicDocumentDataProvider.ExtractSequential"/>).
/// </summary>
public sealed class SalesReturnCreditNoteDataProvider : IElectronicDocumentDataProvider
{
    /// <summary>Código SRI "04" = Nota de Crédito (tabla <c>sri_doc_types</c>) — identidad fija de
    /// este proveedor, no un valor de negocio configurable (igual que <c>DocumentType</c> abajo).
    /// <c>internal</c> porque <see cref="UseCases.AuthorizeSalesReturnHandler"/> reutiliza el mismo
    /// valor al capturar el secuencial — única fuente de verdad, sin duplicar el literal.</summary>
    internal const string CreditNoteDocTypeCode = "04";

    private readonly ISalesReturnRepository _returnRepository;
    private readonly ISalesInvoiceRepository _invoiceRepository;
    private readonly IEmissionPointRepository _emissionPointRepository;
    private readonly IEstablishmentRepository _establishmentRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly ISriSettingsRepository _sriSettingsRepository;
    private readonly ISriDocTypeCatalogResolver _docTypeCatalogResolver;

    public SalesReturnCreditNoteDataProvider(
        ISalesReturnRepository returnRepository,
        ISalesInvoiceRepository invoiceRepository,
        IEmissionPointRepository emissionPointRepository,
        IEstablishmentRepository establishmentRepository,
        ICompanyRepository companyRepository,
        ISriSettingsRepository sriSettingsRepository,
        ISriDocTypeCatalogResolver docTypeCatalogResolver
    )
    {
        _returnRepository = returnRepository;
        _invoiceRepository = invoiceRepository;
        _emissionPointRepository = emissionPointRepository;
        _establishmentRepository = establishmentRepository;
        _companyRepository = companyRepository;
        _sriSettingsRepository = sriSettingsRepository;
        _docTypeCatalogResolver = docTypeCatalogResolver;
    }

    public ElectronicDocumentType DocumentType => ElectronicDocumentType.CreditNote;

    public async Task<Result<ElectronicDocumentData>> GetDataAsync(
        ElectronicDocumentSourceReference reference,
        CancellationToken ct = default
    )
    {
        var salesReturn = await _returnRepository.GetByIdAsync(
            reference.TenantId,
            reference.SourceEntityId,
            ct
        );
        if (salesReturn is null)
            return Result<ElectronicDocumentData>.NotFound("La devolución de venta no existe.");

        var errors = new List<string>();

        if (salesReturn.Status != SalesReturnStatus.Authorized)
            errors.Add("La devolución debe estar autorizada para emitirse electrónicamente.");
        if (string.IsNullOrWhiteSpace(salesReturn.CreditNoteDocumentNumber))
            errors.Add("La devolución no tiene un secuencial de Nota de Crédito asignado.");

        var invoice = await _invoiceRepository.GetByIdAsync(
            reference.TenantId,
            salesReturn.SalesInvoiceId,
            ct
        );
        if (invoice is null)
            errors.Add("La factura original de esta devolución ya no existe.");

        EmissionPoint? emissionPoint = null;
        if (invoice?.EmissionPointId is { } emissionPointId)
        {
            emissionPoint = await _emissionPointRepository.GetByIdAsync(
                emissionPointId,
                reference.TenantId,
                ct
            );
            if (emissionPoint is null)
                errors.Add(
                    "El punto de emisión de la factura original ya no existe o está inactivo."
                );
        }
        else if (invoice is not null)
        {
            errors.Add("La factura original no tiene un punto de emisión asignado.");
        }

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

        // Única fuente de verdad del código de tipo de comprobante: el catálogo sri_doc_types —
        // "04" nunca se asume activo sin validarlo (Infraestructura CLOSED — Configuración Tributaria).
        if (
            !await _docTypeCatalogResolver.IsActiveElectronicDocTypeAsync(CreditNoteDocTypeCode, ct)
        )
            errors.Add(
                $"El tipo de comprobante SRI '{CreditNoteDocTypeCode}' (Nota de Crédito) no está activo o habilitado para emisión electrónica en el catálogo."
            );

        if (errors.Count > 0)
            return Result<ElectronicDocumentData>.ValidationFailure(string.Join(" ", errors));

        var details = salesReturn.Lines.Select(BuildDetailLine).ToList();

        var data = new ElectronicDocumentData(
            Emission: new ElectronicDocumentEmissionContext(
                Environment: sriSettings!.Environment.ToString(CultureInfo.InvariantCulture),
                EmissionType: sriSettings.EmissionType.ToString(CultureInfo.InvariantCulture),
                DocTypeCode: CreditNoteDocTypeCode,
                Establishment: emissionPoint!.Establishment.Code,
                EstablishmentAddress: emissionPoint.Establishment.Address,
                EmissionPoint: emissionPoint.Code,
                Sequential: ExtractSequential(salesReturn.CreditNoteDocumentNumber!),
                IssueDate: salesReturn.UpdatedAt ?? salesReturn.CreatedAt
            ),
            Issuer: new ElectronicDocumentIssuerData(
                TaxId: company!.TaxIdentificationNumber,
                LegalName: company.LegalName,
                TradeName: company.TradeName,
                MatrixAddress: mainEstablishment!.Address,
                TaxRegime: ResolveContribuyenteRimpeText(company.TaxRegime),
                IsAccountingRequired: company.IsAccountingReq
            ),
            Counterparty: new ElectronicDocumentCounterpartyData(
                IdentificationType: invoice!.Customer.IdentificationType,
                IdentificationNumber: invoice.Customer.TaxId,
                LegalName: invoice.Customer.Name,
                Address: invoice.Customer.Address,
                Email: invoice.Customer.Email
            ),
            Details: details,
            TaxSummary: BuildTaxSummary(details),
            Totals: new ElectronicDocumentTotals(
                Subtotal: salesReturn.Subtotal,
                TotalDiscount: salesReturn.TotalDiscount,
                TotalTax: salesReturn.TotalVat + salesReturn.TotalIce,
                GrandTotal: salesReturn.GrandTotal,
                CurrencyCode: invoice.CurrencyCode
            ),
            Payments: [],
            AdditionalInfo: [],
            Reason: salesReturn.Reason,
            ModifiedDocument: new ElectronicDocumentModifiedReference(
                DocTypeCode: invoice.DocTypeCode,
                Number: invoice.InvoiceNumber,
                IssueDate: invoice.IssueDate.ToDateTime(TimeOnly.MinValue)
            )
        );

        return Result<ElectronicDocumentData>.Success(data);
    }

    /// <summary>Ver <see cref="SalesInvoiceElectronicDocumentDataProvider.ResolveContribuyenteRimpeText"/> — mismo criterio exacto.</summary>
    private static string? ResolveContribuyenteRimpeText(SriTaxRegime? taxRegime) =>
        taxRegime?.Abbrev?.StartsWith("RIMPE", StringComparison.OrdinalIgnoreCase) == true
            ? "CONTRIBUYENTE RÉGIMEN RIMPE"
            : null;

    private static ElectronicDocumentDetailLine BuildDetailLine(SalesReturnDetail line)
    {
        var taxes = new List<ElectronicDocumentDetailTax>
        {
            new("VAT", line.VatCode, line.TaxableBase, line.VatRate, line.VatAmount),
        };
        if (!string.IsNullOrWhiteSpace(line.IceCode))
            taxes.Add(
                new ElectronicDocumentDetailTax(
                    "ICE",
                    line.IceCode!,
                    line.TaxableBase,
                    line.IceRate,
                    line.IceAmount
                )
            );

        return new ElectronicDocumentDetailLine(
            Code: line.SnapshotSku ?? string.Empty,
            Description: line.Description,
            Quantity: line.Quantity,
            UnitPrice: line.UnitPrice,
            Discount: line.DiscountAmount,
            Subtotal: line.LineSubtotal,
            Taxes: taxes
        );
    }

    private static IReadOnlyList<ElectronicDocumentTaxSummary> BuildTaxSummary(
        IReadOnlyList<ElectronicDocumentDetailLine> details
    ) =>
        details
            .SelectMany(d => d.Taxes)
            .GroupBy(t => (t.TaxCode, t.TaxPercentageCode))
            .Select(g => new ElectronicDocumentTaxSummary(
                g.Key.TaxCode,
                g.Key.TaxPercentageCode,
                g.Sum(t => t.TaxableBase),
                g.Sum(t => t.TaxAmount)
            ))
            .ToList();

    /// <summary>
    /// El secuencial ya fue capturado y congelado en
    /// <see cref="SalesReturn.CreditNoteDocumentNumber"/> (formato "EST-PTO-SECUENCIAL", ver
    /// <c>AuthorizeSalesReturnHandler</c>) — se extrae, nunca se vuelve a solicitar a
    /// <c>IDocumentSequenceRepository</c> (eso emitiría un número nuevo).
    /// </summary>
    private static string ExtractSequential(string creditNoteDocumentNumber) =>
        creditNoteDocumentNumber.Split('-')[^1];
}
