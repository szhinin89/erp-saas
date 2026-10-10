using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Interfaces;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting;

/// <summary>Resultado de <see cref="PostingPreflight.CheckAsync"/>: el asiento que se publicaría.</summary>
public sealed record PostingPreflightDto(Guid PostingRuleId, Guid AccountingPeriodId, int LineCount, decimal TotalDebit, decimal TotalCredit);

/// <summary>
/// IL-7A — verificación previa de un <see cref="PostingFact"/> con EXACTAMENTE las mismas etapas del
/// pipeline del Posting Engine (regla activa, período que contiene la fecha y admite
/// contabilización, cuentas postables, construcción y validación de partida doble), sin
/// idempotency lock, sin reservar número en <c>JournalEntrySequence</c> y sin agregar nada al
/// contexto: nada se persiste. Las reglas no se duplican aquí — se reutilizan las mismas clases que
/// usa <see cref="PostingEngine"/>; un código de error devuelto es el mismo que daría la publicación
/// real (<c>RULE_NOT_FOUND</c>, <c>PERIOD_NOT_OPEN</c>, <c>POSTING_ACCOUNT_INVALID</c>,
/// <c>VALIDATION_FAILED</c>).
/// </summary>
public sealed class PostingPreflight
{
    private readonly PostingRuleResolver _ruleResolver;
    private readonly PostingPeriodResolver _periodResolver;
    private readonly PostingPeriodGuard _periodGuard = new();
    private readonly PostingAccountGuard _accountGuard;
    private readonly JournalFactory _journalFactory = new();
    private readonly JournalValidator _journalValidator = new();

    public PostingPreflight(
        IPostingRuleRepository postingRuleRepository,
        IAccountingPeriodRepository accountingPeriodRepository,
        IAccountRepository accountRepository,
        ILogger<PostingEngine> logger
    )
    {
        _ruleResolver = new PostingRuleResolver(postingRuleRepository);
        _periodResolver = new PostingPeriodResolver(accountingPeriodRepository);
        _accountGuard = new PostingAccountGuard(accountRepository, logger);
    }

    public async Task<Result<PostingPreflightDto>> CheckAsync(PostingFact fact, CancellationToken ct)
    {
        var rule = await _ruleResolver.ResolveAsync(fact, ct);
        if (!rule.IsSuccess)
            return Result<PostingPreflightDto>.ValidationFailure(rule.Error!, rule.Code);

        var period = await _periodResolver.ResolveAsync(fact, ct);
        if (!period.IsSuccess)
            return Result<PostingPreflightDto>.ValidationFailure(period.Error!, period.Code);

        var openPeriod = _periodGuard.Ensure(period.Value!);
        if (!openPeriod.IsSuccess)
            return Result<PostingPreflightDto>.ValidationFailure(openPeriod.Error!, openPeriod.Code);

        var accounts = await _accountGuard.EnsureAccountsPostableAsync(fact, rule.Value!, ct);
        if (!accounts.IsSuccess)
            return Result<PostingPreflightDto>.ValidationFailure(accounts.Error!, accounts.Code);

        var entry = _journalFactory.Create(fact, rule.Value!, openPeriod.Value!);
        var valid = _journalValidator.Validate(entry);
        if (!valid.IsSuccess)
            return Result<PostingPreflightDto>.ValidationFailure(valid.Error!, valid.Code);

        return Result<PostingPreflightDto>.Success(
            new PostingPreflightDto(
                rule.Value!.Id,
                openPeriod.Value!.Id,
                entry.Lines.Count,
                entry.Lines.Sum(l => l.Debit),
                entry.Lines.Sum(l => l.Credit)
            )
        );
    }
}
