using ERP.Domain.Common;

namespace ERP.Domain.Configuration.Entities;

public sealed class ConfiguracionSRI : AuditableEntity, ITenantEntity
{
    public const int RucEmpresaMaxLen = 13;
    public const int RazonSocialMaxLen = 200;
    public const int NombreComercialMaxLen = 200;
    public const int DireccionMatrizMaxLen = 500;
    public const int ContribuyenteEspecialMaxLen = 200;
    public const int EstablecimientoMaxLen = 3;
    public const int PuntoEmisionMaxLen = 3;
    public const int CertificadoP12PathMaxLen = 500;
    public const int CertificadoPasswordMaxLen = 500;
    public const int UrlSriAutorizacionMaxLen = 500;

    public string RucEmpresa { get; private set; } = null!;
    public string RazonSocial { get; private set; } = null!;
    public string? NombreComercial { get; private set; }
    public string DireccionMatriz { get; private set; } = null!;
    public bool ObligadoContabilidad { get; private set; }
    public string? ContribuyenteEspecial { get; private set; }
    public string Establecimiento { get; private set; } = "001";
    public string PuntoEmision { get; private set; } = "001";
    public int SecuencialActual { get; private set; } = 1;
    public string CertificadoP12Path { get; private set; } = null!;
    public string CertificadoPassword { get; private set; } = null!;
    public int Ambiente { get; private set; }
    public int TipoEmision { get; private set; } = 1;
    public string UrlSriAutorizacion { get; private set; } = null!;

    private ConfiguracionSRI() { }

    public static ConfiguracionSRI Create(
        Guid tenantId,
        string rucEmpresa,
        string razonSocial,
        string? nombreComercial,
        string direccionMatriz,
        bool obligadoContabilidad,
        string? contribuyenteEspecial,
        string establecimiento,
        string puntoEmision,
        int secuencialActual,
        string certificadoP12Path,
        string certificadoPassword,
        int ambiente,
        int tipoEmision,
        string urlSriAutorizacion,
        Guid createdBy)
    {
        var sri = new ConfiguracionSRI
        {
            TenantId = tenantId,
            RucEmpresa = rucEmpresa.Trim(),
            RazonSocial = razonSocial.Trim(),
            NombreComercial = string.IsNullOrWhiteSpace(nombreComercial) ? null : nombreComercial.Trim(),
            DireccionMatriz = direccionMatriz.Trim(),
            ObligadoContabilidad = obligadoContabilidad,
            ContribuyenteEspecial = string.IsNullOrWhiteSpace(contribuyenteEspecial) ? null : contribuyenteEspecial.Trim(),
            Establecimiento = string.IsNullOrWhiteSpace(establecimiento) ? "001" : establecimiento.Trim(),
            PuntoEmision = string.IsNullOrWhiteSpace(puntoEmision) ? "001" : puntoEmision.Trim(),
            SecuencialActual = secuencialActual <= 0 ? 1 : secuencialActual,
            CertificadoP12Path = certificadoP12Path.Trim(),
            CertificadoPassword = certificadoPassword.Trim(),
            Ambiente = ambiente,
            TipoEmision = tipoEmision,
            UrlSriAutorizacion = urlSriAutorizacion.Trim(),
        };
        sri.SetCreated(createdBy);
        return sri;
    }

    public void Update(
        string  rucEmpresa,
        string  razonSocial,
        string? nombreComercial,
        string  direccionMatriz,
        bool    obligadoContabilidad,
        string? contribuyenteEspecial,
        string  establecimiento,
        string  puntoEmision,
        string  certificadoP12Path,
        string  certificadoPassword,
        int     ambiente,
        int     tipoEmision,
        string  urlSriAutorizacion,
        Guid    updatedBy)
    {
        RucEmpresa            = rucEmpresa.Trim();
        RazonSocial           = razonSocial.Trim();
        NombreComercial       = string.IsNullOrWhiteSpace(nombreComercial) ? null : nombreComercial.Trim();
        DireccionMatriz       = direccionMatriz.Trim();
        ObligadoContabilidad  = obligadoContabilidad;
        ContribuyenteEspecial = string.IsNullOrWhiteSpace(contribuyenteEspecial) ? null : contribuyenteEspecial.Trim();
        Establecimiento       = string.IsNullOrWhiteSpace(establecimiento) ? "001" : establecimiento.Trim();
        PuntoEmision          = string.IsNullOrWhiteSpace(puntoEmision) ? "001" : puntoEmision.Trim();
        CertificadoP12Path    = certificadoP12Path.Trim();
        CertificadoPassword   = certificadoPassword.Trim();
        Ambiente              = ambiente;
        TipoEmision           = tipoEmision;
        UrlSriAutorizacion    = urlSriAutorizacion.Trim();
        SetUpdated(updatedBy);
    }

    public void IncrementarSecuencial()
    {
        SecuencialActual++;
    }
}
