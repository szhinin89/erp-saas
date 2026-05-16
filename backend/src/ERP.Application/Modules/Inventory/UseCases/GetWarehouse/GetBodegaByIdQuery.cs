using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;

namespace ERP.Application.Modules.Inventory.UseCases.ObtenerBodega;

public sealed record GetWarehouseByIdQuery(Guid Id)
    : IRequest<Result<WarehouseDetailDto?>>;
