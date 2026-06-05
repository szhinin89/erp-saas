using ERP.Domain.Common.Validators;

namespace ERP.Domain.MasterData.ValueObjects;

/// <summary>
/// Value Object: identificación fiscal/personal del BusinessPartner.
/// Almacena directamente el código oficial SRI (tabla sri_id_type).
///   04 — RUC (Registro Único de Contribuyentes, exactamente 13 dígitos)
///   05 — Cédula de ciudadanía (exactamente 10 dígitos)
///   06 — Pasaporte (libre, max 20 chars)
///   07 — Consumidor Final (número 9999999999999 por convención)
///   08 — Identificación del exterior (libre, max 20 chars)
///   09 — Placa (libre, max 10 chars)
///
/// Validación ecuatoriana aplicada:
///   - RUC: algoritmo SRI (Módulo 10 / Módulo 11 según tipo de contribuyente)
///   - CI:  algoritmo Registro Civil (Módulo 10, provincia válida, 3.er dígito ≤ 6)
/// </summary>
public sealed record TaxIdentification
{
    public const int TypeMaxLen      = 5;
    public const int NumberMaxLen    = 32;

    public const string SriRuc             = "04";
    public const string SriCi              = "05";
    public const string SriPassport        = "06";
    public const string SriConsumidorFinal = "07";
    public const string SriExterior        = "08";
    public const string SriPlaca           = "09";

    // Número estándar para Consumidor Final (usado en facturas sin identificación)
    public const string ConsumidorFinalNumber = "9999999999999";

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

        // ── Validaciones específicas por tipo SRI ───────────────────────────
        switch (t)
        {
            case SriRuc:
                if (n.Length != 13)
                    throw new ArgumentException(
                        $"RUC ecuatoriano debe tener exactamente 13 dígitos (recibido: {n.Length}).", nameof(number));
                if (!RucValidator.EsRucValido(n))
                    throw new ArgumentException(
                        $"RUC '{n}' inválido según el algoritmo del SRI. " +
                        "Verifique provincia (01-24/30), tipo de contribuyente y dígito verificador.", nameof(number));
                break;

            case SriCi:
                if (n.Length != 10)
                    throw new ArgumentException(
                        $"Cédula ecuatoriana debe tener exactamente 10 dígitos (recibido: {n.Length}).", nameof(number));
                if (!EcuadorIdValidator.EsCedulaValida(n))
                    throw new ArgumentException(
                        $"Cédula '{n}' inválida según el algoritmo del Registro Civil. " +
                        "Verifique provincia (01-24), 3.er dígito y dígito verificador.", nameof(number));
                break;

            case SriConsumidorFinal:
                // Consumidor Final puede usar el número estándar o cualquier referencia
                // No se valida dígito verificador — es una categoría especial SRI
                break;

            case SriPassport:
                if (n.Length < 3 || n.Length > 20)
                    throw new ArgumentException(
                        $"Pasaporte debe tener entre 3 y 20 caracteres (recibido: {n.Length}).", nameof(number));
                break;

            case SriExterior:
                if (n.Length < 3 || n.Length > 20)
                    throw new ArgumentException(
                        $"Identificación del exterior debe tener entre 3 y 20 caracteres (recibido: {n.Length}).",
                        nameof(number));
                break;

            case SriPlaca:
                if (n.Length < 6 || n.Length > 10)
                    throw new ArgumentException(
                        $"Placa debe tener entre 6 y 10 caracteres (recibido: {n.Length}).", nameof(number));
                break;
        }

        return new TaxIdentification(t, n);
    }

    public override string ToString() => $"{Type}:{Number}";
}
