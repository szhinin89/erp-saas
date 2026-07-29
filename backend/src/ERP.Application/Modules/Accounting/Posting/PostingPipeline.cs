using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.Posting;

/// <summary>
/// Orden obligatorio del pipeline (ADR-026 §8): Idempotency → PostingRuleResolver →
/// PostingPeriodResolver → PostingPeriodGuard → JournalFactory → JournalValidator →
/// ReserveNextNumberAsync → Post() → Persistencia. No se altera este orden.
/// </summary>
/// <remarks>
/// El pipeline solo prepara el asiento (AddAsync deja el JournalEntry en staging en el
/// ChangeTracker) — nunca comitea. La persistencia física pertenece exclusivamente al ciclo
/// externo de ErpDbContext.SaveChangesAsync que originó el Domain Event (ADR-026 §8,
/// Fase 3.3.1). La idempotencia bajo concurrencia real la garantiza el advisory lock adquirido
/// dentro de PostingIdempotencyGuard antes de la lectura — no una violación UNIQUE capturada
/// aquí (Fase 3.3.2/3.3.3): con el lock, dos ejecuciones concurrentes para la misma clave nunca
/// llegan a competir por el mismo INSERT.
/// </remarks>
internal sealed class PostingPipeline
{
    // Mismo actor de sistema usado por JournalFactory para Create() — el Posting Engine se
    // dispara desde Domain Events, sin usuario interactivo en contexto (Fase 5.2).
    private static readonly Guid SystemActor = Guid.Empty;

    private readonly PostingIdempotencyGuard _idempotencyGuard;
    private readonly PostingRuleResolver _ruleResolver;
    private readonly PostingPeriodResolver _periodResolver;
    private readonly PostingPeriodGuard _periodGuard;
    private readonly JournalFactory _journalFactory;
    private readonly JournalValidator _journalValidator;
    private readonly IJournalEntryRepository _journalEntryRepository;
    private readonly IJournalEntrySequenceRepository _journalEntrySequenceRepository;

    public PostingPipeline(
        PostingIdempotencyGuard idempotencyGuard,
        PostingRuleResolver ruleResolver,
        PostingPeriodResolver periodResolver,
        PostingPeriodGuard periodGuard,
        JournalFactory journalFactory,
        JournalValidator journalValidator,
        IJournalEntryRepository journalEntryRepository,
        IJournalEntrySequenceRepository journalEntrySequenceRepository
    )
    {
        _idempotencyGuard = idempotencyGuard;
        _ruleResolver = ruleResolver;
        _periodResolver = periodResolver;
        _periodGuard = periodGuard;
        _journalFactory = journalFactory;
        _journalValidator = journalValidator;
        _journalEntryRepository = journalEntryRepository;
        _journalEntrySequenceRepository = journalEntrySequenceRepository;
    }

    public async Task<Result<PostingOutcomeDto>> ExecuteAsync(
        PostingFact fact,
        CancellationToken ct
    )
    {
        var existing = await _idempotencyGuard.FindExistingAsync(fact, ct);
        if (existing is not null)
            return Result<PostingOutcomeDto>.Success(
                new PostingOutcomeDto(existing.Id, PostingOutcomeStatus.AlreadyProcessed)
            );

        var ruleResult = await _ruleResolver.ResolveAsync(fact, ct);
        if (!ruleResult.IsSuccess)
            return Result<PostingOutcomeDto>.ValidationFailure(ruleResult.Error!, ruleResult.Code);

        var periodResult = await _periodResolver.ResolveAsync(fact, ct);
        if (!periodResult.IsSuccess)
            return Result<PostingOutcomeDto>.ValidationFailure(
                periodResult.Error!,
                periodResult.Code
            );

        var guardResult = _periodGuard.Ensure(periodResult.Value!);
        if (!guardResult.IsSuccess)
            return Result<PostingOutcomeDto>.ValidationFailure(
                guardResult.Error!,
                guardResult.Code
            );

        // Fase 3.5.5: JournalFactory ahora recibe también la PostingRule ya resuelta (2 líneas
        // arriba) para leer PostingRule.Lines y construir JournalEntryLine reales — el orden de
        // las etapas no cambia, solo se propaga un dato ya calculado a una etapa posterior.
        var entry = _journalFactory.Create(fact, ruleResult.Value!, guardResult.Value!);

        var validationResult = _journalValidator.Validate(entry);
        if (!validationResult.IsSuccess)
            return Result<PostingOutcomeDto>.ValidationFailure(
                validationResult.Error!,
                validationResult.Code
            );

        // Fase 5.3 (ADR-026 §7): la reserva del número corre bajo el advisory lock de
        // ReserveNextNumberAsync sobre la misma transacción ambiente — se asigna aquí, recién
        // validado el asiento, y queda en staging junto con el JournalEntry (ninguno de los dos
        // se persiste hasta el SaveChangesAsync externo).
        var entryNumber = await _journalEntrySequenceRepository.ReserveNextNumberAsync(
            fact.TenantId,
            fact.CompanyId,
            entry.FiscalYear,
            ct
        );

        entry.Post(SystemActor, entryNumber);

        await _journalEntryRepository.AddAsync(entry, ct);
        return Result<PostingOutcomeDto>.Success(
            new PostingOutcomeDto(entry.Id, PostingOutcomeStatus.Created)
        );
    }
}
