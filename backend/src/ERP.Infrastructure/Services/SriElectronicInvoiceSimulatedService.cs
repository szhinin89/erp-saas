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

public sealed class SriElectronicInvoiceSimulatedService : ISriElectronicInvoiceService
{
    private readonly IFileStorage                               _fileStorage;
    private readonly ILogger<SriElectronicInvoiceSimulatedService> _logger;

    public SriElectronicInvoiceSimulatedService(
        IFileStorage fileStorage,
        ILogger<SriElectronicInvoiceSimulatedService> logger)
    {
        _fileStorage = fileStorage;
        _logger      = logger;
    }

    public async Task<string> GenerateInvoiceXmlAsync(SalesBill salesBill, List<SalesBillLine> lines, SriSettings config, Company company)
    {
        _logger.LogDebug(
            "[SRI-SIM] Generando XML para salesBill {FacturaId} (clave={AccessKey}, tenant={SubscriberId})",
            salesBill.Id, salesBill.AccessKey, salesBill.SubscriberId);

        var xmlDoc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("salesBill",
                new XAttribute("id", "comprobante"),
                new XAttribute("version", "1.0.0"),
                new XElement("infoTributaria",
                    new XElement("ambiente", config.Environment),
                    new XElement("tipoEmision", config.EmissionType),
                    new XElement("razonSocial", company.LegalName),
                    new XElement("nombreComercial", company.TradeName ?? company.LegalName),
                    new XElement("ruc", company.Ruc),
                    new XElement("accessKey", salesBill.AccessKey),
                    new XElement("codDoc", salesBill.DocType),
                    new XElement("estab", salesBill.EstabCode),
                    new XElement("ptoEmi", salesBill.EmPointCode),
                    new XElement("secuencial", salesBill.Sequential),
                    new XElement("dirMatriz", company.MainAddress)
                ),
                new XElement("infoFactura",
                    new XElement("issueDate", salesBill.IssueDate.ToString("dd/MM/yyyy")),
                    new XElement("dirEstablecimiento", company.MainAddress),
                    new XElement("obligadoContabilidad", company.IsAccountingReq ? "SI" : "NO"),
                    new XElement("tipoIdentificacionComprador", "04"),
                    new XElement("razonSocialComprador", "CONSUMIDOR FINAL"),
                    new XElement("identificacionComprador", "9999999999"),
                    new XElement("direccionComprador", ""),
                    new XElement("totalSinImpuestos", salesBill.Subtotal.ToString("F2")),
                    new XElement("totalDescuento", "0.00"),
                    new XElement("totalConImpuestos",
                        new XElement("totalVat",
                            new XElement("codigo", "2"),
                            new XElement("codigoPorcentaje", "2"),
                            new XElement("baseImponible", salesBill.Subtotal.ToString("F2")),
                            new XElement("valor", salesBill.VatTotal.ToString("F2"))
                        )
                    ),
                    new XElement("propina", "0.00"),
                    new XElement("importeTotal", salesBill.Total.ToString("F2")),
                    new XElement("moneda", "DOLAR")
                ),
                new XElement("lines",
                    lines.Select(d => new XElement("line",
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

        _logger.LogDebug("[SRI-SIM] XML generado ({Bytes} bytes) para salesBill {FacturaId}",
            xmlString.Length, salesBill.Id);

        var path = $"facturas/ventas/{salesBill.AccessKey}.xml";
        await _fileStorage.SaveAsync(path, new MemoryStream(Encoding.UTF8.GetBytes(xmlString)));
        return xmlString;
    }

    public async Task<string> GenerateCreditDebitNoteXmlAsync(
        SalesBill originalBill,
        SalesNote note,
        List<SalesNoteLine> lines,
        SriSettings config,
        Company company)
    {
        var esCredito = note.NoteType == NoteType.Credit;
        var (rootName, infoName) = NoteTypeConversions.ToXmlElementNames(note.NoteType);

        var numDocSustento =
            $"{originalBill.EstabCode}-{originalBill.EmPointCode}-{originalBill.Sequential}";

        var infoTributaria = new XElement("infoTributaria",
            new XElement("ambiente", config.Environment),
            new XElement("tipoEmision", config.EmissionType),
            new XElement("razonSocial", company.LegalName),
            new XElement("nombreComercial", company.TradeName ?? company.LegalName),
            new XElement("ruc", company.Ruc),
            new XElement("accessKey", note.AccessKey),
            new XElement("codDoc", note.DocType),
            new XElement("estab", note.EstabCode),
            new XElement("ptoEmi", note.EmPointCode),
            new XElement("secuencial", note.Sequential),
            new XElement("dirMatriz", company.MainAddress));

        var infoPrincipal = new XElement(infoName,
            new XElement("issueDate", note.IssueDate.ToString("dd/MM/yyyy")),
            new XElement("dirEstablecimiento", company.MainAddress),
            new XElement("tipoIdentificacionComprador", "04"),
            new XElement("razonSocialComprador", "CONSUMIDOR FINAL"),
            new XElement("identificacionComprador", "9999999999"),
            new XElement("contribuyenteEspecial", ""),
            new XElement("obligadoContabilidad", company.IsAccountingReq ? "SI" : "NO"),
            new XElement("tipoEmision", config.EmissionType),
            new XElement("rise", ""),
            new XElement("codDocModificado", originalBill.DocType.Trim()),
            new XElement("numDocModificado", numDocSustento),
            new XElement("fechaEmisionDocSustento", originalBill.IssueDate.ToString("dd/MM/yyyy")),
            new XElement("motivo", note.Reason),
            new XElement("numAutDocSustento", originalBill.AuthNumber ?? "0000000000"),
            new XElement("totalSinImpuestos", note.Subtotal.ToString("F2")),
            new XElement("valorModificacion", note.Total.ToString("F2")),
            new XElement("totalConImpuestos",
                new XElement("totalVat",
                    new XElement("codigo", "2"),
                    new XElement("codigoPorcentaje", "2"),
                    new XElement("baseImponible", note.Subtotal.ToString("F2")),
                    new XElement("valor", note.VatTotal.ToString("F2")))),
            new XElement("moneda", "DOLAR"));

        var xmlDoc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(rootName,
                new XAttribute("id", "comprobante"),
                new XAttribute("version", "1.0.0"),
                infoTributaria,
                infoPrincipal,
                new XElement("lines",
                    lines.Select(d => new XElement("line",
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
        _logger.LogDebug("[SRI-SIM] XML {Root} generado para note {NoteId}", rootName, note.Id);

        var path = $"notas/ventas/{note.AccessKey}.xml";
        await _fileStorage.SaveAsync(path, new MemoryStream(Encoding.UTF8.GetBytes(xmlString)));
        return xmlString;
    }

    public Task<byte[]> SignXmlAsync(string xmlContent, string p12Path, string password)
    {
        _logger.LogDebug("[SRI-SIM] Firma digital simulada (P12 no aplicado)");
        return Task.FromResult(Encoding.UTF8.GetBytes(xmlContent));
    }

    public Task<SriAuthorizationResponse> SendToSriAsync(byte[] signedXml, string urlWsdl)
    {
        var numeroAuth = Guid.NewGuid().ToString("N")[..10].ToUpper();

        _logger.LogInformation(
            "[SRI-SIM] AutorizaciÃ³n simulada generada: {NumeroAutorizacion}", numeroAuth);

        return Task.FromResult(new SriAuthorizationResponse
        {
            IsAuthorized = true,
            AuthNumber = numeroAuth,
            AuthDate = DateTime.UtcNow,
            AuthorizedXml = Encoding.UTF8.GetString(signedXml),
            ErrorMessage = null,
        });
    }
}

