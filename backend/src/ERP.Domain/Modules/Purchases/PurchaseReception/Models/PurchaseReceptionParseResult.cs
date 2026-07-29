namespace ERP.Domain.Modules.Purchases.PurchaseReception.Models;

/// <summary>Resultado completo de interpretar un archivo TXT de recepción SRI.</summary>
public sealed record PurchaseReceptionParseResult(
    IReadOnlyList<PurchaseReceptionRecord> Records,
    IReadOnlyList<PurchaseReceptionParseError> Errors,
    int SkippedUnsupportedCount
);
