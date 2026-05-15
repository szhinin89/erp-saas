using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.ObtenerProveedor;

public sealed record GetProveedorByIdQuery(Guid Id)
    : IRequest<Result<ProveedorDetailDto?>>;
