using System.Globalization;
using System.Xml.Linq;
using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Parsea comprobantes electrónicos del SRI Ecuador.
///
/// Formato soportado: XML versión SRI 1.0.0 / 2.0.0 (sin namespace en elementos
/// de negocio). La firma digital (dsig) se ignora — solo se procesan:
///   - &lt;infoTributaria&gt; → datos del emisor y clave de acceso
///   - &lt;infoFactura&gt;   → fechas y totales
///   - &lt;detalles&gt;      → líneas de ítem
///
/// ⚠️ El formato SRI NO usa el namespace UBL (cbc:, cac:); los elementos
///    son simples como &lt;ruc&gt;, &lt;importeTotal&gt;, &lt;descripcion&gt;, etc.
/// </summary>
public sealed class SriFacturaParser : IXmlFacturaParser
{
    private static readonly XNamespace DsigNs = "http://www.w3.org/2000/09/xmldsig#";

    public Task<FacturaParseResult> ParseAsync(Stream xmlStream, CancellationToken ct = default)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Load(xmlStream, LoadOptions.None);
        }
        catch (Exception ex)
        {
            throw new XmlParseException("El archivo no es un XML válido.", ex);
        }

        // El elemento raíz puede ser <factura>, <notaCredito> u otro comprobante.
        // Algunos sistemas envuelven el XML en <autorizacion><comprobante>CDATA</comprobante>.
        var root = ResolveRoot(doc);

        var infoTrib   = RequireElement(root, "infoTributaria");
        var infoFact   = RequireElement(root, "infoFactura");
        var detallesEl = RequireElement(root, "detalles");

        // ── infoTributaria ────────────────────────────────────────────────
        var accessKey     = RequireTextAny(infoTrib, "accessKey", "claveAcceso");
        var ruc             = RequireText(infoTrib, "ruc");
        var razonSocial     = RequireText(infoTrib, "razonSocial");
        var estab           = RequireText(infoTrib, "estab");
        var ptoEmi          = RequireText(infoTrib, "ptoEmi");
        var secuencial      = RequireText(infoTrib, "secuencial");
        var numeroFactura   = $"{estab}-{ptoEmi}-{secuencial}";

        ValidarClaveAcceso(accessKey);

        // ── infoFactura ───────────────────────────────────────────────────
        var fechaTexto   = RequireTextAny(infoFact, "issueDate", "fechaEmision");
        var issueDate = ParseFecha(fechaTexto);

        var subtotal     = ParseDecimalAny(infoFact, "totalSinImpuestos");
        var total        = ParseDecimalAny(infoFact, "importeTotal");

        // Suma de todos los impuestos del encabezado (IVA + ICE + otros)
        var impuesto = SumImpuestosEncabezado(infoFact);

        // ── detalles ──────────────────────────────────────────────────────
        var items = detallesEl.Elements("detalle").Select(ParseDetalle).ToList();

        if (items.Count == 0)
            throw new XmlParseException("El comprobante no contiene ningún detalle (<detalle>).");

        return Task.FromResult(new FacturaParseResult(
            AccessKey:         accessKey,
            InvoiceNumber:     numeroFactura,
            IssueDate:         issueDate,
            SupplierRuc:       ruc,
            SupplierLegalName: razonSocial,
            Subtotal:          subtotal,
            VatTotal:          impuesto,
            Total:             total,
            Items:             items));
    }

    public Task<SupplierNoteParseResult> ParseSupplierNoteAsync(Stream xmlStream, CancellationToken ct = default)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Load(xmlStream, LoadOptions.None);
        }
        catch (Exception ex)
        {
            throw new XmlParseException("El archivo no es un XML válido.", ex);
        }

        var root = ResolveRoot(doc);
        var rootName = root.Name.LocalName;
        string tipoNota;
        if (string.Equals(rootName, "notaCredito", StringComparison.OrdinalIgnoreCase))
            tipoNota = "CREDIT";
        else if (string.Equals(rootName, "notaDebito", StringComparison.OrdinalIgnoreCase))
            tipoNota = "DEBIT";
        else
            throw new XmlParseException(
                $"Se esperaba <notaCredito> o <notaDebito> como comprobante; raíz: <{rootName}>.");

        var infoNotaName = tipoNota == "CREDIT" ? "infoNotaCredito" : "infoNotaDebito";
        var infoTrib   = RequireElement(root, "infoTributaria");
        var infoNota   = RequireElement(root, infoNotaName);
        var detallesEl = RequireElement(root, "detalles");

        var accessKey = RequireTextAny(infoTrib, "accessKey", "claveAcceso");
        var ruc         = RequireText(infoTrib, "ruc");
        var razonSocial = RequireText(infoTrib, "razonSocial");
        var estab       = RequireText(infoTrib, "estab");
        var ptoEmi      = RequireText(infoTrib, "ptoEmi");
        var secuencial  = RequireText(infoTrib, "secuencial");
        var numeroNota  = $"{estab}-{ptoEmi}-{secuencial}";
        ValidarClaveAcceso(accessKey);

        var fechaTexto   = RequireTextAny(infoNota, "issueDate", "fechaEmision");
        var issueDate = ParseFecha(fechaTexto);
        var motivo       = infoNota.Element("motivo")?.Value.Trim() ?? string.Empty;

        var subtotal = ParseDecimal(infoNota, "totalSinImpuestos");
        var impuesto = infoNota
            .Element("totalConImpuestos")
            ?.Elements("totalVat")
            .Sum(t => ParseDecimalText(t.Element("valor")?.Value ?? "0")) ?? 0m;

        var total =
            TryParseDecimal(root.Element("importeTotal"))
            ?? TryParseDecimal(infoNota.Element("importeTotal"))
            ?? TryParseDecimal(infoNota.Element("valorModificacion"))
            ?? throw new XmlParseException(
                "No se encontró importeTotal ni valorModificacion en la nota.");

        var items = detallesEl.Elements("detalle").Select(ParseDetalleNota).ToList();
        if (items.Count == 0)
            throw new XmlParseException("El comprobante no contiene ningún detalle (<detalle>).");

        return Task.FromResult(new SupplierNoteParseResult(
            NoteType:          tipoNota,
            Reason:            motivo,
            AccessKey:         accessKey,
            EstabCode:         estab,
            EmPointCode:       ptoEmi,
            Sequential:        secuencial,
            NoteNumber:        numeroNota,
            IssueDate:         issueDate,
            SupplierRuc:       ruc,
            SupplierLegalName: razonSocial,
            Subtotal:          subtotal,
            VatTotal:          impuesto,
            Total:             total,
            Items:             items));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Resuelve el elemento raíz del comprobante. Soporta:
    /// 1. XML directo con raíz &lt;factura&gt; (o notaCredito, etc.)
    /// 2. Respuesta de autorización: &lt;autorizacion&gt; → &lt;comprobante&gt; con CDATA.
    /// </summary>
    private static XElement ResolveRoot(XDocument doc)
    {
        var root = doc.Root
            ?? throw new XmlParseException("El XML no tiene elemento raíz.");

        // Formato respuesta SRI: <autorizacion><comprobante>...</comprobante></autorizacion>
        if (root.Name.LocalName == "autorizacion" || root.Name.LocalName == "autorizaciones")
        {
            var comprobante = root.Descendants("comprobante").FirstOrDefault()
                ?? throw new XmlParseException(
                    "Formato de respuesta SRI: se esperaba el elemento <comprobante>.");

            var cdata = comprobante.Value.Trim();
            if (string.IsNullOrEmpty(cdata))
                throw new XmlParseException("El elemento <comprobante> está vacío.");

            try
            {
                return XDocument.Parse(cdata).Root
                    ?? throw new XmlParseException("El CDATA del comprobante no tiene elemento raíz.");
            }
            catch (XmlParseException) { throw; }
            catch (Exception ex)
            {
                throw new XmlParseException("No se pudo parsear el XML dentro del CDATA <comprobante>.", ex);
            }
        }

        return root;
    }

    private static XElement RequireElement(XElement parent, string name)
    {
        var el = parent.Element(name)
            ?? parent.Elements().FirstOrDefault(e => e.Name.LocalName == name);

        return el ?? throw new XmlParseException(
            $"Nodo crítico ausente: <{name}> no encontrado dentro de <{parent.Name.LocalName}>.");
    }

    private static string RequireTextAny(XElement parent, params string[] names)
    {
        foreach (var name in names)
        {
            var el = parent.Element(name)
                ?? parent.Elements().FirstOrDefault(e => e.Name.LocalName == name);
            if (el is null)
                continue;
            var val = el.Value.Trim();
            if (!string.IsNullOrEmpty(val))
                return val;
        }

        throw new XmlParseException(
            $"Nodo crítico ausente: se esperaba uno de [{string.Join(", ", names)}] dentro de <{parent.Name.LocalName}>.");
    }

    private static decimal ParseDecimalAny(XElement parent, params string[] names)
    {
        foreach (var name in names)
        {
            var el = parent.Element(name)
                ?? parent.Elements().FirstOrDefault(e => e.Name.LocalName == name);
            if (el is null)
                continue;
            return ParseDecimalText(el.Value);
        }

        throw new XmlParseException(
            $"Nodo numérico ausente: se esperaba uno de [{string.Join(", ", names)}] dentro de <{parent.Name.LocalName}>.");
    }

    private static decimal SumImpuestosEncabezado(XElement infoFact)
    {
        var totalConImpuestos = infoFact.Element("totalConImpuestos");
        if (totalConImpuestos is null)
            return 0m;

        var impuestoNodes = totalConImpuestos.Elements("totalImpuesto")
            .Concat(totalConImpuestos.Elements("totalVat"));
        return impuestoNodes.Sum(t => ParseDecimalText(t.Element("valor")?.Value ?? "0"));
    }

    private static string RequireText(XElement parent, string name)
    {
        var el = RequireElement(parent, name);
        var val = el.Value.Trim();
        if (string.IsNullOrEmpty(val))
            throw new XmlParseException($"El nodo <{name}> está vacío (valor requerido).");
        return val;
    }

    private static decimal ParseDecimal(XElement parent, string name)
    {
        var el = RequireElement(parent, name);
        return ParseDecimalText(el.Value);
    }

    private static decimal ParseDecimalText(string raw)
    {
        if (decimal.TryParse(raw.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return d;
        throw new XmlParseException($"No se pudo convertir '{raw}' a número decimal.");
    }

    private static DateTime ParseFecha(string raw)
    {
        // SRI usa DD/MM/YYYY
        if (DateTime.TryParseExact(raw.Trim(), "dd/MM/yyyy",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;

        // Algunos comprobantes usan DD-MM-YYYY o YYYY-MM-DD
        if (DateTime.TryParse(raw.Trim(), CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var dt2))
            return dt2;

        throw new XmlParseException(
            $"Formato de fecha no reconocido: '{raw}'. Se esperaba DD/MM/YYYY.");
    }

    private static void ValidarClaveAcceso(string clave)
    {
        if (clave.Length != 49 || !clave.All(char.IsDigit))
            throw new XmlParseException(
                $"Clave de acceso inválida: debe tener 49 dígitos numéricos (recibido: '{clave}').");

        // Verificar dígito de control (módulo 11 pesos 2-7 cíclico de derecha a izquierda)
        var weights = new[] { 2, 3, 4, 5, 6, 7 };
        int sum = 0;
        for (int i = 47; i >= 0; i--)
            sum += (clave[i] - '0') * weights[(47 - i) % 6];

        int residuo     = sum % 11;
        int verificador = residuo == 0 ? 0 : residuo == 1 ? 1 : 11 - residuo;
        int ultimo      = clave[48] - '0';

        if (verificador != ultimo)
            throw new XmlParseException(
                $"Clave de acceso con dígito verificador inválido " +
                $"(esperado {verificador}, recibido {ultimo}).");
    }

    private static ItemFactura ParseDetalle(XElement det)
    {
        var codigo = det.Element("codigoPrincipal")?.Value.Trim() ?? string.Empty;
        var desc   = RequireText(det, "descripcion");

        var cantidad       = ParseDecimalText(RequireText(det, "cantidad"));
        var precioUnitario = ParseDecimalText(RequireText(det, "precioUnitario"));
        var descuento      = ParseDecimalText(det.Element("descuento")?.Value.Trim() ?? "0");
        var subtotal       = ParseDecimalText(RequireText(det, "precioTotalSinImpuesto"));

        return new ItemFactura(codigo, desc, cantidad, precioUnitario, descuento, subtotal);
    }

    private static decimal? TryParseDecimal(XElement? el)
    {
        if (el is null) return null;
        var v = el.Value.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        return ParseDecimalText(v);
    }

    private static SupplierNoteItem ParseDetalleNota(XElement det)
    {
        var codigo = det.Element("codigoPrincipal")?.Value.Trim() ?? string.Empty;
        var desc   = RequireText(det, "descripcion");

        var cantidad       = ParseDecimalText(RequireText(det, "cantidad"));
        var precioUnitario = ParseDecimalText(RequireText(det, "precioUnitario"));
        var descuento      = ParseDecimalText(det.Element("descuento")?.Value.Trim() ?? "0");
        var subtotal       = ParseDecimalText(RequireText(det, "precioTotalSinImpuesto"));

        var impuesto = det.Element("impuestos")
            ?.Elements("impuesto")
            .Sum(i => ParseDecimalText(i.Element("valor")?.Value ?? "0")) ?? 0m;

        var total = subtotal + impuesto;
        return new SupplierNoteItem(
            codigo, desc, cantidad, precioUnitario, descuento, subtotal, impuesto, total);
    }
}
