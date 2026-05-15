namespace ERP.Application.Common.Interfaces;

public interface ITirillaFacturaService
{
    Task<string> GenerarHtmlFacturaAsync(Guid     salesBillId, CancellationToken ct = default);
}
