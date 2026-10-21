using System.Security.Cryptography;
using System.Text;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Exceptions;
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
/// Autoriza la NC desde la entrada única. Discount aplica su ajuste financiero; Return utiliza
/// el motor interno o completa exclusivamente una vinculación histórica. SSOT: purchases.md.
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
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;
    private readonly IPurchaseCreditNoteReturnMotor? _returnMotor;
    private readonly PurchaseCreditNoteReprocessingGuard? _reprocessing;

    public AuthorizePurchaseCreditNoteHandler(
        IPurchaseCreditNoteRepository creditNoteRepo,
        IPurchaseInvoiceRepository invoiceRepo,
        IAccountsPayableRepository payableRepo,
        IPurchaseReturnRepository lockRepo,
        IPurchaseReceptionDocumentRepository receptionRepo,
        IUnitOfWork uow,
        IDatabaseExceptionTranslator dbEx,
        ICurrentTenant t,
        ICurrentBranch b,
        ICurrentUser u,
        IPurchaseCreditNoteReturnMotor? returnMotor = null,
        PurchaseCreditNoteReprocessingGuard? reprocessing = null
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
        _b = b;
        _u = u;
        _returnMotor = returnMotor;
        _reprocessing = reprocessing;
    }

    public async Task<Result<PurchaseCreditNoteDto>> Handle(
        AuthorizePurchaseCreditNoteCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var uid = _u.UserId;

        if (_reprocessing is not null)
        {
            var candidate = await _creditNoteRepo.GetByIdAsync(tid, cmd.PurchaseCreditNoteId, ct);
            if (candidate is null || candidate.BranchId != _b.BranchId)
                return Result<PurchaseCreditNoteDto>.NotFound("Nota de crédito no encontrada.");
            if (candidate.Status == PurchaseCreditNoteStatus.Draft)
            {
                var blocker = await _reprocessing.CheckAsync(tid, candidate.CompanyId, candidate.SupplierId,
                    candidate.CreditNoteNumber, candidate.AccessKey, candidate.ReceptionDocumentId, ct);
                if (blocker is not null) return Result<PurchaseCreditNoteDto>.ValidationFailure(blocker);
            }
            // La consulta SRI se realiza fuera de transacción; recargar después del lock financiero.
            _uow.ClearChangeTracker();
        }
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
            if (creditNote is null || creditNote.BranchId != _b.BranchId)
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

            if (creditNote.ApplicationType == PurchaseCreditNoteApplicationType.Return)
            {
                if (creditNote.LinkedPurchaseReturnId is not { } returnId)
                    throw new DomainRuleViolationException("La NC por devolución requiere su motor interno vinculado.");
                var purchaseReturn = await _lockRepo.GetByIdAsync(tid, returnId, ct);
                if (purchaseReturn is null || purchaseReturn.BranchId != _b.BranchId
                    || purchaseReturn.CompanyId != creditNote.CompanyId
                    || purchaseReturn.PurchaseInvoiceId != creditNote.PurchaseInvoiceId
                    || purchaseReturn.SupplierId != creditNote.SupplierId)
                    throw new DomainRuleViolationException("La devolución vinculada no corresponde a esta NC.");
                if (purchaseReturn.Status == PurchaseReturnStatus.Draft)
                {
                    // El motor existente toma el lock y controla una única transacción completa.
                    await _uow.RollbackAsync(ct);
                    _uow.ClearChangeTracker();
                    var applied = await _returnMotor!.AuthorizeAsync(returnId, cmd.ClientRequestId, ct);
                    if (!applied.IsSuccess)
                        return Result<PurchaseCreditNoteDto>.Failure(applied.Error!, applied.Code);
                    return Result<PurchaseCreditNoteDto>.Success(CreditNoteMap.ToDto(
                        (await _creditNoteRepo.GetByIdAsync(tid, cmd.PurchaseCreditNoteId, ct))!));
                }

                // Históricos: solo completar el vínculo fiscal, sin llamar al motor financiero.
                if (purchaseReturn.Status != PurchaseReturnStatus.Authorized
                    || purchaseReturn.FiscalStatus != PurchaseReturnFiscalStatus.PendingSupplierCreditNote
                    || purchaseReturn.SupplierCreditNoteDocumentId is not null)
                    throw new DomainRuleViolationException("La devolución histórica ya no está autorizada y pendiente de NC.");
                var sourceInvoice = await _invoiceRepo.GetByIdAsync(tid, creditNote.PurchaseInvoiceId, ct);
                if (sourceInvoice is null)
                    throw new DomainRuleViolationException("Factura de compra no encontrada.");
                if (creditNote.IssueDate < sourceInvoice.IssueDate)
                    throw new DomainRuleViolationException("La NC no puede ser anterior a la factura afectada.");
                if (creditNote.ReceptionDocumentId is { } historicalReceptionId)
                {
                    var reception = await _receptionRepo.GetByIdAsync(tid, historicalReceptionId, ct);
                    if (reception is null || reception.CompanyId != creditNote.CompanyId
                        || reception.BranchId != creditNote.BranchId
                        || reception.SourceDocType != Domain.Modules.Purchases.PurchaseReception.Enums.PurchaseReceptionSourceDocType.CreditNote
                        || reception.Status != Domain.Modules.Purchases.PurchaseReception.Enums.PurchaseReceptionDocumentStatus.Verified
                        || reception.PurchaseId is not null || reception.AccessKey != creditNote.AccessKey
                        || reception.TotalAmount != creditNote.TotalAmount
                        || !string.Equals(reception.SupplierRuc.Trim(), sourceInvoice.SupplierTaxId.Trim(), StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(reception.CurrencyCode, sourceInvoice.CurrencyCode, StringComparison.OrdinalIgnoreCase)
                        || (!string.IsNullOrWhiteSpace(reception.ModifiedDocumentNumber)
                            && !string.Equals(reception.ModifiedDocumentNumber.Trim(), sourceInvoice.InvoiceNumber.Trim(), StringComparison.OrdinalIgnoreCase)))
                        throw new DomainRuleViolationException("El documento recibido no es compatible con la devolución histórica.");
                    reception.MarkProcessed(sourceInvoice.Id, uid);
                }
                creditNote.CompleteLinkedReturn(purchaseReturn, uid, cmd.ClientRequestId,
                    ComputeAuthorizePayloadHash(creditNote.Id, cmd.ClientRequestId));
                purchaseReturn.RegisterLinkedCreditNote(creditNote, uid);
                await _uow.SaveChangesAsync(ct);
                await _uow.CommitAsync(ct);
                return Result<PurchaseCreditNoteDto>.Success(CreditNoteMap.ToDto(creditNote));
            }

            var invoice = await _invoiceRepo.GetByIdAsync(tid, creditNote.PurchaseInvoiceId, ct);
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
            var authorizeHash = ComputeAuthorizePayloadHash(creditNote.Id, cmd.ClientRequestId);

            // Ajuste obligatorio §4.2 del diseño: bloquea si TotalAmount > BalanceDue — nunca
            // trunca ni genera SupplierCredit. El dominio ya valida esto en Authorize().
            creditNote.Authorize(
                balanceDueBeforeApplication,
                uid,
                cmd.ClientRequestId,
                authorizeHash
            );

            payable.ApplyCreditNote(creditNote.AppliedToPayableAmount!.Value, uid);

            if (creditNote.ReceptionDocumentId is { } receptionDocumentId)
            {
                var receptionDoc = await _receptionRepo.GetByIdAsync(tid, receptionDocumentId, ct);
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
                catch (DomainRuleViolationException ex)
                {
                    // Mismo mensaje público con contexto; el catch externo hace rollback y
                    // DomainRuleBehavior traduce (DOMAIN_RULE_VIOLATION).
                    throw new DomainRuleViolationException(
                        $"No se pudo marcar el documento de recepción como procesado: {ex.Message}",
                        ex
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
                CreditNoteMap.ToDto(
                    creditNote,
                    invoice.InvoiceNumber,
                    invoice.SupplierName,
                    payable.OutstandingAmount
                )
            );
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
