using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;

namespace ERP.Application.Modules.Compras.UseCases.GetCompraById;

public sealed record GetCompraByIdQuery(Guid Id)
    : IRequest<Result<CompraFacturaDetailDto?>>;
