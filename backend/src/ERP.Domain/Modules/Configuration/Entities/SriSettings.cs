using ERP.Domain.Common;

namespace ERP.Domain.Configuration.Entities;

public sealed class SriSettings : AuditableEntity, ISubscriberScopedEntity, ICompanyScopedEntity
{
    public const int CertPathMaxLen    = 500;
    public const int CertPasswordMaxLen = 500;
    public const int WsdlUrlMaxLen     = 500;

    public Guid   CompanyId    { get; private set; }
    public string CertP12Path  { get; private set; } = null!;
    /// <summary>Contraseña del certificado cifrada en reposo (prefijo dp1:). Legacy: texto plano hasta próximo guardado.</summary>
    public string CertPassword { get; private set; } = null!;
    public int    Environment  { get; private set; }
    public int    EmissionType { get; private set; } = 1;
    public string WsdlUrl      { get; private set; } = null!;

    private SriSettings() { }

    public static SriSettings Create(
        Guid   subscriberId,
        Guid   companyId,
        string certP12Path,
        string certPassword,
        int    environment,
        int    emissionType,
        string wsdlUrl,
        Guid   createdBy)
    {
        var s = new SriSettings
        {
            SubscriberId = subscriberId,
            CompanyId    = companyId,
            CertP12Path  = certP12Path.Trim(),
            CertPassword = certPassword.Trim(),
            Environment  = environment,
            EmissionType = emissionType,
            WsdlUrl      = wsdlUrl.Trim(),
        };
        s.SetCreated(createdBy);
        return s;
    }

    public void Update(
        string certP12Path,
        string certPassword,
        int    environment,
        int    emissionType,
        string wsdlUrl,
        Guid   updatedBy)
    {
        CertP12Path  = certP12Path.Trim();
        CertPassword = certPassword.Trim();
        Environment  = environment;
        EmissionType = emissionType;
        WsdlUrl      = wsdlUrl.Trim();
        SetUpdated(updatedBy);
    }
}
