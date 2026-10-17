using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Accounting.Repositories;

/// <summary>
/// ACCOUNTING-CONTINUITY-05 — documentos que generan hechos contables (venta, devolución de venta,
/// compra, gasto, NC y devolución de compra, pago a proveedor, cobro/pago financiero), excluidos
/// borradores y descartes. Las devoluciones sin fecha de negocio propia se evalúan por su instante de
/// registro desde el inicio del día de negocio en la zona horaria de la empresa (<see cref="ICompanyClock"/>).
/// </summary>
public sealed class AccountingRelevantFactsReader(ErpDbContext db, ICompanyClock clock) : IAccountingRelevantFactsReader
{
    public async Task<bool> ExistsOnOrAfterAsync(Guid tenantId, Guid companyId, DateOnly date, CancellationToken ct)
    {
        var (instant, _) = await clock.DayUtcRangeAsync(companyId, tenantId, date, ct);
        return await db.SalesInvoices.AnyAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && x.Status != SalesInvoiceStatus.Draft && x.IssueDate >= date, ct)
            || await db.SalesReturns.AnyAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && x.Status != SalesReturnStatus.Draft && x.CreatedAt >= instant, ct)
            || await db.PurchaseInvoices.AnyAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && (x.Status == PurchaseStatus.Confirmed || x.Status == PurchaseStatus.Cancelled) && x.IssueDate >= date, ct)
            || await db.ExpenseDocuments.AnyAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && x.Status != ExpenseStatus.Draft && x.AccountingDate >= date, ct)
            || await db.PurchaseCreditNotes.AnyAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && x.Status != PurchaseCreditNoteStatus.Draft && x.IssueDate >= date, ct)
            || await db.PurchaseReturns.AnyAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && x.Status != PurchaseReturnStatus.Draft && x.CreatedAt >= instant, ct)
            || await db.SupplierPayments.AnyAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && x.PaymentDate >= date, ct)
            || await db.Payments.AnyAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && x.Status != PaymentStatus.Draft && x.PaymentDate >= date, ct)
            || await ValuedStockAdjustments(tenantId, companyId, date, null).AnyAsync(ct)
            || await AccountableCashMovements(tenantId, companyId).AnyAsync(m => m.CreatedAt >= instant, ct)
            || await CashSessionsWithDifference(tenantId, companyId).AnyAsync(s => s.ClosedAt >= instant, ct);
    }

    /// <summary>
    /// ACCOUNTING-12C — movimientos de Caja con hecho contable propio: manuales con motivo elegido por el
    /// cajero (ManualIncome/ManualExpense/Withdrawal; los internos de Finance no llevan motivo y los
    /// contabiliza su documento) y depósitos bancarios. Se leen como hijos de la sesión (scope
    /// Tenant+Company del agregado), nunca con una consulta directa de CashMovement.
    /// </summary>
    private IQueryable<ERP.Domain.Modules.Caja.Entities.CashMovement> AccountableCashMovements(Guid tenantId, Guid companyId) =>
        db.CashSessions
            .Where(s => s.TenantId == tenantId && s.CompanyId == companyId)
            .SelectMany(s => s.Movements)
            .Where(m => (m.ReasonId != null
                    && (m.MovementType == CashMovementType.ManualIncome
                        || m.MovementType == CashMovementType.ManualExpense
                        || m.MovementType == CashMovementType.Withdrawal))
                || m.MovementType == CashMovementType.BankDeposit);

    /// <summary>ACCOUNTING-12C — sesiones cerradas con diferencia de arqueo (sobrante o faltante).</summary>
    private IQueryable<ERP.Domain.Modules.Caja.Entities.CashSession> CashSessionsWithDifference(Guid tenantId, Guid companyId) =>
        db.CashSessions.Where(s => s.TenantId == tenantId && s.CompanyId == companyId
            && s.Status == CashSessionStatus.Closed && s.Difference != null && s.Difference != 0m);

    /// <summary>
    /// ACCOUNTING-12A — ajuste ejecutado (o anulado después) con valor en el Kardex: algún movimiento
    /// ORIGINAL (PositiveAdjust en Ingreso, NegativeAdjust en Egreso; la reversa de la anulación es del tipo
    /// contrario) con TotalCost &gt; 0, fechado por su EffectiveDate — la fecha contable del hecho. Excluye
    /// el saldo inicial (InitialBalance, contabilizado por Carga Inicial) y los ajustes sin valor.
    /// </summary>
    private IQueryable<ERP.Domain.Modules.Inventory.Entities.StockAdjustment> ValuedStockAdjustments(
        Guid tenantId, Guid companyId, DateOnly? from, DateOnly? to) =>
        db.StockAdjustments.Where(x => x.TenantId == tenantId && x.CompanyId == companyId
            && (x.Status == "Executed" || x.Status == "Cancelled")
            && db.StockMovements.Any(m => m.TenantId == tenantId && m.SourceDocId == x.Id
                && m.SourceDocType == StockAdjustmentExecutedPostingTranslator.SourceDocType
                && m.MovementType == (x.MovementType == ERP.Domain.Modules.Inventory.Entities.StockAdjustment.MovementTypeIngreso
                    ? StockMovementType.PositiveAdjust
                    : StockMovementType.NegativeAdjust)
                && m.TotalCost > 0m
                && (from == null || m.EffectiveDate >= from)
                && (to == null || m.EffectiveDate <= to)));

    /// <remarks>
    /// Cada origen usa la fecha contable con la que su traductor registra la obligación y solo los estados
    /// que realmente la generan (accounting.md §19/§21):
    /// <list type="bullet">
    /// <item>Sales/InvoiceIssued — factura Authorized o Cancelled (Cancel exige Authorized); IssueDate.</item>
    /// <item>Sales/SalesReturn — devolución Authorized (Cancel solo desde Draft; Authorized es terminal);
    /// día Company de UpdatedAt, que Authorize fija como última mutación.</item>
    /// <item>Purchases/InvoiceReceived — compra Confirmed o Cancelled (Discarded = borrador); IssueDate.</item>
    /// <item>Purchases/PurchaseReturn — devolución con AuthorizedAtUtc (Authorized o anulada después); día Company.</item>
    /// <item>Purchases/PurchaseCreditNoteAuthorized — NC Discount con AuthorizedAtUtc; día Company. La NC Return
    /// copia AuthorizedAtUtc de su devolución y se contabiliza solo como PurchaseReturn.</item>
    /// <item>Purchases/SupplierCreditApplied — movimiento Application; día Company de CreatedAtUtc.</item>
    /// <item>Purchases/SupplierCreditRefunded — transacción RefundReceived (identidad = movimiento); EffectiveDate.</item>
    /// <item>Expenses/DocumentConfirmed — gasto Confirmed o Cancelled (Cancel exige Confirmed); AccountingDate.</item>
    /// <item>Retentions/DocumentIssued — retención Issued o Cancelled (Cancel exige Issued); IssueDate.</item>
    /// <item>Payables/SupplierPaymentConfirmed — todo pago a proveedor (no hay borrador); PaymentDate.</item>
    /// <item>Finance/CollectionApplied — cobro (Collection) Applied o Reversed; PaymentDate.</item>
    /// <item>Inventory/StockAdjustmentIn|Out — ajuste Executed o Cancelled con valor Kardex; EffectiveDate del movimiento Kardex de la ejecución.</item>
    /// <item>Caja/ManualIncome|ManualExpense|Withdrawal|BankDeposit — movimiento manual con motivo o depósito; día Company de CreatedAt.</item>
    /// <item>Caja/CashCountSurplus|CashCountShortage — sesión Closed con Difference distinta de cero (identidad = sesión); día Company de ClosedAt.</item>
    /// </list>
    /// El costo de ventas no se concilia aquí: lo cubren sus obligaciones y SaleCostObligation pendiente.
    /// </remarks>
    public async Task<int> CountWithoutAccountingAsync(Guid tenantId, Guid companyId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        // Identidades contables existentes (obligación en cualquier estado o asiento histórico) por módulo.
        IQueryable<Guid> Accounted(string module) =>
            db.AccountingPostings
                .Where(p => p.TenantId == tenantId && p.CompanyId == companyId && p.SourceModule == module)
                .Select(p => p.SourceEventId)
                .Concat(db.JournalEntries
                    .Where(j => j.TenantId == tenantId && j.CompanyId == companyId && j.SourceModule == module)
                    .Select(j => j.SourceEventId));

        var sales = Accounted(SalesInvoiceAuthorizedPostingTranslator.SourceModuleName);
        var purchases = Accounted(PurchaseInvoiceConfirmedPostingTranslator.SourceModuleName);
        var expenses = Accounted(ExpenseDocumentConfirmedPostingTranslator.SourceModuleName);
        var supplierPayments = Accounted(SupplierPaymentConfirmedPostingTranslator.SourceModuleName);
        var collections = Accounted(CollectionAppliedPostingTranslator.SourceModuleName);
        var retentions = Accounted(RetentionDocumentIssuedPostingTranslator.SourceModuleName);
        var adjustments = Accounted(StockAdjustmentExecutedPostingTranslator.SourceModuleName);
        var cash = Accounted(CashManualMovementPostingTranslator.SourceModuleName);

        // Orígenes fechados por el instante del acto: día de empresa [from, to] como rango UTC.
        var (fromUtc, toUtcExclusive) = await clock.DaysUtcRangeAsync(companyId, tenantId, from, to, ct);
        var startUtc = fromUtc!.Value;
        var endUtc = toUtcExclusive!.Value;

        return await db.SalesInvoices.CountAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && (x.Status == SalesInvoiceStatus.Authorized || x.Status == SalesInvoiceStatus.Cancelled)
                   && x.IssueDate >= from && x.IssueDate <= to && !sales.Contains(x.Id), ct)
            + await db.SalesReturns.CountAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && x.Status == SalesReturnStatus.Authorized
                   && x.UpdatedAt >= startUtc && x.UpdatedAt < endUtc && !sales.Contains(x.Id), ct)
            + await db.PurchaseInvoices.CountAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && (x.Status == PurchaseStatus.Confirmed || x.Status == PurchaseStatus.Cancelled)
                   && x.IssueDate >= from && x.IssueDate <= to && !purchases.Contains(x.Id), ct)
            + await db.PurchaseReturns.CountAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && x.AuthorizedAtUtc >= startUtc && x.AuthorizedAtUtc < endUtc && !purchases.Contains(x.Id), ct)
            + await db.PurchaseCreditNotes.CountAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && x.ApplicationType == PurchaseCreditNoteApplicationType.Discount
                   && x.AuthorizedAtUtc >= startUtc && x.AuthorizedAtUtc < endUtc && !purchases.Contains(x.Id), ct)
            + await db.SupplierCreditMovements.CountAsync(m => m.TenantId == tenantId
                   && m.MovementType == SupplierCreditMovementType.Application
                   && db.SupplierCredits.Any(c => c.Id == m.SupplierCreditId && c.CompanyId == companyId)
                   && m.CreatedAtUtc >= startUtc && m.CreatedAtUtc < endUtc && !purchases.Contains(m.Id), ct)
            + await db.SupplierCreditRefundTransactions.CountAsync(t => t.TenantId == tenantId && t.CompanyId == companyId
                   && t.TransactionTypeCode == RefundTransactionTypeCode.RefundReceived
                   && t.EffectiveDate >= from && t.EffectiveDate <= to && !purchases.Contains(t.SupplierCreditMovementId), ct)
            + await db.ExpenseDocuments.CountAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && (x.Status == ExpenseStatus.Confirmed || x.Status == ExpenseStatus.Cancelled)
                   && x.AccountingDate >= from && x.AccountingDate <= to && !expenses.Contains(x.Id), ct)
            + await db.RetentionDocuments.CountAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && (x.Status == RetentionStatus.Issued || x.Status == RetentionStatus.Cancelled)
                   && x.IssueDate >= from && x.IssueDate <= to && !retentions.Contains(x.Id), ct)
            + await db.SupplierPayments.CountAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && x.PaymentDate >= from && x.PaymentDate <= to && !supplierPayments.Contains(x.Id), ct)
            + await db.Payments.CountAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && x.Direction == PaymentDirection.Collection
                   && (x.Status == PaymentStatus.Applied || x.Status == PaymentStatus.Reversed)
                   && x.PaymentDate >= from && x.PaymentDate <= to && !collections.Contains(x.Id), ct)
            + await ValuedStockAdjustments(tenantId, companyId, from, to).CountAsync(x => !adjustments.Contains(x.Id), ct)
            + await AccountableCashMovements(tenantId, companyId).CountAsync(m => m.CreatedAt >= startUtc
                   && m.CreatedAt < endUtc && !cash.Contains(m.Id), ct)
            + await CashSessionsWithDifference(tenantId, companyId).CountAsync(x => x.ClosedAt >= startUtc
                   && x.ClosedAt < endUtc && !cash.Contains(x.Id), ct);
    }
}
