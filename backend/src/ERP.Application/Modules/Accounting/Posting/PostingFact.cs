using ERP.Domain.Modules.Accounting.Enums;

namespace ERP.Application.Modules.Accounting.Posting;

/// <summary>
/// Hecho contable de origen que dispara el Posting Engine (ADR-026 §8). Identidad, trazabilidad
/// y los montos ya resueltos por el módulo de origen (ADR-026 §4, Fase 3.5.2) — Accounting los
/// consume tal cual, nunca los recalcula. Currency/ExchangeRate/Branch/CostCenter/Metadata no
/// pertenecen a esta fase (JournalEntryLine/partida doble aún no existen, ver JournalEntry.cs).
///
/// P0-02 Fase 6 (Remediación 01) — 5 campos nuevos, opcionales (<c>null</c> por defecto),
/// agregados al final de la lista posicional para no romper ningún call site existente
/// (<c>SalesReturnAuthorizedPostingTranslator</c>, <c>PurchaseInvoiceConfirmedPostingTranslator</c>,
/// etc. siguen construyendo el record con los 11 argumentos originales sin cambios). Exigidos por
/// el asiento compuesto de §19.1bis (`PurchaseReturnAuthorizedEvent`), que necesita 7 montos
/// independientes con cuenta contable propia — más de los 5 campos genéricos originales pueden
/// representar sin ambigüedad. <c>TotalVat</c>/<c>TotalIce</c> ya existentes se reutilizan tal
/// cual para ese hecho (mismo significado semántico: IVA/ICE de la línea), evitando duplicar
/// campos donde no hace falta.
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
    decimal GrandTotal,
    decimal? AppliedToPayableAmount = null,
    decimal? SupplierCreditAmount = null,
    decimal? CostVarianceDebitAmount = null,
    decimal? CostVarianceCreditAmount = null,
    decimal? HistoricalCostTotal = null,
    // FLOW-READY-02F.2 — IRBPNR (Compras). Mismo criterio aditivo que los 5 campos anteriores:
    // opcional, agregado al final, ningún call site existente se rompe.
    decimal? TotalIrbpnr = null,
    // ACCOUNTING-PAYMENT-METHOD-ACCOUNT-MAPPING-14 — override opcional de UNA cuenta de
    // PostingRule.Lines, resuelto y validado por el módulo de origen (Finance) ANTES de crear
    // este PostingFact, nunca por Accounting (mismo principio que Subtotal/TotalVat arriba: "ya
    // resueltos, Accounting los consume tal cual"). Identifica la línea a sustituir por
    // (AmountKind, Nature) — nunca por SourceModule/FactType, para no introducir un condicional
    // cerrado por tipo de hecho contable en JournalFactory (ADR-026 §6.2). Con ambos en null
    // (default), el comportamiento es idéntico al actual: JournalFactory usa la cuenta fija de la
    // PostingRule. Ver JournalFactory.ResolveLineAccountId/PostingAccountGuard.
    PostingAmountKind? OverrideAmountKind = null,
    AccountNature? OverrideAccountNature = null,
    Guid? OverrideAccountId = null
);
