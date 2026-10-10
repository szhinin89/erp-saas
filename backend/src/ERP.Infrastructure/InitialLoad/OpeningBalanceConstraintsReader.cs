using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.InitialLoad;

/// <inheritdoc cref="IOpeningBalanceConstraintsReader"/>
public sealed class OpeningBalanceConstraintsReader : IOpeningBalanceConstraintsReader
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public OpeningBalanceConstraintsReader(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    // Company es ITenantScopedEntity: el filtro global solo acota por tenant; la empresa es explícita.
    public Task<DateOnly?> GetOpeningBalanceDateAsync(CancellationToken ct) =>
        _db.Companies.AsNoTracking()
            .Where(c => c.Id == _company.CompanyId)
            .Select(c => c.OpeningBalanceDate)
            .FirstOrDefaultAsync(ct);

    public async Task<InitialLoadClosure?> GetInitialLoadClosureAsync(CancellationToken ct)
    {
        var closed = await _db.Companies.AsNoTracking()
            .Where(c => c.Id == _company.CompanyId && c.InitialLoadClosedAt != null)
            .Select(c => new { c.InitialLoadClosedAt, c.InitialLoadClosedBy })
            .FirstOrDefaultAsync(ct);
        return closed is null ? null : new InitialLoadClosure(closed.InitialLoadClosedAt!.Value, closed.InitialLoadClosedBy);
    }

    public async Task<OpeningBalanceDateConstraints> GetAsync(
        DateOnly? currentOpeningBalanceDate, CancellationToken ct)
    {
        var confirmed = await _db.StockMovements.AsNoTracking()
            .Where(m => m.MovementType == StockMovementType.InitialBalance)
            .Select(m => m.EffectiveDate)
            .Distinct()
            .ToListAsync(ct);
        // IL-6B — los saldos iniciales de CxP guardan su corte en AccountingDate.
        var payableCutoffs = await _db.AccountsPayables.AsNoTracking()
            .Where(p => p.OriginType == AccountsPayableOriginType.InitialBalance)
            .Select(p => p.AccountingDate)
            .Distinct()
            .ToListAsync(ct);
        confirmed.AddRange(payableCutoffs.Where(d => !confirmed.Contains(d)));
        // Los saldos iniciales de CxC no guardan su corte: se confirman con la fecha vigente.
        if (currentOpeningBalanceDate is { } current && !confirmed.Contains(current)
            && await _db.SalesReceivables.AsNoTracking().AnyAsync(r => r.Origin == SalesReceivableOrigin.InitialBalance, ct))
            confirmed.Add(current);

        // IL-8A — CUALQUIER versión del ASI de apertura que llegó a publicarse fija la fecha para
        // siempre, aunque después haya sido reemplazada/reversada (historial). PostedAt solo lo
        // asigna MarkPosted y nunca se limpia (filtro global tenant + empresa).
        var hasPostedOpeningJournal = await _db.OpeningJournalEntryPostings.AsNoTracking()
            .AnyAsync(p => p.CompanyId == _company.CompanyId && p.PostedAt != null, ct);

        return new OpeningBalanceDateConstraints(
            await HasRealOperationsAsync(ct), confirmed, hasPostedOpeningJournal);
    }

    /// <summary>
    /// Documentos transaccionales ya emitidos/confirmados (los borradores no cuentan) o actividad
    /// operativa (caja, Kardex fuera del saldo inicial). Las cargas iniciales no son operación.
    /// </summary>
    private async Task<bool> HasRealOperationsAsync(CancellationToken ct) =>
        await _db.SalesInvoices.AsNoTracking().AnyAsync(x => x.Status != SalesInvoiceStatus.Draft, ct)
        || await _db.SalesReturns.AsNoTracking().AnyAsync(x => x.Status != SalesReturnStatus.Draft, ct)
        || await _db.PurchaseInvoices.AsNoTracking().AnyAsync(x => x.Status != PurchaseStatus.Draft, ct)
        || await _db.PurchaseReturns.AsNoTracking().AnyAsync(x => x.Status != PurchaseReturnStatus.Draft, ct)
        || await _db.PurchaseCreditNotes.AsNoTracking().AnyAsync(x => x.Status != PurchaseCreditNoteStatus.Draft, ct)
        || await _db.ExpenseDocuments.AsNoTracking().AnyAsync(x => x.Status != ExpenseStatus.Draft, ct)
        || await _db.Payments.AsNoTracking().AnyAsync(x => x.Status != PaymentStatus.Draft, ct)
        || await _db.SupplierPayments.AsNoTracking().AnyAsync(ct)
        || await _db.CashSessions.AsNoTracking().AnyAsync(ct)
        || await _db.StockMovements.AsNoTracking().AnyAsync(m => m.MovementType != StockMovementType.InitialBalance, ct);
}
