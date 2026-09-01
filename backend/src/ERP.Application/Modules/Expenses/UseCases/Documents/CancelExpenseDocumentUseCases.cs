using ERP.Application.Common;
using ERP.Application.Modules.DocTypes.Services;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Application.Modules.Expenses.Exceptions;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.DocTypes.Constants;
using ERP.Domain.Modules.DocTypes.Enums;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Expenses.UseCases.Documents;

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// EXPENSES-CANCEL-01 — anula un gasto ya confirmado: reversa/bloquea la CxP originada
/// (<see cref="AccountsPayable.Cancel"/>) y anula el documento (<see cref="ExpenseDocument.Cancel"/>),
/// cuyo evento dispara la reversa contable vía <c>ExpenseDocumentCancelledPostingTranslator</c> —
/// este handler nunca reversa contabilidad manualmente. Mismo patrón transaccional que
/// <c>CancelPurchaseHandler</c> (Purchases).
/// </summary>
public sealed record CancelExpenseDocumentCommand(Guid Id, string Reason)
    : IRequest<Result<ExpenseDocumentDetailDto>>,
        IBranchScopedRequest;

// ── Validator ───────────────────────────────────────────────────────────

public sealed class CancelExpenseDocumentValidator : AbstractValidator<CancelExpenseDocumentCommand>
{
    public CancelExpenseDocumentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(ExpenseDocument.CancelReasonMaxLen)
            .WithMessage("El motivo de anulación es obligatorio (máximo 500 caracteres).");
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class CancelExpenseDocumentHandler
    : IRequestHandler<CancelExpenseDocumentCommand, Result<ExpenseDocumentDetailDto>>
{
    private readonly IExpenseDocumentRepository _repo;
    private readonly IAccountsPayableRepository _payableRepo;
    private readonly IUnitOfWork _uow;
    private readonly IDocumentFlowPolicyService _workflowPolicy;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentBranch _branch;
    private readonly ICurrentUser _user;
    private readonly ILogger<CancelExpenseDocumentHandler> _logger;

    public CancelExpenseDocumentHandler(
        IExpenseDocumentRepository repo,
        IAccountsPayableRepository payableRepo,
        IUnitOfWork uow,
        IDocumentFlowPolicyService workflowPolicy,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentBranch branch,
        ICurrentUser user,
        ILogger<CancelExpenseDocumentHandler> logger
    )
    {
        _repo = repo;
        _payableRepo = payableRepo;
        _uow = uow;
        _workflowPolicy = workflowPolicy;
        _tenant = tenant;
        _company = company;
        _branch = branch;
        _user = user;
        _logger = logger;
    }

    public async Task<Result<ExpenseDocumentDetailDto>> Handle(
        CancelExpenseDocumentCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _tenant.TenantId;
        var cid = _company.CompanyId;
        var uid = _user.UserId;

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var document = await _repo.GetByIdAsync(tid, cmd.Id, ct);
            if (document is null || document.BranchId != _branch.BranchId)
            {
                await _uow.RollbackAsync(ct);
                return Result<ExpenseDocumentDetailDto>.NotFound("Gasto no encontrado.");
            }

            DocumentFlowPolicyResult policy;
            try
            {
                // DOCUMENT-FLOW-POLICY-01: valida CÓMO debe comportarse la anulación (modo de
                // anulación, motivo obligatorio) — el permiso expenses.documents.cancel (QUIÉN
                // puede anular) ya se validó en el controller vía [Authorize(Policy = "perm:...")],
                // antes de llegar aquí. Esta llamada nunca reemplaza esa validación de permiso.
                policy = await _workflowPolicy.EnsureCancellationFlowAsync(
                    cid,
                    DocTypeCodes.ExpenseDocument,
                    cmd.Reason,
                    ct
                );
            }
            catch (DocumentFlowPolicyViolationException ex)
            {
                await _uow.RollbackAsync(ct);
                return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message);
            }

            if (document.Status != ExpenseStatus.Confirmed)
            {
                await _uow.RollbackAsync(ct);
                return Result<ExpenseDocumentDetailDto>.ValidationFailure(
                    "Solo se pueden anular gastos confirmados."
                );
            }

            var payable =
                policy.CancellationMode == CancellationMode.AllowedAfterConfirmationWithReversal
                    ? await _payableRepo.GetByOriginAsync(
                        tid,
                        cid,
                        AccountsPayableOriginType.ExpenseDocument,
                        document.Id,
                        ct
                    )
                    : null;

            if (payable is not null)
            {
                try
                {
                    payable.Cancel(uid);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(
                        "Cannot cancel payable for expense document {ExpenseDocumentId}: {Reason}",
                        document.Id,
                        ex.Message
                    );
                    await _uow.RollbackAsync(ct);
                    return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message);
                }
            }

            try
            {
                document.Cancel(cmd.Reason, uid);
            }
            catch (ArgumentException ex)
            {
                await _uow.RollbackAsync(ct);
                return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                await _uow.RollbackAsync(ct);
                return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message);
            }

            try
            {
                // EXPENSES-CONFIRM-07 mismo criterio: el reverso contable es estricto —
                // ExpenseDocumentCancelledPostingTranslator lanza ExpensePostingFailedException si
                // no puede reversar el asiento original, abortando este SaveChangesAsync completo.
                await _repo.SaveChangesAsync(ct);
            }
            catch (ExpensePostingFailedException ex)
            {
                await _uow.RollbackAsync(ct);
                return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message, ex.Code);
            }

            await _uow.CommitAsync(ct);

            _logger.LogInformation(
                "Expense document {DocumentNumber} ({ExpenseDocumentId}) cancelled successfully",
                document.DocumentNumber,
                document.Id
            );

            return Result<ExpenseDocumentDetailDto>.Success(ExpenseDocumentMapper.ToDetail(document));
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }
}
