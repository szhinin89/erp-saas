using ERP.Application.Common;
using ERP.Application.Modules.Companies;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.Costing;

public sealed record PendingInventoryCostDto(Guid InvoiceId, Guid? InvoiceLineId, Guid? ProductId,
    Guid? WarehouseId, decimal PendingQuantity, decimal? ProvisionalUnitCost,
    Guid? PostingId, string Status, string? ErrorCode, string? ErrorMessage);
public sealed record GetPendingInventoryCostsQuery : IRequest<Result<IReadOnlyList<PendingInventoryCostDto>>>, ICompanyScopedRequest;
public sealed record RetryInventoryCostPostingsCommand : IRequest<Result<int>>, ICompanyScopedRequest;

public sealed class GetPendingInventoryCostsHandler(IInventoryCostLedger ledger, ICurrentTenant tenant, ICurrentCompany company)
    : IRequestHandler<GetPendingInventoryCostsQuery, Result<IReadOnlyList<PendingInventoryCostDto>>>
{
    public async Task<Result<IReadOnlyList<PendingInventoryCostDto>>> Handle(GetPendingInventoryCostsQuery request, CancellationToken ct)
    {
        var costs = await ledger.GetPendingObligationsAsync(tenant.TenantId, company.CompanyId, ct);
        var postings = await ledger.GetPendingPostingsAsync(tenant.TenantId, company.CompanyId, ct);
        var rows = costs.Select(o => new PendingInventoryCostDto(o.InvoiceId, o.InvoiceLineId, o.ProductId,
            o.WarehouseId, o.PendingQuantity, o.ProvisionalUnitCost, null,
            o.ProvisionalUnitCost.HasValue ? "Provisional" : "CostPending", null, null))
            .Concat(postings.Select(p => new PendingInventoryCostDto(p.InvoiceId, null, null, null,
                0m, null, p.Id, p.Status, p.ErrorCode, p.ErrorMessage))).ToList();
        return Result<IReadOnlyList<PendingInventoryCostDto>>.Success(rows);
    }
}

public sealed class RetryInventoryCostPostingsHandler(IInventoryCostLedger ledger, InventoryCostAccounting accounting,
    ICurrentTenant tenant, ICurrentCompany company) : IRequestHandler<RetryInventoryCostPostingsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(RetryInventoryCostPostingsCommand request, CancellationToken ct)
    {
        var pending = await ledger.GetPendingPostingsAsync(tenant.TenantId, company.CompanyId, ct);
        var invoices = pending.Select(p => p.InvoiceId).Distinct().Order().ToList();
        foreach (var invoice in invoices)
            await accounting.ProcessAsync(tenant.TenantId, company.CompanyId, invoice, ct);
        await ledger.SaveAsync(ct);
        return Result<int>.Success(pending.Count(p => p.Status == "Posted"));
    }
}
