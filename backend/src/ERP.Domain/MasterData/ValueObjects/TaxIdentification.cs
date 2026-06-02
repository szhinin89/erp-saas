namespace ERP.Domain.MasterData.ValueObjects;

/// <summary>
/// Value Object: identificación fiscal/personal del BusinessPartner.
/// Almacena directamente el código oficial SRI (tabla sri_id_type).
///   04 — RUC (Registro Único de Contribuyentes, 13 dígitos)
///   05 — Cédula de ciudadanía (10 dígitos)
///   06 — Pasaporte
///   07 — Consumidor Final
///   08 — Identificación del exterior
///   09 — Placa
/// </summary>
public sealed record TaxIdentification
{
    public const int TypeMaxLen   = 5;
    public const int NumberMaxLen = 32;

    public const string SriRuc             = "04";
    public const string SriCi              = "05";
    public const string SriPassport        = "06";
    public const string SriConsumidorFinal = "07";
    public const string SriExterior        = "08";
    public const string SriPlaca           = "09";

    private static readonly HashSet<string> _validTypes =
        [SriRuc, SriCi, SriPassport, SriConsumidorFinal, SriExterior, SriPlaca];

    public string Type   { get; }
    public string Number { get; }

    private TaxIdentification(string type, string number)
    {
        Type   = type;
        Number = number;
    }

    public static TaxIdentification Create(string type, string number)
    {
        var t = (type ?? string.Empty).Trim();
        if (!_validTypes.Contains(t))
            throw new ArgumentException(
                $"Tipo de identificación '{type}' no válido. Códigos SRI aceptados: {string.Join(", ", _validTypes)}.",
                nameof(type));

        var n = (number ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(n))
            throw new ArgumentException("El número de identificación es obligatorio.", nameof(number));
        if (n.Length > NumberMaxLen)
            throw new ArgumentException(
                $"El número de identificación no puede superar {NumberMaxLen} caracteres.", nameof(number));

        return new TaxIdentification(t, n);
    }

    /// <summary>El código SRI es el tipo — se mantiene por compatibilidad con generación XML.</summary>
    public string SriCode => Type;

    public override string ToString() => $"{Type}:{Number}";
}
