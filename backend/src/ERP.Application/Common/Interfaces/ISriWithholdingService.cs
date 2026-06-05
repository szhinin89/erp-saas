using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Purchasing.Entities;

namespace ERP.Application.Common.Interfaces;

public interface ISriWithholdingService
{
    Task<string> GenerateWithholdingXmlAsync(
        IssuedRetention retention,
        List<PurchRetentionLine> lines,
        SriSettings config,
        Company company);

    Task<byte[]> SignXmlAsync(string xmlContent, string p12Path, string password);

    Task<SriAuthorizationResponse> EnviarAsync(byte[] signedXml, string urlWsdl);
}
