using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Communications;

public sealed class CommunicationTemplateRepository : ICommunicationTemplateRepository
{
    private readonly ErpDbContext _db;

    public CommunicationTemplateRepository(ErpDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(CommunicationTemplate template, CancellationToken ct = default) =>
        await _db.CommunicationTemplates.AddAsync(template, ct);

    public Task<CommunicationTemplate?> GetActiveAsync(
        Guid tenantId,
        Guid companyId,
        CommunicationChannel channel,
        string code,
        string language,
        CancellationToken ct = default
    ) =>
        _db.CommunicationTemplates.FirstOrDefaultAsync(
            x =>
                x.TenantId == tenantId
                && x.CompanyId == companyId
                && x.Channel == channel
                && x.Code == code.Trim().ToUpperInvariant()
                && x.Language == language.Trim().ToLowerInvariant()
                && x.IsActive,
            ct
        );
}
