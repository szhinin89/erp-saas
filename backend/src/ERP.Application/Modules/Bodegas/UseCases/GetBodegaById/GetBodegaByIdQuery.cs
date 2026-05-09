using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Bodegas.DTOs;

namespace ERP.Application.Modules.Bodegas.UseCases.GetBodegaById;

public sealed record GetBodegaByIdQuery(Guid Id)
    : IRequest<Result<BodegaDetailDto?>>;
