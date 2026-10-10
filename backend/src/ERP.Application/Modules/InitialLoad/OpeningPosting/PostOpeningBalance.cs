using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Domain.Modules.InitialLoad.Constants;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.InitialLoad.OpeningPosting;

/// <summary>Estado contable de la apertura de un lote tras <see cref="PostOpeningBalanceCommand"/>.</summary>
public sealed record OpeningBalancePostingDto(
    Guid ImportBatchId,
    ImportType ImportType,
    string FactType,
    DateOnly EntryDate,
    decimal Amount,
    OpeningBalancePostingStatus Status,
    Guid? JournalEntryId,
    DateTime? PostedAt,
    int Attempts,
    bool AlreadyPosted
);

/// <summary>
/// IL-7B — contabiliza (o reintenta) la apertura de UN lote de saldos <c>Completed</c> (inventario,
/// CxC o CxP), incluidos lotes históricos confirmados antes de IL-7 (backfill). Un asiento por lote
/// vía el Posting Engine existente (<c>InitialLoad</c>/FactType, <c>SourceEventId</c> = lote,
/// <c>EntryDate</c> = <c>Company.OpeningBalanceDate</c>), con el monto confirmado del dominio que
/// calcula el preflight. Nunca toca la carga operativa: un fallo contable deja el estado en
/// <see cref="OpeningBalancePostingStatus.Failed"/> (reintentable) y devuelve el error.
/// </summary>
public sealed record PostOpeningBalanceCommand(Guid ImportBatchId)
    : IRequest<Result<OpeningBalancePostingDto>>,
        ICompanyScopedRequest;

public sealed class PostOpeningBalanceCommandValidator : AbstractValidator<PostOpeningBalanceCommand>
{
    public PostOpeningBalanceCommandValidator()
    {
        RuleFor(x => x.ImportBatchId).NotEmpty();
    }
}

