using ERP.Application.Modules.Sales.Exceptions;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Sales.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce SalesInvoiceAuthorizedEvent (Sales) a PostingFact e invoca IPostingEngine — no crea
/// JournalEntry, no resuelve cuentas, no contiene lógica financiera (ADR-026 §8, Fase 3.2).
/// </summary>
/// <remarks>
/// SALES-JOURNAL-ENTRY-SILENT-FAILURE-01 Lote 2 — el asiento <c>Sales/InvoiceIssued</c> es
/// obligatorio (ingreso + IVA/ICE/IRBPNR + CxC de toda venta autorizada), a diferencia de
/// <see cref="SalesInvoiceCogsPostingTranslator"/> (costo de venta: un fallo ahí nunca debe
/// revertir la venta ya autorizada — "log-and-continue" es una decisión deliberada de ESE
/// traductor, documentada en su propio remarks, no se toca aquí). Antes de este lote, un fallo de
/// <see cref="IPostingEngine.PostAsync"/> aquí solo generaba <c>LogWarning</c> — evidencia real
/// (BD dev, 2026-09-13, log <c>erp-20260913.txt</c>): dos facturas autorizadas
/// (001-001-000000001, 001-001-000000002) sin ningún <c>JournalEntry</c> InvoiceIssued, con el
/// warning "El asiento no está balanceado" tragado silenciosamente. Mismo criterio ya establecido
/// por <c>ExpenseDocumentConfirmedPostingTranslator</c>/<c>SupplierPaymentConfirmedPostingTranslator</c>:
/// lanzar <see cref="SalesInvoicePostingFailedException"/> (nunca solo loguear) para que la
/// transacción completa de autorización se revierta (ADR-026 §8: Publish() ocurre dentro de
/// <c>ErpDbContext.SaveChangesAsync</c>, antes del commit).
/// </remarks>
public sealed class SalesInvoiceAuthorizedPostingTranslator
    : INotificationHandler<SalesInvoiceAuthorizedEvent>
{
    private const string SourceModuleName = "Sales";
    private const string FactTypeName = "InvoiceIssued";

    private readonly IPostingEngine _postingEngine;

    public SalesInvoiceAuthorizedPostingTranslator(IPostingEngine postingEngine)
    {
        _postingEngine = postingEngine;
    }

    public async Task Handle(SalesInvoiceAuthorizedEvent e, CancellationToken ct)
    {
        // SALES-CASH-VS-RECEIVABLE-POSTING-SPLIT-AND-CANCEL-REVERSAL-01 Lote 3 — mismo cálculo que
        // SalesSettlementPolicy.Calculate (Sales, dominio): PendingBalance = GrandTotal -
        // CashApplied, nunca negativo. e.CashApplied ya es el dinero real cobrado (ver
        // SalesInvoiceAuthorizedEvent.CashApplied) — nunca se recalcula aquí, solo se deriva el
        // complemento para separar la línea de Debe Caja/Bancos de la línea de Debe CxC.
        var pendingBalance = e.GrandTotal - e.CashApplied;
        if (pendingBalance < 0)
            pendingBalance = 0;

        // SALES-TRANSFER-ACCOUNTING-CASH-VS-BANK-01: si Application (AuthorizeSalesInvoiceHandler)
        // resolvio el desglose de e.CashApplied por cuenta contable real (CashByAccount, una por
        // cada PaymentMethodAccount configurado y usado en la venta), esas cuentas se contabilizan
        // via PostingFact.Allocations (EXPENSES-POSTING-ALLOCATIONS-06, mismo mecanismo ya usado
        // por Gastos para N cuentas dinamicas), nunca via la cuenta fija de PostingRuleLine.
        // e.CashByAccount NO tiene por que sumar e.CashApplied: Application deliberadamente deja
        // afuera el monto de EFECTIVO sin PaymentMethodAccount configurado (compatibilidad con
        // companies/entornos que no han migrado — ver comentario en AuthorizeSalesInvoiceHandler).
        // factCashApplied transporta exactamente ese remanente no cubierto por allocations, para
        // que la linea fija historica de la PostingRule ("Caja general") lo contabilice como
        // siempre — nunca se pierde ni se duplica un centavo del Debe total. Sin desglose
        // (CashByAccount vacio: callers/tests que no lo proveen), comportamiento IDENTICO al
        // anterior: factCashApplied = e.CashApplied completo, sin allocations, cuenta fija de la
        // PostingRule (compatibilidad total con Lote 1/2/3 ya cerrados).
        IReadOnlyCollection<PostingAllocation>? cashAllocations = null;
        var factCashApplied = e.CashApplied;
        if (e.CashByAccount.Count > 0)
        {
            cashAllocations = e
                .CashByAccount.Where(kv => kv.Value > 0m)
                .Select(kv => new PostingAllocation(kv.Key, kv.Value, AccountNature.Debit))
                .ToList();
            factCashApplied = e.CashApplied - e.CashByAccount.Values.Sum();
            if (factCashApplied < 0m)
                factCashApplied = 0m;
        }

        var fact = new PostingFact(
            e.TenantId!.Value,
            e.CompanyId,
            SourceModuleName,
            FactTypeName,
            e.InvoiceId,
            e.IssueDate,
            e.Subtotal,
            e.TotalVat,
            e.TotalIce,
            e.TotalDiscount,
            e.GrandTotal,
            TotalIrbpnr: e.TotalIrbpnr,
            CashApplied: factCashApplied,
            PendingBalance: pendingBalance,
            Allocations: cashAllocations
        );

        var result = await _postingEngine.PostAsync(fact, ct);

        if (!result.IsSuccess)
            throw new SalesInvoicePostingFailedException(
                result.Error ?? "No se pudo contabilizar la venta.",
                result.Code
            );
    }
}
