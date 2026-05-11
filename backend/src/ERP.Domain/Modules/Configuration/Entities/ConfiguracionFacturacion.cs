using ERP.Domain.Common;

namespace ERP.Domain.Configuration.Entities;

public sealed class ConfiguracionFacturacion : AuditableEntity, ITenantEntity
{
    public const int RazonSocialMaxLen = 200;
    public const int NombreComercialMaxLen = 200;
    public const int RucMaxLen = 13;
    public const int DireccionMatrizMaxLen = 500;
    public const int TelefonoMaxLen = 50;
    public const int CorreoMaxLen = 200;
    public const int ContribuyenteEspecialMaxLen = 200;
    public const int LogoBase64MaxLen = 10000;
    public const int LeyendaAdicionalMaxLen = 500;

    public string RazonSocial { get; private set; } = null!;
    public string NombreComercial { get; private set; } = null!;
    public string Ruc { get; private set; } = null!;
    public string DireccionMatriz { get; private set; } = null!;
    public string Telefono { get; private set; } = null!;
    public string? Correo { get; private set; }
    public bool ObligadoContabilidad { get; private set; }
    public string? ContribuyenteEspecial { get; private set; }
    public string? LogoBase64 { get; private set; }
    public string? LeyendaAdicional { get; private set; }
    public int AnchoTirilla { get; private set; } = 80;

    private ConfiguracionFacturacion() { }

    public static ConfiguracionFacturacion Create(
        Guid tenantId,
        string razonSocial,
        string nombreComercial,
        string ruc,
        string direccionMatriz,
        string telefono,
        string? correo,
        bool obligadoContabilidad,
        string? contribuyenteEspecial,
        string? logoBase64,
        string? leyendaAdicional,
        int anchoTirilla,
        Guid createdBy)
    {
        var config = new ConfiguracionFacturacion
        {
            TenantId = tenantId,
            RazonSocial = razonSocial.Trim(),
            NombreComercial = nombreComercial.Trim(),
            Ruc = ruc.Trim(),
            DireccionMatriz = direccionMatriz.Trim(),
            Telefono = telefono.Trim(),
            Correo = string.IsNullOrWhiteSpace(correo) ? null : correo.Trim(),
            ObligadoContabilidad = obligadoContabilidad,
            ContribuyenteEspecial = string.IsNullOrWhiteSpace(contribuyenteEspecial) ? null : contribuyenteEspecial.Trim(),
            LogoBase64 = string.IsNullOrWhiteSpace(logoBase64) ? null : logoBase64.Trim(),
            LeyendaAdicional = string.IsNullOrWhiteSpace(leyendaAdicional) ? null : leyendaAdicional.Trim(),
            AnchoTirilla = anchoTirilla == 58 ? 58 : 80
        };
        config.SetCreated(createdBy);
        return config;
    }

    public static ConfiguracionFacturacion CreateDefault(Guid tenantId, Guid createdBy)
    {
        return Create(
            tenantId: tenantId,
            razonSocial: "EMPRESA DEMO",
            nombreComercial: "EMPRESA DEMO",
            ruc: "9999999999999",
            direccionMatriz: "Dirección desconocida",
            telefono: "",
            correo: null,
            obligadoContabilidad: false,
            contribuyenteEspecial: null,
            logoBase64: null,
            leyendaAdicional: "Gracias por su compra.",
            anchoTirilla: 80,
            createdBy: createdBy);
    }

    public void Update(
        string razonSocial,
        string nombreComercial,
        string ruc,
        string direccionMatriz,
        string telefono,
        string? correo,
        bool obligadoContabilidad,
        string? contribuyenteEspecial,
        string? logoBase64,
        string? leyendaAdicional,
        int anchoTirilla,
        Guid updatedBy)
    {
        RazonSocial = razonSocial.Trim();
        NombreComercial = nombreComercial.Trim();
        Ruc = ruc.Trim();
        DireccionMatriz = direccionMatriz.Trim();
        Telefono = telefono.Trim();
        Correo = string.IsNullOrWhiteSpace(correo) ? null : correo.Trim();
        ObligadoContabilidad = obligadoContabilidad;
        ContribuyenteEspecial = string.IsNullOrWhiteSpace(contribuyenteEspecial) ? null : contribuyenteEspecial.Trim();
        LogoBase64 = string.IsNullOrWhiteSpace(logoBase64) ? null : logoBase64.Trim();
        LeyendaAdicional = string.IsNullOrWhiteSpace(leyendaAdicional) ? null : leyendaAdicional.Trim();
        AnchoTirilla = anchoTirilla == 58 ? 58 : 80;
        SetUpdated(updatedBy);
    }
}
