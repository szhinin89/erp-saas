namespace ERP.Domain.Modules.Ride.ValueObjects;

/// <summary>
/// Hash SHA-256 (64 caracteres hexadecimales) del XML autorizado — la pieza central de la
/// estrategia de cache (ADR-025 §14). Encapsula únicamente el formato: el cálculo del hash
/// pertenece a <c>IRideContentHasher</c> (Application), fuera de alcance de Domain.
/// </summary>
public sealed record RideContentHash
{
    public const int Length = 64;

    public string Value { get; }

    private RideContentHash(string value) => Value = value;

    public static RideContentHash Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("El hash del XML autorizado es obligatorio.", nameof(value));

        var trimmed = value.Trim().ToLowerInvariant();

        if (trimmed.Length != Length || !trimmed.All(Uri.IsHexDigit))
            throw new ArgumentException($"El hash del XML autorizado debe tener exactamente {Length} caracteres hexadecimales.", nameof(value));

        return new RideContentHash(trimmed);
    }

    public override string ToString() => Value;
}
