using ERP.Application.Common;
using ERP.Application.Modules.Purchases.DTOs;

namespace ERP.Application.Modules.Purchases.UseCases;

/// <summary>Motor interno de las notas de crédito por devolución. No es una operación independiente.</summary>
public interface IPurchaseCreditNoteReturnMotor
{
    Task<Result<PurchaseReturnDto>> AuthorizeAsync(Guid returnId, Guid requestId, CancellationToken ct);
    Task<Result<PurchaseReturnDto>> CancelAsync(Guid returnId, string reason, Guid requestId, CancellationToken ct);
}

internal sealed class PurchaseCreditNoteReturnMotor(
    AuthorizePurchaseReturnHandler authorize, CancelPurchaseReturnHandler cancel) : IPurchaseCreditNoteReturnMotor
{
    public Task<Result<PurchaseReturnDto>> AuthorizeAsync(Guid returnId, Guid requestId, CancellationToken ct) =>
        authorize.Handle(new AuthorizePurchaseReturnCommand(returnId, requestId), ct);

    public Task<Result<PurchaseReturnDto>> CancelAsync(Guid returnId, string reason, Guid requestId, CancellationToken ct) =>
        cancel.Handle(new CancelPurchaseReturnCommand(returnId, reason, requestId), ct);
}
