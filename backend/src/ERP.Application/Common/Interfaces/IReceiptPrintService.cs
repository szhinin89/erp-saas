namespace ERP.Application.Common.Interfaces;

public interface IReceiptPrintService
{
    Task<string> GenerateInvoiceHtmlAsync(Guid     salesBillId, CancellationToken ct = default);
}
