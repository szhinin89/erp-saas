using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Companies;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Events;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// ACCOUNTING-12A — registra la obligación del ajuste de inventario en la transacción operativa.
/// Importe = TotalCost real de los movimientos Kardex del documento (leídos ya persistidos, después del
/// reintento de secuencia), redondeado con la precisión contable; un movimiento sin costo conocido no
/// movió el valor del Kardex y no aporta importe. Fecha = fecha efectiva de esos movimientos (la de la
/// ejecución, nunca la de creación del borrador): Kardex y asiento comparten fecha y el gate Closed/Locked
/// del recorder se evalúa contra ella. La contrapartida es la cuenta del motivo para la Company del hecho,
/// resuelta en cada intento (sin configuración → Failed, el ajuste se conserva).
/// </summary>
public sealed class StockAdjustmentExecutedPostingTranslator(
    AccountingPostingRecorder recorder,
    IStockRepository stock,
    ICompanyPrecisionPolicyProvider precision)
    : INotificationHandler<StockAdjustmentExecutedEvent>
{
    public const string SourceModuleName = "Inventory";
    public const string SourceDocType = "StockAdjustment";
    public const string InFactType = "StockAdjustmentIn";
    public const string OutFactType = "StockAdjustmentOut";

    public static string FactTypeFor(string movementType) =>
        movementType == StockAdjustment.MovementTypeIngreso ? InFactType : OutFactType;

    public async Task Handle(StockAdjustmentExecutedEvent e, CancellationToken ct)
    {
        var tenantId = e.TenantId!.Value;
        var isIn = e.MovementType == StockAdjustment.MovementTypeIngreso;
        var movementType = isIn ? StockMovementType.PositiveAdjust : StockMovementType.NegativeAdjust;
        var movements = (await stock.GetMovementsByDocumentAsync(tenantId, e.StockAdjustmentId, SourceDocType, ct))
            .Where(m => m.MovementType == movementType)
            .ToList();
        var decimals = (await precision.GetEffectiveAsync(ct)).AccountingDecimals;
        var amount = Math.Round(movements.Sum(m => m.TotalCost ?? 0m), decimals, MidpointRounding.AwayFromZero);
        if (amount <= 0m)
            return; // Sin valor en el Kardex: no hay efecto contable que exigir.
        // Todas las líneas se registran en la misma ejecución con la misma fecha efectiva del Kardex.
        var effectiveDate = movements.Select(m => m.EffectiveDate).Distinct().Single();

        await recorder.RecordAsync(
            new AccountingPostingRequest(
                tenantId,
                e.CompanyId,
                SourceModuleName,
                FactTypeFor(e.MovementType),
                e.StockAdjustmentId,
                e.AdjustmentNumber,
                effectiveDate,
                effectiveDate,
                new AccountingPostingPayload(
                    Subtotal: amount,
                    TotalVat: 0m,
                    TotalIce: 0m,
                    TotalDiscount: 0m,
                    GrandTotal: amount,
                    Destinations:
                    [
                        new AccountingPostingDestination(
                            AccountingPostingDestination.InventoryAdjustmentReason,
                            e.ReasonId,
                            amount,
                            isIn ? AccountNature.Credit : AccountNature.Debit,
                            Description: e.AdjustmentNumber
                        ),
                    ]
                ),
                e.StockAdjustmentId
            ),
            ct
        );
    }
}

/// <summary>ACCOUNTING-12A — la anulación espeja el asiento publicado o cancela la obligación pendiente.</summary>
public sealed class StockAdjustmentCancelledPostingTranslator(IAccountingReversalRecorder reversals)
    : INotificationHandler<StockAdjustmentCancelledEvent>
{
    public Task Handle(StockAdjustmentCancelledEvent e, CancellationToken ct) =>
        reversals.RecordSourceAsync(e.TenantId!.Value, e.CompanyId,
            StockAdjustmentExecutedPostingTranslator.SourceModuleName,
            StockAdjustmentExecutedPostingTranslator.FactTypeFor(e.MovementType),
            e.StockAdjustmentId, e.CancelReason, e.OccurredOn, ct);
}
