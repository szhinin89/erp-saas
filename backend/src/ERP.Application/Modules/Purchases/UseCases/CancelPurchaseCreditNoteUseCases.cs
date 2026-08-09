using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
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
/// DECISIÓN PENDIENTE DOCUMENTADA (regla obligatoria #6 del encargo): si la nota de crédito estaba
/// <c>Authorized</c> y tenía un <c>ReceptionDocumentId</c> vinculado, ese
/// <c>PurchaseReceptionDocument</c> ya fue marcado <c>Processed</c> al autorizar
/// (<see cref="AuthorizePurchaseCreditNoteHandler"/>). <c>PurchaseReceptionDocument</c> (entidad
/// FROZEN de Recepción) NO expone ningún método de reversa de <c>MarkProcessed</c> — su único
/// método de baja, <c>Cancel()</c>, lanza <c>InvalidOperationException</c> explícitamente si el
/// documento ya está <c>Processed</c> ("No se puede anular un documento ya procesado"). Este
/// handler, siguiendo la instrucción explícita de "no inventar sin auditoría", NO intenta revertir
/// ese estado — el documento de recepción permanece <c>Processed</c> incluso después de cancelar la
/// nota de crédito que lo procesó. Esto deja un documento de recepción "huérfano" (procesado, pero
/// cuya nota de crédito fue cancelada) sin forma de reprocesarlo por este módulo. Requiere una
/// decisión de negocio explícita en una fase futura (p. ej. un método
/// <c>PurchaseReceptionDocument.ReopenAfterCancelledProcessing</c>, con su propia auditoría/ADR) —
/// no se implementa aquí.
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
    private readonly IPurchaseReturnRepository _lockRepo;
    private readonly IUnitOfWork _uow;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public CancelPurchaseCreditNoteHandler(
        IPurchaseCreditNoteRepository creditNoteRepo,
        IPurchaseInvoiceRepository invoiceRepo,
        IPurchaseReturnRepository lockRepo,
        IUnitOfWork uow,
        IDatabaseExceptionTranslator dbEx,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _creditNoteRepo = creditNoteRepo;
        _invoiceRepo = invoiceRepo;
        _lockRepo = lockRepo;
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

                var payable = await _invoiceRepo.GetPayableByPurchaseIdAsync(tid, invoice.Id, ct);
                if (payable is null)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<PurchaseCreditNoteDto>.NotFound(
                        "Cuenta por pagar de la factura de compra no encontrada."
                    );
                }

                if (creditNote.AppliedToPayableAmount is > 0m)
                    payable.ReverseCreditNote(creditNote.AppliedToPayableAmount.Value, uid);

                // Ver DECISIÓN PENDIENTE DOCUMENTADA en el comentario XML de este archivo — el
                // PurchaseReceptionDocument vinculado (si existe) NO se revierte: no existe método
                // de reversa de MarkProcessed en esa entidad FROZEN y no se inventa uno aquí.
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
