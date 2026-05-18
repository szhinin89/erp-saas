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
    private const string Currency = "DOLAR";

    // ── API pública ───────────────────────────────────────────────────────────

    /// <summary>Genera el XML de una factura de venta (tipo 01).</summary>
    public static string BuildFactura(
        SalesBill            factura,
        List<SalesBillLine>  lineas,
        SriSettings          cfg)
    {
        var totalDescuento = 0m;
        var detElements    = lineas.Select(l =>
            BuildDetalleFactura(l, ref totalDescuento)).ToList();

        // totalConImpuestos: un <totalImpuesto> por cada tasa IVA distinta que aparezca
        var gruposIva = lineas
            .GroupBy(l => l.VatCode)
            .Select(g => BuildTotalImpuesto(
                vatCode: g.Key,
                baseImp: g.Sum(l => l.Subtotal),
                valor:   g.Sum(l => l.VatTotal)))
            .ToList();

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
                    new XElement("totalDescuento",      F2(factura.TotalDiscount)),
                    new XElement("totalConImpuestos",   gruposIva),
                    new XElement("propina",  "0.00"),
                    new XElement("importeTotal", F2(factura.Total)),
                    new XElement("moneda", Currency),
                    new XElement("pagos",
                        new XElement("pago",
                            new XElement("formaPago",   factura.PaymentMethodCode),
                            new XElement("total",       F2(factura.Total)),
                            new XElement("plazo",       factura.PaymentDays.ToString()),
                            new XElement("unidadTiempo","dias")))),

                new XElement("detalles", detElements),

                BuildInfoAdicional(factura)));

        return ToUtf8String(doc);
    }

    /// <summary>Genera el XML de una nota de crédito (tipo 04) o débito (tipo 05).</summary>
    public static string BuildNotaCreditoDebito(
        SalesBill           factOrig,
        SalesNote           nota,
        List<SalesNoteLine> lineas,
        SriSettings         cfg)
    {
        var esCredito = nota.NoteType.Equals("CREDITO", StringComparison.OrdinalIgnoreCase);
        var rootName  = esCredito ? "notaCredito" : "notaDebito";
        var infoName  = esCredito ? "infoNotaCredito" : "infoNotaDebito";
        var totalDesc = 0m;

        var numDocSustento =
            $"{factOrig.EstabCode}-{factOrig.EmPointCode}-{Seq(factOrig.Sequential)}";

        var detalles = lineas.Select(l => BuildDetalleNota(l, ref totalDesc)).ToList();

        // totalConImpuestos agrupado por código IVA (igual que factura)
        var gruposIva = lineas
            .GroupBy(l => l.VatCode)
            .Select(g => BuildTotalImpuesto(g.Key, g.Sum(l => l.Subtotal), g.Sum(l => l.VatTotal)))
            .ToList();

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(rootName,
                new XAttribute("id", "comprobante"),
                new XAttribute("version", NotaVersion),

                BuildInfoTributaria(cfg, nota.AccessKey, nota.DocType,
                    nota.EstabCode, nota.EmPointCode, nota.Sequential),

                new XElement(infoName,
                    new XElement("fechaEmision",            nota.IssueDate.ToString("dd/MM/yyyy")),
                    new XElement("dirEstablecimiento",      cfg.MainAddress),
                    new XElement("tipoIdentificacionComprador", IdTypeCode(factOrig.Cliente)),
                    new XElement("razonSocialComprador",    factOrig.Cliente.LegalName),
                    new XElement("identificacionComprador", factOrig.Cliente.IdentificationNumber),
                    ContribEspecialElement(cfg),
                    new XElement("obligadoContabilidad",    cfg.RequiresAccounting ? "SI" : "NO"),
                    new XElement("codDocModificado",        factOrig.DocType.Trim()),
                    new XElement("numDocModificado",        numDocSustento),
                    new XElement("fechaEmisionDocSustento", factOrig.IssueDate.ToString("dd/MM/yyyy")),
                    new XElement("numAutDocSustento",       factOrig.AuthNumber ?? "0"),
                    new XElement("motivo",                  nota.Reason),
                    new XElement("totalSinImpuestos",       F2(nota.Subtotal)),
                    new XElement("valorModificacion",       F2(nota.Total)),
                    new XElement("totalConImpuestos",       gruposIva),
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
        SalesBillLine l,
        ref decimal   totalDesc)
    {
        totalDesc += l.DiscountAmount;
        return new XElement("detalle",
            new XElement("codigoPrincipal",        l.ProductCode),
            new XElement("descripcion",            l.Description),
            new XElement("cantidad",               F6(l.Quantity)),
            new XElement("precioUnitario",         F6(l.UnitPrice)),
            new XElement("descuento",              F2(l.DiscountAmount)),
            new XElement("precioTotalSinImpuesto", F2(l.Subtotal)),
            new XElement("impuestos",
                BuildImpuestoLinea(
                    l.VatCode,
                    l.VatPercentage.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    l.Subtotal,
                    l.VatTotal)));
    }

    private static XElement BuildDetalleNota(SalesNoteLine l, ref decimal totalDesc)
    {
        totalDesc += 0m; // notas no tienen descuento por línea aún
        return new XElement("detalle",
            new XElement("codigoInterno",          l.ProductCode),
            new XElement("descripcion",            l.Description),
            new XElement("cantidad",               F6(l.Quantity)),
            new XElement("precioUnitario",         F6(l.UnitPrice)),
            new XElement("descuento",              "0.00"),
            new XElement("precioTotalSinImpuesto", F2(l.Subtotal)),
            new XElement("impuestos",
                BuildImpuestoLinea(
                    l.VatCode,
                    l.VatPercentage.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    l.Subtotal,
                    l.VatTotal)));
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

    // ── infoAdicional ─────────────────────────────────────────────────────────

    /// <summary>
    /// Construye el bloque &lt;infoAdicional&gt; con los campos disponibles.
    /// El SRI permite múltiples &lt;campoAdicional&gt;; se omiten los vacíos.
    /// </summary>
    private static XElement BuildInfoAdicional(SalesBill factura)
    {
        var campos = new List<XElement>();

        if (!string.IsNullOrWhiteSpace(factura.Cliente.Email))
            campos.Add(CampoAdicional("Email", factura.Cliente.Email));

        if (!string.IsNullOrWhiteSpace(factura.Cliente.Phone))
            campos.Add(CampoAdicional("Telefono", factura.Cliente.Phone));

        if (!string.IsNullOrWhiteSpace(factura.Cliente.AddressLine))
            campos.Add(CampoAdicional("DireccionComprador", factura.Cliente.AddressLine));

        if (!string.IsNullOrWhiteSpace(factura.Notes))
            campos.Add(CampoAdicional("Observaciones", factura.Notes));

        // El SRI requiere al menos un campoAdicional; si no hay datos, emitir uno vacío
        if (campos.Count == 0)
            campos.Add(CampoAdicional("Info", "-"));

        return new XElement("infoAdicional", campos);
    }

    private static XElement CampoAdicional(string nombre, string valor)
        => new("campoAdicional", new XAttribute("nombre", nombre), valor);

    // ── Helpers IVA ───────────────────────────────────────────────────────────

    // ResolveVatCodeNota y VatPorcentaje eliminados — se usan snapshots por línea.

    // ── Helpers identificación comprador ─────────────────────────────────────

    private static string IdTypeCode(Customer cliente)
        => cliente.SriIdTypeCode; // "04"=RUC, "05"=CI, "06"=Pasaporte, "08"=Otro

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
