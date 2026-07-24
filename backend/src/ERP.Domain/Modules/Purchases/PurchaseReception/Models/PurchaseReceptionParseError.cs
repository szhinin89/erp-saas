namespace ERP.Domain.Modules.Purchases.PurchaseReception.Models;

/// <summary>Una línea del TXT que no pudo interpretarse — no aborta el resto del archivo.</summary>
public sealed record PurchaseReceptionParseError(int LineNumber, string RawLine, string Reason);
