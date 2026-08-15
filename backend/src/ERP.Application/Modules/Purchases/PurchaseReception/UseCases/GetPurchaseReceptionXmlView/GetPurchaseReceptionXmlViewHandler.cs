using ERP.Application.Common;
using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using ERP.Domain.Modules.Purchases;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Purchases.PurchaseReception.UseCases.GetPurchaseReceptionXmlView;

/// <summary>
/// Handler de solo lectura: nunca llama <c>SaveChangesAsync</c>, nunca muta el documento. Lee el
/// XML ya guardado en <c>XmlContent</c> y lo parsea en memoria únicamente para completar los
/// campos que la entidad no persiste (ver <see cref="PurchaseReceptionXmlViewExtractor"/>); el
/// resto de la respuesta sale directamente de <c>PurchaseReceptionDocument</c>/<c>PurchaseReceptionLine</c>
/// ya persistidos.
/// </summary>
public sealed class GetPurchaseReceptionXmlViewHandler
    : IRequestHandler<GetPurchaseReceptionXmlViewQuery, Result<PurchaseReceptionXmlViewDto>>
{
    private const string SriVatTaxCode = SriTaxCategoryCodes.Vat;
    private const string SriIceTaxCode = SriTaxCategoryCodes.Ice;
    private const string SriIrbpnrTaxCode = SriTaxCategoryCodes.Irbpnr;

    private readonly IPurchaseReceptionDocumentRepository _documentRepo;
    private readonly ICurrentTenant _tenant;

    public GetPurchaseReceptionXmlViewHandler(
        IPurchaseReceptionDocumentRepository documentRepo,
        ICurrentTenant tenant
    )
    {
        _documentRepo = documentRepo;
        _tenant = tenant;
    }

    public async Task<Result<PurchaseReceptionXmlViewDto>> Handle(
        GetPurchaseReceptionXmlViewQuery request,
        CancellationToken cancellationToken
    )
    {
        var document = await _documentRepo.GetByIdAsync(
            _tenant.TenantId,
            request.PurchaseReceptionDocumentId,
            cancellationToken
        );
        if (document is null)
            return Result<PurchaseReceptionXmlViewDto>.NotFound(
                "El documento de recepción no existe."
            );

        var extras = string.IsNullOrWhiteSpace(document.XmlContent)
            ? null
            : TryExtract(document.XmlContent);

        var lines = document
            .Lines.Select((line, index) =>
                ToLineDto(
                    line,
                    extras?.Lines.Count > index ? extras.Lines[index] : null
                )
            )
            .ToList();
        var lineCalculatedTotal = lines.Sum(l => l.LineTotal);
        var taxSummaries = extras is null
            ? []
            : extras
                .TaxSummaries.Select(t => ToTaxSummaryDto(t, lines))
                .ToList();
        var subtotal = extras?.Totals.TotalWithoutTaxes ?? document.Subtotal;
        var discount = extras?.Totals.TotalDiscount ?? extras?.DiscountAmount ?? 0m;
        var totalIce = SumTax(taxSummaries, SriIceTaxCode, extras?.IceAmount ?? 0m);
        var totalIrbpnr = SumTax(taxSummaries, SriIrbpnrTaxCode, 0m);
        var totalVat = SumTax(taxSummaries, SriVatTaxCode, document.VatAmount);
        var tip = extras?.Totals.Tip ?? 0m;
        var totalAmount = extras?.Totals.TotalAmount ?? document.TotalAmount;

        var dto = new PurchaseReceptionXmlViewDto(
            DocumentId: document.Id,
            DocumentType: ToSourceDocTypeCode(document.SourceDocType),
            DocumentNumber: document.InvoiceNumber,
            IssueDate: document.IssueDate,
            AccessKey: document.AccessKey,
            AuthorizationNumber: document.AuthorizationNumber,
            AuthorizationDate: document.AuthorizationDate,
            SupplierName: document.SupplierName,
            SupplierTradeName: extras?.SupplierTradeName,
            SupplierTaxId: document.SupplierRuc,
            ReferralGuide: extras?.ReferralGuide,
            PaymentMethodCode: extras?.PaymentMethodCode ?? document.SriPaymentMethodCode,
            PaymentTerm: extras?.PaymentTerm,
            PaymentTimeUnit: extras?.PaymentTimeUnit,
            ModifiedDocumentNumber: document.ModifiedDocumentNumber,
            ModifiedDocumentType: extras?.ModifiedDocumentType,
            ModifiedDocumentDate: extras?.ModifiedDocumentDate,
            ModificationReason: extras?.ModificationReason,
            Subtotal: subtotal,
            DiscountAmount: discount,
            IceAmount: totalIce,
            IrbpnrAmount: totalIrbpnr,
            VatAmount: totalVat,
            TipAmount: tip,
            TotalAmount: totalAmount,
            LineCalculatedTotal: lineCalculatedTotal,
            RoundingDifference: totalAmount - lineCalculatedTotal,
            TaxSummaries: taxSummaries,
            Lines: lines,
            RawXmlAvailable: !string.IsNullOrWhiteSpace(document.XmlContent),
            RawXml: document.XmlContent
        );

        return Result<PurchaseReceptionXmlViewDto>.Success(dto);
    }

    // Un XML autorizado por el SRI que ya pasamos con éxito por AttachSriAuthorization nunca
    // debería fallar aquí — pero si el esquema cambia entre versiones, esta vista degrada a "sin
    // extras" en vez de romper el resto de la respuesta (que sí tiene datos reales y útiles).
    private static PurchaseReceptionXmlViewExtras? TryExtract(string xmlContent)
    {
        try
        {
            return PurchaseReceptionXmlViewExtractor.Extract(xmlContent);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or FormatException)
        {
            return null;
        }
    }

    private static PurchaseReceptionXmlViewTaxSummaryDto ToTaxSummaryDto(
        PurchaseReceptionXmlViewExtraTax tax,
        IReadOnlyList<PurchaseReceptionXmlViewLineDto> lines
    )
    {
        var rate = tax.TaxRate
            ?? lines
                .SelectMany(l => l.Taxes)
                .Where(t => t.TaxCode == tax.TaxCode && t.TaxRateCode == tax.TaxRateCode)
                .Select(t => (decimal?)t.Rate)
                .FirstOrDefault();

        return new PurchaseReceptionXmlViewTaxSummaryDto(
            TaxCode: tax.TaxCode,
            TaxRateCode: tax.TaxRateCode,
            TaxName: TaxName(tax.TaxCode),
            Rate: rate,
            TaxableBase: tax.TaxableBase,
            Amount: tax.TaxAmount
        );
    }

    private static decimal SumTax(
        IReadOnlyList<PurchaseReceptionXmlViewTaxSummaryDto> taxSummaries,
        string taxCode,
        decimal fallback
    )
    {
        var total = taxSummaries.Where(t => t.TaxCode == taxCode).Sum(t => t.Amount);
        return total == 0m ? fallback : total;
    }

    private static PurchaseReceptionXmlViewLineDto ToLineDto(
        PurchaseReceptionLine line,
        PurchaseReceptionXmlViewExtraLine? extraLine
    )
    {
        // FLOW-READY-02F.1 — cuando la línea capturó el detalle genérico de impuestos (esta fase en
        // adelante), se usa como fuente: incluye automáticamente cualquier código (IRBPNR, etc.) sin
        // necesidad de un caso especial por impuesto. Líneas de recepciones anteriores a esta fase
        // (sin esa colección) siguen construyendo Taxes solo desde VatCode/IceCode, exactamente como
        // antes — regresión cero.
        var taxes = BuildLineTaxes(line, extraLine);
        var vatAmount = taxes.Where(t => t.TaxCode == SriVatTaxCode).Sum(t => t.Amount);
        var iceAmount = taxes.Where(t => t.TaxCode == SriIceTaxCode).Sum(t => t.Amount);
        var irbpnrAmount = taxes.Where(t => t.TaxCode == SriIrbpnrTaxCode).Sum(t => t.Amount);
        var lineTotal = line.LineSubtotal + taxes.Sum(t => t.Amount);

        return new PurchaseReceptionXmlViewLineDto(
            MainCode: line.SupplierCode,
            AuxCode: line.SupplierAuxCode,
            Description: line.Description,
            Quantity: line.Quantity,
            UnitPrice: line.UnitPrice,
            DiscountAmount: line.Discount,
            TaxableBase: line.LineSubtotal,
            IceAmount: iceAmount,
            IrbpnrAmount: irbpnrAmount,
            VatAmount: vatAmount,
            TotalAmount: line.TotalLine,
            LineTotal: lineTotal,
            Taxes: taxes,
            AdditionalDetails: extraLine
                    ?.AdditionalDetails.Select(d => new PurchaseReceptionXmlViewAdditionalDetailDto(
                        d.Name,
                        d.Value
                    ))
                    .ToList()
                ?? []
        );
    }

    private static List<PurchaseReceptionXmlViewLineTaxDto> BuildLineTaxes(
        PurchaseReceptionLine line,
        PurchaseReceptionXmlViewExtraLine? extraLine
    )
    {
        if (extraLine?.Taxes.Count > 0)
            return extraLine
                .Taxes.Select(t => new PurchaseReceptionXmlViewLineTaxDto(
                    t.TaxCode,
                    t.TaxRateCode,
                    TaxName(t.TaxCode),
                    t.Rate,
                    t.TaxableBase,
                    t.TaxAmount
                ))
                .ToList();

        if (line.Taxes.Count > 0)
            return line
                .Taxes.Select(t => new PurchaseReceptionXmlViewLineTaxDto(
                    t.TaxCode,
                    t.TaxRateCode,
                    TaxName(t.TaxCode),
                    t.Tarifa,
                    t.TaxableBase,
                    t.TaxAmount
                ))
                .ToList();

        return BuildLegacyTaxes(line);
    }

    private static List<PurchaseReceptionXmlViewLineTaxDto> BuildLegacyTaxes(PurchaseReceptionLine line)
    {
        var taxes = new List<PurchaseReceptionXmlViewLineTaxDto>
        {
            new(
                SriVatTaxCode,
                line.VatCode,
                TaxName(SriVatTaxCode),
                line.VatPercentage,
                line.LineSubtotal,
                line.TaxValue
            ),
        };
        if (!string.IsNullOrWhiteSpace(line.IceCode))
            taxes.Add(
                new PurchaseReceptionXmlViewLineTaxDto(
                    SriIceTaxCode,
                    line.IceCode,
                    TaxName(SriIceTaxCode),
                    0m,
                    line.LineSubtotal,
                    line.IceValue
                )
            );
        return taxes;
    }

    private static string TaxName(string taxCode) =>
        taxCode switch
        {
            SriVatTaxCode => "IVA",
            SriIceTaxCode => "ICE",
            SriIrbpnrTaxCode => "IRBPNR",
            _ => taxCode,
        };

    private static string ToSourceDocTypeCode(PurchaseReceptionSourceDocType sourceDocType) =>
        sourceDocType switch
        {
            PurchaseReceptionSourceDocType.Invoice => "INVOICE",
            PurchaseReceptionSourceDocType.CreditNote => "CREDIT_NOTE",
            PurchaseReceptionSourceDocType.DebitNote => "DEBIT_NOTE",
            PurchaseReceptionSourceDocType.Retention => "RETENTION",
            PurchaseReceptionSourceDocType.Unknown => "UNKNOWN",
            _ => throw new ArgumentOutOfRangeException(nameof(sourceDocType), sourceDocType, null),
        };
}
