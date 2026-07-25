using ERP.Domain.Modules.Accounting.Entities;

namespace ERP.Domain.Modules.Accounting.Interfaces;

public interface IPostingRuleRepository
{
    Task<PostingRule?> GetByIdAsync(Guid tenantId, Guid companyId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<PostingRule>> GetByCompanyAsync(Guid tenantId, Guid companyId, CancellationToken ct = default);

    /// <summary>Búsqueda por la clave única (CompanyId, SourceModule, FactType) — ver uq_posting_rules_company_source_fact.</summary>
    Task<PostingRule?> FindByKeyAsync(
        Guid tenantId, Guid companyId, string sourceModule, string factType, CancellationToken ct = default);

    Task AddAsync(PostingRule rule, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
