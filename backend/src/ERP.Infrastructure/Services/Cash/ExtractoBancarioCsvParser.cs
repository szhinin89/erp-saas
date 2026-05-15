using System.Globalization;
using System.Text;
using ERP.Domain.Modules.Cash;

namespace ERP.Infrastructure.Services.Cash;

/// <summary>
/// CSV con cabecera flexible (es/en). Columnas soportadas:
/// fecha | descripcion | debito | credito | referencia (opcional),
/// o fecha | descripcion | monto | tipo (Debito/Credito).
/// </summary>
public sealed class ExtractoBancarioCsvParser : IExtractoParser
{
    public async Task<IReadOnlyList<MovimientoExtractoParseRow>> ParseAsync(Stream stream, CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<MovimientoExtractoParseRow>();

        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 2)
            return Array.Empty<MovimientoExtractoParseRow>();

        var header = SplitCsv(lines[0]);
        var idx = BuildHeaderIndex(header);
        var rows = new List<MovimientoExtractoParseRow>();

        for (var i = 1; i < lines.Length; i++)
        {
            var cols = SplitCsv(lines[i]);
            if (cols.Count == 0 || cols.All(string.IsNullOrWhiteSpace))
                continue;

            var fecha = ParseFecha(GetCol(cols, idx, "fecha", "date"));
            var desc  = GetCol(cols, idx, "descripcion", "description", "detalle", "concepto");
            var refe  = TryGetCol(cols, idx, "referencia", "reference", "numero", "documento");

            if (idx.TryGetValue("monto", out var im) || idx.TryGetValue("amount", out im))
            {
                var monto = ParseDecimal(GetCol(cols, idx, "monto", "amount"));
                var tipo  = NormalizeTipo(GetCol(cols, idx, "tipo", "type"));
                rows.Add(new MovimientoExtractoParseRow(fecha, desc, monto, tipo, refe));
                continue;
            }

            var debito  = ParseDecimal(TryGetCol(cols, idx, "debito", "debit", "egreso") ?? "0");
            var credito = ParseDecimal(TryGetCol(cols, idx, "credito", "credit", "ingreso") ?? "0");
            if (debito > 0 && credito > 0)
                throw new InvalidOperationException($"Línea {i + 1}: no puede haber débito y crédito simultáneos.");
            if (debito > 0)
                rows.Add(new MovimientoExtractoParseRow(fecha, desc, debito, "Debito", refe));
            else if (credito > 0)
                rows.Add(new MovimientoExtractoParseRow(fecha, desc, credito, "Credito", refe));
        }

        return rows;
    }

    private static Dictionary<string, int> BuildHeaderIndex(IReadOnlyList<string> header)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++)
        {
            var key = CanonicalHeader(header[i]);
            if (key.Length > 0)
                map[key] = i;
        }
        return map;
    }

    /// <summary>Quita tildes para emparejar cabeceras en español (p. ej. Descripción → descripcion).</summary>
    private static string CanonicalHeader(string raw)
    {
        var s = raw.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString();
    }

    private static IReadOnlyList<string> SplitCsv(string line)
    {
        var sep = line.Contains(';') && !line.Contains(',') ? ';' : ',';
        return line.Split(sep, StringSplitOptions.TrimEntries).ToList();
    }

    private static string GetCol(IReadOnlyList<string> cols, IReadOnlyDictionary<string, int> idx, params string[] keys)
    {
        var v = TryGetCol(cols, idx, keys);
        if (string.IsNullOrWhiteSpace(v))
            throw new InvalidOperationException($"Falta columna obligatoria: {keys[0]}");
        return v.Trim();
    }

    private static string? TryGetCol(IReadOnlyList<string> cols, IReadOnlyDictionary<string, int> idx, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (idx.TryGetValue(k.ToLowerInvariant(), out var i) && i < cols.Count)
                return cols[i];
        }
        return null;
    }

    private static DateTime ParseFecha(string s)
    {
        var formats = new[]
        {
            "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy/MM/dd",
        };
        if (DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return DateTime.SpecifyKind(d, DateTimeKind.Utc);
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out d))
            return DateTime.SpecifyKind(d.ToUniversalTime(), DateTimeKind.Utc);
        throw new InvalidOperationException($"Fecha no válida: '{s}'");
    }

    private static decimal ParseDecimal(string s)
    {
        s = s.Trim().Replace("%", "");
        if (string.IsNullOrWhiteSpace(s))
            return 0;
        if (decimal.TryParse(s, NumberStyles.Number, new CultureInfo("es-EC"), out var d))
            return Math.Abs(d);
        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out d))
            return Math.Abs(d);
        throw new InvalidOperationException($"Importe no válido: '{s}'");
    }

    private static string NormalizeTipo(string tipo)
    {
        var t = tipo.Trim();
        if (t.Equals("Debito", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("D", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("Debit", StringComparison.OrdinalIgnoreCase))
            return "Debito";
        if (t.Equals("Credito", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("C", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("Credit", StringComparison.OrdinalIgnoreCase))
            return "Credito";
        throw new InvalidOperationException($"Tipo no válido (use Debito o Credito): '{tipo}'");
    }
}
