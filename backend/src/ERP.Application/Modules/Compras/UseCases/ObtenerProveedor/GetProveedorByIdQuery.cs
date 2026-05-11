using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;

namespace ERP.Application.Modules.Compras.UseCases.ObtenerProveedor;

public sealed record GetProveedorByIdQuery(Guid Id)
    : IRequest<Result<ProveedorDetailDto?>>;
