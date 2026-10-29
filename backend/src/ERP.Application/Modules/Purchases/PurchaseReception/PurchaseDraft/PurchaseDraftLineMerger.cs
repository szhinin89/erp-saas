using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;

namespace ERP.Application.Modules.Purchases.PurchaseReception.PurchaseDraft;

/// <summary>
/// FLOW-READY-02F.2 — espejo plano (no EF) de un <see cref="PurchaseReceptionLineTax"/> o de un
/// <see cref="ParsedPurchaseXmlLineTax"/> recién parseado. Nunca se persiste.
/// </summary>
public sealed record PurchaseDraftMergedLineTax(
    string TaxCode,
    string TaxRateCode,
    decimal Tarifa,
    decimal TaxableBase,
    decimal TaxAmount,
    Guid? PurchaseReceptionLineTaxId = null
)
{
    public static PurchaseDraftMergedLineTax FromPersisted(PurchaseReceptionLineTax tax) =>
        new(tax.TaxCode, tax.TaxRateCode, tax.Tarifa, tax.TaxableBase, tax.TaxAmount, tax.Id);

    public static PurchaseDraftMergedLineTax FromParsed(ParsedPurchaseXmlLineTax tax) =>
        new(tax.TaxCode, tax.TaxRateCode, tax.Tarifa, tax.TaxableBase, tax.TaxAmount);
}

/// <summary>
/// PURCHASE-XML-LINE-ADDITIONAL-FIELDS-01 — espejo plano (no EF) de un
/// <see cref="PurchaseReceptionLineAdditionalField"/> o de un <see cref="ParsedPurchaseXmlLineAdditionalField"/>
/// recién parseado. Nunca se persiste, nunca se interpreta.
/// </summary>
public sealed record PurchaseDraftMergedLineAdditionalField(string Name, string Value, int Position)
{
    public static PurchaseDraftMergedLineAdditionalField FromPersisted(
        PurchaseReceptionLineAdditionalField field
    ) => new(field.Name, field.Value, field.SortOrder);

    public static PurchaseDraftMergedLineAdditionalField FromParsed(
        ParsedPurchaseXmlLineAdditionalField field
    ) => new(field.Name, field.Value, field.Position);
}

