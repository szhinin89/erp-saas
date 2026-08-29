using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Purchases;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Entities;
using System.Globalization;

namespace ERP.Application.Modules.Sales.Services;

/// <summary>
/// Primer proveedor real de <see cref="IElectronicDocumentDataProvider"/> — traduce una
/// <see cref="SalesInvoice"/> autorizada al modelo común <see cref="ElectronicDocumentData"/>.
///
/// Toda la información proviene de su fuente única: la factura y sus líneas/pagos (Sales),
/// el establecimiento/punto de emisión de la factura y el establecimiento matriz (Company),
/// y el ambiente/tipo de emisión SRI configurado para la empresa (SriSettings). Nada se
/// recalcula ni se inventa: los totales, bases imponibles e impuestos ya vienen calculados
/// por el propio agregado <see cref="SalesInvoice"/>/<see cref="SalesInvoiceDetail"/>, y el
/// secuencial se extrae del <see cref="SalesInvoice.InvoiceNumber"/> ya asignado — nunca se
/// vuelve a capturar mediante <c>IDocumentSequenceRepository</c>.
/// </summary>
public sealed class SalesInvoiceElectronicDocumentDataProvider : IElectronicDocumentDataProvider
{
    private readonly ISalesInvoiceRepository _invoiceRepository;
    private readonly IEmissionPointRepository _emissionPointRepository;
    private readonly IEstablishmentRepository _establishmentRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly ISriSettingsRepository _sriSettingsRepository;
    private readonly ISriDocTypeCatalogResolver _docTypeCatalogResolver;

    public SalesInvoiceElectronicDocumentDataProvider(
        ISalesInvoiceRepository invoiceRepository,
        IEmissionPointRepository emissionPointRepository,
        IEstablishmentRepository establishmentRepository,
        ICompanyRepository companyRepository,
        ISriSettingsRepository sriSettingsRepository,
        ISriDocTypeCatalogResolver docTypeCatalogResolver
    )
    {
        _invoiceRepository = invoiceRepository;
        _emissionPointRepository = emissionPointRepository;
        _establishmentRepository = establishmentRepository;
        _companyRepository = companyRepository;
        _sriSettingsRepository = sriSettingsRepository;
        _docTypeCatalogResolver = docTypeCatalogResolver;
    }

    public ElectronicDocumentType DocumentType => ElectronicDocumentType.Invoice;

