using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;
using ERP.Domain.Modules.Compras.Enums;

namespace ERP.Application.Modules.Compras.UseCases.GetCompras;

public sealed record GetComprasQuery(
    EstadoCompra? Estado,
    Guid?         ProveedorId,
    DateTime?     Desde,
    DateTime?     Hasta,
    string?       Search
) : IRequest<Result<IReadOnlyList<CompraFacturaDto>>>;
