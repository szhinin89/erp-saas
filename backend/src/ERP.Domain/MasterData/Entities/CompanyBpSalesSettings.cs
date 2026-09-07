using ERP.Domain.Common;

namespace ERP.Domain.MasterData.Entities;

public sealed class CompanyBpSalesSettings
    : AuditableEntity,
        ITenantScopedEntity,
        ICompanyScopedEntity
{
    public Guid CompanyId { get; private set; }
    public Guid BusinessPartnerId { get; private set; }
    public Guid? PaymentTermId { get; private set; }

    private CompanyBpSalesSettings() { }

    public static CompanyBpSalesSettings Create(
        Guid tenantId,
        Guid companyId,
        Guid businessPartnerId,
        Guid? paymentTermId,
        Guid createdBy
    )
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId es obligatorio.", nameof(tenantId));
        if (companyId == Guid.Empty)
            throw new ArgumentException("CompanyId es obligatorio.", nameof(companyId));
        if (businessPartnerId == Guid.Empty)
            throw new ArgumentException(
                "BusinessPartnerId es obligatorio.",
                nameof(businessPartnerId)
            );

        var settings = new CompanyBpSalesSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BusinessPartnerId = businessPartnerId,
            PaymentTermId = paymentTermId,
        };
        settings.SetCreated(createdBy);
        return settings;
    }

    public void SetPaymentTerm(Guid? paymentTermId, Guid updatedBy)
    {
        PaymentTermId = paymentTermId;
        SetUpdated(updatedBy);
    }
}
