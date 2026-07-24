namespace ERP.Domain.MasterData.ValueObjects;

/// <summary>
/// Datos de contacto electrónico de un BusinessPartnerContact.
/// Todos los campos son opcionales — al menos uno debe tener valor para que el contacto
/// sea operacionalmente útil (validación en Application layer, no invariante de dominio).
/// Almacenado aplanado en columnas de master_bp_contacts.
/// </summary>
public sealed record ContactInfo
{
    public const int PhoneMaxLen = 20;
    public const int EmailMaxLen = 254;

    public string? Phone  { get; }
    public string? Mobile { get; }
    public string? Email  { get; }

    private ContactInfo(string? phone, string? mobile, string? email)
    {
        Phone  = phone;
        Mobile = mobile;
        Email  = email;
    }

    public static ContactInfo Create(string? phone = null, string? mobile = null, string? email = null)
    {
        var p = Normalize(phone,  PhoneMaxLen, nameof(phone));
        var m = Normalize(mobile, PhoneMaxLen, nameof(mobile));
        var e = NormalizeEmail(email);

        return new ContactInfo(p, m, e);
    }

    private static string? Normalize(string? value, int maxLen, string paramName)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        if (v.Length > maxLen)
            throw new ArgumentException($"{paramName} no puede superar {maxLen} caracteres.", paramName);
        return v;
    }

    private static string? NormalizeEmail(string? email)
    {
        var e = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(e)) return null;
        if (e.Length > EmailMaxLen)
            throw new ArgumentException($"Email no puede superar {EmailMaxLen} caracteres.", nameof(email));
        if (!e.Contains('@') || e.IndexOf('.', e.IndexOf('@')) < 0)
            throw new ArgumentException("Formato de email inválido.", nameof(email));
        return e;
    }
}
