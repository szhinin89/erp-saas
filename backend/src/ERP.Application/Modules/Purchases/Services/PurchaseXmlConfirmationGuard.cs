using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using ERP.Domain.Common;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;

namespace ERP.Application.Modules.Purchases.Services;

public interface IPurchaseXmlConfirmationGuard
{
    Task<string?> ValidateAsync(PurchaseInvoice invoice, CancellationToken ct);
}

/// <summary>
/// COMPRAS-METODO-ZH-01A — reconciles the purchase against its authorized XML snapshot before any
/// confirmation effect (Kardex, AP, accounting). Blocks only what compromises quantities, costs or
/// totals, or breaks line traceability; informational processing warnings do not block.
/// </summary>
public sealed class PurchaseXmlConfirmationGuard(
    IPurchaseReceptionDocumentRepository receptions,
    IPurchaseXmlDraftParser parser) : IPurchaseXmlConfirmationGuard
{
    public async Task<string?> ValidateAsync(PurchaseInvoice invoice, CancellationToken ct)
    {
        var linked = invoice.Lines.Where(l => l.PurchaseReceptionLineId.HasValue).ToList();
        PurchaseReceptionDocument? source = null;
        if (!string.IsNullOrWhiteSpace(invoice.AccessKey))
            source = await receptions.GetByAccessKeyAsync(invoice.TenantId, invoice.AccessKey, ct);
        if (source is null && linked.Count > 0)
            source = await receptions.GetByLineIdAsync(invoice.TenantId, linked[0].PurchaseReceptionLineId!.Value, ct);
        if (source is null)
            return linked.Count == 0 ? null : "No se encuentra la recepción XML vinculada a la compra.";
        // A manual purchase whose SRI record has no authorized detail yet has nothing to reconcile.
        if (linked.Count == 0 && source.Status is PurchaseReceptionDocumentStatus.Imported
                or PurchaseReceptionDocumentStatus.Cancelled)
            return null;

        if (!BelongsTo(source, invoice))
            return "La recepción XML no corresponde a la empresa, sucursal, proveedor o comprobante de esta compra.";
        if (!IsFullyProcessed(source, invoice))
            return "El XML tiene líneas sin procesar. Debe conciliar el documento completo antes de confirmar.";

        var parsed = parser.Parse(source.XmlContent!);
        if (!parsed.IsSuccess || parsed.Value is not { } xml || xml.LineErrors.Count > 0
            || xml.Lines.Count != source.Lines.Count || xml.TotalWithoutTaxes is null)
            return "El XML no permite conciliar todas las líneas y totales. Revise sus advertencias antes de confirmar.";
        if (!HeaderReconciles(source, xml))
            return "Los totales o la cabecera de la recepción no concilian con el XML autorizado.";
        if (!SnapshotMatchesXml(source, xml))
            return "El detalle persistido de la recepción difiere del XML autorizado.";
        if (!EveryXmlLineLinkedOnce(source, invoice, linked))
            return "Hay líneas XML omitidas, duplicadas o sin vínculo. Concilie cada línea antes de confirmar; redistribuir un importe no sustituye su trazabilidad.";

        var changed = linked.FirstOrDefault(line =>
            !LineMatches(line, source.Lines.Single(l => l.Id == line.PurchaseReceptionLineId)));
        return changed is null
            ? null
            : $"La línea '{changed.Description}' difiere del XML en cantidad, precio, descuento o impuestos. Debe conciliarla antes de confirmar.";
    }

    private static bool BelongsTo(PurchaseReceptionDocument source, PurchaseInvoice invoice) =>
        source.TenantId == invoice.TenantId && source.CompanyId == invoice.CompanyId
        && source.BranchId == invoice.BranchId
        && source.SupplierRuc == invoice.SupplierTaxId
        && (!source.SupplierId.HasValue || source.SupplierId == invoice.SupplierId)
        && source.InvoiceNumber == invoice.InvoiceNumber && source.IssueDate == invoice.IssueDate
        && source.CurrencyCode == invoice.CurrencyCode
        && (!source.PurchaseId.HasValue || source.PurchaseId == invoice.Id)
        && (string.IsNullOrWhiteSpace(invoice.AccessKey) || source.AccessKey == invoice.AccessKey);

    // Creating the draft marks the reception Processed for this same purchase (MarkProcessed).
    private static bool IsFullyProcessed(PurchaseReceptionDocument source, PurchaseInvoice invoice) =>
        (source.Status == PurchaseReceptionDocumentStatus.Verified
            || (source.Status == PurchaseReceptionDocumentStatus.Processed && source.PurchaseId == invoice.Id))
        && source.ProcessingStatus is not (PurchaseReceptionProcessingStatus.Pending or PurchaseReceptionProcessingStatus.Failed)
        && source.LinesDetectedCount > 0 && source.LinesDetectedCount == source.LinesProcessedCount
        && source.LinesProcessedCount == source.Lines.Count && !string.IsNullOrWhiteSpace(source.XmlContent);

    // Every non-zero line base is part of totalSinImpuestos, so an omitted line cannot hide here.
    // importeTotal vs line totals may differ by header tax rounding or tip: informational only
    // (already shown as RoundingDifference in the XML view), never a reason to block.
    private static bool HeaderReconciles(PurchaseReceptionDocument source, ParsedPurchaseXml xml) =>
        xml.SupplierRuc == source.SupplierRuc && xml.InvoiceNumber == source.InvoiceNumber
        && xml.IssueDate == source.IssueDate
        && Money(xml.Lines.Sum(l => l.LineSubtotal)) == Money(xml.TotalWithoutTaxes!.Value);

    // Compare the persisted detail snapshot with the fresh parser without relying on EF row order.
    private static bool SnapshotMatchesXml(PurchaseReceptionDocument source, ParsedPurchaseXml xml)
    {
        var storedRows = source.Lines.Select(l => (l.Quantity, l.UnitPrice, l.Discount, l.LineSubtotal, l.TotalLine))
            .GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        var parsedRows = xml.Lines.Select(l => (l.Quantity, l.UnitPrice, l.Discount, l.LineSubtotal, l.TotalLine))
            .GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        return storedRows.Count == parsedRows.Count && storedRows.All(pair =>
            parsedRows.TryGetValue(pair.Key, out var count) && count == pair.Value);
    }

    private static bool EveryXmlLineLinkedOnce(
        PurchaseReceptionDocument source, PurchaseInvoice invoice, IReadOnlyList<PurchaseInvoiceDetail> linked)
    {
        var ids = linked.Select(l => l.PurchaseReceptionLineId!.Value).ToHashSet();
        return linked.Count == invoice.Lines.Count && ids.Count == linked.Count
            && ids.Count == source.Lines.Count && source.Lines.All(l => ids.Contains(l.Id));
    }

    private static bool LineMatches(PurchaseInvoiceDetail line, PurchaseReceptionLine original) =>
        line.Quantity == original.Quantity && line.UnitPrice == original.UnitPrice
        && Money(line.DiscountAmount) == Money(original.Discount)
        && Money(line.TaxableBase) == Money(original.LineSubtotal)
        && Money(line.TaxInclusiveTotal) == Money(original.TotalLine);

    private static decimal Money(decimal value) =>
        Math.Round(value, FiscalPrecision.TaxAmount, MidpointRounding.AwayFromZero);
}