/// <summary>
/// FLOW-READY-02F.2 — línea de borrador ya fusionada: datos fiscales/documentales frescos del XML
/// re-parseado (fuente documental principal) + datos operativos (Item Matching: <see cref="ItemId"/>/
/// <see cref="MatchStatus"/>) tal como quedaron persistidos en la recepción. Record inmutable, plano
/// (no una entidad EF trackeada) — vive solo en memoria para armar el <see cref="PurchaseDraft"/> de
/// "Crear compra"; nunca se persiste ni muta <see cref="PurchaseReceptionLine"/>.
/// </summary>
public sealed record PurchaseDraftMergedLine(
    Guid Id,
    Guid? ItemId,
    ItemMatchStatus MatchStatus,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    string VatCode,
    decimal DiscountPct,
    string? IceCode,
    string? SupplierCode,
    string? SupplierAuxCode,
    decimal Discount,
    decimal LineSubtotal,
    string TaxCode,
    decimal VatPercentage,
    decimal TaxValue,
    decimal TotalLine,
    IReadOnlyList<PurchaseDraftMergedLineTax> Taxes,
    IReadOnlyList<PurchaseDraftMergedLineAdditionalField> AdditionalFields
)
{
    /// <summary>
    /// Camino "sin fusión": la línea persistida tal cual, sin ningún dato fresco del XML. Se usa
    /// cuando no hay XML, cuando no parsea, o cuando una clave de correlación queda ambigua — en ese
    /// caso los AdditionalFields persistidos (si existen) son los que se muestran, nunca se cruzan
    /// con los de otra línea del mismo grupo ambiguo.
    /// </summary>
    public static PurchaseDraftMergedLine FromPersisted(PurchaseReceptionLine line) =>
        new(
            Id: line.Id,
            ItemId: line.ItemId,
            MatchStatus: line.MatchStatus,
            Description: line.Description,
            Quantity: line.Quantity,
            UnitPrice: line.UnitPrice,
            VatCode: line.VatCode,
            DiscountPct: line.DiscountPct,
            IceCode: line.IceCode,
            SupplierCode: line.SupplierCode,
            SupplierAuxCode: line.SupplierAuxCode,
            Discount: line.Discount,
            LineSubtotal: line.LineSubtotal,
            TaxCode: line.TaxCode,
            VatPercentage: line.VatPercentage,
            TaxValue: line.TaxValue,
            TotalLine: line.TotalLine,
            Taxes: line.Taxes.Select(PurchaseDraftMergedLineTax.FromPersisted).ToList(),
            AdditionalFields: line.AdditionalFields.Select(
                    PurchaseDraftMergedLineAdditionalField.FromPersisted
                )
                .ToList()
        );

    /// <summary>
    /// Camino "fusionado": todos los campos fiscales/documentales (incl. AdditionalFields) vienen del
    /// XML recién parseado (<paramref name="fresh"/>) — el ítem/matching viene de la línea persistida
    /// (<paramref name="persisted"/>). AdditionalFields es documental, igual que Taxes: siempre
    /// prefiere el XML fresco cuando hay fusión.
    /// </summary>
    public static PurchaseDraftMergedLine FromFreshXmlWithPersistedMatching(
        PurchaseReceptionLine persisted,
        ParsedPurchaseXmlLine fresh
    ) =>
        new(
            Id: persisted.Id,
            ItemId: persisted.ItemId,
            MatchStatus: persisted.MatchStatus,
            Description: fresh.Description,
            Quantity: fresh.Quantity,
            UnitPrice: fresh.UnitPrice,
            VatCode: fresh.VatCode,
            DiscountPct: fresh.DiscountPct,
            IceCode: fresh.IceCode,
            SupplierCode: fresh.SupplierCode,
            SupplierAuxCode: fresh.SupplierAuxCode,
            Discount: fresh.Discount,
            LineSubtotal: fresh.LineSubtotal,
            TaxCode: fresh.TaxCode,
            VatPercentage: fresh.VatPercentage,
            TaxValue: fresh.TaxValue,
            TotalLine: fresh.TotalLine,
            Taxes: fresh.Taxes.Select(t => PurchaseDraftMergedLineTax.FromParsed(t) with
            {
                PurchaseReceptionLineTaxId = persisted.Taxes.Where(p => p.TaxCode == t.TaxCode
                    && p.TaxRateCode == t.TaxRateCode && p.Tarifa == t.Tarifa
                    && p.TaxableBase == t.TaxableBase && p.TaxAmount == t.TaxAmount)
                    .Select(p => (Guid?)p.Id).SingleOrDefault()
            }).ToList(),
            AdditionalFields: fresh
                .AdditionalFields.Select(PurchaseDraftMergedLineAdditionalField.FromParsed)
                .ToList()
        );
}

/// <summary>
/// Solo claves unicas en ambos lados admiten recuperar evidencia del XML reparseado.
/// Los grupos repetidos conservan el snapshot persistido de cada ID: ni el conteo igual
/// ni el orden de enumeracion acreditan identidad entre detalles XML y recepcion.
/// Nunca modifica las entidades ni persiste cambios.
/// </summary>
public static class PurchaseDraftLineMerger
{
    public static IReadOnlyList<PurchaseDraftMergedLine> Merge(
        IReadOnlyList<PurchaseReceptionLine> persistedLines,
        IReadOnlyList<ParsedPurchaseXmlLine> freshLines,
        Action<string>? onAmbiguousGroup = null
    )
    {
        var freshGroups = freshLines
            .GroupBy(f => Key(f.SupplierCode, f.Description))
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<PurchaseDraftMergedLine>();
        foreach (
            var persistedGroup in persistedLines.GroupBy(p => Key(p.SupplierCode, p.Description))
        )
        {
            var persistedList = persistedGroup.ToList();
            if (
                freshGroups.TryGetValue(persistedGroup.Key, out var freshList)
                && freshList.Count == 1
                && persistedList.Count == 1
            )
            {
                result.Add(PurchaseDraftMergedLine.FromFreshXmlWithPersistedMatching(
                    persistedList[0], freshList[0]));
            }
            else
            {
                onAmbiguousGroup?.Invoke(persistedGroup.Key);
                result.AddRange(persistedList.Select(PurchaseDraftMergedLine.FromPersisted));
            }
        }

        return result;
    }

    // Separador improbable en descripciones reales — evita colisiones de clave entre, p. ej.,
    // SupplierCode="A" + Description="B" y SupplierCode="" + Description="AB".
    private static string Key(string? supplierCode, string description) =>
        (supplierCode?.Trim() ?? string.Empty) + "␟" + description.Trim();
}
