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

/// <summary>Resultado individual de un intento solicitado sobre una obligación.</summary>
public enum AccountingPostingExecutionOutcome
{
    /// <summary>Se ejecutó un intento nuevo: la obligación quedó Posted o Failed.</summary>
    Attempted = 1,

    /// <summary>Ya estaba Posted o Canceled: no se ejecutó intento.</summary>
    AlreadyResolved = 2,

    /// <summary>No existe en el Tenant/Company del contexto.</summary>
    NotFound = 3,

    /// <summary>Falló el intento y tampoco pudo registrarse el fallo: conserva su estado previo.</summary>
    NotRecorded = 4,
}

public sealed record AccountingPostingExecution(
    Guid PostingId,
    AccountingPostingExecutionOutcome Outcome,
    AccountingPostingDto? Posting
);

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
            await RunAsync(id, AccountingPostingAttemptTrigger.Initial, reason: null, correlationId: null, ct);
    }

    /// <summary>Reproceso manual individual: traduce el resultado del intento a la respuesta HTTP.</summary>
    public async Task<Result<AccountingPostingDto>> ExecuteAsync(
        Guid postingId,
        AccountingPostingAttemptTrigger trigger,
        string? reason,
        CancellationToken ct
    )
    {
        var execution = await RunAsync(postingId, trigger, reason, correlationId: null, ct);
        return execution.Outcome switch
        {
            AccountingPostingExecutionOutcome.NotFound => Result<AccountingPostingDto>.NotFound(
                "Contabilización no encontrada."
            ),
            AccountingPostingExecutionOutcome.NotRecorded => Result<AccountingPostingDto>.Failure(
                InternalErrorMessage
            ),
            _ => Result<AccountingPostingDto>.Success(execution.Posting!),
        };
    }

    /// <summary>
    /// Ejecuta un intento sobre una obligación y devuelve su resultado individual. Un error contable
    /// o interno no se propaga (queda Failed); solo se propagan la cancelación del llamador y un
    /// motivo manual inválido (antes de abrir la transacción).
    /// </summary>
    /// <param name="correlationId">
    /// Correlación explícita (p. ej. la del lote completo); null usa la del contexto de auditoría.
    /// </param>
    public async Task<AccountingPostingExecution> RunAsync(
        Guid postingId,
        AccountingPostingAttemptTrigger trigger,
        string? reason,
        string? correlationId,
        CancellationToken ct
    )
    {
        if (_unitOfWork.HasActiveTransaction)
            throw new InvalidOperationException(
                "Un intento contable requiere su propia transacción; no puede ejecutarse dentro de otra."
            );

        // Tambi?n a?sla el primer intento del contexto utilizado por el n?cleo ya confirmado.
        _unitOfWork.ClearChangeTracker();
        var context = new AttemptContext(
            trigger,
            AccountingPosting.NormalizeReason(trigger, reason),
            correlationId ?? _audit.Actor.CorrelationId,
            DateTime.UtcNow
        );
        try
        {
            return await RunInTransactionAsync(postingId, context, ct);
        }
        finally
        {
            // B1 (ACCOUNTING-CONTINUITY-02): cada intento termina con un contexto limpio, así el
            // siguiente intento del mismo scope (lote o varias obligaciones de un documento) no
            // reutiliza entidades ya comiteadas con valores obsoletos — p. ej. la
            // JournalEntrySequence que ReserveNextNumberAsync busca primero en el ChangeTracker, o
            // cuentas/reglas cambiadas por otra transacción. Dentro de un mismo intento (una sola
            // transacción) el ChangeTracker no se toca.
            _unitOfWork.ClearChangeTracker();
        }
    }

    private async Task<AccountingPostingExecution> RunInTransactionAsync(
        Guid postingId,
        AttemptContext context,
        CancellationToken ct
    )
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync(ct);
            var posting = await _postings.LockByIdAsync(_tenant.TenantId, _company.CompanyId, postingId, ct);
            if (posting is null)
            {
                await _unitOfWork.RollbackAsync(ct);
                return new(postingId, AccountingPostingExecutionOutcome.NotFound, null);
            }

            // Otro intento concurrente ya la resolvió, o su origen se anuló: no hay nada que ejecutar.
            if (posting.IsResolved)
            {
                await _unitOfWork.CommitAsync(ct);
                return new(postingId, AccountingPostingExecutionOutcome.AlreadyResolved, AccountingPostingDto.From(posting));
            }

            var attempt = await AttemptAsync(posting, context, ct);
            await _postings.AddAttemptAsync(attempt, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            return new(postingId, AccountingPostingExecutionOutcome.Attempted, AccountingPostingDto.From(posting));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await RollbackAsync();
            throw;
        }
        catch (Exception ex)
        {
            await RollbackAsync();
            LogAttemptFailed(ex, postingId);
            return await RecordInternalFailureAsync(postingId, context, ct);
        }
    }

    private async Task<AccountingPostingAttempt> AttemptAsync(
        AccountingPosting posting,
        AttemptContext context,
        CancellationToken ct
    )
    {
        var fact = await _factBuilder.BuildAsync(posting, ct);
        if (!fact.IsSuccess)
            return posting.RecordFailed(fact.Code, fact.Error, context.Trigger, _user.UserId, context.CorrelationId, context.Reason, context.StartedAtUtc, DateTime.UtcNow);

        // AlreadyProcessed también es éxito: el asiento de esta identidad ya existía (falló la
        // persistencia de un resultado previo) y se enlaza sin crear otro.
        var outcome = await _engine.PostAsync(fact.Value!, ct);
        return outcome.IsSuccess
            ? posting.RecordPosted(outcome.Value!.JournalEntryId, context.Trigger, _user.UserId, context.CorrelationId, context.Reason, context.StartedAtUtc, DateTime.UtcNow)
            : posting.RecordFailed(outcome.Code, outcome.Error, context.Trigger, _user.UserId, context.CorrelationId, context.Reason, context.StartedAtUtc, DateTime.UtcNow);
    }

    private async Task<AccountingPostingExecution> RecordInternalFailureAsync(
        Guid postingId,
        AttemptContext context,
        CancellationToken ct
    )
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync(ct);
            var posting = await _postings.LockByIdAsync(_tenant.TenantId, _company.CompanyId, postingId, ct);
            if (posting is null || posting.IsResolved)
            {
                await _unitOfWork.CommitAsync(ct);
                return posting is null
                    ? new(postingId, AccountingPostingExecutionOutcome.NotFound, null)
                    : new(postingId, AccountingPostingExecutionOutcome.AlreadyResolved, AccountingPostingDto.From(posting));
            }

            var attempt = posting.RecordFailed(
                InternalErrorCode,
                InternalErrorMessage,
                context.Trigger,
                _user.UserId,
                context.CorrelationId,
                context.Reason,
                context.StartedAtUtc,
                DateTime.UtcNow
            );
            await _postings.AddAttemptAsync(attempt, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            return new(postingId, AccountingPostingExecutionOutcome.Attempted, AccountingPostingDto.From(posting));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await RollbackAsync();
            throw;
        }
        catch (Exception ex)
        {
            // La obligación conserva su último estado durable (Pending/Failed): no se pierde.
            await RollbackAsync();
            LogInternalFailureNotRecorded(ex, postingId);
            return new(postingId, AccountingPostingExecutionOutcome.NotRecorded, null);
        }
    }

    private sealed record AttemptContext(
        AccountingPostingAttemptTrigger Trigger,
        string? Reason,
        string? CorrelationId,
        DateTime StartedAtUtc
    );

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
