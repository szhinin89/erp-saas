using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Inventario.DTOs;

namespace ERP.Application.Modules.Inventario.UseCases.ObtenerBodega;

public sealed record GetBodegaByIdQuery(Guid Id)
    : IRequest<Result<BodegaDetailDto?>>;
