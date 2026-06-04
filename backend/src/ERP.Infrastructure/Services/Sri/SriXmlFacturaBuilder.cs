using System.Text;
using System.Xml.Linq;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Infrastructure.Persistence.Converters;

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
        SriSettings          cfg,
        Company              company,
        BusinessPartner?     buyer = null)
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

                BuildInfoTributaria(cfg, company, factura.AccessKey, factura.DocType,
                    factura.EstabCode, factura.EmPointCode, factura.Sequential),

                new XElement("infoFactura",
                    new XElement("fechaEmision",        factura.IssueDate.ToString("dd/MM/yyyy")),
                    new XElement("dirEstablecimiento",  company.MainAddress),
                    ContribEspecialElement(company),
                    new XElement("obligadoContabilidad", company.IsAccountingReq ? "SI" : "NO"),
                    new XElement("tipoIdentificacionComprador", SriIdTypeCode(buyer)),
                    new XElement("razonSocialComprador", buyer?.Name.LegalName ?? "CONSUMIDOR FINAL"),
                    new XElement("identificacionComprador", buyer?.Identification.Number ?? "9999999999"),
                    new XElement("direccionComprador",  ""),
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

                BuildInfoAdicional(buyer, factura.Notes)));

        return ToUtf8String(doc);
    }

    /// <summary>Genera el XML de una nota de crédito (tipo 04) o débito (tipo 05).</summary>
    public static string BuildNotaCreditoDebito(
        SalesBill           factOrig,
        SalesNote           nota,
        List<SalesNoteLine> lineas,
        SriSettings         cfg,
        Company             company,
        BusinessPartner?    buyer = null)
    {
        var (rootName, infoName) = NoteTypeConversions.ToXmlElementNames(nota.NoteType);
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

                BuildInfoTributaria(cfg, company, nota.AccessKey, nota.DocType,
                    nota.EstabCode, nota.EmPointCode, nota.Sequential),

                new XElement(infoName,
                    new XElement("fechaEmision",            nota.IssueDate.ToString("dd/MM/yyyy")),
                    new XElement("dirEstablecimiento",      company.MainAddress),
                    new XElement("tipoIdentificacionComprador", SriIdTypeCode(buyer)),
                    new XElement("razonSocialComprador",    buyer?.Name.LegalName ?? "CONSUMIDOR FINAL"),
                    new XElement("identificacionComprador", buyer?.Identification.Number ?? "9999999999"),
                    ContribEspecialElement(company),
                    new XElement("obligadoContabilidad",    company.IsAccountingReq ? "SI" : "NO"),
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
        Company     company,
        string      claveAcceso,
        string      codDoc,
        string      estab,
        string      ptoEmi,
        string      secuencial)
        => new("infoTributaria",
            new XElement("ambiente",       cfg.Environment),
            new XElement("tipoEmision",    cfg.EmissionType),
            new XElement("razonSocial",    company.LegalName),
            new XElement("nombreComercial",company.TradeName ?? company.LegalName),
            new XElement("ruc",            company.Ruc),
            new XElement("claveAcceso",    claveAcceso),
            new XElement("codDoc",         codDoc.PadLeft(2, '0')),
            new XElement("estab",          estab.PadLeft(3, '0')),
            new XElement("ptoEmi",         ptoEmi.PadLeft(3, '0')),
            new XElement("secuencial",     Seq(secuencial)),
            new XElement("dirMatriz",      company.MainAddress));

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

    private static XElement? ContribEspecialElement(Company company)
        => string.IsNullOrWhiteSpace(company.SpecialTaxpayerNo)
            ? null
            : new XElement("contribuyenteEspecial", company.SpecialTaxpayerNo.Trim());

    // ── infoAdicional ─────────────────────────────────────────────────────────

    /// <summary>
    /// Construye el bloque &lt;infoAdicional&gt; con los campos disponibles.
    /// El SRI permite múltiples &lt;campoAdicional&gt;; se omiten los vacíos.
    /// </summary>
    private static XElement BuildInfoAdicional(BusinessPartner? buyer, string? notes)
    {
        var campos = new List<XElement>();

        if (!string.IsNullOrWhiteSpace((string?)null  /* Phase 13: load from contacts */))
            campos.Add(CampoAdicional("Email", (string?)null  /* Phase 13: load from contacts */));

        if (!string.IsNullOrWhiteSpace((string?)null  /* Phase 13: load from contacts */))
            campos.Add(CampoAdicional("Telefono", (string?)null  /* Phase 13: load from contacts */));

        if (!string.IsNullOrWhiteSpace(notes))
            campos.Add(CampoAdicional("Observaciones", notes));

        if (campos.Count == 0)
            campos.Add(CampoAdicional("Info", "-"));

        return new XElement("infoAdicional", campos);
    }

    private static XElement CampoAdicional(string nombre, string valor)
        => new("campoAdicional", new XAttribute("nombre", nombre), valor);

    // ── Helpers IVA ───────────────────────────────────────────────────────────

    // ResolveVatCodeNota y VatPorcentaje eliminados — se usan snapshots por línea.

    // ── Helpers identificación comprador ─────────────────────────────────────

    private static string SriIdTypeCode(BusinessPartner? bp) => bp?.Identification.Type ?? "07";

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

