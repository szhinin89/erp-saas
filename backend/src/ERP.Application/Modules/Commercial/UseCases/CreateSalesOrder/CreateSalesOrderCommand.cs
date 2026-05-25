using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using MediatR;

namespace ERP.Application.Modules.Commercial.UseCases.CreateSalesOrder;

public sealed record CreateSalesOrderCommand(
    Guid BusinessPartnerId,
    Guid WarehouseId,
    DateOnly? RequiredDate,
    short PaymentTermDays,
    string? Notes,
    IReadOnlyList<SalesOrderLineInputDto> Lines) : IRequest<Result<SalesOrderDto>>;
