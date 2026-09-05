using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Modules.Accounting.Enums;
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
/// FLOW-READY-02C.2 — autoriza una <c>PurchaseCreditNote</c> (descuento/promoción) en <c>Draft</c>:
/// bajo Lock A (reutiliza <see cref="IPurchaseReturnRepository.AcquireFinancialLockAsync"/>, mismo
/// namespace "PurchaseInvoice.FinancialLock" que ya usa <c>ApplySupplierCreditUseCases</c> —
/// precedente de reutilización cross-módulo del mismo advisory lock), aplica
/// <c>PurchasePayable.ApplyCreditNote</c> (bloquea si excede el saldo — nunca trunca ni genera
/// <c>SupplierCredit</c>, ajuste obligatorio del diseño §4.2), congela el snapshot financiero y, si
/// hay <c>ReceptionDocumentId</c>, marca el documento de recepción como procesado. Nunca mueve
/// inventario ni genera <c>PostingFact</c>. Idempotente por <c>ClientRequestId</c>.
/// </summary>
public sealed record AuthorizePurchaseCreditNoteCommand(
    Guid PurchaseCreditNoteId,
    Guid ClientRequestId
) : IRequest<Result<PurchaseCreditNoteDto>>, IBranchScopedRequest;

// ── Validator ───────────────────────────────────────────────────────────

