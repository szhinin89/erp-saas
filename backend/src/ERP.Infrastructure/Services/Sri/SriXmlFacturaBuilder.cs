using System.Text;
using System.Xml.Linq;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Infrastructure.Services.Sri;

/// <summary>
/// Construye el XML de comprobantes electrónicos según el XSD del SRI Ecuador v1.1.0.
/// Sin dependencias de infraestructura — función pura: entidades → string XML.
/// Reutilizable desde cualquier contexto (API, jobs, CLI, tests).
/// </summary>
public static class SriXmlFacturaBuilder
{
    // ── Constantes SRI ────────────────────────────────────────────────────────

    private const string FacturaVersion   = "1.1.0";
    private const string NotaVersion      = "1.0.0";
    private const string DefaultPayMethod = "01"; // efectivo
    private const string Currency         = "DOLAR";

    // ── API pública ───────────────────────────────────────────────────────────

    /// <summary>Genera el XML de una factura de venta (tipo 01).</summary>
    public static string BuildFactura(
        SalesBill            factura,
        List<SalesBillLine>  lineas,
        SriSettings          cfg)
    {
        var totalDescuento = 0m;
        var vatCode        = ResolveVatCode(lineas);
        var vatPct         = VatPorcentaje(vatCode);

        var detElements = lineas.Select(l =>
            BuildDetalleFactura(l, vatCode, vatPct, ref totalDescuento)).ToList();

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("factura",
                new XAttribute("id", "comprobante"),
                new XAttribute("version", FacturaVersion),

                BuildInfoTributaria(cfg, factura.AccessKey, factura.DocType,
                    factura.EstabCode, factura.EmPointCode, factura.Sequential),

                new XElement("infoFactura",
                    new XElement("fechaEmision",        factura.IssueDate.ToString("dd/MM/yyyy")),
                    new XElement("dirEstablecimiento",  cfg.MainAddress),
                    ContribEspecialElement(cfg),
                    new XElement("obligadoContabilidad", cfg.RequiresAccounting ? "SI" : "NO"),
                    new XElement("tipoIdentificacionComprador", IdTypeCode(factura.Cliente)),
                    new XElement("razonSocialComprador", factura.Cliente.LegalName),
                    new XElement("identificacionComprador", factura.Cliente.IdentificationNumber),
                    new XElement("direccionComprador",  factura.Cliente.AddressLine ?? ""),
                    new XElement("totalSinImpuestos",   F2(factura.Subtotal)),
                    new XElement("totalDescuento",      F2(totalDescuento)),
                    new XElement("totalConImpuestos",
                        BuildTotalImpuesto(vatCode, factura.Subtotal, factura.VatTotal)),
                    new XElement("propina",  "0.00"),
                    new XElement("importeTotal", F2(factura.Total)),
                    new XElement("moneda", Currency),
                    new XElement("pagos",
                        new XElement("pago",
                            new XElement("formaPago",   DefaultPayMethod),
                            new XElement("total",       F2(factura.Total)),
                            new XElement("plazo",       "0"),
                            new XElement("unidadTiempo","dias")))),

                new XElement("detalles", detElements),

                new XElement("infoAdicional",
                    new XElement("campoAdicional",
                        new XAttribute("nombre", "Email"),
                        factura.Cliente.Email ?? ""))));

