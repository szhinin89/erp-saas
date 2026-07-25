namespace ERP.Application.Modules.Accounting.Posting;

/// <summary>
/// Hecho contable de origen que dispara el Posting Engine (ADR-026 §8). Identidad, trazabilidad
/// y los montos ya resueltos por el módulo de origen (ADR-026 §4, Fase 3.5.2) — Accounting los
/// consume tal cual, nunca los recalcula. Currency/ExchangeRate/Branch/CostCenter/Metadata no
/// pertenecen a esta fase (JournalEntryLine/partida doble aún no existen, ver JournalEntry.cs).
/// </summary>
public sealed record PostingFact(
    Guid TenantId,
    Guid CompanyId,
    string SourceModule,
    string FactType,
    Guid SourceEventId,
    DateOnly EntryDate,
    decimal Subtotal,
    decimal TotalVat,
    decimal TotalIce,
    decimal TotalDiscount,
    decimal GrandTotal);
