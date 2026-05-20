using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Aplica cambios hechos sobre agregados legacy (desacoplados) a las tablas unificadas antes de SaveChanges.
/// </summary>
public interface IUnifiedDocumentSync
{
    void StageSalesBill(SalesBill bill);
    void StageSalesNote(SalesNote note);
    void StagePurchBill(PurchBill bill);
    void StagePurchNote(PurchNote note);
    void StageExpenseInvoice(ExpenseInvoice invoice);
    void StageSalesRetention(SalesRetention retention);
    void StageIssuedRetention(IssuedRetention retention);
    Task FlushAsync(CancellationToken ct = default);
}
