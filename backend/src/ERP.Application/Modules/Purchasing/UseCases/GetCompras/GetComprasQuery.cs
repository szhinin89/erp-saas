using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Modules.Purchasing.Enums;

namespace ERP.Application.Modules.Purchasing.UseCases.GetCompras;

public sealed record GetComprasQuery(
    EstadoCompra? Estado,
    Guid?         ProveedorId,
    DateTime?     Desde,
    DateTime?     Hasta,
    string?       Search
) : IRequest<Result<IReadOnlyList<CompraFacturaDto>>>;
