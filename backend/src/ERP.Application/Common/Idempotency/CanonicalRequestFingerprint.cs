using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ERP.Application.Common.Idempotency;

/// <summary>
/// ZH-FINANCIAL-COMMAND-IDEMPOTENCY-01 — representación canónica y huella de la intención de un
/// comando (extraída sin cambios de <c>CashFundingPaymentSnapshot</c>, ZH-CASH-FUNDING-REQUEST-
/// FOUNDATION-02E-B, que la sigue usando para sus snapshots persistidos). Orden de propiedades fijo
/// (el del record), camelCase, sin espacios y decimales sin ceros de relleno: el mismo request
/// produce siempre los mismos bytes, así que "misma intención" = misma huella.
/// <para>
/// Cada comando define su propio record canónico versionado (solo datos del usuario, nunca
/// contexto derivable como tenant/empresa/actor); cambiar su forma = nueva versión.
/// </para>
/// </summary>
public static class CanonicalRequestFingerprint
{
    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new CanonicalDecimalConverter() },
    };

    public static string Serialize<T>(T canonical) => JsonSerializer.Serialize(canonical, CanonicalOptions);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, CanonicalOptions);

    /// <summary>SHA-256 (hex en mayúsculas, 64 caracteres) de la representación canónica.</summary>
    public static string Compute<T>(T canonical) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(canonical))));

    /// <summary>Texto libre opcional normalizado: sin espacios de borde; vacío = ausente.</summary>
    public static string? NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Decimales canónicos: mismo valor ⇒ mismo texto (80, 80.0 y 80.00 → "80").</summary>
    private sealed class CanonicalDecimalConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.GetDecimal();

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteRawValue(value.ToString("0.############################", CultureInfo.InvariantCulture));
    }
}
