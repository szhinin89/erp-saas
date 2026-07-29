using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetAggregatedStock;

public sealed record GetAggregatedStockQuery(Guid ItemId)
    : IRequest<Result<AggregatedStockDto>>,
        ICompanyScopedRequest;
