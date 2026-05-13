using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;

namespace ERP.Application.Modules.Compras.UseCases.NotasProveedor;

public sealed record GetComprasNotasProveedorQuery(
    Guid?   ProveedorId,
    Guid?   CompraFacturaId,
    Guid?   GastoFacturaId,
    string? Estado
) : IRequest<Result<IReadOnlyList<CompraNotaProveedorDto>>>;