    public async Task<Result<ElectronicDocumentData>> GetDataAsync(
        ElectronicDocumentSourceReference reference,
        CancellationToken ct = default
    )
    {
        var invoice = await _invoiceRepository.GetByIdAsync(
            reference.TenantId,
            reference.SourceEntityId,
            ct
        );
        if (invoice is null)
            return Result<ElectronicDocumentData>.NotFound("La factura de venta no existe.");

        var errors = new List<string>();

        if (invoice.Status != SalesInvoiceStatus.Authorized)
            errors.Add("La factura debe estar autorizada para emitirse electrónicamente.");

        if (invoice.EmissionType != EmissionType.Electronic)
            errors.Add("La factura no está configurada como emisión electrónica.");

        if (string.IsNullOrWhiteSpace(invoice.SriPaymentMethodCode))
            errors.Add("La factura no tiene una forma de pago SRI asignada.");

        if (invoice.Payments.Count == 0)
            errors.Add("La factura no tiene formas de cobro registradas.");

        foreach (var line in invoice.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.SnapshotSku))
                errors.Add($"La línea '{line.Description}' no tiene código de producto (SKU).");
        }

        EmissionPoint? emissionPoint = null;
        if (invoice.EmissionPointId is { } emissionPointId)
        {
            emissionPoint = await _emissionPointRepository.GetByIdAsync(
                emissionPointId,
                reference.TenantId,
                ct
            );
            if (emissionPoint is null)
                errors.Add("El punto de emisión de la factura ya no existe o está inactivo.");
        }
        else
        {
            errors.Add("La factura no tiene un punto de emisión asignado.");
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

        // Única fuente de verdad del código de tipo de comprobante: el catálogo sri_doc_types.
        // Nunca se asume que invoice.DocTypeCode ("01" por defecto) sigue vigente sin validarlo.
        if (!await _docTypeCatalogResolver.IsActiveElectronicDocTypeAsync(invoice.DocTypeCode, ct))
            errors.Add(
                $"El tipo de comprobante SRI '{invoice.DocTypeCode}' no está activo o habilitado para emisión electrónica en el catálogo."
            );

        if (errors.Count > 0)
            return Result<ElectronicDocumentData>.ValidationFailure(string.Join(" ", errors));

        var details = invoice.Lines.Select(BuildDetailLine).ToList();

        var data = new ElectronicDocumentData(
            Emission: new ElectronicDocumentEmissionContext(
                Environment: sriSettings!.Environment.ToString(CultureInfo.InvariantCulture),
                EmissionType: sriSettings.EmissionType.ToString(CultureInfo.InvariantCulture),
                DocTypeCode: invoice.DocTypeCode,
                Establishment: emissionPoint!.Establishment.Code,
                EstablishmentAddress: emissionPoint.Establishment.Address,
                EmissionPoint: emissionPoint.Code,
                Sequential: ExtractSequential(invoice.InvoiceNumber),
                IssueDate: invoice.IssueDate.ToDateTime(TimeOnly.MinValue)
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
                IdentificationType: invoice.Customer.IdentificationType,
                IdentificationNumber: invoice.Customer.TaxId,
                LegalName: invoice.Customer.Name,
                Address: invoice.Customer.Address,
                Email: invoice.Customer.Email
            ),
            Details: details,
            TaxSummary: BuildTaxSummary(details),
            Totals: new ElectronicDocumentTotals(
                Subtotal: invoice.Subtotal,
                TotalDiscount: invoice.TotalDiscount,
                TotalTax: invoice.TotalTax,
                GrandTotal: invoice.GrandTotal,
                CurrencyCode: invoice.CurrencyCode
            ),
            Payments: invoice
                .Payments.Select(p => new ElectronicDocumentPayment(
                    PaymentMethodCode: invoice.SriPaymentMethodCode!,
                    Amount: p.Amount,
                    Term: null,
                    TimeUnit: null
                ))
                .ToList(),
            AdditionalInfo: string.IsNullOrWhiteSpace(invoice.Notes)
                ? []
                : [new ElectronicDocumentAdditionalField("Observación", invoice.Notes)]
        );

        return Result<ElectronicDocumentData>.Success(data);
    }

    /// <summary>
    /// Ficha técnica SRI (ver <c>factura_V2.1.0.xsd</c>, tipo <c>contribuyenteRimpe</c>): el
    /// elemento &lt;contribuyenteRimpe&gt; solo debe existir para contribuyentes bajo régimen
    /// RIMPE, y su contenido debe ser exactamente el texto fijo exigido por el SRI — nunca un
    /// código. "RIMPE" se determina desde el catálogo oficial <c>sri_tax_regime</c> (columna
    /// <c>Abbrev</c>, ver <see cref="SriTaxRegimeConfiguration"/>: "RIMPE_ME"/"RIMPE_NP"), nunca
    /// comparando códigos numéricos hardcodeados. Régimen General o Contribuyente Especial
    /// devuelven <c>null</c> y el elemento se omite por completo.
    /// </summary>
    private static string? ResolveContribuyenteRimpeText(SriTaxRegime? taxRegime) =>
        taxRegime?.Abbrev?.StartsWith("RIMPE", StringComparison.OrdinalIgnoreCase) == true
            ? "CONTRIBUYENTE RÉGIMEN RIMPE"
            : null;

    /// <summary>
    /// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.5, Subfase 5C) — etiqueta que
    /// <see cref="ERP.Infrastructure.Services.ElectronicDocuments.SriTaxCategoryCodeResolver"/> ya
    /// traduce a código SRI. Mapeo por <c>SriTaxCategoryCodes</c> (Domain), no un literal nuevo:
    /// "IRBPNR" queda listado aquí para no perder la fila si algún día el resolver la reconoce, pero
    /// hoy el resolver (infraestructura FROZEN de ElectronicDocuments, fuera de esta subfase) solo
    /// traduce "VAT"/"ICE" — una línea con IRBPNR ya llega completa en la lista, lista para cuando
    /// esa infraestructura se extienda en un ticket propio.
    ///
    /// PENDIENTE — ELECTRONIC-DOCUMENTS-IRBPNR-CATEGORY-01: SriTaxCategoryCodeResolver hoy solo
    /// reconoce VAT→"2" e ICE→"3"; falta extenderlo para IRBPNR→"5". No se toca en esta subfase
    /// (fuera de alcance de 5C — InvoiceXmlBuilder/SriTaxCategoryCodeResolver quedan intactos).
    /// Registrado también en ADR-032 §9.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> ElectronicDocumentTaxLabelByCode =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SriTaxCategoryCodes.Vat] = "VAT",
            [SriTaxCategoryCodes.Ice] = "ICE",
            [SriTaxCategoryCodes.Irbpnr] = "IRBPNR",
        };

    /// <summary>
    /// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.5, Subfase 5C) — construye la lista de impuestos de
    /// línea desde <see cref="SalesInvoiceDetail.Taxes"/> (SalesInvoiceDetailTax, fuente de verdad),
    /// nunca desde los campos escalares legacy (IceCode/IceRate/IceAmount) — esos quedan como
    /// legacy compatibility mirror, no se leen aquí para ninguna decisión. Sin impuestos especiales,
    /// Taxes contiene únicamente la fila de IVA — mismo resultado que antes, byte a byte.
    /// </summary>
    /// <summary>
    /// Orden canónico del nodo &lt;impuesto&gt; por línea (IVA, ICE, IRBPNR, cualquier otro código).
    /// No se confía en el orden de inserción de <see cref="SalesInvoiceDetail.Taxes"/> — Authorize
    /// vuelve a sincronizar IVA/ICE (upsert = remove+add), lo que los movería al final de la
    /// colección sin este orden explícito.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, int> ElectronicDocumentTaxSortOrder =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [SriTaxCategoryCodes.Vat] = 0,
            [SriTaxCategoryCodes.Ice] = 1,
            [SriTaxCategoryCodes.Irbpnr] = 2,
        };

    private static ElectronicDocumentDetailLine BuildDetailLine(SalesInvoiceDetail line)
    {
        var taxes = line
            .Taxes.OrderBy(t =>
                ElectronicDocumentTaxSortOrder.TryGetValue(t.TaxCode, out var order) ? order : int.MaxValue
            )
            .Select(t => new ElectronicDocumentDetailTax(
                ElectronicDocumentTaxLabelByCode.TryGetValue(t.TaxCode, out var label)
                    ? label
                    : t.TaxCode,
                t.TaxRateCode,
                t.TaxableBase,
                t.Rate ?? 0m,
                t.TaxAmount
            ))
            .ToList();

        return new ElectronicDocumentDetailLine(
            Code: line.SnapshotSku!,
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
    /// El secuencial ya fue capturado y congelado en <see cref="SalesInvoice.InvoiceNumber"/>
    /// (formato "EST-PTO-SECUENCIAL", ver AuthorizeSalesUseCases) — se extrae, nunca se vuelve
    /// a solicitar a <c>IDocumentSequenceRepository</c> (eso emitiría un número nuevo).
    /// </summary>
    private static string ExtractSequential(string invoiceNumber) => invoiceNumber.Split('-')[^1];
}
