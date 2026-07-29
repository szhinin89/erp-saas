using ERP.Application.Modules.Sales.DTOs;

namespace ERP.Application.Modules.Sales;

public interface IInvoiceItemSearchRepository
{
    Task<IReadOnlyList<InvoiceItemMatch>> SearchAsync(
        Guid tenantId,
        Guid companyId,
        string query,
        Guid? warehouseId,
        int pageSize,
        CancellationToken ct = default
    );
}
