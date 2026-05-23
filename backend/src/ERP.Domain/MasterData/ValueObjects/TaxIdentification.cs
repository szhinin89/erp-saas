namespace ERP.Domain.MasterData.ValueObjects;

/// <summary>
/// Value Object: identificación fiscal/personal unificada del BusinessPartner.
///
/// Reemplaza la duplicación entre:
///   - CustomerIdentification (ERP.Domain.Modules.Sales.ValueObjects)
///   - Supplier.Ruc + Supplier.PersonType (ERP.Domain.Modules.Purchasing.Entities)
///
/// Tipos soportados (alineados con SRI Ecuador):
///   RUC      — Registro Único de Contribuyentes (13 dígitos)
///   CI       — Cédula de Identidad (10 dígitos)
///   PASSPORT — Pasaporte (extranjeros)
///   OTHER    — Otros documentos (consumidor final, etc.)
/// </summary>
public sealed record TaxIdentification
{
    public const int TypeMaxLen   = 20;
    public const int NumberMaxLen = 32;

    public const string TypeRuc      = "RUC";
    public const string TypeCi       = "CI";
    public const string TypePassport = "PASSPORT";
    public const string TypeOther    = "OTHER";

    private static readonly HashSet<string> _validTypes =
        [TypeRuc, TypeCi, TypePassport, TypeOther];

    public string Type   { get; }
    public string Number { get; }

    private TaxIdentification(string type, string number)
    {
        Type   = type;
        Number = number;
    }

    /// <summary>
    /// Crea una identificación validada.
    /// Lanza <see cref="ArgumentException"/> si el tipo no es válido o el número está vacío.
    /// </summary>
    public static TaxIdentification Create(string type, string number)
    {
        var normalizedType   = NormalizeType(type);
        var normalizedNumber = NormalizeNumber(number);
        return new TaxIdentification(normalizedType, normalizedNumber);
    }

    public static string NormalizeType(string type)
    {
        var t = (type ?? string.Empty).Trim().ToUpperInvariant();
        // Aliases comunes
        t = t switch
        {
            "CEDULA" or "CÉDULA" or "CEDULA_IDENTIDAD" => TypeCi,
            "PASAPORTE"                                 => TypePassport,
            _                                           => t,
        };
        if (!_validTypes.Contains(t))
            throw new ArgumentException(
                $"Tipo de identificación '{type}' no válido. Use: {string.Join(", ", _validTypes)}.",
                nameof(type));
        return t;
    }

    public static string NormalizeNumber(string number)
    {
        var n = (number ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(n))
            throw new ArgumentException("El número de identificación es obligatorio.", nameof(number));
        if (n.Length > NumberMaxLen)
            throw new ArgumentException(
                $"El número de identificación no puede superar {NumberMaxLen} caracteres.", nameof(number));
        return n;
    }

    /// <summary>Código SRI del tipo de documento (para XML comprobantes electrónicos).</summary>
    public string SriCode => Type switch
    {
        TypeRuc      => "04",
        TypeCi       => "05",
        TypePassport => "06",
        TypeOther    => "08",
        _            => "07",
    };

    public override string ToString() => $"{Type}:{Number}";
}
