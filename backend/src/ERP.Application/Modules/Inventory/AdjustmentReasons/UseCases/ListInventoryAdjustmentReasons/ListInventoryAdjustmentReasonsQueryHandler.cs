using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Application.Modules.Inventory.Stock.Mapping;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.ListInventoryAdjustmentReasons;

public sealed class ListInventoryAdjustmentReasonsQueryHandler
    : IRequestHandler<
        ListInventoryAdjustmentReasonsQuery,
        Result<IReadOnlyList<InventoryAdjustmentReasonDto>>
    >
{
    private readonly IInventoryAdjustmentReasonRepository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly IInventoryAdjustmentReasonAccountRepository _accounts;

    public ListInventoryAdjustmentReasonsQueryHandler(
        IInventoryAdjustmentReasonRepository repo,
        ICurrentTenant tenant,
        ICurrentCompany company,
        IInventoryAdjustmentReasonAccountRepository accounts
    )
    {
        _repo = repo;
        _tenant = tenant;
        _company = company;
        _accounts = accounts;
    }

    public async Task<Result<IReadOnlyList<InventoryAdjustmentReasonDto>>> Handle(
        ListInventoryAdjustmentReasonsQuery request,
        CancellationToken ct
    )
    {
        var items = await _repo.ListAsync(
            _tenant.TenantId,
            request.CompanyId,
            request.IncludeInactive,
            ct
        );

        // ACCOUNTING-12A — la cuenta contable es la configurada para la Company activa.
        var accounts = await _accounts.GetMapAsync(_tenant.TenantId, _company.CompanyId, ct);
        return Result<IReadOnlyList<InventoryAdjustmentReasonDto>>.Success(
            items
                .Select(r => StockAdjustmentMapper.ToDto(r,
                    accounts.TryGetValue(r.Id, out var link) ? link.AccountingAccountId : null))
                .ToList()
        );
    }
}
