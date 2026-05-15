using ERP.Domain.Common;

namespace ERP.Domain.Configuration.Entities;

public sealed class RetentionSettings : AuditableEntity, ITenantEntity
{
    public const int TaxTypeMaxLen      = 20;
    public const int SubjectTypeMaxLen  = 20;
    public const int SriCodeMaxLen      = 10;

    public string  TaxType      { get; private set; } = null!;
    public string  SubjectType  { get; private set; } = null!;
    public string  SriCode      { get; private set; } = null!;
    public decimal Percentage   { get; private set; }
    public bool    IsActive     { get; private set; } = true;

    private RetentionSettings() { }

    public static RetentionSettings Create(
        Guid    tenantId,
        string  taxType,
        string  subjectType,
        string  sriCode,
        decimal percentage,
        Guid    createdBy)
    {
        var c = new RetentionSettings
        {
            Id          = Guid.NewGuid(),
            TenantId    = tenantId,
            TaxType     = (taxType ?? string.Empty).Trim().ToUpperInvariant(),
            SubjectType = (subjectType ?? string.Empty).Trim().ToUpperInvariant(),
            SriCode     = (sriCode ?? string.Empty).Trim(),
            Percentage  = percentage,
            IsActive    = true,
        };
        c.SetCreated(createdBy);
        return c;
    }

    public void SetActive(bool isActive, Guid userId)
    {
        IsActive = isActive;
        SetUpdated(userId);
    }
}
