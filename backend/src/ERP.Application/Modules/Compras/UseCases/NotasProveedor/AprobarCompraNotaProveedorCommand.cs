using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;

namespace ERP.Application.Modules.Compras.UseCases.NotasProveedor;

public sealed record AprobarCompraNotaProveedorCommand(
    Guid    NotaId,
    string? NumeroAutorizacion,
    DateTime? FechaAutorizacion
) : IRequest<Result<CompraNotaProveedorDto>>;