/// <summary>
/// Orden: transacción → <c>FOR UPDATE</c> del lote (mismo bloqueo que confirmar/cancelar; dos
/// intentos simultáneos se serializan y el segundo ve el resultado del primero) → Posted devuelve
/// el resultado existente sin re-ejecutar → preflight obligatorio (lote, alcance, fecha, monto &gt; 0,
/// regla, período abierto, cuentas, partida doble) → el estado guardado debe coincidir con el
/// preflight (monto/fecha) → <see cref="IPostingEngine.PostAsync"/> (su idempotencia por
/// (empresa, módulo, hecho, lote) devuelve el asiento ya existente si lo hubiera) → Posted.
/// </summary>
public sealed partial class PostOpeningBalanceCommandHandler
    : IRequestHandler<PostOpeningBalanceCommand, Result<OpeningBalancePostingDto>>
{
    public const string SourceChangedCode = "OPENING_SOURCE_CHANGED";
    private const string InternalErrorMessage = "Error interno del sistema.";

    private readonly IOperationalContext _ctx;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOpeningBalancePostingRepository _postings;
    private readonly OpeningBalancePostingPreflight _preflight;
    private readonly IPostingEngine _postingEngine;
    private readonly ILogger<PostOpeningBalanceCommandHandler> _logger;

    public PostOpeningBalanceCommandHandler(
        IOperationalContext ctx,
        IUnitOfWork unitOfWork,
        IOpeningBalancePostingRepository postings,
        OpeningBalancePostingPreflight preflight,
        IPostingEngine postingEngine,
        ILogger<PostOpeningBalanceCommandHandler> logger
    )
    {
        _ctx = ctx;
        _unitOfWork = unitOfWork;
        _postings = postings;
        _preflight = preflight;
        _postingEngine = postingEngine;
        _logger = logger;
    }

    public async Task<Result<OpeningBalancePostingDto>> Handle(
        PostOpeningBalanceCommand command,
        CancellationToken ct
    )
    {
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _postings.LockBatchAsync(_ctx.TenantId, _ctx.CompanyId, command.ImportBatchId, ct);
            // IL-8E — tras el cierre definitivo nada se contabiliza (el cierre bloquea todos los lotes
            // de saldos: el estado se lee después del bloqueo del lote).
            if (await _preflight.CheckInitialLoadOpenAsync(ct) is { } closed)
            {
                await RollbackAsync();
                return Result<OpeningBalancePostingDto>.ValidationFailure(closed, InitialLoadClosedGuard.Code);
            }
            var posting = await _postings.FindByBatchAsync(
                _ctx.TenantId, _ctx.CompanyId, command.ImportBatchId, ct);
            if (posting?.Status == OpeningBalancePostingStatus.Posted)
            {
                await _unitOfWork.CommitAsync(ct);
                return Result<OpeningBalancePostingDto>.Success(ToDto(posting, alreadyPosted: true));
            }

            var check = await _preflight.CheckAsync(command.ImportBatchId, ct);
            if (check.Fact is null)
            {
                // El lote no llega a tener hecho contable (inexistente, no contabilizable, sin
                // fecha/monto…): sin estado previo no hay nada que registrar.
                var issue = check.Issues[0];
                return await FailAsync(posting, issue.Code, issue.Message, ct);
            }

            var fact = check.Fact;
            if (posting is null)
            {
                posting = OpeningBalancePosting.CreatePending(
                    _ctx.TenantId, _ctx.CompanyId, command.ImportBatchId,
                    ImportTypeOf(fact.FactType), fact.EntryDate, fact.GrandTotal, _ctx.UserId);
                await _postings.AddAsync(posting, ct);
            }
            else if (posting.Amount != fact.GrandTotal || posting.EntryDate != fact.EntryDate)
            {
                return await FailAsync(posting, SourceChangedCode,
                    $"El monto/fecha registrados ({posting.Amount:F2} al {posting.EntryDate:yyyy-MM-dd}) ya no "
                        + $"coinciden con los datos confirmados ({fact.GrandTotal:F2} al {fact.EntryDate:yyyy-MM-dd}).",
                    ct);
            }

            if (!check.IsReady)
            {
                var issue = check.Issues[0];
                return await FailAsync(posting, issue.Code, issue.Message, ct);
            }

            var outcome = await _postingEngine.PostAsync(fact, ct);
            if (!outcome.IsSuccess)
                return await FailAsync(posting, outcome.Code ?? "POSTING_FAILED", outcome.Error!, ct);

            posting.MarkPosted(outcome.Value!.JournalEntryId, _ctx.UserId);
            await _postings.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            return Result<OpeningBalancePostingDto>.Success(ToDto(posting, alreadyPosted: false));
        }
        catch (OperationCanceledException)
        {
            await RollbackAsync();
            throw;
        }
        catch (Exception ex)
        {
            await RollbackAsync();
            LogPostingFailed(ex, command.ImportBatchId);
            return Result<OpeningBalancePostingDto>.Failure(InternalErrorMessage);
        }
    }

    /// <summary>
    /// Registra el intento fallido (si el lote ya tiene estado contable) y CONFIRMA la transacción:
    /// el estado Failed queda persistido para el reintento; la carga operativa no se toca.
    /// </summary>
    private async Task<Result<OpeningBalancePostingDto>> FailAsync(
        OpeningBalancePosting? posting, string code, string message, CancellationToken ct)
    {
        if (posting is not null)
        {
            posting.MarkFailed(code, message, _ctx.UserId);
            await _postings.SaveChangesAsync(ct);
        }
        await _unitOfWork.CommitAsync(ct);
        return code == "BATCH_NOT_FOUND"
            ? Result<OpeningBalancePostingDto>.NotFound(message)
            : Result<OpeningBalancePostingDto>.ValidationFailure(message, code);
    }

    private async Task RollbackAsync()
    {
        await _unitOfWork.RollbackAsync(CancellationToken.None);
        _unitOfWork.ClearChangeTracker();
    }

    private static ImportType ImportTypeOf(string factType) =>
        Enum.GetValues<ImportType>().Single(t => OpeningBalancePostingFacts.ForImportType(t) == factType);

    private static OpeningBalancePostingDto ToDto(OpeningBalancePosting p, bool alreadyPosted) =>
        new(p.ImportBatchId, p.ImportType, p.FactType, p.EntryDate, p.Amount, p.Status,
            p.JournalEntryId, p.PostedAt, p.Attempts, alreadyPosted);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Contabilización de la apertura del lote {BatchId} falló por un error interno"
    )]
    private partial void LogPostingFailed(Exception ex, Guid batchId);
}
