using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.Posting;

/// <summary>Resuelve la PostingRule aplicable a un hecho contable — fail-closed (ADR-026 §8): sin regla, no hay asiento.</summary>
internal sealed class PostingRuleResolver
{
    private readonly IPostingRuleRepository _postingRuleRepository;

    public PostingRuleResolver(IPostingRuleRepository postingRuleRepository)
    {
        _postingRuleRepository = postingRuleRepository;
    }

    public async Task<Result<PostingRule>> ResolveAsync(PostingFact fact, CancellationToken ct)
    {
        var rule = await _postingRuleRepository.FindByKeyAsync(
            fact.TenantId,
            fact.CompanyId,
            fact.SourceModule,
            fact.FactType,
            ct
        );

        return rule is null || !rule.IsActive
            ? Result<PostingRule>.ValidationFailure(
                "No existe una regla de contabilización activa para el hecho contable.",
                "RULE_NOT_FOUND"
            )
            : Result<PostingRule>.Success(rule);
    }
}
