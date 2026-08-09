using ERP.Domain.Common;
using ERP.Domain.Modules.SriCatalogs.Constants;

namespace ERP.Domain.Configuration.Entities;

public sealed class SriSettings : AuditableEntity, ITenantScopedEntity, ICompanyScopedEntity
{
    public const int CertPathMaxLen = 500;
    public const int CertPasswordMaxLen = 500;
    public const int CertFileNameMaxLen = 260;
    public const int WsdlUrlMaxLen = 500;
    public const int DocTypeCodeMaxLen = 5;
    public const int PaymentMethodCodeMaxLen = 5;

    /// <summary>Tamaño máximo aceptado para el archivo .p12/.pfx subido (5 MB).</summary>
    public const long CertMaxSizeBytes = 5 * 1024 * 1024;

    // Defaults SRI Ecuador cuando la empresa no ha configurado un valor específico
    public const string FallbackDocTypeCode = SriDocumentTypeCodes.Invoice;
    public const string FallbackSriPaymentMethodCode = "01";

    public Guid CompanyId { get; private set; }

    /// <summary>Clave de almacenamiento opaca de <see cref="Application.Common.Interfaces.IFileStorage"/> — nunca una ruta de filesystem escrita por el usuario.</summary>
    public string? CertP12Path { get; private set; }

    /// <summary>Contraseña del certificado cifrada en reposo (prefijo dp1:). Legacy: texto plano hasta próximo guardado.</summary>
    public string? CertPassword { get; private set; }
    public string? CertFileName { get; private set; }
    public long? CertSizeBytes { get; private set; }
    public DateTime? CertUploadedAtUtc { get; private set; }
    public int Environment { get; private set; }
    public int EmissionType { get; private set; } = 1;
    public string WsdlUrl { get; private set; } = null!;

    private SriSettings() { }

    public static SriSettings Create(
        Guid tenantId,
        Guid companyId,
        int environment,
        int emissionType,
        string wsdlUrl,
        Guid createdBy
    )
    {
        var s = new SriSettings
        {
            TenantId = tenantId,
            CompanyId = companyId,
            Environment = environment,
            EmissionType = emissionType,
            WsdlUrl = wsdlUrl.Trim(),
        };
        s.SetCreated(createdBy);
        return s;
    }

    public void UpdateConfiguration(
        int environment,
        int emissionType,
        string wsdlUrl,
        string? certPassword,
        Guid updatedBy
    )
    {
        Environment = environment;
        EmissionType = emissionType;
        WsdlUrl = wsdlUrl.Trim();
        if (!string.IsNullOrWhiteSpace(certPassword))
            CertPassword = certPassword.Trim();
        SetUpdated(updatedBy);
    }

    public void AttachCertificate(
        string certP12Path,
        string certFileName,
        long certSizeBytes,
        DateTime uploadedAtUtc,
        Guid updatedBy
    )
    {
        CertP12Path = certP12Path;
        CertFileName = certFileName;
        CertSizeBytes = certSizeBytes;
        CertUploadedAtUtc = uploadedAtUtc;
        SetUpdated(updatedBy);
    }
}
