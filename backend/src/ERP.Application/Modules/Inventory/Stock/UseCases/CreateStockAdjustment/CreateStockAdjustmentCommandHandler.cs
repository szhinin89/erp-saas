using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.CreateStockAdjustment;

public sealed class CreateStockAdjustmentCommandHandler
    : IRequestHandler<CreateStockAdjustmentCommand, Result<StockAdjustmentDto>>
{
    private readonly IStockAdjustmentRepository _adjRepo;
    private readonly ICurrentTenant             _tenant;
    private readonly ICurrentCompany            _company;
    private readonly ICurrentUser               _user;

    public CreateStockAdjustmentCommandHandler(
        IStockAdjustmentRepository adjRepo,
        ICurrentTenant tenant, ICurrentCompany company, ICurrentUser user)
    {
        _adjRepo = adjRepo;
        _tenant  = tenant;
        _company = company;
        _user    = user;
    }

    public async Task<Result<StockAdjustmentDto>> Handle(
        CreateStockAdjustmentCommand request, CancellationToken ct)
    {
        var seq = await _adjRepo.GetNextSequentialAsync(_tenant.TenantId, ct);

        var adj = StockAdjustment.Create(
            _tenant.TenantId, seq,
            request.WarehouseId, request.WarehouseName,
            request.ProductId, request.ProductName,
            request.AdjustmentQty, request.Reason, request.Notes,
            _user.UserId, _company.CompanyId);

        await _adjRepo.AddAsync(adj, ct);
        await _adjRepo.SaveChangesAsync(ct);

        return Result<StockAdjustmentDto>.Success(ToDto(adj));
    }

    internal static StockAdjustmentDto ToDto(StockAdjustment a) => new(
        a.Id, a.AdjustmentNumber,
        a.WarehouseId, a.WarehouseName,
        a.ProductId, a.ProductName,
        a.AdjustmentQty, a.AdjustmentType,
        a.Reason, a.Notes, a.AdjustmentDate,
        a.Status, a.ExecutedAt);
}
