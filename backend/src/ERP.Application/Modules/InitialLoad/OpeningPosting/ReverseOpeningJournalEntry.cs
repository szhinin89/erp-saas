using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.InitialLoad.Constants;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.InitialLoad.OpeningPosting;

/// <summary>Resultado del reverso del ASI de apertura (<see cref="ReverseOpeningJournalEntryCommand"/>).</summary>
public sealed record OpeningJournalEntryReversalDto(
    Guid PostingId,
    int Version,
    Guid JournalEntryId,
    Guid ReversalJournalEntryId,
    DateTime? ReversedAtUtc,
    string? Reason,
    bool AlreadyReversed
);

/// <summary>
/// IL-8B — paso 1 de la corrección del ASI de apertura: reversa por completo el asiento de la
/// versión VIGENTE y publicada (<paramref name="PostingId"/>) y la deja como historial no vigente.
/// El paso 2 es publicar una versión nueva con <see cref="PublishOpeningJournalEntryCommand"/>
/// (Id/<c>SourceEventId</c> nuevo); mientras no exista versión vigente, IL-8 queda incompleto. Nunca
/// edita ni borra el asiento original; <c>Company.OpeningBalanceDate</c> sigue inmutable (lo fija
/// cualquier versión que llegó a publicarse); el reverso y la versión nueva usan esa misma fecha.
/// Solo se corrige mientras TODOS los períodos contables desde la fecha de apertura hasta hoy estén
/// abiertos (las operaciones reales no bloquean por sí solas); con un período cerrado la apertura
/// queda histórica y se corrige con un ajuste contable en un período abierto. Es el único camino de reverso de un asiento
/// <c>InitialLoad</c>: el reverso genérico (<see cref="ReverseJournalEntryCommand"/>) los rechaza.
/// </summary>
public sealed record ReverseOpeningJournalEntryCommand(Guid PostingId, string Reason)
    : IRequest<Result<OpeningJournalEntryReversalDto>>,
        ICompanyScopedRequest;

public sealed class ReverseOpeningJournalEntryCommandValidator
    : AbstractValidator<ReverseOpeningJournalEntryCommand>
{
    public ReverseOpeningJournalEntryCommandValidator()
    {
        RuleFor(x => x.PostingId).NotEmpty();
        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r))
            .WithMessage("El motivo del reverso es obligatorio.")
            .MaximumLength(JournalEntry.ReverseReasonMaxLength)
            .WithMessage($"El motivo admite como máximo {JournalEntry.ReverseReasonMaxLength} caracteres.");
    }
}

