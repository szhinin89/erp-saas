using System.Security.Cryptography;
using System.Text;
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

namespace ERP.Application.Modules.Purchases.UseCases;

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// Cancelación interna desde Nota de crédito. Return utiliza su motor para las reversas;
/// Discount revierte su ajuste. No acredita anulación SRI ni garantiza la publicación
/// de un reverso contable pendiente. SSOT: purchases.md.
/// </summary>
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
    private readonly IPurchaseCreditNoteReturnMotor? _returnMotor;

    public CancelPurchaseCreditNoteHandler(
        IPurchaseCreditNoteRepository creditNoteRepo,
        IPurchaseInvoiceRepository invoiceRepo,
        IAccountsPayableRepository payableRepo,
        IPurchaseReturnRepository lockRepo,
        IPurchaseReceptionDocumentRepository receptionRepo,
        IUnitOfWork uow,
        IDatabaseExceptionTranslator dbEx,
        ICurrentTenant t,
        ICurrentUser u,
        IPurchaseCreditNoteReturnMotor? returnMotor = null
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
        _returnMotor = returnMotor;
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

                var expectedHash = creditNote.ApplicationType == PurchaseCreditNoteApplicationType.Return
                    ? CancelPurchaseReturnHandler.ComputeCancelPayloadHash(creditNote.LinkedPurchaseReturnId!.Value, cmd.ClientRequestId, cmd.Reason)
                    : ComputeCancelPayloadHash(
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

            if (
                creditNote.ApplicationType == PurchaseCreditNoteApplicationType.Return
                && creditNote.LinkedPurchaseReturnId is not null
            )
            {
                await _uow.RollbackAsync(ct);
                _uow.ClearChangeTracker();
                var cancelled = await _returnMotor!.CancelAsync(
                    creditNote.LinkedPurchaseReturnId.Value, cmd.Reason, cmd.ClientRequestId, ct);
                if (!cancelled.IsSuccess)
                    return Result<PurchaseCreditNoteDto>.Failure(cancelled.Error!, cancelled.Code);
                return Result<PurchaseCreditNoteDto>.Success(CreditNoteMap.ToDto(
                    (await _creditNoteRepo.GetByIdAsync(tid, cmd.PurchaseCreditNoteId, ct))!));
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
                    var receptionDoc = await _receptionRepo.GetByIdAsync(
                        tid,
                        receptionDocumentId,
                        ct
                    );
                    receptionDoc?.UnmarkProcessed(uid);
                }
            }

            var cancelHash = ComputeCancelPayloadHash(
                creditNote.Id,
                cmd.ClientRequestId,
                cmd.Reason
            );
            creditNote.Cancel(cmd.Reason, uid, cmd.ClientRequestId, cancelHash);

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
