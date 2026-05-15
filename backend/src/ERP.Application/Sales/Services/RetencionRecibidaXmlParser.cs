using System.Xml.Linq;

namespace ERP.Application.Sales.Services;

/// <summary>Extracción mínima de datos desde XML de comprobante de retención (SRI / Supplier).</summary>
public static class RetentionRecibidaXmlParser
{
    public static bool TryParse(string xml, out string claveAcceso, out DateTime? fechaEmision, out decimal  totalRetained)
    {
        claveAcceso   = string.Empty;
        fechaEmision = null;
        totalRetained = 0m;
        try
        {
            var doc = XDocument.Parse(xml);
            var root = doc.Root;
            if (root is null) return false;

            claveAcceso = FindFirstDescendantValue(root, "claveAcceso") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(claveAcceso) || claveAcceso.Length < 40)
                return false;

            var fechaStr = FindFirstDescendantValue(root, "fechaEmision");
            if (DateTime.TryParse(fechaStr, out var fd))
                fechaEmision = fd;
            else if (fechaStr?.Length == 10 && fechaStr.Contains('/'))
            {
                var p = fechaStr.Split('/');
                if (p.Length == 3
                    && int.TryParse(p[0], out var d)
                    && int.TryParse(p[1], out var m)
                    && int.TryParse(p[2], out var y))
                    fechaEmision = new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc);
            }

            decimal suma = 0m;
            foreach (var el in root.Descendants().Where(e => e.Name.LocalName == "valorRetenido"))
            {
                if (decimal.TryParse(el.Value.Trim().Replace(",", "."),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var v))
                    suma += v;
            }

            if (suma == 0m)
            {
                var cod = FindFirstDescendantValue(root, "codDocSustento");
                _ = cod;
                if (decimal.TryParse(
                        FindFirstDescendantValue(root, "valorRetenido") ?? "0",
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var vr))
                    suma = vr;
            }

            totalRetained = suma;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindFirstDescendantValue(XElement root, string localName)
        => root.Descendants().FirstOrDefault(e => e.Name.LocalName == localName)?.Value.Trim();
}
