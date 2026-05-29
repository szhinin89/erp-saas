using ERP.Domain.Common;

namespace ERP.Domain.Modules.Company.Entities;

/// <summary>
/// Certificado digital .p12 para firma del XML de comprobantes electrónicos.
/// La contraseña se almacena CIFRADA a nivel de aplicación (AES-256). Nunca en texto plano.
///
/// MULTI-TENANT: ISubscriberScopedEntity para que EnterpriseQueryFilterConfigurator aplique
/// el filtro automático subscriber_id = current_subscriber. Resuelve el warning de EF Core
/// sobre Company (ISubscriberScopedEntity) siendo el required end de esta relación.
/// </summary>
public class DigitalCertificate : ISubscriberScopedEntity
{
    public Guid     Id           { get; set; }
    /// <summary>Tenant owner — required for multi-tenant query filter.</summary>
    public Guid     SubscriberId { get; set; }
    public Guid     CompanyId    { get; set; }
    public string   FilePath     { get; set; } = null!;
    public string   PasswordHash { get; set; } = null!;
    public string?  OwnerName    { get; set; }
    public DateOnly? IssuedAt    { get; set; }
    public DateOnly  ExpiresAt   { get; set; }
    public bool     IsActive     { get; set; } = true;
    public DateTime CreatedAt    { get; set; }

    public Company Company { get; set; } = null!;
}
