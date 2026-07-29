namespace ERP.Domain.MasterData.ValueObjects;

/// <summary>
/// Dirección física con codificación geográfica INEC Ecuador.
/// Jerarquía obligatoria: si ParishCode → CantonCode obligatorio; si CantonCode → ProvinceCode obligatorio.
/// Almacenado aplanado en columnas de master_bp_locations.
/// </summary>
public sealed record PhysicalAddress
{
    public const int AddressLineMaxLen = 300;
    public const int ProvinceCodeLen = 2;
    public const int CantonCodeLen = 4;
    public const int ParishCodeLen = 6;

    public string AddressLine { get; }
    public string? ProvinceCode { get; }
    public string? CantonCode { get; }
    public string? ParishCode { get; }

    private PhysicalAddress(string addressLine, string? provinceCode, string? cantonCode, string? parishCode)
    {
        AddressLine = addressLine;
        ProvinceCode = provinceCode;
        CantonCode = cantonCode;
        ParishCode = parishCode;
    }

    public static PhysicalAddress Create(
        string addressLine,
        string? provinceCode = null,
        string? cantonCode = null,
        string? parishCode = null)
    {
        var address = (addressLine ?? string.Empty).Trim();
        if (address.Length < 5)
            throw new ArgumentException("La dirección debe tener al menos 5 caracteres.", nameof(addressLine));
        if (address.Length > AddressLineMaxLen)
            throw new ArgumentException($"La dirección no puede superar {AddressLineMaxLen} caracteres.", nameof(addressLine));

        var province = NormalizeGeoCode(provinceCode, ProvinceCodeLen, nameof(provinceCode));
        var canton = NormalizeGeoCode(cantonCode, CantonCodeLen, nameof(cantonCode));
        var parish = NormalizeGeoCode(parishCode, ParishCodeLen, nameof(parishCode));

        // Jerarquía INEC: parroquia requiere cantón, cantón requiere provincia
        if (canton is not null && province is null)
            throw new ArgumentException("ProvinceCode es obligatorio cuando se especifica CantonCode.", nameof(provinceCode));
        if (parish is not null && canton is null)
            throw new ArgumentException("CantonCode es obligatorio cuando se especifica ParishCode.", nameof(cantonCode));

        return new PhysicalAddress(address, province, canton, parish);
    }

    private static string? NormalizeGeoCode(string? code, int expectedLen, string paramName)
    {
        var c = code?.Trim();
        if (string.IsNullOrEmpty(c)) return null;
        if (c.Length != expectedLen)
            throw new ArgumentException($"{paramName} debe tener exactamente {expectedLen} dígitos.", paramName);
        if (!c.All(char.IsDigit))
            throw new ArgumentException($"{paramName} debe contener solo dígitos.", paramName);
        return c;
    }

    public override string ToString() => AddressLine;
}
