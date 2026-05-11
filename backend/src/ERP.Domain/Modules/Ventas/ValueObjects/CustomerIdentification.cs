namespace ERP.Domain.Modules.Ventas.ValueObjects;

/// <summary>
/// Identificación tributaria/personal válida para clientes.
/// Regla: solo se admiten RUC y CI.
/// </summary>
public sealed record CustomerIdentification
{
    public const int MaxLen = 32;

    public string Type { get; }
    public string Number { get; }

    private CustomerIdentification(string type, string number)
    {
        Type = type;
        Number = number;
    }

    public static CustomerIdentification Create(string type, string number)
    {
        var normalizedType = NormalizeType(type);
        var normalizedNumber = NormalizeNumber(number);
        return new CustomerIdentification(normalizedType, normalizedNumber);
    }

    public static string NormalizeType(string type)
    {
        var normalized = (type ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "RUC" => normalized,
            "CI" => normalized,
            "CEDULA" or "CÉDULA" => "CI",
            _ => throw new ArgumentException("Tipo de identificación no válido (use RUC o CI).", nameof(type)),
        };
    }

    public static string NormalizeNumber(string number)
    {
        var normalized = (number ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("El número de identificación es obligatorio.", nameof(number));
        if (normalized.Length > MaxLen)
            throw new ArgumentException($"El documento no puede superar {MaxLen} caracteres.", nameof(number));
        return normalized;
    }
}
