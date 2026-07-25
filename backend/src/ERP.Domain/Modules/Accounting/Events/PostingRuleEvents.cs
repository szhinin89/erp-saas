using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Accounting.Events;

/// <summary>Se levanta cuando <c>PostingRule.Create()</c> registra una nueva regla de configuración de mapeo contable.</summary>
public sealed class PostingRuleCreatedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid PostingRuleId { get; }
    public Guid CompanyId { get; }

    public PostingRuleCreatedEvent(Guid tenantId, Guid postingRuleId, Guid companyId)
    {
        TenantId = tenantId;
        PostingRuleId = postingRuleId;
        CompanyId = companyId;
    }

    Guid IAuditEvent.EntityId => PostingRuleId;
    string IAuditEvent.Action => "Created";
    string? IAuditEvent.Reason => null;
}
