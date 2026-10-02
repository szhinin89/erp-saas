using ERP.Application.Common;
using ERP.Application.Modules.Expenses.UseCases.Documents;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;

namespace ERP.Application.Modules.Expenses.Services;

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01 — finaliza la anulación de un GASTO cuya retención el SRI confirmó
/// ANULADO, por el flujo oficial existente (<see cref="CancelExpenseDocumentHandler.ExecuteAsync"/>:
/// política documental, CxP, retención y reverso contable — nada se duplica aquí), con el motivo y el
/// usuario registrados en la solicitud. Un gasto ya anulado se reporta como tal (idempotencia).
/// </summary>
public sealed class ExpenseRetentionOriginCancellation : IRetentionOriginCancellation
{
    private readonly CancelExpenseDocumentHandler _cancelExpense;
    private readonly IExpenseDocumentRepository _expenses;

    public ExpenseRetentionOriginCancellation(
        CancelExpenseDocumentHandler cancelExpense,
        IExpenseDocumentRepository expenses
    )
    {
        _cancelExpense = cancelExpense;
        _expenses = expenses;
    }

    public RetentionSourceDocumentType SourceType => RetentionSourceDocumentType.ExpenseDocument;

    public async Task<Result<RetentionOriginCancellationOutcome>> CancelAsync(
        RetentionAnnulmentRequest request,
        CancellationToken ct = default
    )
    {
        var expense = await _expenses.GetByIdAsync(request.TenantId, request.SourceDocumentId, ct);
        if (expense is null || expense.CompanyId != request.CompanyId)
            return Result<RetentionOriginCancellationOutcome>.NotFound("El gasto de la retención no existe.");
        if (expense.Status == ExpenseStatus.Cancelled)
            return Result<RetentionOriginCancellationOutcome>.Success(RetentionOriginCancellationOutcome.AlreadyCancelled);

        var result = await _cancelExpense.ExecuteAsync(
            new ExpenseCancellationContext(
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
