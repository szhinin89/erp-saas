using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.NotasProveedor;

public sealed record GetComprasNotasProveedorQuery(
    Guid?   ProveedorId,
    Guid?   CompraFacturaId,
    Guid?   GastoFacturaId,
    string? Estado
) : IRequest<Result<IReadOnlyList<CompraNotaProveedorDto>>>;
