namespace ERP.Domain.Modules.ElectronicDocuments.ValueObjects;

/// <summary>
/// Clave de acceso SRI (49 dígitos numéricos). Encapsula únicamente el formato —
/// el algoritmo de construcción (dígito verificador módulo 11, etc.) pertenece a
/// la fase que implemente el XML Builder, fuera de alcance aquí.
/// </summary>
public sealed record AccessKey
{
    public const int Length = 49;

    public string Value { get; }

    private AccessKey(string value) => Value = value;

    public static AccessKey Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("La clave de acceso es obligatoria.", nameof(value));

        var trimmed = value.Trim();

        if (trimmed.Length != Length || !trimmed.All(char.IsDigit))
            throw new ArgumentException(
                $"La clave de acceso debe tener exactamente {Length} dígitos numéricos.",
                nameof(value)
            );

        return new AccessKey(trimmed);
    }

    public override string ToString() => Value;
}
