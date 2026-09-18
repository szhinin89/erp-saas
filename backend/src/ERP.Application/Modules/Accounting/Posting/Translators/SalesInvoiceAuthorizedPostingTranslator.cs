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

        // SALES-COLLECTION-ACCOUNT-SSOT-CLEANUP-01: Application (AuthorizeSalesInvoiceHandler)
        // resuelve, para CADA pago no-Crédito de la venta, la cuenta contable real desde su única
        // fuente de verdad (Efectivo -> CashRegister, Transferencia -> CompanyBankAccount,
        // Tarjeta/Cheque -> PaymentMethodAccount; fail-closed incondicional, sin excepciones) — el
        // desglose resultante (CashByAccount) se contabiliza vía PostingFact.Allocations
        // (EXPENSES-POSTING-ALLOCATIONS-06, mismo mecanismo ya usado por Gastos para N cuentas
        // dinámicas), nunca vía la cuenta fija de PostingRuleLine. En producción, CashByAccount
        // siempre cubre el 100% de e.CashApplied (Application no autoriza la venta si algún método
        // queda sin cuenta resuelta) — factCashApplied da 0 y solo existen allocations. El camino
        // "sin desglose" (CashByAccount vacío, factCashApplied = e.CashApplied completo contra la
        // cuenta fija histórica de la PostingRule) es exclusivamente una conveniencia para tests
        // que construyen el evento/fact directamente sin pasar por el handler — nunca ocurre en un
        // flujo real de autorización.
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
