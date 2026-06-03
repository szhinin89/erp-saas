using ERP.Domain.Common;
using ERP.Domain.Modules.SriCatalogs.Entities;

namespace ERP.Domain.Configuration.Entities;

/// <summary>
/// Configuración de retenciones activas para un suscriptor.
/// SriCode referencia global.sri_retention_code — el porcentaje NO se duplica aquí.
/// </summary>
public sealed class RetentionSettings : AuditableEntity, ISubscriberScopedEntity
{
    public const int TaxTypeMaxLen      = 20;
    public const int SubjectTypeMaxLen  = 20;
    public const int SriCodeMaxLen      = 10;

    public string  TaxType      { get; private set; } = null!;
    public string  SubjectType  { get; private set; } = null!;
    /// <summary>FK a global.sri_retention_code.Code — fuente única del porcentaje.</summary>
    public string  SriCode      { get; private set; } = null!;
    public bool    IsActive     { get; private set; } = true;

    /// <summary>Navigation a la fuente de verdad del porcentaje.</summary>
    public SriRetentionCode? SriRetentionCode { get; private set; }

    private RetentionSettings() { }

    public static RetentionSettings Create(
        Guid    subscriberId,
        string  taxType,
        string  subjectType,
        string  sriCode,
        Guid    createdBy)
    {
        var c = new RetentionSettings
        {
            Id          = Guid.NewGuid(),
            SubscriberId    = subscriberId,
            TaxType     = (taxType ?? string.Empty).Trim().ToUpperInvariant(),
            SubjectType = (subjectType ?? string.Empty).Trim().ToUpperInvariant(),
            SriCode     = (sriCode ?? string.Empty).Trim(),
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
