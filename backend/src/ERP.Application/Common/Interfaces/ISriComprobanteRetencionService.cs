using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Purchasing.Entities;

namespace ERP.Application.Common.Interfaces;

public interface ISriComprobanteRetentionService
{
    Task<string> GenerarXmlRetencionAsync(
        IssuedRetention retencion,
        List<PurchRetentionLine> detalles,
        SriSettings config,
        Company company);

    Task<byte[]> FirmarXmlAsync(string xmlContent, string p12Path, string password);

    Task<SriAutorizacionResponse> EnviarAsync(byte[] xmlFirmado, string urlWsdl);
}
