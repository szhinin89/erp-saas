using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Proveedores.DTOs;

namespace ERP.Application.Modules.Proveedores.UseCases.GetProveedorById;

public sealed record GetProveedorByIdQuery(Guid Id)
    : IRequest<Result<ProveedorDetailDto?>>;
