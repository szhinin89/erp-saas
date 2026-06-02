using ERP.Domain.Common;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Infrastructure.Persistence.Converters;

namespace ERP.Infrastructure.Services;

public sealed class SriFacturaElectronicaSimuladoService : ISriFacturaElectronicaService
{
    private readonly IFileStorage                               _fileStorage;
    private readonly ILogger<SriFacturaElectronicaSimuladoService> _logger;

    public SriFacturaElectronicaSimuladoService(
        IFileStorage fileStorage,
        ILogger<SriFacturaElectronicaSimuladoService> logger)
    {
        _fileStorage = fileStorage;
        _logger      = logger;
    }

    public async Task<string> GenerarXmlFacturaAsync(SalesBill factura, List<SalesBillLine> detalles, SriSettings config, Company company)
    {
        _logger.LogDebug(
            "[SRI-SIM] Generando XML para factura {FacturaId} (clave={AccessKey}, tenant={SubscriberId})",
            factura.Id, factura.AccessKey, factura.SubscriberId);

        var xmlDoc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("factura",
                new XAttribute("id", "comprobante"),
                new XAttribute("version", "1.0.0"),
                new XElement("infoTributaria",
                    new XElement("ambiente", config.Environment),
                    new XElement("tipoEmision", config.EmissionType),
                    new XElement("razonSocial", company.LegalName),
                    new XElement("nombreComercial", company.TradeName ?? company.LegalName),
                    new XElement("ruc", company.Ruc),
                    new XElement("accessKey", factura.AccessKey),
                    new XElement("codDoc", factura.DocType),
                    new XElement("estab", factura.EstabCode),
                    new XElement("ptoEmi", factura.EmPointCode),
                    new XElement("secuencial", factura.Sequential),
                    new XElement("dirMatriz", company.MainAddress)
                ),
                new XElement("infoFactura",
                    new XElement("issueDate", factura.IssueDate.ToString("dd/MM/yyyy")),
                    new XElement("dirEstablecimiento", company.MainAddress),
                    new XElement("obligadoContabilidad", company.IsAccountingReq ? "SI" : "NO"),
                    new XElement("tipoIdentificacionComprador", "04"),
                    new XElement("razonSocialComprador", "CONSUMIDOR FINAL"),
                    new XElement("identificacionComprador", "9999999999"),
                    new XElement("direccionComprador", ""),
                    new XElement("totalSinImpuestos", factura.Subtotal.ToString("F2")),
                    new XElement("totalDescuento", "0.00"),
                    new XElement("totalConImpuestos",
                        new XElement("totalVat",
                            new XElement("codigo", "2"),
                            new XElement("codigoPorcentaje", "2"),
                            new XElement("baseImponible", factura.Subtotal.ToString("F2")),
                            new XElement("valor", factura.VatTotal.ToString("F2"))
                        )
                    ),
                    new XElement("propina", "0.00"),
                    new XElement("importeTotal", factura.Total.ToString("F2")),
                    new XElement("moneda", "DOLAR")
                ),
                new XElement("detalles",
                    detalles.Select(d => new XElement("detalle",
                        new XElement("codigoPrincipal", d.ProductId.ToString()),
                        new XElement("descripcion", d.Description),
                        new XElement("cantidad", d.Quantity.ToString("F4")),
                        new XElement("precioUnitario", d.UnitPrice.ToString("F6")),
                        new XElement("descuento", "0.00"),
                        new XElement("precioTotalSinImpuesto", d.Subtotal.ToString("F2")),
                        new XElement("impuestos",
                            new XElement("impuesto",
                                new XElement("codigo", "2"),
                                new XElement("codigoPorcentaje", "2"),
                                new XElement("tarifa", "12.00"),
                                new XElement("baseImponible", d.Subtotal.ToString("F2")),
                                new XElement("valor", d.VatTotal.ToString("F2"))
                            )
                        )
                    ))
                ),
                new XElement("infoAdicional",
                    new XElement("campoAdicional",
                        new XAttribute("nombre", "Info"),
                        "-"
                    )
                )
            )
        );

        var xmlString = xmlDoc.ToString();

        _logger.LogDebug("[SRI-SIM] XML generado ({Bytes} bytes) para factura {FacturaId}",
            xmlString.Length, factura.Id);

