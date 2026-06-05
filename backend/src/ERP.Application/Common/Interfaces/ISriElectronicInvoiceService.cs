using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Application.Common.Interfaces;

public interface ISriElectronicInvoiceService
{
    Task<string> GenerateInvoiceXmlAsync(SalesBill salesBill, List<SalesBillLine> lines, SriSettings config, Company company);

    Task<string> GenerateCreditDebitNoteXmlAsync(
        SalesBill originalBill,
        SalesNote note,
        List<SalesNoteLine> lines,
        SriSettings config,
        Company company);

    Task<byte[]> SignXmlAsync(string xmlContent, string p12Path, string password);
    Task<SriAuthorizationResponse> SendToSriAsync(byte[] signedXml, string urlWsdl);
}

public class SriAuthorizationResponse
{
    public bool     IsAuthorized  { get; set; }
    public string   AuthNumber    { get; set; } = null!;
    public DateTime AuthDate      { get; set; }
    public string   AuthorizedXml { get; set; } = null!;
    public string?  ErrorMessage  { get; set; }
}
