using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Purchasing.Entities;

namespace ERP.Infrastructure.Services;

public sealed class SriWithholdingSimulatedService : ISriWithholdingService
{
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<SriWithholdingSimulatedService> _logger;

    public SriWithholdingSimulatedService(
        IFileStorage fileStorage,
        ILogger<SriWithholdingSimulatedService> logger)
    {
        _fileStorage = fileStorage;
        _logger      = logger;
    }

    public async Task<string> GenerateWithholdingXmlAsync(
        IssuedRetention retention,
        List<PurchRetentionLine> lines,
        SriSettings config,
        Company company)
    {
        var xmlDoc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("comprobanteRetencion",
                new XAttribute("id", "comprobante"),
                new XAttribute("version", "1.0.0"),
                new XElement("infoTributaria",
                    new XElement("ambiente", config.Environment),
                    new XElement("tipoEmision", config.EmissionType),
                    new XElement("razonSocial", company.LegalName),
                    new XElement("ruc", company.Ruc),
                    new XElement("accessKey", retention.AccessKey),
                    new XElement("codDoc", "07"),
                    new XElement("estab", retention.EstablishmentCode),
                    new XElement("ptoEmi", retention.EmissionPointCode),
                    new XElement("secuencial", retention.Sequential),
                    new XElement("dirMatriz", company.MainAddress)),
                new XElement("infoCompRetencion",
                    new XElement("issueDate", retention.IssueDate.ToString("dd/MM/yyyy")),
                    new XElement("dirEstablecimiento", company.MainAddress),
                    new XElement("obligadoContabilidad", company.IsAccountingReq ? "SI" : "NO"),
                    new XElement("tipoIdentificacionSujetoRetenido", "04"),
                    new XElement("razonSocialSujetoRetenido", "PROVEEDOR"),
                    new XElement("identificacionSujetoRetenido", "9999999999999"),
                    new XElement("periodoFiscal", retention.IssueDate.ToString("MM/yyyy"))),
                new XElement("impuestos",
                    lines.Select(d => new XElement("impuesto",
                        new XElement("codigo", d.TaxType == "IVA" ? "2" : "1"),
                        new XElement("codigoRetencion", d.RetentionCode),
                        new XElement("baseImponible", d.TaxableBase.ToString("F2")),
                        new XElement("porcentajeRetener", d.RetentionPct.ToString("F2")),
                        new XElement("valorRetenido", d.AmountRetained.ToString("F2")))))
            ));

        var xmlString = xmlDoc.ToString();
        _logger.LogDebug("[SRI-SIM] XML retención generado para {RetentionId}", retention.Id);
        var path = $"retenciones/compras/{retention.AccessKey}.xml";
        await _fileStorage.SaveAsync(path, new MemoryStream(Encoding.UTF8.GetBytes(xmlString)));
        return xmlString;
    }

    public Task<byte[]> SignXmlAsync(string xmlContent, string p12Path, string password)
        => Task.FromResult(Encoding.UTF8.GetBytes(xmlContent));

    public Task<SriAuthorizationResponse> EnviarAsync(byte[] signedXml, string urlWsdl)
    {
        var numeroAuth = Guid.NewGuid().ToString("N")[..10].ToUpper();
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
