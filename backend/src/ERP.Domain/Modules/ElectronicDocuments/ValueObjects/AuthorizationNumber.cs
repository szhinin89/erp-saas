namespace ERP.Domain.Modules.ElectronicDocuments.ValueObjects;

/// <summary>
/// Número de autorización devuelto por el SRI. En el esquema offline (el único que implementa
/// este ERP) SIEMPRE coincide con la clave de acceso del comprobante autorizado — ficha técnica,
/// sección 5.9: "la clave de acceso generada por el emisor se constituye en el número de
/// autorización del mismo", confirmado también por el ejemplo oficial de la sección 7.2.3
/// (&lt;numeroAutorizacion&gt; idéntico a &lt;claveAccesoConsultada&gt;). Por eso comparte
/// exactamente el mismo formato que <see cref="AccessKey"/> — 49 dígitos numéricos exactos, no
/// solo "hasta 49 caracteres" — aunque se sigue modelando como VO independiente porque su origen
/// y su momento de asignación en el ciclo de vida del documento son distintos a los de AccessKey
/// (AUTH-01, auditoría SRI Fase 2).
/// </summary>
public sealed record AuthorizationNumber
{
    public const int MaxLength = 49;

    public string Value { get; }

    private AuthorizationNumber(string value) => Value = value;

    public static AuthorizationNumber Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("El número de autorización es obligatorio.", nameof(value));

        var trimmed = value.Trim();

        if (trimmed.Length != MaxLength || !trimmed.All(char.IsDigit))
            throw new ArgumentException(
                $"El número de autorización debe tener exactamente {MaxLength} dígitos numéricos.",
                nameof(value)
            );

        return new AuthorizationNumber(trimmed);
    }

    public override string ToString() => Value;
}
