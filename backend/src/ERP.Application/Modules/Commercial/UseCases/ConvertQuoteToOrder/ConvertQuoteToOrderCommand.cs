using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using MediatR;

namespace ERP.Application.Modules.Commercial.UseCases.ConvertQuoteToOrder;

public sealed record ConvertQuoteToOrderCommand(
    Guid QuotePublicId,
    Guid WarehouseId,
    DateOnly? RequiredDate) : IRequest<Result<SalesOrderDto>>;
