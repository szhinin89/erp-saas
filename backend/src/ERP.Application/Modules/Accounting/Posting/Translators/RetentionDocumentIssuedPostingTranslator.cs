using ERP.Application.Modules.Retentions.Exceptions;
using ERP.Domain.Modules.Retentions.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// RETENTIONS-EXPENSES-INTEGRATION-01D-2 — traduce RetentionDocumentIssuedEvent (Retentions) a
/// PostingFact e invoca IPostingEngine — no crea JournalEntry, no resuelve cuentas, no contiene
/// lógica financiera (ADR-026 §8, Fase 3.4), mismo criterio que el resto de traductores.
///
/// Genera el asiento de RECLASIFICACIÓN de la retención (nunca modifica el asiento del documento
/// origen ya generado por <see cref="ExpenseDocumentConfirmedPostingTranslator"/>): mueve el monto
/// retenido de "CxP proveedor" (Debe) a "Retención por pagar" (Haber) — ver
/// docs/decisions/RETENTIONS-MODULE-DESIGN-01.md § "Impacto contable" para el ejemplo conceptual
/// (Gasto 100 + IVA 15 = 115; Retención IVA 4.50 → Debe CxP proveedor 4.50, Haber Retención IVA por
/// pagar 4.50). Todas las cuentas se resuelven dinámicamente vía <c>PostingRule</c>/
/// <c>PostingRuleLine</c> configuradas por la empresa para SourceModule="Retentions",
/// FactType="DocumentIssued" — SIN cuentas hardcodeadas: si la empresa no configuró esta regla,
/// <c>PostingRuleResolver</c> falla fail-closed (mismo mecanismo ya usado por el resto del ERP,
/// nunca un "cuenta de sistema" nueva ni un GUID fijo en código).
///
/// RETENTIONS-TAX-COMPONENT-POSTING-02C — el Haber ya no es una sola línea por el monto total: se
/// separa por componente tributario (<see cref="PostingFact.RetainedVatAmount"/>/
/// <see cref="PostingFact.RetainedIncomeAmount"/>, resueltos por JournalFactory a
/// <see cref="ERP.Domain.Modules.Accounting.Enums.PostingAmountKind.RetentionVat"/>/
/// <c>RetentionIncome</c> respectivamente), tomados directamente de
/// <see cref="RetentionDocumentIssuedEvent.TotalRetainedVat"/>/<c>TotalRetainedIncome</c> — el
/// evento ya los transporta por separado desde <c>RetentionDocument.Issue()</c> (construido a
/// partir de <c>RetentionDocument.Lines</c>), así que no hay un segundo cálculo: es la misma
/// fuente de verdad, solo consumida sin sumar. <c>RetainedAmount</c> (total) se mantiene sin
/// cambios — sigue siendo el monto del Debe de CxP proveedor. Un componente en 0 no genera línea
/// contable (JournalFactory ya omite líneas de monto cero, ver su doc comment) — nunca se emite
/// una línea de Renta cuando la retención es solo de IVA, ni viceversa.
///
/// Posting ESTRICTO (decisión aprobada en el diseño normativo, "Impacto contable"): si falla,
/// LANZA <see cref="RetentionPostingFailedException"/> en vez de solo loguear — la transacción
/// ambiente completa (documento origen + AP + retención) hace rollback.
/// </summary>
public sealed class RetentionDocumentIssuedPostingTranslator
    : INotificationHandler<RetentionDocumentIssuedEvent>
{
    private const string SourceModuleName = "Retentions";
    private const string FactTypeName = "DocumentIssued";

    private readonly IPostingEngine _postingEngine;

    public RetentionDocumentIssuedPostingTranslator(IPostingEngine postingEngine) =>
        _postingEngine = postingEngine;

    public async Task Handle(RetentionDocumentIssuedEvent e, CancellationToken ct)
    {
        var fact = new PostingFact(
            e.TenantId!.Value,
            e.CompanyId,
            SourceModuleName,
            FactTypeName,
            e.RetentionDocumentId,
            e.IssueDate,
            Subtotal: 0m,
            TotalVat: e.TotalRetainedVat,
            TotalIce: 0m,
            TotalDiscount: 0m,
            GrandTotal: e.TotalRetained,
            RetainedAmount: e.TotalRetained,
            RetainedVatAmount: e.TotalRetainedVat,
            RetainedIncomeAmount: e.TotalRetainedIncome
        );

        var result = await _postingEngine.PostAsync(fact, ct);

        if (!result.IsSuccess)
            throw new RetentionPostingFailedException(
                result.Error ?? "No se pudo contabilizar la retención.",
                result.Code
            );
    }
}
