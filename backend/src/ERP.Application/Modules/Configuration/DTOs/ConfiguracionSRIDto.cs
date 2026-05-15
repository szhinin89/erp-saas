namespace ERP.Application.Configuration.DTOs;

public record SriConfigurationDto(
    Guid    TenantId,
    string  RucEmpresa,
    string  RazonSocial,
    string? NombreComercial,
    string  DireccionMatriz,
    bool    ObligadoContabilidad,
    string? ContribuyenteEspecial,
    string    EstabCode,
    string    EmPointCode,
    int     SecuencialActual,
    string  CertificadoP12Path,
    int     Ambiente,
    int     TipoEmision,
    string  UrlSriAutorizacion);
