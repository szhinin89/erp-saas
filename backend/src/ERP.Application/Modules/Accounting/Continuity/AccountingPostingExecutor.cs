using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Continuity;

/// <summary>
/// Punto de entrada de los casos de uso operativos: ejecuta, después de su commit, el intento
/// inicial de las obligaciones que crearon. Nunca reintenta obligaciones de otros documentos.
/// </summary>
public interface IAccountingPostingDispatcher
{
    Task DispatchPendingAsync(CancellationToken ct);
}

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — único ejecutor de intentos sobre una <see cref="AccountingPosting"/>
/// (intento inicial y reproceso manual comparten este código). Cada intento corre en su propia
/// transacción: lock de la fila → hecho reconstruido → <see cref="IPostingEngine"/> → resultado
/// Posted/Failed + historial, todo atómico con el asiento. Un error contable nunca afecta al
/// núcleo operativo ya comiteado; una excepción interna se registra como Failed INTERNAL en una
/// transacción nueva y limpia (docs/architecture/accounting.md §4–§6).
/// </summary>
public sealed partial class AccountingPostingExecutor : IAccountingPostingDispatcher
{
    public const string InternalErrorCode = "INTERNAL";
    public const string InternalErrorMessage = "Error interno del sistema al contabilizar.";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccountingPostingRepository _postings;
    private readonly AccountingPostingFactBuilder _factBuilder;
    private readonly IPostingEngine _engine;
    private readonly AccountingPostingDispatchQueue _queue;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;
    private readonly IAuditContext _audit;
    private readonly ILogger<AccountingPostingExecutor> _logger;

    public AccountingPostingExecutor(
        IUnitOfWork unitOfWork,
        IAccountingPostingRepository postings,
        AccountingPostingFactBuilder factBuilder,
        IPostingEngine engine,
        AccountingPostingDispatchQueue queue,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentUser user,
        IAuditContext audit,
        ILogger<AccountingPostingExecutor> logger
    )
    {
        _unitOfWork = unitOfWork;
        _postings = postings;
        _factBuilder = factBuilder;
        _engine = engine;
        _queue = queue;
        _tenant = tenant;
        _company = company;
        _user = user;
        _audit = audit;
        _logger = logger;
    }

    public async Task DispatchPendingAsync(CancellationToken ct)
    {
        var ids = _queue.Drain();
        if (ids.Count == 0)
            return;

        // El intento inicial solo ocurre fuera de cualquier transacción: dentro de una ajena no
        // podría aislarse del núcleo. La obligación ya persistida queda Pending (reproceso manual).
        if (_unitOfWork.HasActiveTransaction)
        {
            LogDispatchSkipped(ids.Count);
            return;
        }

        foreach (var id in ids)
            await ExecuteAsync(id, AccountingPostingAttemptTrigger.Initial, ct);
    }

    public async Task<Result<AccountingPostingDto>> ExecuteAsync(
        Guid postingId,
        AccountingPostingAttemptTrigger trigger,
        CancellationToken ct
    )
    {
        if (_unitOfWork.HasActiveTransaction)
            throw new InvalidOperationException(
                "Un intento contable requiere su propia transacción; no puede ejecutarse dentro de otra."
            );

        var startedAtUtc = DateTime.UtcNow;
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var posting = await _postings.LockByIdAsync(_tenant.TenantId, _company.CompanyId, postingId, ct);
            if (posting is null)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<AccountingPostingDto>.NotFound("Contabilización no encontrada.");
            }

            // Otro intento concurrente ya la resolvió, o su origen se anuló: no hay nada que ejecutar.
            if (posting.IsResolved)
            {
                await _unitOfWork.CommitAsync(ct);
                return Result<AccountingPostingDto>.Success(AccountingPostingDto.From(posting));
            }

            var attempt = await AttemptAsync(posting, trigger, startedAtUtc, ct);
            await _postings.AddAttemptAsync(attempt, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            return Result<AccountingPostingDto>.Success(AccountingPostingDto.From(posting));
        }
        catch (OperationCanceledException)
        {
            await RollbackAsync();
            throw;
        }
        catch (Exception ex)
        {
            await RollbackAsync();
            LogAttemptFailed(ex, postingId);
            return await RecordInternalFailureAsync(postingId, trigger, startedAtUtc, ct);
        }
    }

    private async Task<AccountingPostingAttempt> AttemptAsync(
        AccountingPosting posting,
        AccountingPostingAttemptTrigger trigger,
        DateTime startedAtUtc,
        CancellationToken ct
    )
    {
        var correlationId = _audit.Actor.CorrelationId;
        var fact = await _factBuilder.BuildAsync(posting, ct);
        if (!fact.IsSuccess)
            return posting.RecordFailed(fact.Code, fact.Error, trigger, _user.UserId, correlationId, startedAtUtc, DateTime.UtcNow);

        // AlreadyProcessed también es éxito: el asiento de esta identidad ya existía (falló la
        // persistencia de un resultado previo) y se enlaza sin crear otro.
        var outcome = await _engine.PostAsync(fact.Value!, ct);
        return outcome.IsSuccess
            ? posting.RecordPosted(outcome.Value!.JournalEntryId, trigger, _user.UserId, correlationId, startedAtUtc, DateTime.UtcNow)
            : posting.RecordFailed(outcome.Code, outcome.Error, trigger, _user.UserId, correlationId, startedAtUtc, DateTime.UtcNow);
    }

    private async Task<Result<AccountingPostingDto>> RecordInternalFailureAsync(
        Guid postingId,
        AccountingPostingAttemptTrigger trigger,
        DateTime startedAtUtc,
        CancellationToken ct
    )
    {
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var posting = await _postings.LockByIdAsync(_tenant.TenantId, _company.CompanyId, postingId, ct);
            if (posting is null || posting.IsResolved)
            {
                await _unitOfWork.CommitAsync(ct);
                return posting is null
                    ? Result<AccountingPostingDto>.NotFound("Contabilización no encontrada.")
                    : Result<AccountingPostingDto>.Success(AccountingPostingDto.From(posting));
            }

            var attempt = posting.RecordFailed(
                InternalErrorCode,
                InternalErrorMessage,
                trigger,
                _user.UserId,
                _audit.Actor.CorrelationId,
                startedAtUtc,
                DateTime.UtcNow
            );
            await _postings.AddAttemptAsync(attempt, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            return Result<AccountingPostingDto>.Success(AccountingPostingDto.From(posting));
        }
        catch (OperationCanceledException)
        {
            await RollbackAsync();
            throw;
        }
        catch (Exception ex)
        {
            // La obligación conserva su último estado durable (Pending/Failed): no se pierde.
            await RollbackAsync();
            LogInternalFailureNotRecorded(ex, postingId);
            return Result<AccountingPostingDto>.Failure(InternalErrorMessage);
        }
    }

    private async Task RollbackAsync()
    {
        await _unitOfWork.RollbackAsync(CancellationToken.None);
        _unitOfWork.ClearChangeTracker();
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Intento inicial contable omitido para {Count} obligación(es): hay una transacción activa; quedan Pending"
    )]
    private partial void LogDispatchSkipped(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Intento contable de la obligación {PostingId} falló por un error interno")]
    private partial void LogAttemptFailed(Exception ex, Guid postingId);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "No se pudo registrar el fallo interno de la obligación contable {PostingId}; conserva su estado previo"
    )]
    private partial void LogInternalFailureNotRecorded(Exception ex, Guid postingId);
}