        var path = $"facturas/ventas/{factura.AccessKey}.xml";
        await _fileStorage.SaveAsync(path, new MemoryStream(Encoding.UTF8.GetBytes(xmlString)));
        return xmlString;
    }

    public async Task<string> GenerarXmlNotaCreditoDebitoAsync(
        SalesBill facturaOriginal,
        SalesNote nota,
        List<SalesNoteLine> detalles,
        SriSettings config,
        Company company)
    {
        var esCredito = nota.NoteType == NoteType.Credit;
        var (rootName, infoName) = NoteTypeConversions.ToXmlElementNames(nota.NoteType);

        var numDocSustento =
            $"{facturaOriginal.EstabCode}-{facturaOriginal.EmPointCode}-{facturaOriginal.Sequential}";

        var infoTributaria = new XElement("infoTributaria",
            new XElement("ambiente", config.Environment),
            new XElement("tipoEmision", config.EmissionType),
            new XElement("razonSocial", company.LegalName),
            new XElement("nombreComercial", company.TradeName ?? company.LegalName),
            new XElement("ruc", company.Ruc),
            new XElement("accessKey", nota.AccessKey),
            new XElement("codDoc", nota.DocType),
            new XElement("estab", nota.EstabCode),
            new XElement("ptoEmi", nota.EmPointCode),
            new XElement("secuencial", nota.Sequential),
            new XElement("dirMatriz", company.MainAddress));

        var infoPrincipal = new XElement(infoName,
            new XElement("issueDate", nota.IssueDate.ToString("dd/MM/yyyy")),
            new XElement("dirEstablecimiento", company.MainAddress),
            new XElement("tipoIdentificacionComprador", "04"),
            new XElement("razonSocialComprador", "CONSUMIDOR FINAL"),
            new XElement("identificacionComprador", "9999999999"),
            new XElement("contribuyenteEspecial", ""),
            new XElement("obligadoContabilidad", company.IsAccountingReq ? "SI" : "NO"),
            new XElement("tipoEmision", config.EmissionType),
            new XElement("rise", ""),
            new XElement("codDocModificado", facturaOriginal.DocType.Trim()),
            new XElement("numDocModificado", numDocSustento),
            new XElement("fechaEmisionDocSustento", facturaOriginal.IssueDate.ToString("dd/MM/yyyy")),
            new XElement("motivo", nota.Reason),
            new XElement("numAutDocSustento", facturaOriginal.AuthNumber ?? "0000000000"),
            new XElement("totalSinImpuestos", nota.Subtotal.ToString("F2")),
            new XElement("valorModificacion", nota.Total.ToString("F2")),
            new XElement("totalConImpuestos",
                new XElement("totalVat",
                    new XElement("codigo", "2"),
                    new XElement("codigoPorcentaje", "2"),
                    new XElement("baseImponible", nota.Subtotal.ToString("F2")),
                    new XElement("valor", nota.VatTotal.ToString("F2")))),
            new XElement("moneda", "DOLAR"));

        var xmlDoc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(rootName,
                new XAttribute("id", "comprobante"),
                new XAttribute("version", "1.0.0"),
                infoTributaria,
                infoPrincipal,
                new XElement("detalles",
                    detalles.Select(d => new XElement("detalle",
                        new XElement("codigoInterno", d.ProductId.ToString()),
                        new XElement("descripcion", d.Description),
                        new XElement("cantidad", d.Quantity.ToString("F4")),
                        new XElement("precioUnitario", d.UnitPrice.ToString("F6")),
                        new XElement("descuento", "0.00"),
                        new XElement("precioTotalSinImpuesto", d.Subtotal.ToString("F2")),
                        new XElement("impuestos",
                            new XElement("impuesto",
                                new XElement("codigo", "2"),
                                new XElement("codigoPorcentaje", "2"),
                                new XElement("tarifa", "12.00"),
                                new XElement("baseImponible", d.Subtotal.ToString("F2")),
                                new XElement("valor", d.VatTotal.ToString("F2")))))))
            ));

        var xmlString = xmlDoc.ToString();
        _logger.LogDebug("[SRI-SIM] XML {Root} generado para nota {NotaId}", rootName, nota.Id);

        var path = $"notas/ventas/{nota.AccessKey}.xml";
        await _fileStorage.SaveAsync(path, new MemoryStream(Encoding.UTF8.GetBytes(xmlString)));
        return xmlString;
    }

    public Task<byte[]> FirmarXmlAsync(string xmlContent, string p12Path, string password)
    {
        _logger.LogDebug("[SRI-SIM] Firma digital simulada (P12 no aplicado)");
        return Task.FromResult(Encoding.UTF8.GetBytes(xmlContent));
    }

    public Task<SriAutorizacionResponse> EnviarAlSriAsync(byte[] xmlFirmado, string urlWsdl)
    {
        var numeroAuth = Guid.NewGuid().ToString("N")[..10].ToUpper();

        _logger.LogInformation(
            "[SRI-SIM] AutorizaciÃ³n simulada generada: {NumeroAutorizacion}", numeroAuth);

        return Task.FromResult(new SriAutorizacionResponse
        {
            IsAuthorized = true,
            AuthNumber = numeroAuth,
            AuthDate = DateTime.UtcNow,
            AuthorizedXml = Encoding.UTF8.GetString(xmlFirmado),
            ErrorMessage = null,
        });
    }
}

