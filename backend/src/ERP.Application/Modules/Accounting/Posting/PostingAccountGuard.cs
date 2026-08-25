using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting;

/// <summary>
/// ACCOUNTING-POSTING-RULES-AUDIT-03: última línea de defensa antes de que
/// <see cref="JournalFactory"/> genere <c>JournalEntryLine</c> — resuelve cada cuenta referenciada
/// por <c>rule.Lines</c> (única fuente real que <see cref="JournalFactory"/> consume; los campos
/// legacy <c>PostingRule.DebitAccountId</c>/<c>CreditAccountId</c> nunca llegan a producir una
/// línea de asiento, ver remarks de <see cref="JournalFactory"/>) y exige que exista, pertenezca a
/// la misma Company/Tenant, esté activa y admita movimiento (<c>AllowsPosting</c>).
/// </summary>
/// <remarks>
/// Fail-closed y sin asiento parcial: se ejecuta antes de <c>JournalFactory.Create</c> — si falla,
/// <see cref="JournalFactory"/> nunca llega a construirse, así que no hay <c>JournalEntry</c> en
/// staging que descartar (mismo principio ya aplicado por <see cref="PostingPeriodGuard"/>). Una
/// cuenta pudo ser válida al crear/editar la <c>PostingRule</c> (validado en
/// <c>CreatePostingRuleHandler</c>/<c>UpdatePostingRuleHandler</c>) y volverse inválida después
/// (desactivada, AllowsPosting revocado) — este guard es la protección de tiempo de ejecución
/// para ese caso, independiente de la validación de configuración.
/// </remarks>
internal sealed class PostingAccountGuard
{
    private const string InvalidAccountCode = "POSTING_ACCOUNT_INVALID";

    private readonly IAccountRepository _accountRepository;
    private readonly ILogger<PostingEngine> _logger;

    /// <summary>
    /// Comparte <see cref="ILogger{PostingEngine}"/> con <see cref="PostingEngine"/> (categoría
    /// única para todo el Posting Engine, mismo criterio ya exigido en el resto del repo — un
    /// logger nuevo por componente interno no registrado en DI no aporta valor) en vez de un
    /// <c>ILogger&lt;PostingAccountGuard&gt;</c> propio.
    /// </summary>
    public PostingAccountGuard(IAccountRepository accountRepository, ILogger<PostingEngine> logger)
    {
        _accountRepository = accountRepository;
        _logger = logger;
    }

    public async Task<Result<PostingRule>> EnsureAccountsPostableAsync(
        PostingFact fact,
        PostingRule rule,
        CancellationToken ct
    )
    {
        var accountIds = rule.Lines.Select(l => l.AccountId).Distinct();

        foreach (var accountId in accountIds)
        {
            var account = await _accountRepository.GetByIdAsync(
                fact.TenantId,
                fact.CompanyId,
                accountId,
                ct
            );

            if (account is null || !account.IsActive || !account.AllowsPosting)
            {
                LogRejection(fact, rule, accountId, account);
                return Result<PostingRule>.ValidationFailure(
                    "La regla de contabilización referencia una cuenta inválida para contabilizar "
                        + "(inexistente, de otra empresa, inactiva o que no admite movimientos).",
                    InvalidAccountCode
                );
            }
        }

        return Result<PostingRule>.Success(rule);
    }

    private void LogRejection(PostingFact fact, PostingRule rule, Guid accountId, Account? account) =>
        _logger.LogWarning(
            "Posting rejected: account {AccountId} not postable (found={Found}, active={IsActive}, "
                + "allowsPosting={AllowsPosting}). SourceModule={SourceModule} "
                + "SourceEventType={SourceEventType} SourceEventId={SourceEventId} PostingRuleId={PostingRuleId}",
            accountId,
            account is not null,
            account?.IsActive,
            account?.AllowsPosting,
            fact.SourceModule,
            fact.FactType,
            fact.SourceEventId,
            rule.Id
        );
}
