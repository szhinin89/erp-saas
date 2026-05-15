namespace ERP.Application.Configuration.DTOs;

public sealed record BillingSettingsDto(
    Guid Id,
    Guid TenantId,
    string RazonSocial,
    string NombreComercial,
    string Ruc,
    string DireccionMatriz,
    string Telefono,
    string? Correo,
    bool ObligadoContabilidad,
    string? ContribuyenteEspecial,
    string? LogoBase64,
    string? LeyendaAdicional,
    int AnchoTirilla);
