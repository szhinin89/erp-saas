using ERP.Application.Common;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;

namespace ERP.Application.Modules.Purchases.Services;

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01 — finaliza la anulación de una COMPRA cuya retención el SRI confirmó
/// ANULADO, por el flujo oficial existente (<see cref="CancelPurchaseHandler.ExecuteAsync"/>: Lock A,
/// guards, reverso de Kardex, CxP, retención y asientos — nada se duplica aquí), con el motivo y el
/// usuario registrados en la solicitud. Una compra ya anulada se reporta como tal (idempotencia).
/// </summary>
public sealed class PurchaseRetentionOriginCancellation : IRetentionOriginCancellation
{
    private readonly CancelPurchaseHandler _cancelPurchase;
    private readonly IPurchaseInvoiceRepository _purchases;

    public PurchaseRetentionOriginCancellation(
        CancelPurchaseHandler cancelPurchase,
        IPurchaseInvoiceRepository purchases
    )
    {
        _cancelPurchase = cancelPurchase;
        _purchases = purchases;
    }

    public RetentionSourceDocumentType SourceType => RetentionSourceDocumentType.PurchaseInvoice;

    public async Task<Result<RetentionOriginCancellationOutcome>> CancelAsync(
        RetentionAnnulmentRequest request,
        CancellationToken ct = default
    )
    {
        var purchase = await _purchases.GetByIdAsync(request.TenantId, request.SourceDocumentId, ct);
        if (purchase is null || purchase.CompanyId != request.CompanyId)
            return Result<RetentionOriginCancellationOutcome>.NotFound("La compra de la retención no existe.");
        if (purchase.Status == PurchaseStatus.Cancelled)
            return Result<RetentionOriginCancellationOutcome>.Success(RetentionOriginCancellationOutcome.AlreadyCancelled);

        var result = await _cancelPurchase.ExecuteAsync(
            new PurchaseCancellationContext(
                request.TenantId,
                request.CompanyId,
                request.SourceDocumentId,
                request.Reason,
                request.RequestedBy,
                RequiredBranchId: null,
                RequestSriAnnulment: false
            ),
            ct
        );
        return result.IsSuccess
            ? Result<RetentionOriginCancellationOutcome>.Success(RetentionOriginCancellationOutcome.Cancelled)
            : Result<RetentionOriginCancellationOutcome>.Failure(result.Error!, result.Code);
    }
}
