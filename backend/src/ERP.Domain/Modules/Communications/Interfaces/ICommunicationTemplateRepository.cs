using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Enums;

namespace ERP.Domain.Modules.Communications.Interfaces;

public interface ICommunicationTemplateRepository
{
    Task AddAsync(CommunicationTemplate template, CancellationToken ct = default);

    Task<CommunicationTemplate?> GetActiveAsync(
        Guid tenantId,
        Guid companyId,
        CommunicationChannel channel,
        string code,
        string language,
        CancellationToken ct = default
    );
}
