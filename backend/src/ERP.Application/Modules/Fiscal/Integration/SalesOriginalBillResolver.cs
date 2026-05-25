using ERP.Application.Modules.Fiscal.Integration;
using ERP.Domain.Modules.Fiscal.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Modules.Fiscal.Integration;

public interface ISalesOriginalBillResolver
{
    Task<SalesBill?> ResolveAsync(Guid subscriberId, Guid originalBillId, CancellationToken ct = default);
}

public sealed class SalesOriginalBillResolver : ISalesOriginalBillResolver
{
    private readonly ISalesRepository _sales;
    private readonly IInvoiceRepository _invoices;

    public SalesOriginalBillResolver(ISalesRepository sales, IInvoiceRepository invoices)
    {
        _sales = sales;
        _invoices = invoices;
    }

    public async Task<SalesBill?> ResolveAsync(Guid subscriberId, Guid originalBillId, CancellationToken ct = default)
    {
        var legacy = await _sales.GetBillByIdAsync(subscriberId, originalBillId, ct);
        if (legacy is not null)
            return legacy;

        var invoice = await _invoices.GetByPublicIdAsync(originalBillId, ct);
        return invoice is null ? null : FiscalInvoiceSriBridge.ToLegacyBill(invoice);
    }
}
