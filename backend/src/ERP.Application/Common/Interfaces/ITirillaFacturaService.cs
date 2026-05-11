namespace ERP.Application.Common.Interfaces;

public interface ITirillaFacturaService
{
    Task<string> GenerarHtmlFacturaAsync(Guid ventaId, CancellationToken ct = default);
}
