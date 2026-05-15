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

    // ── Campos SRI / comerciales ──────────────────────────────────
    /// <summary>ISO-3 del país. FK → sri_country.code. Ej: "ECU".</summary>
    public string?  CountryCode    { get; private set; }
    /// <summary>Sustento tributario por defecto. FK → sri_tax_support.code.</summary>
    public string?  TaxSupportCode { get; private set; }
    /// <summary>Días de crédito acordados (0 = contado).</summary>
    public short    PaymentDays    { get; private set; }
    /// <summary>Límite de crédito aprobado. Null = sin límite.</summary>
    public decimal? CreditLimit    { get; private set; }

    /// <summary>
    /// Código SRI de tipo de identificación. No persiste en BD.
    /// Requerido para el XML del comprobante de retención emitido.
    /// </summary>
    public string SriIdTypeCode => TipoPersona == TipoNatural ? "05" : "04";

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
        Guid   createdBy,
        string? countryCode    = null,
        string? taxSupportCode = null,
        short   paymentDays    = 0,
        decimal? creditLimit   = null)
    {
        ValidarTipo(tipoPersona);
        ValidarRuc(ruc);

        var p = new Proveedor
        {
            Id             = Guid.NewGuid(),
            TenantId       = tenantId,
            TipoPersona    = tipoPersona,
            RazonSocial    = razonSocial.Trim(),
            Ruc            = ruc.Trim(),
            Correo         = Trim(correo),
            Telefono       = Trim(telefono),
            Direccion      = Trim(direccion),
            CondicionPago  = condicionPago,
            CountryCode    = Trim(countryCode),
            TaxSupportCode = Trim(taxSupportCode),
            PaymentDays    = paymentDays,
            CreditLimit    = creditLimit,
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
        Guid   updatedBy,
        string? countryCode    = null,
        string? taxSupportCode = null,
        short   paymentDays    = 0,
        decimal? creditLimit   = null)
    {
        ValidarTipo(tipoPersona);
        ValidarRuc(ruc);

        TipoPersona    = tipoPersona;
        RazonSocial    = razonSocial.Trim();
        Ruc            = ruc.Trim();
        Correo         = Trim(correo);
        Telefono       = Trim(telefono);
        Direccion      = Trim(direccion);
        CondicionPago  = condicionPago;
        CountryCode    = Trim(countryCode);
        TaxSupportCode = Trim(taxSupportCode);
        PaymentDays    = paymentDays;
        CreditLimit    = creditLimit;
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
