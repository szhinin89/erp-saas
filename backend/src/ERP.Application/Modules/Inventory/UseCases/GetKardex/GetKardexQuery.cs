using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;

namespace ERP.Application.Inventory.UseCases.GetKardex;

public sealed record GetKardexQuery(
    Guid    ProductId,
    Guid    WarehouseId,
    DateTime? DateFrom,
    DateTime? DateTo)
    : IRequest<Result<KardexResponse>>, ICompanyScopedRequest;
