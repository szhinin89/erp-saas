using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Application.Common.Interfaces;

public interface ISriFacturaElectronicaService
{
    Task<string> GenerarXmlFacturaAsync(SalesBill factura, List<SalesBillLine> detalles, SriSettings config);

    Task<string> GenerarXmlNotaCreditoDebitoAsync(
        SalesBill facturaOriginal,
        SalesNote nota,
        List<SalesNoteLine> detalles,
        SriSettings config);

    Task<byte[]> FirmarXmlAsync(string xmlContent, string p12Path, string password);
    Task<SriAutorizacionResponse> EnviarAlSriAsync(byte[] xmlFirmado, string urlWsdl);
}

public class SriAutorizacionResponse
{
    public bool     IsAuthorized  { get; set; }
    public string   AuthNumber    { get; set; } = null!;
    public DateTime AuthDate      { get; set; }
    public string   AuthorizedXml { get; set; } = null!;
    public string?  ErrorMessage  { get; set; }
}
