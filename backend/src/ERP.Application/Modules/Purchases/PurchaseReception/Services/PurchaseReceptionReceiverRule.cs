using ERP.Domain.Common.Validators;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;

namespace ERP.Application.Modules.Purchases.PurchaseReception.Services;

/// <summary>Resultado de comparar <c>IDENTIFICACION_RECEPTOR</c> con el RUC de la empresa operativa.</summary>
public enum PurchaseReceptionReceiverMatch
{
    Match,
    Missing,
    Invalid,
    Different,
}

/// <summary>
/// PURCHASES-RECEPTION-RECEIVER-RUC-01 (H1) — única regla de pertenencia de un comprobante recibido
/// a la empresa operativa. Coincide un RUC idéntico al de la empresa o, solo para persona natural, la
/// cédula válida (módulo 10, tercer dígito 0–5) cuyo RUC es exactamente esa cédula + <c>001</c>.
/// Reutiliza <see cref="RucValidator"/> y <see cref="EcuadorIdValidator"/>; nunca devuelve la
/// identificación ajena en los mensajes.
/// </summary>
public static class PurchaseReceptionReceiverRule
{
    private const int MaxListedLines = 10;
    private const string NaturalPersonEstablishment = "001";

    /// <summary><c>true</c> si la empresa tiene un RUC definitivo y estructuralmente válido.</summary>
    public static bool IsCompanyRucUsable(string? companyRuc, bool isTemporary) =>
        !isTemporary && companyRuc is not null && RucValidator.EsRucValido(companyRuc);

    public static PurchaseReceptionReceiverMatch Evaluate(string companyRuc, string? receiver)
    {
        if (string.IsNullOrWhiteSpace(receiver))
            return PurchaseReceptionReceiverMatch.Missing;

        var value = receiver.Trim();
        if (!value.All(char.IsAsciiDigit))
            return PurchaseReceptionReceiverMatch.Invalid;

        var ruc = companyRuc.Trim();
        return value.Length switch
        {
            13 when !RucValidator.EsRucValido(value) => PurchaseReceptionReceiverMatch.Invalid,
            13 => value == ruc
                ? PurchaseReceptionReceiverMatch.Match
                : PurchaseReceptionReceiverMatch.Different,
            10 when !EcuadorIdValidator.EsCedulaValida(value) => PurchaseReceptionReceiverMatch.Invalid,
            10 => IsNaturalPersonEquivalent(value, ruc)
                ? PurchaseReceptionReceiverMatch.Match
                : PurchaseReceptionReceiverMatch.Different,
            _ => PurchaseReceptionReceiverMatch.Invalid,
        };
    }

    /// <summary>
    /// Evalúa todas las filas y devuelve el mensaje de rechazo del archivo completo, o <c>null</c> si
    /// todas pertenecen a la empresa. Solo informa números de línea por categoría.
    /// </summary>
    public static string? FindRejection(
        string companyRuc,
        IReadOnlyList<PurchaseReceptionRecord> records
    )
    {
        var byMatch = records
            .Select(r => (LineNumber: r.SourceLineNumber, Match: Evaluate(companyRuc, r.ReceiverIdentification)))
            .Where(x => x.Match != PurchaseReceptionReceiverMatch.Match)
            .GroupBy(x => x.Match)
            .ToDictionary(g => g.Key, g => g.Select(x => x.LineNumber).Order().ToList());
        if (byMatch.Count == 0)
            return null;

        var parts = new List<string>(3);
        AddPart(parts, byMatch, PurchaseReceptionReceiverMatch.Different,
            "no corresponden a la empresa activa");
        AddPart(parts, byMatch, PurchaseReceptionReceiverMatch.Missing,
            "no indican la identificación del receptor");
        AddPart(parts, byMatch, PurchaseReceptionReceiverMatch.Invalid,
            "tienen una identificación del receptor inválida");

        return $"No se importó ningún documento: hay comprobantes que {string.Join("; ", parts)}.";
    }

    private static bool IsNaturalPersonEquivalent(string cedula, string companyRuc) =>
        cedula[2] is >= '0' and <= '5'
        && companyRuc == cedula + NaturalPersonEstablishment;

    private static void AddPart(
        List<string> parts,
        Dictionary<PurchaseReceptionReceiverMatch, List<int>> byMatch,
        PurchaseReceptionReceiverMatch match,
        string description
    )
    {
        if (!byMatch.TryGetValue(match, out var lines))
            return;
        var listed = string.Join(", ", lines.Take(MaxListedLines));
        var more = lines.Count > MaxListedLines ? $" y {lines.Count - MaxListedLines} más" : "";
        parts.Add($"{description} (líneas {listed}{more})");
    }
}
