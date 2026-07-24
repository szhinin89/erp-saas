namespace ERP.Domain.Modules.Ride.ValueObjects;

/// <summary>
/// Clave de acceso SRI (49 dígitos numéricos) tal como aparece en el XML autorizado. Ride
/// nunca la deriva ni la recalcula (ADR-025 §3/§17) — solo la envuelve. No referencia el
/// <c>AccessKey</c> de <c>ElectronicDocuments</c> (ADR-025 §2: Ride no conoce ese módulo);
/// la validación de formato se reimplementa aquí sobre el mismo hecho objetivo del esquema SRI.
/// </summary>
public sealed record RideAccessKey
{
    public const int Length = 49;

    public string Value { get; }

    private RideAccessKey(string value) => Value = value;

    public static RideAccessKey Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("La clave de acceso es obligatoria.", nameof(value));

        var trimmed = value.Trim();

        if (trimmed.Length != Length || !trimmed.All(char.IsDigit))
            throw new ArgumentException($"La clave de acceso debe tener exactamente {Length} dígitos numéricos.", nameof(value));

        return new RideAccessKey(trimmed);
    }

    public override string ToString() => Value;
}