        return ToUtf8String(doc);
    }

    /// <summary>Genera el XML de una nota de crédito (tipo 04) o débito (tipo 05).</summary>
    public static string BuildNotaCreditoDebito(
        SalesBill           factOrig,
        SalesNote           nota,
        List<SalesNoteLine> lineas,
        SriSettings         cfg)
    {
        var esCredito  = nota.NoteType.Equals("CREDITO", StringComparison.OrdinalIgnoreCase);
        var rootName   = esCredito ? "notaCredito" : "notaDebito";
        var infoName   = esCredito ? "infoNotaCredito" : "infoNotaDebito";
        var vatCode    = ResolveVatCodeNota(lineas);
        var vatPct     = VatPorcentaje(vatCode);
        var totalDesc  = 0m;

        var numDocSustento =
            $"{factOrig.EstabCode}-{factOrig.EmPointCode}-{Seq(factOrig.Sequential)}";

        var detalles = lineas.Select(l =>
            BuildDetalleNota(l, vatCode, vatPct, ref totalDesc)).ToList();

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(rootName,
                new XAttribute("id", "comprobante"),
                new XAttribute("version", NotaVersion),

                BuildInfoTributaria(cfg, nota.AccessKey, nota.DocType,
                    nota.EstabCode, nota.EmPointCode, nota.Sequential),

                new XElement(infoName,
                    new XElement("fechaEmision",           nota.IssueDate.ToString("dd/MM/yyyy")),
                    new XElement("dirEstablecimiento",     cfg.MainAddress),
                    new XElement("tipoIdentificacionComprador", IdTypeCode(factOrig.Cliente)),
                    new XElement("razonSocialComprador",   factOrig.Cliente.LegalName),
                    new XElement("identificacionComprador",factOrig.Cliente.IdentificationNumber),
                    ContribEspecialElement(cfg),
                    new XElement("obligadoContabilidad",   cfg.RequiresAccounting ? "SI" : "NO"),
                    new XElement("codDocModificado",       factOrig.DocType.Trim()),
                    new XElement("numDocModificado",       numDocSustento),
                    new XElement("fechaEmisionDocSustento",factOrig.IssueDate.ToString("dd/MM/yyyy")),
                    new XElement("numAutDocSustento",      factOrig.AuthNumber ?? "0"),
                    new XElement("motivo",                 nota.Reason),
                    new XElement("totalSinImpuestos",      F2(nota.Subtotal)),
                    new XElement("valorModificacion",      F2(nota.Total)),
                    new XElement("totalConImpuestos",
                        BuildTotalImpuesto(vatCode, nota.Subtotal, nota.VatTotal)),
                    new XElement("moneda", Currency)),

                new XElement("detalles", detalles)));

        return ToUtf8String(doc);
    }

    // ── Bloques reutilizables ─────────────────────────────────────────────────

    private static XElement BuildInfoTributaria(
        SriSettings cfg,
        string      claveAcceso,
        string      codDoc,
        string      estab,
        string      ptoEmi,
        string      secuencial)
        => new("infoTributaria",
            new XElement("ambiente",       cfg.Environment),
            new XElement("tipoEmision",    cfg.EmissionType),
            new XElement("razonSocial",    cfg.LegalName),
            new XElement("nombreComercial",cfg.TradeName ?? cfg.LegalName),
            new XElement("ruc",            cfg.Ruc),
            new XElement("claveAcceso",    claveAcceso),
            new XElement("codDoc",         codDoc.PadLeft(2, '0')),
            new XElement("estab",          estab.PadLeft(3, '0')),
            new XElement("ptoEmi",         ptoEmi.PadLeft(3, '0')),
            new XElement("secuencial",     Seq(secuencial)),
            new XElement("dirMatriz",      cfg.MainAddress));

    private static XElement BuildTotalImpuesto(string vatCode, decimal baseImp, decimal valor)
        => new XElement("totalImpuesto",
            new XElement("codigo",           "2"),
            new XElement("codigoPorcentaje", vatCode),
            new XElement("descuentoAdicional","0.00"),
            new XElement("baseImponible",    F2(baseImp)),
            new XElement("valor",            F2(valor)));

    private static XElement BuildDetalleFactura(
        SalesBillLine  l,
        string         vatCode,
        string         vatPct,
        ref decimal    totalDesc)
    {
        totalDesc += 0m; // descuentos futuros
        return new XElement("detalle",
            new XElement("codigoPrincipal",          l.ProductId.ToString()),
            new XElement("descripcion",              l.Description),
            new XElement("cantidad",                 F6(l.Quantity)),
            new XElement("precioUnitario",           F6(l.UnitPrice)),
            new XElement("descuento",                "0.00"),
            new XElement("precioTotalSinImpuesto",   F2(l.Subtotal)),
            new XElement("impuestos",
                BuildImpuestoLinea(vatCode, vatPct, l.Subtotal, l.VatTotal)));
    }

    private static XElement BuildDetalleNota(
        SalesNoteLine  l,
        string         vatCode,
        string         vatPct,
        ref decimal    totalDesc)
    {
        totalDesc += 0m;
        return new XElement("detalle",
            new XElement("codigoInterno",            l.ProductId.ToString()),
            new XElement("descripcion",              l.Description),
            new XElement("cantidad",                 F6(l.Quantity)),
            new XElement("precioUnitario",           F6(l.UnitPrice)),
            new XElement("descuento",                "0.00"),
            new XElement("precioTotalSinImpuesto",   F2(l.Subtotal)),
            new XElement("impuestos",
                BuildImpuestoLinea(vatCode, vatPct, l.Subtotal, l.VatTotal)));
    }

    private static XElement BuildImpuestoLinea(
        string  vatCode,
        string  vatPct,
        decimal baseImp,
        decimal valor)
        => new XElement("impuesto",
            new XElement("codigo",           "2"),
            new XElement("codigoPorcentaje", vatCode),
            new XElement("tarifa",           vatPct),
            new XElement("baseImponible",    F2(baseImp)),
            new XElement("valor",            F2(valor)));

    private static XElement? ContribEspecialElement(SriSettings cfg)
        => string.IsNullOrWhiteSpace(cfg.SpecialTaxpayer)
            ? null
            : new XElement("contribuyenteEspecial", cfg.SpecialTaxpayer.Trim());

    // ── Helpers IVA ───────────────────────────────────────────────────────────

    /// <summary>
    /// Determina el código SRI de IVA a partir de las líneas.
    /// Usa el ratio vatTotal/subtotal para identificar la tarifa.
    /// </summary>
    private static string ResolveVatCode(List<SalesBillLine> lineas)
    {
        if (!lineas.Any()) return "4"; // 15% por defecto
        var totalSub = lineas.Sum(l => l.Subtotal);
        var totalVat = lineas.Sum(l => l.VatTotal);
        return totalSub == 0 ? "4" : VatCodeFromRate(totalVat / totalSub);
    }

    private static string ResolveVatCodeNota(List<SalesNoteLine> lineas)
    {
        if (!lineas.Any()) return "4";
        var totalSub = lineas.Sum(l => l.Subtotal);
        var totalVat = lineas.Sum(l => l.VatTotal);
        return totalSub == 0 ? "4" : VatCodeFromRate(totalVat / totalSub);
    }

    private static string VatCodeFromRate(decimal rate) => rate switch
    {
        0m                          => "0",   // 0%
        var r when Near(r, 0.05m)  => "5",   // 5%
        var r when Near(r, 0.12m)  => "2",   // 12%
        var r when Near(r, 0.14m)  => "3",   // 14%
        var r when Near(r, 0.15m)  => "4",   // 15%
        _                          => "4",   // default 15%
    };

    private static string VatPorcentaje(string code) => code switch
    {
        "0" => "0.00",
        "2" => "12.00",
        "3" => "14.00",
        "4" => "15.00",
        "5" => "5.00",
        _   => "15.00",
    };

    // ── Helpers identificación comprador ─────────────────────────────────────

    private static string IdTypeCode(dynamic cliente)
    {
        var id = (string)(cliente.IdentificationNumber ?? "");
        if (id.Length == 13) return "04"; // RUC
        if (id.Length == 10) return "05"; // Cédula
        return "06"; // Pasaporte / otro
    }

    // ── Formato ───────────────────────────────────────────────────────────────

    private static string F2(decimal v) => v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
    private static string F6(decimal v) => v.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
    private static string Seq(string s) => s.PadLeft(9, '0');
    private static bool   Near(decimal a, decimal b) => Math.Abs(a - b) < 0.005m;

    private static string ToUtf8String(XDocument doc)
    {
        using var ms = new MemoryStream();
        using var w  = new System.Xml.XmlTextWriter(ms, new UTF8Encoding(false));
        w.Formatting = System.Xml.Formatting.None;
        doc.Save(w);
        w.Flush();
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
