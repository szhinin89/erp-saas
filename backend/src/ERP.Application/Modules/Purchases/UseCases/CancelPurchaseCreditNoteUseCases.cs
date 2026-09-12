using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using FluentValidation;
using MediatR;
using System.Security.Cryptography;
using System.Text;

namespace ERP.Application.Modules.Purchases.UseCases;

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// FLOW-READY-02C.2 — cancela una <c>PurchaseCreditNote</c> en <c>Draft</c> (sin reversas) o
/// <c>Authorized</c> (reversa de <c>PurchasePayable.CreditNoteAppliedAmount</c> vía
/// <see cref="Domain.Modules.Purchases.Entities.PurchasePayable.ReverseCreditNote"/>). Nunca mueve
/// inventario ni toca contabilidad. Bajo Lock A (mismo mecanismo reutilizado que
/// <see cref="AuthorizePurchaseCreditNoteHandler"/>). Idempotente por <c>ClientRequestId</c>.
/// </summary>
/// <remarks>
/// PURCHASE-CREDIT-NOTE-DISCOUNT-CANCEL-RELEASES-RECEPTION-01 — si la NC estaba <c>Authorized</c>
/// y tenía un <c>ReceptionDocumentId</c> vinculado, ese <c>PurchaseReceptionDocument</c> ya fue
/// marcado <c>Processed</c> al autorizar (<see cref="AuthorizePurchaseCreditNoteHandler"/>). Al
/// cancelar, se revierte con <c>PurchaseReceptionDocument.UnmarkProcessed</c> (vuelve a
/// <c>Verified</c>, limpia <c>PurchaseId</c>) — el documento queda libre para "Procesar NC" de
/// nuevo, sin borrar el XML ni la NC cancelada (que permanece como historial). Este método YA
/// existía (agregado por PURCHASE-RECEPTION-CREDIT-NOTE-CANCELLED-REPROCESS-01 para el camino
/// Return, invocado desde <c>CancelPurchaseReturnUseCases</c>) — este handler es el único que
/// cancela una NC tipo <c>Discount</c> directamente (el tipo <c>Return</c> ya vinculado a un
/// <c>PurchaseReturn</c> se rechaza más abajo, redirigiendo a cancelar la devolución), así que antes
/// de esta fase el camino Discount nunca liberaba su documento de recepción. Draft nunca llega a
/// marcar <c>Processed</c> (eso solo ocurre en Authorize), así que solo se revierte cuando
/// <c>wasAuthorized</c> es verdadero — cancelar un Draft no necesita ni puede revertir nada.
/// </remarks>
public sealed record CancelPurchaseCreditNoteCommand(
    Guid PurchaseCreditNoteId,
    string Reason,
    Guid ClientRequestId
) : IRequest<Result<PurchaseCreditNoteDto>>, IBranchScopedRequest;

// ── Validator ───────────────────────────────────────────────────────────

