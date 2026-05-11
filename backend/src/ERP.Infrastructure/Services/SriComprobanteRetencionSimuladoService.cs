using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Compras.Entities;

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
        CompraRetencionEmitida retencion,
        List<CompraDetalleRetencionEmitida> detalles,
        ConfiguracionSRI config)
    {
        var xmlDoc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("comprobanteRetencion",
                new XAttribute("id", "comprobante"),
                new XAttribute("version", "1.0.0"),
                new XElement("infoTributaria",
                    new XElement("ambiente", config.Ambiente),
                    new XElement("tipoEmision", config.TipoEmision),
                    new XElement("razonSocial", config.RazonSocial),
                    new XElement("ruc", config.RucEmpresa),
                    new XElement("claveAcceso", retencion.ClaveAcceso),
                    new XElement("codDoc", "07"),
                    new XElement("estab", retencion.Establecimiento),
                    new XElement("ptoEmi", retencion.PuntoEmision),
                    new XElement("secuencial", retencion.Secuencial),
                    new XElement("dirMatriz", config.DireccionMatriz)),
                new XElement("infoCompRetencion",
                    new XElement("fechaEmision", retencion.FechaEmision.ToString("dd/MM/yyyy")),
                    new XElement("dirEstablecimiento", config.DireccionMatriz),
                    new XElement("obligadoContabilidad", config.ObligadoContabilidad ? "SI" : "NO"),
                    new XElement("tipoIdentificacionSujetoRetenido", "04"),
                    new XElement("razonSocialSujetoRetenido", retencion.Proveedor.RazonSocial),
                    new XElement("identificacionSujetoRetenido", retencion.Proveedor.Ruc),
                    new XElement("periodoFiscal", retencion.FechaEmision.ToString("MM/yyyy"))),
                new XElement("impuestos",
                    detalles.Select(d => new XElement("impuesto",
                        new XElement("codigo", d.Impuesto == "IVA" ? "2" : "1"),
                        new XElement("codigoRetencion", d.CodigoRetencion),
                        new XElement("baseImponible", d.BaseImponible.ToString("F2")),
                        new XElement("porcentajeRetener", d.PorcentajeRetencion.ToString("F2")),
                        new XElement("valorRetenido", d.ValorRetenido.ToString("F2")))))
            ));

        var xmlString = xmlDoc.ToString();
        _logger.LogDebug("[SRI-SIM] XML retención generado para {RetencionId}", retencion.Id);
        var path = $"retenciones/compras/{retencion.ClaveAcceso}.xml";
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
            Autorizada         = true,
            NumeroAutorizacion = numeroAuth,
            FechaAutorizacion  = DateTime.UtcNow,
            XmlAutorizado      = Encoding.UTF8.GetString(xmlFirmado),
            MensajeError       = null,
        });
    }
}
