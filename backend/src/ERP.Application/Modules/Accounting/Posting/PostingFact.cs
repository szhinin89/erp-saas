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
    Guid? OverrideAccountId = null,
    // EXPENSES-POSTING-ALLOCATIONS-06 — líneas dinámicas por cuenta, cardinalidad variable
    // (a diferencia de los campos anteriores, todos monto único). Mismo criterio aditivo:
    // opcional, agregada al final, null por defecto — ningún call site existente se rompe.
    // Ver PostingAllocation.cs y JournalFactory.Create para cómo se consume.
    IReadOnlyCollection<PostingAllocation>? Allocations = null,
    // RETENTIONS-EXPENSES-INTEGRATION-01D-2 — cierra el gap dejado deliberadamente abierto por
    // PostingAmountKind.Retention (ver JournalFactory.ResolveAmount: hasta ahora resolvía siempre
    // 0m porque "PostingFact todavía no transporta ese monto"). Mismo criterio aditivo que los
    // campos anteriores: opcional, agregado al final, null por defecto — ningún call site
    // existente cambia de comportamiento. Usado por RetentionDocumentIssuedPostingTranslator para
    // el asiento de reclasificación (CxP proveedor → Retención por pagar).
    decimal? RetainedAmount = null,
    // RETENTIONS-TAX-COMPONENT-POSTING-02C — separa RetainedAmount (total, sin cambios) por
    // componente tributario, para que el asiento de retención pueda acreditar dos cuentas
    // distintas (Retenciones IVA / Retenciones Renta) en vez de una sola. Mismo criterio aditivo:
    // opcionales, agregados al final, null por defecto — ningún call site existente cambia de
    // comportamiento. Usados por RetentionDocumentIssuedPostingTranslator junto con RetainedAmount
    // (que sigue representando el total, para el Debe de CxP proveedor).
    decimal? RetainedVatAmount = null,
    decimal? RetainedIncomeAmount = null
);
