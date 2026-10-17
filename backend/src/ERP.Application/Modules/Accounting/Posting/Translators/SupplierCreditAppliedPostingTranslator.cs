using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Purchases.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce <see cref="SupplierCreditAppliedEvent"/> al hecho Purchases/SupplierCreditApplied
/// (Debe CxP destino, Haber saldo a favor) y lo registra como obligación durable en la transacción
/// de la aplicación (ACCOUNTING-CONTINUITY-04). Identidad: el movimiento de aplicación.
/// </summary>
public sealed class SupplierCreditAppliedPostingTranslator(
    AccountingPostingRecorder recorder,
    ICompanyClock companyClock
) : INotificationHandler<SupplierCreditAppliedEvent>
{
    public const string SourceModuleName = "Purchases";
    public const string FactTypeName = "SupplierCreditApplied";

    public async Task Handle(SupplierCreditAppliedEvent e, CancellationToken ct)
    {
        var tenantId = e.TenantId!.Value;
        var entryDate = await companyClock.TodayAsync(e.CompanyId, tenantId, ct);
        await recorder.RecordAsync(
            new AccountingPostingRequest(
                tenantId,
                e.CompanyId,
                SourceModuleName,
                FactTypeName,
                e.SupplierCreditMovementId,
                null,
                entryDate,
                entryDate,
                new AccountingPostingPayload(
                    0m,
                    0m,
                    0m,
                    0m,
                    e.Amount,
                    References: new Dictionary<string, Guid>
                    {
                        ["SupplierCreditId"] = e.SupplierCreditId,
                        ["TargetPurchasePayableId"] = e.TargetPurchasePayableId,
                    }
                )
            ),
            ct
        );
    }
}