/// <summary>
/// Orden, en UNA transacción propia: <c>FOR UPDATE</c> de la empresa y sus lotes de saldos (mismo
/// bloqueo que la publicación del ASI, el cambio de fecha y IL-7B: serializa reversos concurrentes
/// y reverso vs. publicación) → versión por Id en la empresa activa → si ya fue reemplazada con su
/// asiento reversado, devuelve ese resultado sin re-ejecutar (reintento seguro) → vigente y
/// Posted → asiento del ASI de esta versión, aún Posted → todos los períodos de
/// [fecha de apertura, hoy] abiertos (<see cref="PostingPeriodGuard"/>) → núcleo del
/// reverso (<see cref="JournalEntryReversal"/>: período abierto, número, mismo EntryDate) →
/// <c>MarkSuperseded</c> → commit. Cualquier bloqueo o error hace rollback: ni asiento, ni número,
/// ni cambio de versión.
/// </summary>
public sealed partial class ReverseOpeningJournalEntryCommandHandler
    : IRequestHandler<ReverseOpeningJournalEntryCommand, Result<OpeningJournalEntryReversalDto>>
{
    public const string NotCurrentCode = "OPENING_ASI_NOT_CURRENT";
    public const string NotPostedCode = "OPENING_ASI_NOT_POSTED";
    public const string JournalMismatchCode = "OPENING_ASI_JOURNAL_MISMATCH";
    private const string InternalErrorMessage = "Error interno del sistema.";

    private readonly IOperationalContext _ctx;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOpeningJournalEntryPostingRepository _postings;
    private readonly IAccountingPeriodRepository _periods;
    private readonly ICompanyClock _clock;
    private readonly IJournalEntryRepository _journal;
    private readonly JournalEntryReversal _reversal;
    private readonly ILogger<ReverseOpeningJournalEntryCommandHandler> _logger;

    public ReverseOpeningJournalEntryCommandHandler(
        IOperationalContext ctx,
        IUnitOfWork unitOfWork,
        IOpeningJournalEntryPostingRepository postings,
        IJournalEntryRepository journal,
        IAccountingPeriodRepository periods,
        IJournalEntrySequenceRepository sequences,
        ICompanyClock clock,
        ILogger<ReverseOpeningJournalEntryCommandHandler> logger
    )
    {
        _ctx = ctx;
        _unitOfWork = unitOfWork;
        _postings = postings;
        _journal = journal;
        _periods = periods;
        _clock = clock;
        _reversal = new JournalEntryReversal(journal, periods, sequences);
        _logger = logger;
    }

    public async Task<Result<OpeningJournalEntryReversalDto>> Handle(
        ReverseOpeningJournalEntryCommand command,
        CancellationToken ct
    )
    {
        var tenantId = _ctx.TenantId;
        var companyId = _ctx.CompanyId;
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _postings.LockCompanyOpeningAsync(tenantId, companyId, includeBalanceBatches: true, ct);
            var posting = await _postings.GetByIdAsync(tenantId, companyId, command.PostingId, ct);
            if (posting is null)
                return await RejectAsync(Result<OpeningJournalEntryReversalDto>.NotFound(
                    "Asiento de apertura no encontrado."));

            var entry = posting.JournalEntryId is { } entryId
                ? await _journal.GetByIdAsync(tenantId, companyId, entryId, ct)
                : null;

            if (!posting.IsCurrent)
            {
                // Reintento de un reverso ya hecho (o reverso concurrente que ganó): mismo resultado.
                if (entry is { Status: JournalEntryStatus.Reversed, ReverseJournalEntryId: { } reversalId })
                {
                    await _unitOfWork.CommitAsync(ct);
                    return Result<OpeningJournalEntryReversalDto>.Success(
                        ToDto(posting.Id, posting.Version, entry, reversalId, alreadyReversed: true));
                }
                return await RejectAsync(Fail(NotCurrentCode,
                    "Esta versión del asiento de apertura ya no es la vigente; solo se reversa la versión vigente."));
            }

            if (posting.Status != OpeningBalancePostingStatus.Posted)
                return await RejectAsync(Fail(NotPostedCode,
                    "El asiento de apertura aún no está publicado: no hay nada que reversar; corrija y vuelva a publicar."));

            if (entry is null
                || entry.SourceModule != OpeningBalancePostingFacts.SourceModule
                || entry.SourceEventType != OpeningBalancePostingFacts.OpeningJournalEntry
                || entry.SourceEventId != posting.Id
                || entry.Status != JournalEntryStatus.Posted)
                return await RejectAsync(Fail(JournalMismatchCode,
                    "El asiento contable de esta versión no corresponde a un asiento de apertura publicado."));

            // IL-8E (cierre definitivo de la implementación inicial) aún no existe: cuando exista,
            // ese estado también bloquea aquí cualquier corrección.
            var closedPeriod = await FindClosedPeriodSinceOpeningAsync(tenantId, companyId, posting.EntryDate, ct);
            if (closedPeriod is not null)
                return await RejectAsync(Fail(closedPeriod.Code,
                    closedPeriod.Error + " Hay un período contable cerrado desde la fecha de apertura: el asiento de "
                        + "apertura queda histórico y su corrección debe hacerse con un ajuste contable en un período abierto."));

            var reversed = await _reversal.ReverseAsync(entry, _ctx.UserId, command.Reason, ct);
            if (!reversed.IsSuccess)
                return await RejectAsync(Fail(reversed.Code, reversed.Error!));

            posting.MarkSuperseded(_ctx.UserId);
            await _postings.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            return Result<OpeningJournalEntryReversalDto>.Success(
                ToDto(posting.Id, posting.Version, entry, reversed.Value!.Id, alreadyReversed: false));
        }
        catch (OperationCanceledException)
        {
            await RollbackAsync();
            throw;
        }
        catch (Exception ex)
        {
            await RollbackAsync();
            LogReverseFailed(ex, companyId);
            return Result<OpeningJournalEntryReversalDto>.Failure(InternalErrorMessage);
        }
    }

    /// <summary>
    /// Primer período de [fecha de apertura, hoy de la empresa] que ya no admite contabilización,
    /// con el mismo guard del Posting Engine (Closed/Locked → PERIOD_NOT_OPEN); null si todos están
    /// abiertos. Que exista el período de la fecha de apertura lo exige el núcleo del reverso.
    /// </summary>
    private async Task<Result<AccountingPeriod>?> FindClosedPeriodSinceOpeningAsync(
        Guid tenantId, Guid companyId, DateOnly openingDate, CancellationToken ct)
    {
        var today = await _clock.TodayAsync(companyId, tenantId, ct);
        var periods = await _periods.GetOverlappingAsync(
            tenantId, companyId, openingDate, today > openingDate ? today : openingDate, ct);
        var guard = new PostingPeriodGuard();
        return periods.OrderBy(p => p.StartDate).Select(guard.Ensure).FirstOrDefault(r => !r.IsSuccess);
    }

    private static Result<OpeningJournalEntryReversalDto> Fail(string? code, string message) =>
        Result<OpeningJournalEntryReversalDto>.ValidationFailure(message, code);

    /// <summary>Un bloqueo no deja rastro: rollback (sin asiento, número ni cambio de versión).</summary>
    private async Task<Result<OpeningJournalEntryReversalDto>> RejectAsync(Result<OpeningJournalEntryReversalDto> result)
    {
        await RollbackAsync();
        return result;
    }

    private async Task RollbackAsync()
    {
        await _unitOfWork.RollbackAsync(CancellationToken.None);
        _unitOfWork.ClearChangeTracker();
    }

    private static OpeningJournalEntryReversalDto ToDto(
        Guid postingId, int version, JournalEntry entry, Guid reversalId, bool alreadyReversed) =>
        new(postingId, version, entry.Id, reversalId, entry.ReversedAtUtc, entry.ReverseReason, alreadyReversed);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Reverso del asiento de apertura de la empresa {CompanyId} falló por un error interno"
    )]
    private partial void LogReverseFailed(Exception ex, Guid companyId);
}
