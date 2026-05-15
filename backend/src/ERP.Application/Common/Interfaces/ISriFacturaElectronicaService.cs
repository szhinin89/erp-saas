using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Application.Common.Interfaces;

public interface ISriFacturaElectronicaService
{
    Task<string> GenerarXmlFacturaAsync(VentasFactura factura, List<VentasDetalle> detalles, ConfiguracionSRI config);

    Task<string> GenerarXmlNotaCreditoDebitoAsync(
        VentasFactura facturaOriginal,
        VentasNotaCreditoDebito nota,
        List<VentasNotaDetalle> detalles,
        ConfiguracionSRI config);

    Task<byte[]> FirmarXmlAsync(string xmlContent, string p12Path, string password);
    Task<SriAutorizacionResponse> EnviarAlSriAsync(byte[] xmlFirmado, string urlWsdl);
}

public class SriAutorizacionResponse
{
    public bool Autorizada { get; set; }
    public string NumeroAutorizacion { get; set; } = null!;
    public DateTime FechaAutorizacion { get; set; }
    public string XmlAutorizado { get; set; } = null!;
    public string? MensajeError { get; set; }
}