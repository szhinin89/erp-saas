using ERP.Application.Common;
using ERP.Application.Modules.Companies;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.Costing;

/// <summary>
/// Costo de ventas todavía no resuelto por el Kardex (incertidumbre operativa). ACCOUNTING-CONTINUITY-05:
/// el estado contable del costo y su reproceso viven en Contabilizaciones (AccountingPosting).
/// </summary>
public sealed record PendingInventoryCostDto(Guid ObligationId, Guid InvoiceId, string? InvoiceNumber,
    DateOnly? IssueDate, Guid InvoiceLineId, Guid ProductId, string? ItemSku, string? ItemName,
    Guid WarehouseId, string? WarehouseName, decimal PendingQuantity, decimal? ProvisionalUnitCost, string Status);
public sealed record GetPendingInventoryCostsQuery : IRequest<Result<IReadOnlyList<PendingInventoryCostDto>>>, ICompanyScopedRequest;

public sealed class GetPendingInventoryCostsHandler(IInventoryCostLedger ledger, ICurrentTenant tenant, ICurrentCompany company)
    : IRequestHandler<GetPendingInventoryCostsQuery, Result<IReadOnlyList<PendingInventoryCostDto>>>
{
    public async Task<Result<IReadOnlyList<PendingInventoryCostDto>>> Handle(GetPendingInventoryCostsQuery request, CancellationToken ct)
    {
        var costs = await ledger.GetPendingObligationsAsync(tenant.TenantId, company.CompanyId, ct);
        var rows = costs.Select(o => new PendingInventoryCostDto(o.ObligationId, o.InvoiceId, o.InvoiceNumber,
            o.IssueDate, o.InvoiceLineId, o.ProductId, o.ItemSku, o.ItemName, o.WarehouseId, o.WarehouseName,
            o.PendingQuantity, o.ProvisionalUnitCost,
            o.ProvisionalUnitCost.HasValue ? "Provisional" : "CostPending")).ToList();
        return Result<IReadOnlyList<PendingInventoryCostDto>>.Success(rows);
    }
}