public sealed class CancelPurchaseCreditNoteValidator
    : AbstractValidator<CancelPurchaseCreditNoteCommand>
{
    public CancelPurchaseCreditNoteValidator()
    {
        RuleFor(x => x.PurchaseCreditNoteId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo de la cancelación es obligatorio.");
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class CancelPurchaseCreditNoteHandler
    : IRequestHandler<CancelPurchaseCreditNoteCommand, Result<PurchaseCreditNoteDto>>
{
    private readonly IPurchaseCreditNoteRepository _creditNoteRepo;
    private readonly IPurchaseInvoiceRepository _invoiceRepo;
    private readonly IAccountsPayableRepository _payableRepo;
    private readonly IPurchaseReturnRepository _lockRepo;
    private readonly IPurchaseReceptionDocumentRepository _receptionRepo;
    private readonly IUnitOfWork _uow;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public CancelPurchaseCreditNoteHandler(
        IPurchaseCreditNoteRepository creditNoteRepo,
        IPurchaseInvoiceRepository invoiceRepo,
        IAccountsPayableRepository payableRepo,
        IPurchaseReturnRepository lockRepo,
        IPurchaseReceptionDocumentRepository receptionRepo,
        IUnitOfWork uow,
        IDatabaseExceptionTranslator dbEx,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _creditNoteRepo = creditNoteRepo;
        _invoiceRepo = invoiceRepo;
        _payableRepo = payableRepo;
        _lockRepo = lockRepo;
        _receptionRepo = receptionRepo;
        _uow = uow;
        _dbEx = dbEx;
        _t = t;
        _u = u;
    }

    public async Task<Result<PurchaseCreditNoteDto>> Handle(
        CancelPurchaseCreditNoteCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var uid = _u.UserId;

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var purchaseInvoiceId = await _creditNoteRepo.GetPurchaseInvoiceIdAsync(
                tid,
                cmd.PurchaseCreditNoteId,
                ct
            );
            if (purchaseInvoiceId is null)
            {
                await _uow.RollbackAsync(ct);
                return Result<PurchaseCreditNoteDto>.NotFound("Nota de crédito no encontrada.");
            }

            await _lockRepo.AcquireFinancialLockAsync(tid, purchaseInvoiceId.Value, ct);

            var creditNote = await _creditNoteRepo.GetByIdAsync(tid, cmd.PurchaseCreditNoteId, ct);
            if (creditNote is null)
            {
                await _uow.RollbackAsync(ct);
                return Result<PurchaseCreditNoteDto>.NotFound("Nota de crédito no encontrada.");
            }

            // Idempotencia verificada ANTES de las revalidaciones de negocio — mismo criterio que
            // CancelPurchaseReturnHandler.
            if (creditNote.Status == PurchaseCreditNoteStatus.Cancelled)
            {
                await _uow.RollbackAsync(ct);
                if (creditNote.CancelClientRequestId != cmd.ClientRequestId)
                    return Result<PurchaseCreditNoteDto>.ValidationFailure(
                        "Esta nota de crédito ya está cancelada."
                    );

                var expectedHash = ComputeCancelPayloadHash(
                    creditNote.Id,
                    cmd.ClientRequestId,
                    cmd.Reason
                );
                return creditNote.CancelRequestPayloadHash == expectedHash
                    ? Result<PurchaseCreditNoteDto>.Success(CreditNoteMap.ToDto(creditNote))
                    : Result<PurchaseCreditNoteDto>.ValidationFailure(
                        "Ya existe una solicitud de cancelación con este identificador pero con datos distintos."
                    );
            }

            if (creditNote.ApplicationType == PurchaseCreditNoteApplicationType.Return && creditNote.LinkedPurchaseReturnId is not null)
            {
                await _uow.RollbackAsync(ct);
                return Result<PurchaseCreditNoteDto>.ValidationFailure("Cancele la devolución vinculada para revertir la NC y sus movimientos.");
            }

            var wasAuthorized = creditNote.Status == PurchaseCreditNoteStatus.Authorized;

            if (wasAuthorized)
            {
                var invoice = await _invoiceRepo.GetByIdAsync(
                    tid,
                    creditNote.PurchaseInvoiceId,
                    ct
                );
                if (invoice is null)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<PurchaseCreditNoteDto>.NotFound(
                        "Factura de compra de origen no encontrada."
                    );
                }

                var payable = await _payableRepo.GetByOriginAsync(
                    tid,
                    invoice.CompanyId,
                    AccountsPayableOriginType.PurchaseInvoice,
                    invoice.Id,
                    ct
                );
                if (payable is null)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<PurchaseCreditNoteDto>.NotFound(
                        "Cuenta por pagar de la factura de compra no encontrada."
                    );
                }

                if (creditNote.AppliedToPayableAmount is > 0m)
                    payable.ReverseCreditNote(creditNote.AppliedToPayableAmount.Value, uid);

                // PURCHASE-CREDIT-NOTE-DISCOUNT-CANCEL-RELEASES-RECEPTION-01 — libera el documento
                // de recepción vinculado (si existe) para que "Procesar NC" pueda volver a crear una
                // NC nueva y limpia; nunca reutiliza ni borra la NC/XML cancelados (quedan como
                // historial). Mismo mecanismo ya usado por CancelPurchaseReturnUseCases para el
                // camino Return.
                if (creditNote.ReceptionDocumentId is { } receptionDocumentId)
                {
                    var receptionDoc = await _receptionRepo.GetByIdAsync(tid, receptionDocumentId, ct);
                    receptionDoc?.UnmarkProcessed(uid);
                }
            }

            var cancelHash = ComputeCancelPayloadHash(creditNote.Id, cmd.ClientRequestId, cmd.Reason);
            try
            {
                creditNote.Cancel(cmd.Reason, uid, cmd.ClientRequestId, cancelHash);
            }
            catch (InvalidOperationException ex)
            {
                await _uow.RollbackAsync(ct);
                return Result<PurchaseCreditNoteDto>.ValidationFailure(ex.Message);
            }

            try
            {
                await _uow.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
            {
                await _uow.RollbackAsync(ct);
                _uow.ClearChangeTracker();

                await _uow.BeginTransactionAsync(ct);
                try
                {
                    var reloaded = await _creditNoteRepo.GetByIdAsync(
                        tid,
                        cmd.PurchaseCreditNoteId,
                        ct
                    );
                    await _uow.CommitAsync(ct); // solo lectura, nada que persistir

                    if (
                        reloaded is not null
                        && reloaded.Status == PurchaseCreditNoteStatus.Cancelled
                        && reloaded.CancelClientRequestId == cmd.ClientRequestId
                    )
                        return Result<PurchaseCreditNoteDto>.Success(CreditNoteMap.ToDto(reloaded));

                    return Result<PurchaseCreditNoteDto>.ValidationFailure(
                        "Ya existe una solicitud de cancelación con este identificador pero con datos distintos."
                    );
                }
                catch
                {
                    await _uow.RollbackAsync(ct);
                    throw;
                }
            }

            await _uow.CommitAsync(ct);

            return Result<PurchaseCreditNoteDto>.Success(CreditNoteMap.ToDto(creditNote));
        }
        catch (InvalidOperationException ex)
        {
            await _uow.RollbackAsync(ct);
            return Result<PurchaseCreditNoteDto>.ValidationFailure(ex.Message);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    internal static string ComputeCancelPayloadHash(
        Guid purchaseCreditNoteId,
        Guid clientRequestId,
        string reason
    )
    {
        var normalizedReason = string.Join(
            " ",
            reason.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        );
        var canonical = string.Join(
            "",
            "CancelPurchaseCreditNote",
            purchaseCreditNoteId.ToString("D"),
            clientRequestId.ToString("D"),
            normalizedReason
        );
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }
}
