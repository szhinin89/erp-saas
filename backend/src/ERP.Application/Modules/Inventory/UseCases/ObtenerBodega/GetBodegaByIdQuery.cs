using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventory.DTOs;

namespace ERP.Application.Modules.Inventory.UseCases.ObtenerBodega;

public sealed record GetBodegaByIdQuery(Guid Id)
    : IRequest<Result<BodegaDetailDto?>>;
