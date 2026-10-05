namespace ERP.Domain.Modules.Communications.ValueObjects;

/// <summary>
/// ZH-COMMUNICATIONS-CONTRACT-01 — origen de negocio de una comunicación (trazabilidad,
/// idempotencia, monitor y contributors futuros). <see cref="Id"/> identifica la entidad de origen
/// en su módulo; no se exige que sea FK (p. ej. un token de recuperación).
/// Ejemplos: Sales / SalesInvoice / {id}; Authentication / PasswordReset / {id}.
/// </summary>
public sealed record CommunicationSource
{
    public const int ModuleMaxLen = 50;
    public const int TypeMaxLen = 100;

    public CommunicationSource(string module, string type, Guid id)
    {
        Module = Required(module, ModuleMaxLen, nameof(module));
        Type = Required(type, TypeMaxLen, nameof(type));
        if (id == Guid.Empty)
            throw new ArgumentException("El origen de la comunicación requiere Id.", nameof(id));
        Id = id;
    }

    public string Module { get; }
    public string Type { get; }
    public Guid Id { get; }

    private static string Required(string value, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("El valor es obligatorio.", paramName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength || normalized.Contains('|'))
            throw new ArgumentException(
                $"El valor no puede superar {maxLength} caracteres ni contener '|'.",
                paramName
            );
        return normalized;
    }
}
