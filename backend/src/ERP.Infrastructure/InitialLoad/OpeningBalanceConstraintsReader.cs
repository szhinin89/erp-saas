using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.InitialLoad;

/// <inheritdoc cref="IOpeningBalanceConstraintsReader"/>
public sealed class OpeningBalanceConstraintsReader : IOpeningBalanceConstraintsReader
{
    private readonly ErpDbContext _db;

    public OpeningBalanceConstraintsReader(ErpDbContext db) => _db = db;

    public async Task<OpeningBalanceDateConstraints> GetAsync(
        DateOnly? currentOpeningBalanceDate, CancellationToken ct)
    {
        var confirmed = await _db.StockMovements.AsNoTracking()
            .Where(m => m.MovementType == StockMovementType.InitialBalance)
            .Select(m => m.EffectiveDate)
            .Distinct()
            .ToListAsync(ct);
        // Los saldos iniciales de CxC no guardan su corte: se confirman con la fecha vigente.
        if (currentOpeningBalanceDate is { } current && !confirmed.Contains(current)
            && await _db.SalesReceivables.AsNoTracking().AnyAsync(r => r.Origin == SalesReceivableOrigin.InitialBalance, ct))
            confirmed.Add(current);

        return new OpeningBalanceDateConstraints(await HasRealOperationsAsync(ct), confirmed);
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
