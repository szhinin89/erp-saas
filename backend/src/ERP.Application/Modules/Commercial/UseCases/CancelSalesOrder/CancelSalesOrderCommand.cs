using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using MediatR;

namespace ERP.Application.Modules.Commercial.UseCases.CancelSalesOrder;

public sealed record CancelSalesOrderCommand(Guid PublicId, string? Reason = null)
    : IRequest<Result<SalesOrderDto>>;
