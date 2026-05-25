using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using MediatR;

namespace ERP.Application.Modules.Commercial.UseCases.ConfirmSalesOrder;

public sealed record ConfirmSalesOrderCommand(Guid PublicId) : IRequest<Result<SalesOrderDto>>;
