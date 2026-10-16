using ERP.Application.Common;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;

namespace ERP.Application.Modules.Purchases.Services;

/// <summary>Precondiciones operativas únicas, bajo lock financiero y antes de cualquier reverso.
/// El gate fiscal de la retención continúa en IRetentionCanceller (ADR-036).
/// No atribuye unidades a compras: cualquier salida posterior del recurso es irreversible para Cancel.
/// </summary>
public sealed class PurchaseCancellationEligibility(
    IStockRepository stock,
    IPurchaseReturnRepository returns)
{
    public async Task<Result<bool>> EvaluateAsync(PurchaseInvoice invoice, AccountsPayable? payable,
        CancellationToken ct)
    {
        if (invoice.Status != PurchaseStatus.Confirmed)
            return Result<bool>.ValidationFailure(invoice.Status == PurchaseStatus.Cancelled
                ? "Esta compra ya fue anulada."
                : "Solo se puede anular una compra confirmada. Utilice Descartar para un borrador.");
        if (payable is not null && payable.PaidAmount > 0)
            return Result<bool>.ValidationFailure(
                "No se puede anular una compra con pagos aplicados. Reverse los pagos primero.");
        if (await returns.ExistsAuthorizedByPurchaseInvoiceIdAsync(invoice.TenantId,
                invoice.CompanyId, invoice.Id, ct))
            return Result<bool>.ValidationFailure(
                "No se puede anular una compra que tiene una devolución de compra autorizada asociada. Cancele primero la devolución.");
        if (payable is not null && payable.SupplierCreditAmount > 0)
            return Result<bool>.ValidationFailure(
                "No se puede anular una compra con crédito de proveedor ya aplicado contra su cuenta por pagar.");
        if (payable is not null && (payable.CreditNoteAmount > 0 || payable.ReturnCreditAmount > 0))
            return Result<bool>.ValidationFailure(
                "No se puede anular una compra con devolución o nota de crédito aplicada. Utilice devolución o nota de crédito según corresponda.");

        var entries = (await stock.GetMovementsByDocumentAsync(invoice.TenantId, invoice.Id,
                "PurchaseInvoice", ct))
            .Where(m => m.CompanyId == invoice.CompanyId && m.MovementType == StockMovementType.PurchaseEntry)
            .GroupBy(m => (m.ProductId, m.WarehouseId))
            .OrderBy(g => g.Key.ProductId).ThenBy(g => g.Key.WarehouseId).ToArray();
        // Orden estable para compras con varios recursos. Locks retenidos hasta commit/rollback.
        foreach (var resource in entries)
            await stock.AcquireResourceLockAsync(invoice.TenantId, invoice.CompanyId,
                resource.Key.ProductId, resource.Key.WarehouseId, ct);
        foreach (var resource in entries)
            if (await stock.HasExitAfterAsync(invoice.TenantId, invoice.CompanyId,
                    resource.Key.ProductId, resource.Key.WarehouseId, resource.Min(m => m.SequenceNumber), ct))
                return Result<bool>.ValidationFailure(
                    "Existen movimientos posteriores de salida de inventario. Utilice devolución o nota de crédito según corresponda.",
                    ApiResponseCodes.Purchases.CancellationSubsequentInventoryExit);
        return Result<bool>.Success(true);
    }
}
