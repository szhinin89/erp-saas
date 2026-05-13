using ERP.Domain.Common;

namespace ERP.Domain.Modules.Compras.Entities;

/// <summary>
/// Proveedor (persona natural o jurídica) del tenant.
/// Soft delete vía <see cref="MasterEntity"/>.
/// </summary>
public sealed class Proveedor : MasterEntity, ITenantEntity
{
    public const string TipoNatural  = "Natural";
    public const string TipoJuridica = "Juridica";

    public const int RazonSocialMaxLen   = 200;
    public const int RucMaxLen           = 13;
    public const int CorreoMaxLen        = 120;
    public const int TelefonoMaxLen      = 40;
    public const int DireccionMaxLen     = 300;
    public const int CondicionPagoMaxLen = 30;

    public string  TipoPersona    { get; private set; } = null!;
    public string  RazonSocial    { get; private set; } = null!;
    public string  Ruc            { get; private set; } = null!;
    public string? Correo         { get; private set; }
    public string? Telefono       { get; private set; }
    public string? Direccion      { get; private set; }
    public string  CondicionPago  { get; private set; } = null!;

    private Proveedor() { }

    public static Proveedor Create(
        Guid   tenantId,
        string tipoPersona,
        string razonSocial,
        string ruc,
        string? correo,
        string? telefono,
        string? direccion,
        string condicionPago,
        Guid   createdBy)
    {
        ValidarTipo(tipoPersona);
        ValidarRuc(ruc);

        var p = new Proveedor
        {
            Id           = Guid.NewGuid(),
            TenantId     = tenantId,
            TipoPersona  = tipoPersona,
            RazonSocial  = razonSocial.Trim(),
            Ruc          = ruc.Trim(),
            Correo       = Trim(correo),
            Telefono     = Trim(telefono),
            Direccion    = Trim(direccion),
            CondicionPago = condicionPago,
        };
        p.SetCreated(createdBy);
        return p;
    }

    public void Update(
        string tipoPersona,
        string razonSocial,
        string ruc,
        string? correo,
        string? telefono,
        string? direccion,
        string condicionPago,
        Guid   updatedBy)
    {
        ValidarTipo(tipoPersona);
        ValidarRuc(ruc);

        TipoPersona   = tipoPersona;
        RazonSocial   = razonSocial.Trim();
        Ruc           = ruc.Trim();
        Correo        = Trim(correo);
        Telefono      = Trim(telefono);
        Direccion     = Trim(direccion);
        CondicionPago = condicionPago;
        SetUpdated(updatedBy);
    }

    private static void ValidarTipo(string tipo)
    {
        if (tipo != TipoNatural && tipo != TipoJuridica)
            throw new ArgumentException($"TipoPersona debe ser '{TipoNatural}' o '{TipoJuridica}'.", nameof(tipo));
    }

    private static void ValidarRuc(string ruc)
    {
        if (string.IsNullOrWhiteSpace(ruc) || ruc.Trim().Length != 13)
            throw new ArgumentException("El RUC debe tener exactamente 13 dígitos.", nameof(ruc));
    }

    private static string? Trim(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
