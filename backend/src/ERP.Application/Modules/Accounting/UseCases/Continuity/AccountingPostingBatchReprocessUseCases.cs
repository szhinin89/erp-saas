using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Accounting.Enums;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Accounting.UseCases.Continuity;

// ACCOUNTING-CONTINUITY-02 — reproceso manual en lote (docs/architecture/accounting.md §6/§14).
// Recorre el mismo ejecutor del reproceso individual, una obligación por transacción: un fallo no
// detiene las demás y cada una informa su propio resultado. Sin tabla de lote: el lote se identifica
// por el CorrelationId común de sus intentos. Sin reintento automático, worker ni cola.

public sealed record ReprocessAccountingPostingsCommand(IReadOnlyList<Guid>? Ids, string? Reason)
    : IRequest<Result<AccountingPostingBatchReprocessDto>>,
        ICompanyScopedRequest
{
    public const int MaxIds = 100;
}

/// <summary>Resultado de una obligación dentro del lote.</summary>
public enum AccountingPostingBatchItemOutcome
{
    /// <summary>Intento ejecutado; Posting contiene el resultado Posted o Failed.</summary>
    Attempted = 1,

    /// <summary>Ya estaba Posted o Canceled: sin intento.</summary>
    AlreadyResolved = 2,

    /// <summary>Inexistente o fuera del Tenant/Company del contexto.</summary>
    NotFound = 3,

    /// <summary>La solicitud se canceló antes de intentarla: conserva el estado previo.</summary>
    NotAttempted = 4,
}

public sealed record AccountingPostingBatchItemDto(
    Guid Id,
    AccountingPostingBatchItemOutcome Outcome,
    AccountingPostingDto? Posting,
    string? ErrorMessage = null
);

public sealed record AccountingPostingBatchSummaryDto(
    int Requested,
    int Attempted,
    int AlreadyResolved,
    int NotFound,
    int NotAttempted
);

public sealed record AccountingPostingBatchReprocessDto(
    string? CorrelationId,
    IReadOnlyList<AccountingPostingBatchItemDto> Items,
    AccountingPostingBatchSummaryDto Summary
);

public sealed class ReprocessAccountingPostingsCommandValidator
    : AbstractValidator<ReprocessAccountingPostingsCommand>
{
    public ReprocessAccountingPostingsCommandValidator()
    {
        RuleFor(x => x.Ids)
            .NotEmpty()
            .WithMessage("Seleccione al menos una contabilización.")
            .Must(ids => ids!.Count <= ReprocessAccountingPostingsCommand.MaxIds)
            .WithMessage(
                $"Puede reprocesar como máximo {ReprocessAccountingPostingsCommand.MaxIds} contabilizaciones por lote."
            )
            .When(x => x.Ids is not null, ApplyConditionTo.CurrentValidator);
        RuleForEach(x => x.Ids).NotEmpty().WithMessage("Identificador de contabilización inválido.");
        RuleFor(x => x.Reason).ValidReprocessReason();
    }
}

public sealed class ReprocessAccountingPostingsHandler
    : IRequestHandler<ReprocessAccountingPostingsCommand, Result<AccountingPostingBatchReprocessDto>>
{
    private readonly AccountingPostingExecutor _executor;
    private readonly IAuditContext _audit;

    public ReprocessAccountingPostingsHandler(AccountingPostingExecutor executor, IAuditContext audit)
    {
        _executor = executor;
        _audit = audit;
    }

    public async Task<Result<AccountingPostingBatchReprocessDto>> Handle(
        ReprocessAccountingPostingsCommand command,
        CancellationToken ct
    )
    {
        // Duplicados eliminados conservando el orden solicitado; una sola correlación para todo el lote.
        var ids = command.Ids!.Distinct().ToList();
        var correlationId = _audit.Actor.CorrelationId ?? Guid.NewGuid().ToString("N");
        var items = new List<AccountingPostingBatchItemDto>(ids.Count);

        foreach (var id in ids)
        {
            if (ct.IsCancellationRequested)
            {
                items.Add(new(id, AccountingPostingBatchItemOutcome.NotAttempted, null));
                continue;
            }

            try
            {
                var execution = await _executor.RunAsync(
                    id,
                    AccountingPostingAttemptTrigger.Manual,
                    command.Reason,
                    correlationId,
                    ct
                );
                items.Add(new(id, Map(execution), execution.Posting,
                    execution.Outcome == AccountingPostingExecutionOutcome.NotRecorded
                        ? AccountingPostingExecutor.InternalErrorMessage : null));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // La transacción del intento se revirtió: la obligación conserva su estado previo.
                items.Add(new(id, AccountingPostingBatchItemOutcome.NotAttempted, null));
            }
        }

        return Result<AccountingPostingBatchReprocessDto>.Success(
            new AccountingPostingBatchReprocessDto(correlationId, items, Summarize(items))
        );
    }

    private static AccountingPostingBatchItemOutcome Map(AccountingPostingExecution execution) =>
        execution.Outcome switch
        {
            AccountingPostingExecutionOutcome.Attempted => AccountingPostingBatchItemOutcome.Attempted,
            AccountingPostingExecutionOutcome.AlreadyResolved =>
                AccountingPostingBatchItemOutcome.AlreadyResolved,
            AccountingPostingExecutionOutcome.NotFound => AccountingPostingBatchItemOutcome.NotFound,
            _ => AccountingPostingBatchItemOutcome.Attempted,
        };

    private static AccountingPostingBatchSummaryDto Summarize(
        IReadOnlyList<AccountingPostingBatchItemDto> items
    )
    {
        int Count(AccountingPostingBatchItemOutcome outcome) => items.Count(i => i.Outcome == outcome);
        return new(
            items.Count,
            Count(AccountingPostingBatchItemOutcome.Attempted),
            Count(AccountingPostingBatchItemOutcome.AlreadyResolved),
            Count(AccountingPostingBatchItemOutcome.NotFound),
            Count(AccountingPostingBatchItemOutcome.NotAttempted)
        );
    }
}
