using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.Posting;

/// <summary>
/// Única implementación pública del Posting Engine (ADR-026 §8). Ensambla el pipeline interno
/// (Idempotency/RuleResolver/PeriodResolver/PeriodGuard/Factory/Validator) directamente en el
/// constructor — esos componentes son de implementación y no se registran en el contenedor DI.
/// </summary>
/// <remarks>
/// <see cref="PostAsync"/> ejecuta validación y preparación del asiento — deja las entidades en
/// staging en el <c>ChangeTracker</c> del <c>ErpDbContext</c> ambiente, pero **no garantiza
/// persistencia física inmediata**. La persistencia depende del ciclo externo de
/// <c>ErpDbContext.SaveChangesAsync</c> que originó el Domain Event que disparó este llamado
/// (en producción: <c>ErpDbContext.SaveChangesAsync()</c> → Domain Event Publish → Posting
/// Engine → flush final del mismo <c>SaveChangesAsync</c>, ADR-026 §8, Fase 3.3.1).
/// </remarks>
public sealed class PostingEngine : IPostingEngine
{
    private readonly PostingPipeline _pipeline;
    private readonly IPostingRuleRepository _postingRuleRepository;

    public PostingEngine(
        IJournalEntryRepository journalEntryRepository,
        IPostingRuleRepository postingRuleRepository,
        IAccountingPeriodRepository accountingPeriodRepository,
        IJournalEntrySequenceRepository journalEntrySequenceRepository
    )
    {
        _postingRuleRepository = postingRuleRepository;
        _pipeline = new PostingPipeline(
            new PostingIdempotencyGuard(journalEntryRepository),
            new PostingRuleResolver(postingRuleRepository),
            new PostingPeriodResolver(accountingPeriodRepository),
            new PostingPeriodGuard(),
            new JournalFactory(),
            new JournalValidator(),
            journalEntryRepository,
            journalEntrySequenceRepository
        );
    }

    public Task<Result<PostingOutcomeDto>> PostAsync(
        PostingFact fact,
        CancellationToken cancellationToken = default
    ) => _pipeline.ExecuteAsync(fact, cancellationToken);

    public async Task<bool> IsAmountKindConfiguredAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        string factType,
        PostingAmountKind amountKind,
        CancellationToken cancellationToken = default
    )
    {
        var rule = await _postingRuleRepository.FindByKeyAsync(
            tenantId,
            companyId,
            sourceModule,
            factType,
            cancellationToken
        );
        return rule is { IsActive: true } && rule.Lines.Any(l => l.AmountKind == amountKind);
    }
}
