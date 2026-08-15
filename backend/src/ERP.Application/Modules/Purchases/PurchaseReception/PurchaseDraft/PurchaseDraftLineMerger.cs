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
    decimal TaxAmount
)
{
    public static PurchaseDraftMergedLineTax FromPersisted(PurchaseReceptionLineTax tax) =>
        new(tax.TaxCode, tax.TaxRateCode, tax.Tarifa, tax.TaxableBase, tax.TaxAmount);

    public static PurchaseDraftMergedLineTax FromParsed(ParsedPurchaseXmlLineTax tax) =>
        new(tax.TaxCode, tax.TaxRateCode, tax.Tarifa, tax.TaxableBase, tax.TaxAmount);
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
    IReadOnlyList<PurchaseDraftMergedLineTax> Taxes
)
{
    /// <summary>
    /// Camino "sin fusión": la línea persistida tal cual, sin ningún dato fresco del XML. Se usa
    /// cuando no hay XML, cuando no parsea, o cuando una clave de correlación queda ambigua.
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
            Taxes: line.Taxes.Select(PurchaseDraftMergedLineTax.FromPersisted).ToList()
        );

    /// <summary>
    /// Camino "fusionado": todos los campos fiscales/documentales vienen del XML recién parseado
    /// (<paramref name="fresh"/>) — el ítem/matching viene de la línea persistida (<paramref name="persisted"/>).
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
            Taxes: fresh.Taxes.Select(PurchaseDraftMergedLineTax.FromParsed).ToList()
        );
}

/// <summary>
/// FLOW-READY-02F.2 — fusiona líneas recién parseadas del XML (fuente fiscal/documental fresca, ver
/// <see cref="IPurchaseXmlDraftParser"/>) con las líneas persistidas de la recepción (fuente de
/// datos operativos: Item Matching). Clave de correlación: (SupplierCode normalizado, Description
/// normalizada) — nunca índice de lista, porque <c>purchase_reception_lines</c> no tiene columna de
/// orden y la navegación EF <c>document.Lines</c> no garantiza orden estable sin <c>OrderBy</c>
/// explícito. Si para una clave hay N líneas persistidas y M líneas frescas con N != M, esas líneas
/// específicas NO se fusionan — se devuelven tal cual estaban persistidas (fallback seguro, nunca se
/// inventa una correlación ambigua). Nunca muta las entidades persistidas — solo lee de ellas, jamás
/// llama <c>SaveChangesAsync</c>.
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
                && freshList.Count == persistedList.Count
            )
            {
                for (var i = 0; i < persistedList.Count; i++)
                    result.Add(
                        PurchaseDraftMergedLine.FromFreshXmlWithPersistedMatching(
                            persistedList[i],
                            freshList[i]
                        )
                    );
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