public sealed class AuthorizePurchaseCreditNoteValidator
    : AbstractValidator<AuthorizePurchaseCreditNoteCommand>
{
    public AuthorizePurchaseCreditNoteValidator()
    {
        RuleFor(x => x.PurchaseCreditNoteId).NotEmpty();
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class AuthorizePurchaseCreditNoteHandler
    : IRequestHandler<AuthorizePurchaseCreditNoteCommand, Result<PurchaseCreditNoteDto>>
{
    private readonly IPurchaseCreditNoteRepository _creditNoteRepo;
    private readonly IPurchaseInvoiceRepository _invoiceRepo;
    private readonly IAccountsPayableRepository _payableRepo;
    private readonly IPurchaseReturnRepository _lockRepo;
    private readonly IPurchaseReceptionDocumentRepository _receptionRepo;
    private readonly IUnitOfWork _uow;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly IPostingEngine _postingEngine;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;

    public AuthorizePurchaseCreditNoteHandler(
        IPurchaseCreditNoteRepository creditNoteRepo,
        IPurchaseInvoiceRepository invoiceRepo,
        IAccountsPayableRepository payableRepo,
        IPurchaseReturnRepository lockRepo,
        IPurchaseReceptionDocumentRepository receptionRepo,
        IUnitOfWork uow,
        IDatabaseExceptionTranslator dbEx,
        IPostingEngine postingEngine,
        ICurrentTenant t,
        ICurrentBranch b,
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
        _postingEngine = postingEngine;
        _t = t;
        _b = b;
        _u = u;
    }

    public async Task<Result<PurchaseCreditNoteDto>> Handle(
        AuthorizePurchaseCreditNoteCommand cmd,
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
            // AuthorizePurchaseReturnHandler: un reintento legítimo retorna el snapshot confirmado
            // sin reejecutar el efecto ni ser rechazado por EnsureDraft().
            if (creditNote.Status == PurchaseCreditNoteStatus.Authorized)
            {
                await _uow.RollbackAsync(ct);
                return creditNote.AuthorizeClientRequestId == cmd.ClientRequestId
                    ? Result<PurchaseCreditNoteDto>.Success(CreditNoteMap.ToDto(creditNote))
                    : Result<PurchaseCreditNoteDto>.ValidationFailure(
                        "Ya existe una solicitud de autorización con este identificador pero con datos distintos."
                    );
            }
            if (creditNote.Status == PurchaseCreditNoteStatus.Cancelled)
            {
                await _uow.RollbackAsync(ct);
                return Result<PurchaseCreditNoteDto>.ValidationFailure(
                    "Esta nota de crédito ya fue cancelada."
                );
            }

            var invoice = await _invoiceRepo.GetByIdAsync(
                tid,
                creditNote.PurchaseInvoiceId,
                ct
            );
            if (invoice is null)
            {
                await _uow.RollbackAsync(ct);
                return Result<PurchaseCreditNoteDto>.NotFound("Factura de compra no encontrada.");
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

            var balanceDueBeforeApplication = payable.OutstandingAmount;
            var authorizeHash = ComputeAuthorizePayloadHash(
                creditNote.Id,
                cmd.ClientRequestId
            );

            // ── Guard IRBPNR (TAX-LINE-SSOT-ICE-IRBPNR-01 Fase 5E) — mismo criterio que
            // ConfirmPurchaseUseCases STEP 0 / AuthorizePurchaseReturnHandler: precondición antes
            // de Authorize(), el posting nunca revierte una autorización ya persistida. ──
            if (creditNote.IrbpnrAmount > 0)
            {
                var irbpnrConfigured = await _postingEngine.IsAmountKindConfiguredAsync(
                    tid,
                    creditNote.CompanyId,
                    "Purchases",
                    "PurchaseCreditNoteAuthorized",
                    PostingAmountKind.TaxIrbpnr,
                    ct
                );
                if (!irbpnrConfigured)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<PurchaseCreditNoteDto>.ValidationFailure(
                        "Esta nota de crédito contiene IRBPNR (impuesto código 5), pero no existe una regla de "
                            + "contabilización (PostingRuleLine) configurada para IRBPNR en Notas de Crédito de Compra. "
                            + "Configure la cuenta contable correspondiente antes de autorizar."
                    );
                }
            }

            try
            {
                // Ajuste obligatorio §4.2 del diseño: bloquea si TotalAmount > BalanceDue — nunca
                // trunca ni genera SupplierCredit. El dominio ya valida esto en Authorize().
                creditNote.Authorize(
                    balanceDueBeforeApplication,
                    uid,
                    cmd.ClientRequestId,
                    authorizeHash
                );
            }
            catch (InvalidOperationException ex)
            {
                await _uow.RollbackAsync(ct);
                return Result<PurchaseCreditNoteDto>.ValidationFailure(ex.Message);
            }

            payable.ApplyCreditNote(creditNote.AppliedToPayableAmount!.Value, uid);

            if (creditNote.ReceptionDocumentId is { } receptionDocumentId)
            {
                var receptionDoc = await _receptionRepo.GetByIdAsync(
                    tid,
                    receptionDocumentId,
                    ct
                );
                if (receptionDoc is null)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<PurchaseCreditNoteDto>.NotFound(
                        "Documento de recepción (Nota de Crédito) no encontrado."
                    );
                }

                try
                {
                    receptionDoc.MarkProcessed(invoice.Id, uid);
                }
                catch (InvalidOperationException ex)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<PurchaseCreditNoteDto>.ValidationFailure(
                        $"No se pudo marcar el documento de recepción como procesado: {ex.Message}"
                    );
                }
            }

            try
            {
                await _uow.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
            {
                // §16.2bis — colisión de AuthorizeClientRequestId por una causa distinta al lock,
                // mismo patrón exacto que AuthorizePurchaseReturnHandler.
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
                        && reloaded.Status == PurchaseCreditNoteStatus.Authorized
                        && reloaded.AuthorizeClientRequestId == cmd.ClientRequestId
                    )
                        return Result<PurchaseCreditNoteDto>.Success(CreditNoteMap.ToDto(reloaded));

                    return Result<PurchaseCreditNoteDto>.ValidationFailure(
                        "Ya existe una solicitud de autorización con este identificador pero con datos distintos."
                    );
                }
                catch
                {
                    await _uow.RollbackAsync(ct);
                    throw;
                }
            }

            await _uow.CommitAsync(ct);

            return Result<PurchaseCreditNoteDto>.Success(
                CreditNoteMap.ToDto(creditNote, invoice.InvoiceNumber, invoice.SupplierName, payable.OutstandingAmount)
            );
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

    internal static string ComputeAuthorizePayloadHash(
        Guid purchaseCreditNoteId,
        Guid clientRequestId
    )
    {
        var canonical = string.Join(
            "",
            "AuthorizePurchaseCreditNote",
            purchaseCreditNoteId.ToString("D"),
            clientRequestId.ToString("D")
        );
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }
}
