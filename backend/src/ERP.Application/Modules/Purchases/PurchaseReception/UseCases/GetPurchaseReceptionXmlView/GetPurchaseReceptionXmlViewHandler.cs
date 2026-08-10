using ERP.Application.Common;
using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
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
            ModifiedDocumentNumber: document.ModifiedDocumentNumber,
            ModifiedDocumentType: extras?.ModifiedDocumentType,
            ModifiedDocumentDate: extras?.ModifiedDocumentDate,
            ModificationReason: extras?.ModificationReason,
            Subtotal: document.Subtotal,
            DiscountAmount: extras?.DiscountAmount ?? 0m,
            IceAmount: extras?.IceAmount ?? 0m,
            VatAmount: document.VatAmount,
            TotalAmount: document.TotalAmount,
            TaxSummaries: extras is null
                ? []
                : extras
                    .TaxSummaries.Select(t => new PurchaseReceptionXmlViewTaxSummaryDto(
                        t.TaxType,
                        t.TaxCode,
                        t.TaxRate,
                        t.TaxableBase,
                        t.TaxAmount
                    ))
                    .ToList(),
            Lines: document.Lines.Select(ToLineDto).ToList(),
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

    private static PurchaseReceptionXmlViewLineDto ToLineDto(PurchaseReceptionLine line)
    {
        var taxes = new List<PurchaseReceptionXmlViewLineTaxDto>
        {
            new("2", line.VatCode, line.VatPercentage, line.TaxValue),
        };
        if (!string.IsNullOrWhiteSpace(line.IceCode))
            taxes.Add(new PurchaseReceptionXmlViewLineTaxDto("3", line.IceCode, 0m, line.IceValue));

        return new PurchaseReceptionXmlViewLineDto(
            MainCode: line.SupplierCode,
            AuxCode: line.SupplierAuxCode,
            Description: line.Description,
            Quantity: line.Quantity,
            UnitPrice: line.UnitPrice,
            DiscountAmount: line.Discount,
            TaxableBase: line.LineSubtotal,
            IceAmount: line.IceValue,
            VatAmount: line.TaxValue,
            TotalAmount: line.TotalLine,
            Taxes: taxes
        );
    }

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
