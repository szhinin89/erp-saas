using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Purchasing.Entities;

namespace ERP.Application.Common.Interfaces;

public interface ISriComprobanteRetencionService
{
    Task<string> GenerarXmlRetencionAsync(
        IssuedRetention retencion,
        List<PurchRetentionLine> detalles,
        SriSettings config);

    Task<byte[]> FirmarXmlAsync(string xmlContent, string p12Path, string password);

    Task<SriAutorizacionResponse> EnviarAsync(byte[] xmlFirmado, string urlWsdl);
}
