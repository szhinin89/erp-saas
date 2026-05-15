using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Purchasing.Entities;

namespace ERP.Infrastructure.Services;

public sealed class SriComprobanteRetencionSimuladoService : ISriComprobanteRetencionService
{
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<SriComprobanteRetencionSimuladoService> _logger;

    public SriComprobanteRetencionSimuladoService(
        IFileStorage fileStorage,
        ILogger<SriComprobanteRetencionSimuladoService> logger)
    {
        _fileStorage = fileStorage;
        _logger      = logger;
    }

    public async Task<string> GenerarXmlRetencionAsync(
        IssuedRetention retencion,
        List<PurchRetentionLine> detalles,
        SriSettings config)
    {
        var xmlDoc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("comprobanteRetencion",
                new XAttribute("id", "comprobante"),
                new XAttribute("version", "1.0.0"),
                new XElement("infoTributaria",
                    new XElement("ambiente", config.Environment),
                    new XElement("tipoEmision", config.EmissionType),
                    new XElement("razonSocial", config.LegalName),
                    new XElement("ruc", config.Ruc),
                    new XElement("claveAcceso", retencion.AccessKey),
                    new XElement("codDoc", "07"),
                    new XElement("estab", retencion.EstablishmentCode),
                    new XElement("ptoEmi", retencion.EmissionPointCode),
                    new XElement("secuencial", retencion.Sequential),
                    new XElement("dirMatriz", config.MainAddress)),
                new XElement("infoCompRetencion",
                    new XElement("fechaEmision", retencion.IssueDate.ToString("dd/MM/yyyy")),
                    new XElement("dirEstablecimiento", config.MainAddress),
                    new XElement("obligadoContabilidad", config.RequiresAccounting ? "SI" : "NO"),
                    new XElement("tipoIdentificacionSujetoRetenido", "04"),
                    new XElement("razonSocialSujetoRetenido", retencion.Supplier.LegalName),
                    new XElement("identificacionSujetoRetenido", retencion.Supplier.Ruc),
                    new XElement("periodoFiscal", retencion.IssueDate.ToString("MM/yyyy"))),
                new XElement("impuestos",
                    detalles.Select(d => new XElement("impuesto",
                        new XElement("codigo", d.TaxType == "IVA" ? "2" : "1"),
                        new XElement("codigoRetencion", d.RetentionCode),
                        new XElement("baseImponible", d.TaxableBase.ToString("F2")),
                        new XElement("porcentajeRetener", d.RetentionPct.ToString("F2")),
                        new XElement("valorRetenido", d.AmountRetained.ToString("F2")))))
            ));

        var xmlString = xmlDoc.ToString();
        _logger.LogDebug("[SRI-SIM] XML retención generado para {RetencionId}", retencion.Id);
        var path = $"retenciones/compras/{retencion.AccessKey}.xml";
        await _fileStorage.SaveAsync(path, new MemoryStream(Encoding.UTF8.GetBytes(xmlString)));
        return xmlString;
    }

    public Task<byte[]> FirmarXmlAsync(string xmlContent, string p12Path, string password)
        => Task.FromResult(Encoding.UTF8.GetBytes(xmlContent));

    public Task<SriAutorizacionResponse> EnviarAsync(byte[] xmlFirmado, string urlWsdl)
    {
        var numeroAuth = Guid.NewGuid().ToString("N")[..10].ToUpper();
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


