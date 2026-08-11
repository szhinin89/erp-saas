namespace ERP.Application.Modules.Purchases.PurchaseReception.UseCases.GetPurchaseReceptionXmlView;

/// <summary>
/// Vista de solo lectura del XML ya persistido en <c>PurchaseReceptionDocument.XmlContent</c>
/// (FLOW-READY-02E.1). Los campos de cabecera/líneas provienen de la entidad y de
/// <c>PurchaseReceptionLine</c> ya guardados — nunca se recalculan; solo <see cref="SupplierTradeName"/>,
/// <see cref="DiscountAmount"/>, <see cref="IceAmount"/>, <see cref="TaxSummaries"/> y los campos
/// del documento modificado se leen del XML crudo porque la entidad no los persiste. Cuando
/// <see cref="RawXmlAvailable"/> es <see langword="false"/> esos campos quedan en su valor por
/// defecto (nunca inventados) y <see cref="RawXml"/> es <see langword="null"/>.
/// </summary>
public sealed record PurchaseReceptionXmlViewDto(
    Guid DocumentId,
    string DocumentType,
    string DocumentNumber,
    DateOnly IssueDate,
    string AccessKey,
    string? AuthorizationNumber,
    DateTime? AuthorizationDate,
    string SupplierName,
    string? SupplierTradeName,
    string SupplierTaxId,
    string? ReferralGuide,
    string? PaymentMethodCode,
    string? PaymentTerm,
    string? PaymentTimeUnit,
    string? ModifiedDocumentNumber,
    string? ModifiedDocumentType,
    DateOnly? ModifiedDocumentDate,
    string? ModificationReason,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal IceAmount,
    decimal IrbpnrAmount,
    decimal VatAmount,
    decimal TipAmount,
    decimal TotalAmount,
    decimal LineCalculatedTotal,
    decimal RoundingDifference,
    IReadOnlyList<PurchaseReceptionXmlViewTaxSummaryDto> TaxSummaries,
    IReadOnlyList<PurchaseReceptionXmlViewLineDto> Lines,
    bool RawXmlAvailable,
    string? RawXml
);

/// <summary>Un <c>&lt;totalImpuesto&gt;</c> de cabecera, enriquecido solo con tarifa observada en impuestos reales de línea cuando existe.</summary>
public sealed record PurchaseReceptionXmlViewTaxSummaryDto(
    string TaxCode,
    string TaxRateCode,
    string TaxName,
    decimal? Rate,
    decimal TaxableBase,
    decimal Amount
);

public sealed record PurchaseReceptionXmlViewLineDto(
    string? MainCode,
    string? AuxCode,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal TaxableBase,
    decimal IceAmount,
    decimal IrbpnrAmount,
    decimal VatAmount,
    decimal TotalAmount,
    decimal LineTotal,
    IReadOnlyList<PurchaseReceptionXmlViewLineTaxDto> Taxes,
    IReadOnlyList<PurchaseReceptionXmlViewAdditionalDetailDto> AdditionalDetails
);

public sealed record PurchaseReceptionXmlViewLineTaxDto(
    string TaxCode,
    string TaxRateCode,
    string TaxName,
    decimal Rate,
    decimal TaxableBase,
    decimal Amount
);

public sealed record PurchaseReceptionXmlViewAdditionalDetailDto(string Name, string Value);
