using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;

namespace ERP.Application.Modules.Compras.UseCases.NotasProveedor;

public sealed record ImportarCompraNotaProveedorCommand(
    byte[]  XmlContent,
    string? XmlNombreArchivo,
    Guid?   CompraFacturaId,
    Guid?   GastoFacturaId
) : IRequest<Result<CompraNotaProveedorDto>>;
